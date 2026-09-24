using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

[assembly: ComVisible(true)]

namespace FlashBoxApp
{
    // FbHost: COM proxy exposed to the page via AddHostObjectToScript. Uses a
    // completely different delivery path than PostWebMessageAsJson (which is
    // silently broken on this machine: C#->JS web messages never arrive).
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class FbHost
    {
        readonly AppForm _form;

        public FbHost(AppForm form) { _form = form; }

        void Ui(Action a) { if (_form.IsHandleCreated) _form.Invoke((Action)a); else a(); }
        T Ui<T>(Func<T> f) { if (_form.IsHandleCreated) return (T)_form.Invoke((Func<T>)f); return f(); }

        public string Ping(string s) { return "pong:" + s; }
        public void JsLog(string json) { AppForm.Dbg("JS: " + json); }
        public string GetAppDir() { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'); }
        public string GetLog() { return AppForm.GetLog(); }
        public void ClearLog() { AppForm.ClearLog(); }

        public void Minimize() { Ui(() => _form.DoMinimize()); }
        public void Maximize() { Ui(() => _form.DoMaximize()); }
        public void CloseWindow() { Ui(() => _form.DoClose()); }
        public void StartDrag() { Ui(() => _form.DoStartDrag()); }
        public void ReportRect(int x, int y, int w, int h) { Ui(() => _form.DoReportRect(x, y, w, h)); }
        public void FlashCall(string method, string paramsJson) { Ui(() => _form.DoFlashCall(method, paramsJson)); }
        public void Gear(string rowsJson) { Ui(() => _form.DoGear(rowsJson)); }
        public void Cosmetics(string stateJson) { Ui(() => _form.DoCosmeticsButton(stateJson)); }
        // Click counter for the overlay shirt button. The page polls this
        // (see cosSeq): C#->JS messages don't arrive on this machine, so a
        // push is impossible. Plain int read is atomic; only the UI thread
        // ever writes it.
        public int CosmeticsSeq() { return _form.CosmeticsToggleSeq; }
        // Loader line: the page polls for a committed name (same reason as
        // CosmeticsSeq: C#->JS pushes don't arrive). Take-and-clear.
        public string LoadNameRequest() { return _form.TakePendingLoad(); }
        // NOTE: never Task.Run()+GetResult() here (that deadlocks when the
        // host call arrives on the UI thread: pool waits on Invoke while the
        // UI thread waits on the pool). InvokeRequired-gated direct call is
        // safe from either thread.
        public string FlashQuery(string method)
        {
            if (_form.InvokeRequired) return (string)_form.Invoke(new Func<string>(() => _form.DoFlashQuery(method)));
            return _form.DoFlashQuery(method);
        }
        public void Background(string name) { Ui(() => _form.DoBackground(name)); }
        public void Reset() { Ui(() => _form.DoReset()); }
        public void LoadItem(string itemType, string itemFile, string weaponType) { Ui(() => _form.DoLoadItem(itemType, itemFile, weaponType)); }
        public void DropItemBytes(string itemType, string fileName, string base64) { Ui(() => _form.DoDropItem(itemType, fileName, base64)); }

        public string LoadChar(string name)
        {
            // Run on the threadpool: the host-object call may be marshaled onto
            // the UI thread, and blocking .GetResult() on an async method that
            // captured the WinForms sync context would deadlock forever.
            return Task.Run(() => LoadCharCore(name)).GetAwaiter().GetResult();
        }

        static string LoadCharCore(string name)
        {
            try
            {
                string body = null;
                foreach (var url in new[]
                {
                    "https://account.aq.com/CharPage?id=" + Uri.EscapeDataString(name),
                    "http://www.aq.com/character.asp?id=" + Uri.EscapeDataString(name)
                })
                {
                    body = Downloads.GetAsync(url).GetAwaiter().GetResult();
                    if (body != null) break;
                }
                if (body == null)
                    return JsonConvert.SerializeObject(new { ok = false, error = "Could not reach the character page." });
                var vars = Downloads.ParseFlashVars(body);
                if (vars == null)
                    return JsonConvert.SerializeObject(new { ok = false, error = "No FlashVars on that character page." });
                var v = new JObject();
                foreach (var kv in vars) v[kv.Key] = kv.Value;
                return JsonConvert.SerializeObject(new { ok = true, vars = v });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        public string DownloadAll(string varsJson, string dir)
        {
            return Task.Run(() => DownloadAllCore(varsJson, dir)).GetAwaiter().GetResult();
        }

        static string DownloadAllCore(string varsJson, string dir)
        {
            try
            {
                var vo = JObject.Parse(varsJson);
                var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in vo.Properties()) vars[p.Name] = p.Value.Value<string>();
                var items = Downloads.BuildDownloads(vars);
                try { Directory.CreateDirectory(dir); } catch { }
                var results = new JArray();
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    string dest = Path.Combine(dir, it.File);
                    // Already on disk from an earlier load: reuse it, don't
                    // re-download. Reloads become near-instant (delete the
                    // folder to force a fresh fetch).
                    bool ok;
                    try { ok = File.Exists(dest) && new FileInfo(dest).Length > 0; } catch { ok = false; }
                    if (!ok) ok = Downloads.DownloadAsync(it.Url, dest).GetAwaiter().GetResult();
                    results.Add(new JObject
                    {
                        { "type", it.Type },
                        { "file", it.File },
                        { "ok", ok },
                        { "path", ok ? dest : "" },
                        { "cosmetic", it.Cosmetic }
                    });
                }
                return JsonConvert.SerializeObject(new { ok = true, items = results, dir });
            }
            catch (Exception ex) { return JsonConvert.SerializeObject(new { ok = false, error = ex.Message }); }
        }

        public string PickFolder() { return Ui(() => _form.DoPickFolder()); }
        public string SaveFile(string data, string name) { return Ui(() => _form.DoSaveFile(data, name)); }
        public string OpenFile() { return Ui(() => _form.DoOpenFile()); }
    }

    // AppForm: the combined one-window app. A WebView2 fills the window and
    // renders ui/index.html; FlashCore (the real Flash ActiveX) is a SIBLING
    // control positioned over the HTML preview region, so the Chromium
    // compositor never paints over it. JS <-> C# IPC is pure message passing:
    // JS posts {id, method, args} via window.chrome.webview.postMessage;
    // C# replies {id, result} and pushes {ev:...} events.
    // (IPC now goes through the FbHost COM proxy: the classic web-message
    // channel silently fails C#->JS on some machines.)
    // OleDrop: shared COM drag-drop plumbing (IDropTarget + CF_HDROP file
    // extraction). Register on any hit-testable window handle; drops carry
    // real Explorer file paths.
    static class OleDrop
    {
        [ComImport, Guid("00000122-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ITarget
        {
            void DragEnter([In, MarshalAs(UnmanagedType.Interface)] object pDataObj, [In] int grfKeyState, [In] POINT pt, [In, Out] ref int pdwEffect);
            void DragOver([In] int grfKeyState, [In] POINT pt, [In, Out] ref int pdwEffect);
            void DragLeave();
            void Drop([In, MarshalAs(UnmanagedType.Interface)] object pDataObj, [In] int grfKeyState, [In] POINT pt, [In, Out] ref int pdwEffect);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        [DllImport("ole32.dll")]
        public static extern int RegisterDragDrop(IntPtr hwnd, ITarget pDropTarget);
        [DllImport("ole32.dll")]
        public static extern int RevokeDragDrop(IntPtr hwnd);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern uint DragQueryFile(IntPtr hDrop, uint iFile, System.Text.StringBuilder lpszFile, int cch);
        [DllImport("ole32.dll")]
        static extern void ReleaseStgMedium(ref System.Runtime.InteropServices.ComTypes.STGMEDIUM pmedium);

        public const int CF_HDROP = 15;
        public const int COPY = 1;
        public const int NONE = 0;

        public static List<string> Files(object pDataObj)
        {
            var list = new List<string>();
            try
            {
                var data = (System.Runtime.InteropServices.ComTypes.IDataObject)pDataObj;
                var fmt = new System.Runtime.InteropServices.ComTypes.FORMATETC
                {
                    cfFormat = (short)CF_HDROP,
                    ptd = IntPtr.Zero,
                    dwAspect = System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT,
                    lindex = -1,
                    tymed = System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL
                };
                var med = new System.Runtime.InteropServices.ComTypes.STGMEDIUM();
                data.GetData(ref fmt, out med);
                try
                {
                    uint n = DragQueryFile(med.unionmember, 0xFFFFFFFF, null, 0);
                    for (uint i = 0; i < n; i++)
                    {
                        uint len = DragQueryFile(med.unionmember, i, null, 0);
                        var sb = new System.Text.StringBuilder((int)len + 1);
                        if (DragQueryFile(med.unionmember, i, sb, sb.Capacity) > 0)
                            list.Add(sb.ToString());
                    }
                }
                finally { try { ReleaseStgMedium(ref med); } catch { } }
            }
            catch { }
            return list;
        }
    }

    // HudOverlay: transparent, click-through layered window floating over the
    // Flash preview's left edge (CharPage-style icon + name list). A normal
    // WinForms panel can't do this: "transparent" only fakes the parent's
    // background, never the sibling Flash window beneath. A WS_EX_LAYERED
    // window composited with per-pixel alpha really floats above the scene,
    // and WS_EX_TRANSPARENT lets every click fall through to the avatar.
    public class HudOverlay : Form
    {
        public class Row
        {
            public string Key;
            public Image Icon;
            public string Name;
            public bool Dim;
        }

        readonly List<Row> _rows = new List<Row>();
        readonly Dictionary<string, Image> _icons = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        readonly Font _font;
        const int IconSize = 26;
        const int RowH = 34;
        const int PadX = 10;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        const int ULW_ALPHA = 2;
        const byte AC_SRC_ALPHA = 1;
        // Clickable icons, click-through everywhere else: the window keeps
        // WS_EX_LAYERED (per-pixel alpha) but drops WS_EX_TRANSPARENT in
        // favor of WM_NCHITTEST hit-testing - HTCLIENT over an icon rect,
        // HTTRANSPARENT anywhere else so avatar clicks pass through.
        const int WS_EX_LAYERED = 0x80000;
        const int WS_EX_NOACTIVATE = 0x8000000;
        const int WM_NCHITTEST = 0x84;
        const int HTCLIENT = 1;
        const int HTTRANSPARENT = -1;

        public event Action<string> IconClicked;
        public event Action<string, string> FileDropped;

        readonly List<Tuple<Rectangle, string>> _hits = new List<Tuple<Rectangle, string>>();

        class GearDrop : OleDrop.ITarget
        {
            readonly HudOverlay _o;
            public GearDrop(HudOverlay o) { _o = o; }
            public void DragEnter(object pDataObj, int grfKeyState, OleDrop.POINT pt, ref int pdwEffect)
            {
                var f = OleDrop.Files(pDataObj).FindAll(x => x.EndsWith(".swf", StringComparison.OrdinalIgnoreCase));
                AppForm.Dbg("GearDrop DragEnter files=" + f.Count);
                pdwEffect = f.Count > 0 ? OleDrop.COPY : OleDrop.NONE;
            }
            public void DragOver(int grfKeyState, OleDrop.POINT pt, ref int pdwEffect) { pdwEffect = OleDrop.COPY; }
            public void DragLeave() { }
            public void Drop(object pDataObj, int grfKeyState, OleDrop.POINT pt, ref int pdwEffect)
            {
                var files = OleDrop.Files(pDataObj).FindAll(x => x.EndsWith(".swf", StringComparison.OrdinalIgnoreCase));
                string slot = _o.SlotAt(_o.PointToClient(new Point(pt.X, pt.Y)));
                AppForm.Dbg("GearDrop Drop files=" + files.Count + " slot=" + (slot ?? "-"));
                pdwEffect = OleDrop.NONE;
                if (slot == null) return;
                var cb = _o.FileDropped;
                if (cb == null) return;
                foreach (var f in files)
                {
                    try { cb(slot, f); } catch { }
                }
                pdwEffect = files.Count > 0 ? OleDrop.COPY : OleDrop.NONE;
            }
        }

        readonly GearDrop _dropTarget;

        public string SlotAt(Point p)
        {
            foreach (var h in _hits) { if (h.Item1.Contains(p)) return h.Item2; }
            return null;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                int lp = m.LParam.ToInt32();
                var p = PointToClient(new Point(lp & 0xFFFF, (lp >> 16) & 0xFFFF));
                foreach (var h in _hits)
                {
                    if (h.Item1.Contains(p)) { m.Result = (IntPtr)HTCLIENT; return; }
                }
                m.Result = (IntPtr)HTTRANSPARENT;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            foreach (var h in _hits)
            {
                if (h.Item1.Contains(e.Location))
                {
                    var cb = IconClicked;
                    if (cb != null) cb(h.Item2);
                    return;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool over = false;
            foreach (var h in _hits) { if (h.Item1.Contains(e.Location)) { over = true; break; } }
            Cursor = over ? Cursors.Hand : Cursors.Default;
        }

        public bool HasHit(Point p)
        {
            foreach (var h in _hits) { if (h.Item1.Contains(p)) return true; }
            return false;
        }

        public bool ClickAt(Point p)
        {
            foreach (var h in _hits)
            {
                if (h.Item1.Contains(p))
                {
                    var cb = IconClicked;
                    if (cb != null) cb(h.Item2);
                    return true;
                }
            }
            return false;
        }

        [DllImport("user32.dll")]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pptSrc, int crKey, ref BlendFunction pblend, int dwFlags);
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr hDC);

        public HudOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            _font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            Width = 240;
            _dropTarget = new GearDrop(this);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int rc = OleDrop.RegisterDragDrop(Handle, _dropTarget);
                AppForm.Dbg("GearDrop RegisterDragDrop rc=" + rc);
            }
            catch (Exception ex) { AppForm.Dbg("GearDrop register EX: " + ex.Message); }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            try { OleDrop.RevokeDragDrop(Handle); } catch { }
            base.OnHandleDestroyed(e);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        Image GetIcon(string dir, string name)
        {
            Image img;
            if (_icons.TryGetValue(name, out img)) return img;
            try
            {
                string p = Path.Combine(dir, "ui", "icons", name + ".png");
                if (File.Exists(p)) img = Image.FromFile(p);
            }
            catch { img = null; }
            _icons[name] = img;
            return img;
        }

        public void SetRows(string appDir, JArray rows)
        {
            _rows.Clear();
            if (rows != null)
            {
                foreach (var r in rows)
                {
                    if (!(r is JObject)) continue;
                    var o = (JObject)r;
                    string key = (o.Value<string>("icon") ?? "").ToLowerInvariant();
                    _rows.Add(new Row
                    {
                        Key = key,
                        Icon = GetIcon(appDir, key),
                        Name = o.Value<string>("name") ?? "",
                        Dim = o.Value<bool>("dim")
                    });
                }
            }
            if (_rows.Count == 0) { Hide(); return; }
            Render();
        }

        public bool HasRows { get { return _rows.Count > 0; } }

        void Render()
        {
            // Measure pass: long names (e.g. "Prince of Dragon's Shield and
            // Blade") word-wrap to at most 2 lines and those rows grow taller
            // instead of running off the 240px window edge.
            float maxW = Width - (PadX + IconSize + 8) - PadX;
            using (var fmt = StringFormat.GenericDefault)
            {
            var heights = new List<int>(_rows.Count);
            using (var mBmp = new Bitmap(1, 1))
            using (var measure = Graphics.FromImage(mBmp))
            {
                float lineH = _font.GetHeight(measure);
                foreach (var r in _rows)
                {
                    var sz = measure.MeasureString(r.Name ?? "", _font, new SizeF(maxW, 1000), fmt);
                    int lines = Math.Max(1, Math.Min(2, (int)Math.Ceiling(sz.Height / lineH - 0.01)));
                    heights.Add(Math.Max(RowH, (int)Math.Ceiling(lines * lineH) + 10));
                }
            }
            int total = 8;
            foreach (var h in heights) total += h;
            Height = total;
            using (var bmp = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    using (var white = new SolidBrush(Color.FromArgb(255, 255, 255, 255)))
                    using (var shade = new SolidBrush(Color.FromArgb(230, 0, 0, 0)))
                    {
                    _hits.Clear();
                        int y = 4;
                        // Names sit low next to their icons: center the text
                        // block in the row (measurement above is already done,
                        // so changing alignment here only moves rendering).
                        fmt.LineAlignment = StringAlignment.Center;
                        for (int i = 0; i < _rows.Count; i++)
                        {
                            var r = _rows[i];
                            int rh = heights[i];
                            int iy = y + (rh - IconSize) / 2;
                            if (r.Icon != null)
                            {
                                _hits.Add(Tuple.Create(new Rectangle(PadX, iy, IconSize, IconSize), r.Key));
                                var fit = FitIcon(r.Icon, PadX, iy, IconSize, IconSize);
                                using (var sil = MakeSilhouette(r.Icon))
                                {
                                    for (int ox = -1; ox <= 1; ox++)
                                        for (int oy = -1; oy <= 1; oy++)
                                            if (ox != 0 || oy != 0)
                                                g.DrawImage(sil, fit.X + ox, fit.Y + oy, fit.Width, fit.Height);
                                }
                                g.DrawImage(r.Icon, fit);
                            }
                            float tx = PadX + IconSize + 8;
                            var layout = new RectangleF(tx, y + 5, maxW, rh - 10);
                            foreach (var o in new[] { new PointF(-1, 0), new PointF(1, 0), new PointF(0, -1), new PointF(0, 1), new PointF(-1, -1), new PointF(1, -1), new PointF(-1, 1), new PointF(1, 1) })
                            {
                                var lr = layout; lr.Offset(o.X, o.Y);
                                g.DrawString(r.Name, _font, shade, lr, fmt);
                            }
                            g.DrawString(r.Name, _font, white, layout, fmt);
                            y += rh;
                        }
                    }
                }
                IntPtr screenDc = GetDC(IntPtr.Zero);
                IntPtr memDc = CreateCompatibleDC(screenDc);
                IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                IntPtr oldBmp = SelectObject(memDc, hBmp);
                try
                {
                    var pos = new Point(Left, Top);
                    var size = new Size(Width, Height);
                    var src = new Point(0, 0);
                    var blend = new BlendFunction { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
                    UpdateLayeredWindow(Handle, screenDc, ref pos, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
                }
                finally
                {
                    SelectObject(memDc, oldBmp);
                    DeleteObject(hBmp);
                    DeleteDC(memDc);
                    ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
            }
        }

        // Black silhouette of an icon (alpha kept, RGB flattened) for outlining.
        static Bitmap MakeSilhouette(Image src)
        {
            var bmp = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var attrs = new System.Drawing.Imaging.ImageAttributes())
            {
                attrs.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new float[][]
                {
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 0, 0, 0, 0, 1 }
                }));
                g.DrawImage(src, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
            }
            return bmp;
        }

        // Contain-fit: icons ship at all aspect ratios/paddings, and
        // stretching them into the cell is what makes the column look
        // ragged. Scale to fit and center instead, so every glyph sits on
        // the same optical box and the name column lines up.
        static internal Rectangle FitIcon(Image icon, int x, int y, int w, int h)
        {
            float s = Math.Min((float)w / icon.Width, (float)h / icon.Height);
            int dw = Math.Max(1, (int)(icon.Width * s));
            int dh = Math.Max(1, (int)(icon.Height * s));
            return new Rectangle(x + (w - dw) / 2, y + (h - dh) / 2, dw, dh);
        }

        public void Follow(Rectangle flashClientRect, Func<Point, Point> toScreen)
        {
            if (!Visible) return;
            var p = toScreen(new Point(flashClientRect.X + 12, flashClientRect.Y + Math.Max(0, (flashClientRect.Height - Height) / 2)));
            Location = p;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_font != null) _font.Dispose(); } catch { }
                foreach (var kv in _icons) try { if (kv.Value != null) kv.Value.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }

    // CosmeticsOverlay: CharPage-style "Cosmetics" shirt button floating over
    // the Flash preview's bottom-right corner. Same layered-window trick as
    // the gear HUD (per-pixel alpha; click-through everywhere but the
    // button). char6.swf ships no such asset, so the shirt icon is drawn for
    // this app (ui/icons/cosmetic.png), not ripped game art.
    // State flows one way only: the page pushes {has,on} down via the
    // Cosmetics host call, and button clicks bump a counter the page polls
    // via CosmeticsSeq (C#->JS web messages don't arrive on this machine,
    // so the page can't be notified any other way).
    public class CosmeticsOverlay : Form
    {
        // Bare icon with a black silhouette outline, exactly like the gear
        // list icons (no plate, no label, no dimming).
        const int IconBox = 40;
        const int Pad = 6;

        Image _icon;
        Rectangle _hit = Rectangle.Empty;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BlendFunction
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        const int ULW_ALPHA = 2;
        const byte AC_SRC_ALPHA = 1;
        const int WS_EX_LAYERED = 0x80000;
        const int WS_EX_NOACTIVATE = 0x8000000;
        const int WM_NCHITTEST = 0x84;
        const int HTCLIENT = 1;
        const int HTTRANSPARENT = -1;

        public event Action Clicked;

        [DllImport("user32.dll")]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pptSrc, int crKey, ref BlendFunction pblend, int dwFlags);
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr hDC);

        readonly string _iconFile;

        public CosmeticsOverlay() : this("cosmetic.png") { }

        public CosmeticsOverlay(string iconFile)
        {
            _iconFile = string.IsNullOrEmpty(iconFile) ? "cosmetic.png" : iconFile;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Width = IconBox + Pad * 2;
            Height = IconBox + Pad * 2;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                int lp = m.LParam.ToInt32();
                var p = PointToClient(new Point(lp & 0xFFFF, (lp >> 16) & 0xFFFF));
                m.Result = (IntPtr)(_hit.Contains(p) ? HTCLIENT : HTTRANSPARENT);
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_hit.Contains(e.Location))
            {
                var cb = Clicked;
                if (cb != null) cb();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = _hit.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
        }

        public void SetState(string appDir, bool has, bool on)
        {
            if (_icon == null)
            {
                try
                {
                    string p = Path.Combine(appDir, "ui", "icons", _iconFile);
                    if (File.Exists(p)) _icon = Image.FromFile(p);
                    else AppForm.Dbg("Overlay icon missing: " + p);
                }
                catch (Exception ex) { AppForm.Dbg("Cosmetics icon load EX: " + ex.Message); }
            }
            Render();
        }

        static System.Drawing.Drawing2D.GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        void Render()
        {
            var dst = new Rectangle(Pad, Pad, IconBox, IconBox);
            _hit = dst;
            using (var bmp = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    if (_icon != null)
                    {
                        // Black silhouette outline, same as the gear icons.
                        // Contain-fit like the gear list so the non-square
                        // sprite isn't stretched.
                        var fit = HudOverlay.FitIcon(_icon, dst.X, dst.Y, dst.Width, dst.Height);
                        using (var sil = MakeSilhouette(_icon))
                        {
                            for (int ox = -1; ox <= 1; ox++)
                                for (int oy = -1; oy <= 1; oy++)
                                    if (ox != 0 || oy != 0)
                                        g.DrawImage(sil, fit.X + ox, fit.Y + oy, fit.Width, fit.Height);
                        }
                        g.DrawImage(_icon, fit);
                    }
                }
                IntPtr screenDc = GetDC(IntPtr.Zero);
                IntPtr memDc = CreateCompatibleDC(screenDc);
                IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                IntPtr oldBmp = SelectObject(memDc, hBmp);
                try
                {
                    var pos = new Point(Left, Top);
                    var size = new Size(Width, Height);
                    var src = new Point(0, 0);
                    var blend = new BlendFunction { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
                    UpdateLayeredWindow(Handle, screenDc, ref pos, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
                }
                finally
                {
                    SelectObject(memDc, oldBmp);
                    DeleteObject(hBmp);
                    DeleteDC(memDc);
                    ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
        }

        // Black silhouette of the icon (alpha kept, RGB flattened), same
        // outlining helper as the gear list.
        static Bitmap MakeSilhouette(Image src)
        {
            var bmp = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var attrs = new System.Drawing.Imaging.ImageAttributes())
            {
                attrs.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix(new float[][]
                {
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 0, 0, 0, 0, 1 }
                }));
                g.DrawImage(src, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
            }
            return bmp;
        }

        public void Follow(Rectangle flashClientRect, Func<Point, Point> toScreen)
        {
            if (!Visible) return;
            var p = toScreen(new Point(flashClientRect.Right - Width - 4, flashClientRect.Bottom - Height - 4));
            Location = p;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_icon != null) _icon.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }

    // LoaderOverlay: minimal centered typing line for character names. The
    // middle of the preview is Flash territory so this is native, not HTML:
    // a borderless dark line with an accent underline. Enter commits the
    // name for the page to pick up (LoadNameRequest poll); Esc cancels.
    public class LoaderOverlay : Form
    {
        readonly TextBox _box;
        bool _cue = true;
        const string CueText = "character name...";

        public event Action<string> Committed;
        public event Action Cancelled;

        const int WS_EX_TOOLWINDOW = 0x80;

        public LoaderOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            // Transparent body (TransparencyKey): only the text strip and
            // the accent underline show, the preview shows through the rest.
            BackColor = Color.FromArgb(255, 1, 2, 3);
            TransparencyKey = BackColor;
            Width = 360;
            Height = 46;
            _box = new TextBox
            {
                BorderStyle = BorderStyle.None,
                // Same as the transparency key: the edit field itself goes
                // invisible too, leaving only the typed glyphs floating.
                BackColor = BackColor,
                ForeColor = Color.FromArgb(120, 130, 145),
                Font = new Font("Segoe UI", 14f),
                Location = new Point(16, 9),
                Width = Width - 32
            };
            _box.Text = CueText;
            _box.GotFocus += (o, e) => ClearCue();
            _box.Click += (o, e) => ClearCue();
            _box.KeyDown += (o, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Commit(); }
                else if (e.KeyCode == Keys.Escape) { Cancel(); }
            };
            Controls.Add(_box);
            var line = new Panel { BackColor = Color.White, Height = 2, Dock = DockStyle.Bottom };
            Controls.Add(line);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        void ClearCue()
        {
            if (_cue) { _cue = false; _box.Text = ""; _box.ForeColor = Color.White; }
        }

        void Commit()
        {
            string t = _cue ? "" : _box.Text.Trim();
            if (t.Length == 0) return;
            var cb = Committed;
            if (cb != null) cb(t);
        }

        void Cancel()
        {
            var cb = Cancelled;
            if (cb != null) cb();
        }

        public void Present()
        {
            try { if (!Visible) Show(); } catch { try { Show(); } catch { } }
            Refocus(true);
        }

        public void Refocus(bool selectAll)
        {
            try
            {
                if (!Visible || _box.Focused) return;
                _box.Focus();
                if (selectAll) _box.SelectAll();
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_box != null) { _box.Font.Dispose(); _box.Dispose(); } } catch { }
            }
            base.Dispose(disposing);
        }
    }

    public class AppForm : Form
    {
        WebView2 _web;
        CoreWebView2 _core;
        FlashCore _flash;
        HudOverlay _gear;
        CosmeticsOverlay _cos;
        LoaderOverlay _loader;
        bool _loaderPresented;
        CosmeticsOverlay _mag;
        readonly object _loadLock = new object();
        string _pendingLoad;
        volatile int _cosToggleSeq;
        bool _cosHas, _cosOn;                 // last state pushed by the page
        bool _cosPushedHas, _cosPushedOn;     // state last rendered (kept for diagnostics)
        System.Windows.Forms.Timer _watchdog; // re-assert overlays: layered windows can miss a show/position (minimize, DWM transitions)
        bool _flashReady;
        bool _pageReady;
        bool _hostReadySent;
        bool _reloading;
        int _reloadCount;
        DateTime _reloadWindow = DateTime.MinValue;

        static readonly bool AutoTest = Environment.GetCommandLineArgs().Length > 1 && Array.IndexOf(Environment.GetCommandLineArgs(), "--autotest") >= 0;

        static readonly Color UiBg = Color.FromArgb(0x0A, 0x0C, 0x14);

        public AppForm()
        {
            Text = "FlashBox";
            // Real frame, custom-drawn over: the OS supplies minimize/maximize
            // animations, Aero snap, taskbar thumbnails and shadow
            // (Chrome-style). WM_NCCALCSIZE below strips the non-client area
            // so only our dark titlebar shows.
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, 680);
            MinimumSize = new Size(640, 480);
            BackColor = UiBg;

            _web = new WebView2 { Dock = DockStyle.Fill };
            Controls.Add(_web);

            Load += OnLoad;
            FormClosed += OnFormClosed;
            Shown += (o, e) =>
            {
                // First-shown reseat: owned layered windows shown during
                // startup can end up under the Flash surface until something
                // (minimize/restore, alt-tab) forces recomposition. Reseat
                // them once the owner is really on screen.
                // (The loader line is NOT presented here: the booting Flash
                // ActiveX grabs focus seconds after start, killing typing.
                // It presents from OnFlashReady instead, once boot settles.)
                try { if (!IsDisposed && _flash != null && _flash.Visible) FollowGear(); } catch { }
            };
        }

        // Normal-app close: the gear overlay is a separate top-level window,
        // so close it explicitly. (Owned windows usually follow the owner,
        // but the fallback Show() path has no owner and would linger.)
        void OnFormClosed(object s, FormClosedEventArgs e)
        {
            try { if (_watchdog != null) { _watchdog.Stop(); _watchdog.Dispose(); } } catch { }
            try { if (_gear != null) _gear.Close(); } catch { }
            try { if (_loader != null) _loader.Close(); } catch { }
            try { if (_mag != null) _mag.Close(); } catch { }
            try { if (_cos != null) _cos.Close(); } catch { }
            try { if (_flash != null) _flash.Dispose(); } catch { }
        }

        // Classic drop shadow for the borderless window (Chrome-style).
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;
                return cp;
            }
        }

        async void OnLoad(object s, EventArgs e)
        {
            // Each subsystem initializes independently: a missing WebView2
            // runtime or an unregistered Flash ActiveX degrades to a usable
            // window with an explanation, never a silent instant exit.
            // (async-void exceptions otherwise kill the process with no UI.)
            try { ApplyWindowRounding(); } catch { }

            try
            {
                await _web.EnsureCoreWebView2Async(null);
            }
            catch (Exception ex)
            {
                Dbg("WebView2 init failed: " + ex);
                MessageBox.Show(this,
                    "FlashBox needs the WebView2 Runtime to show its control panel.\n\n" +
                    "Install it from https://developer.microsoft.com/microsoft-edge/webview2/ and relaunch FlashBox.\n\n" +
                    "Technical detail: " + ex.Message,
                    "FlashBox", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _core = _web.CoreWebView2;
                _core.Settings.IsStatusBarEnabled = false;
                _core.Settings.IsZoomControlEnabled = false;
                _core.Settings.AreBrowserAcceleratorKeysEnabled = false;
                _core.WebMessageReceived += OnWebMessage;
                _core.AddHostObjectToScript("fbhost", new FbHost(this));
                _core.DOMContentLoaded += (o, ev) =>
                {
                    _pageReady = true;
                    MaybeSendHostReady();
                };
                _core.NavigationCompleted += OnNavigationCompleted;
            }
            catch (Exception ex)
            {
                Dbg("WebView2 setup failed: " + ex);
                MessageBox.Show(this, "WebView2 setup failed: " + ex.Message, "FlashBox",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // FlashCore is created AFTER the WebView2 channel is up, so the
            // legacy Flash ActiveX can't corrupt WebView2 init (seen before:
            // C#->JS web messages silently never arriving). If Flash isn't
            // registered the control panel still works; only the avatar
            // preview is unavailable.
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                _flash = new FlashCore(baseDir);
                _flash.Bounds = new Rectangle(0, 0, 0, 0);
                _flash.Visible = false;
                _flash.Ready += OnFlashReady;
                Controls.Add(_flash);
                _flash.BringToFront();
                _web.BringToFront();
                _flash.BringToFront();
            }
            catch (Exception ex)
            {
                Dbg("Flash init failed (continuing without avatar preview): " + ex);
                _flash = null;
                MessageBox.Show(this,
                    "Flash Player ActiveX is not registered on this machine, so the avatar preview is unavailable.\n" +
                    "The control panel still works.\n\nTechnical detail: " + ex.Message,
                    "FlashBox", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            try
            {
                _gear = new HudOverlay();
                _gear.IconClicked += OnGearIcon;
                _gear.FileDropped += (slot, path) => DoDropFile(slot, path);
                // Slot icons visible from the start (dimmed, no names): they
                // double as drop targets for SWF files.
                _gear.SetRows(AppDomain.CurrentDomain.BaseDirectory, DefaultGearRows());
                try { _gear.Show(this); } catch { try { _gear.Show(); } catch { } }
                LocationChanged += (o, ev) => FollowGear();
                SizeChanged += (o, ev) => FollowGear();
            }
            catch (Exception ex)
            {
                Dbg("Gear overlay init failed (continuing without it): " + ex);
                _gear = null;
            }

            try
            {
                // Shirt button, dimmed until the page reports a cosmetic set.
                _cos = new CosmeticsOverlay();
                _cos.Clicked += OnCosmeticsClicked;
                _cos.SetState(AppDomain.CurrentDomain.BaseDirectory, false, false);
            }
            catch (Exception ex)
            {
                Dbg("Cosmetics button init failed (continuing without it): " + ex);
                _cos = null;
            }

            try
            {
                // Center typing line (empty-state loader) + magnifier button
                // that re-summons it.
                _loader = new LoaderOverlay();
                _loader.Committed += OnLoaderCommit;
                _loader.Cancelled += () => { try { if (_loader != null) _loader.Hide(); } catch { } };
                _mag = new CosmeticsOverlay("search.png");
                _mag.Clicked += () => ToggleLoader();
                _mag.SetState(AppDomain.CurrentDomain.BaseDirectory, true, false);
            }
            catch (Exception ex)
            {
                Dbg("Loader init failed (continuing without it): " + ex);
                _loader = null;
                _mag = null;
            }

            try
            {
                _watchdog = new System.Windows.Forms.Timer { Interval = 2000 };
                _watchdog.Tick += (o, ev) =>
                {
                    try
                    {
                        if (!IsDisposed && _flash != null && _flash.Visible)
                        {
                            FollowGear();
                            // Z-order insurance: if anything (Flash surface,
                            // DWM hiccup) covers the button overlays, reseat
                            // them. NOACTIVATE windows can't steal focus.
                            try { if (_cos != null && _cos.Visible) _cos.BringToFront(); } catch { }
                            try { if (_mag != null && _mag.Visible) _mag.BringToFront(); } catch { }
                            // Typing-line guard: pull focus back when it was
                            // yanked elsewhere in our own windows (Flash boot
                            // does this). Never from another app, never from
                            // the page (drawer inputs need their focus).
                            try
                            {
                                if (_loader != null && _loader.Visible && ForegroundIsOurs() && !FocusInPage())
                                    _loader.Refocus(false);
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex) { Dbg("watchdog EX: " + ex.Message); }
                };
                _watchdog.Start();
            }
            catch (Exception ex) { Dbg("watchdog init EX: " + ex.Message); }

            try
            {
                string html = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui", "index.html");
                if (!File.Exists(html))
                {
                    Dbg("UI missing: " + html);
                    MessageBox.Show(this, "FlashBox UI files are missing:\n" + html +
                        "\n\nReinstall FlashBox with its ui\\ folder next to the exe.",
                        "FlashBox", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                _core.Navigate("file:///" + html.Replace('\\', '/'));
            }
            catch (Exception ex)
            {
                Dbg("Navigation failed: " + ex);
                MessageBox.Show(this, "FlashBox could not open its control panel: " + ex.Message,
                    "FlashBox", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            _reloading = false;
            if (!e.IsSuccess) { Dbg("Navigation failed"); return; }
            if (AutoTest) _ = RunAutoTestAsync();
        }

        async Task RunAutoTestAsync()
        {
            try
            {
                for (int i = 0; i < 60; i++) { if (_pageReady && _flashReady) break; await Task.Delay(200); }
                await Task.Delay(800);
                Dbg("AUTOTEST: invoke loadCharacter('alina')");
                await _core.ExecuteScriptAsync("document.getElementById('charName').value='alina';loadCharacter();");
            }
            catch (Exception ex) { Dbg("AUTOTEST EX: " + ex.Message); }
        }

        void RecoverPage()
        {
            try
            {
                if (_reloading || _core == null) return;
                var now = DateTime.UtcNow;
                if ((now - _reloadWindow).TotalSeconds > 30) { _reloadCount = 0; _reloadWindow = now; }
                if (++_reloadCount > 3) { Dbg("CHANNEL UNRECOVERABLE after 3 reloads"); return; }
                _reloading = true;
                _hostReadySent = false;
                _pageReady = false;
                string src = _core.Source;
                Dbg("IPC STUCK -> reloading page: " + src);
                _core.Navigate(src);
            }
            catch (Exception ex) { Dbg("RecoverPage EX: " + ex.Message); }
        }

        void OnFlashReady()
        {
            if (InvokeRequired) { BeginInvoke((Action)OnFlashReady); return; }
            Dbg("FlashCore ready");
            _flashReady = true;
            MaybeSendHostReady();
            // Present the typing line now that boot (and its focus grab)
            // is over; presenting earlier lets Flash steal typing seconds
            // after start.
            if (!_loaderPresented) PresentLoader();
        }

        void MaybeSendHostReady()
        {
            if (_hostReadySent || !_pageReady || !_flashReady || _core == null) return;
            _hostReadySent = true;
            Dbg("-> hostReady");
            _core.PostWebMessageAsJson("{\"ev\":\"hostReady\"}");
        }

        static readonly string DebugLog = Path.Combine(Path.GetTempPath(), "fb-debug.log");

        public static void Dbg(string line)
        {
            try { File.AppendAllText(DebugLog, DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + Environment.NewLine); } catch { }
        }

        public static string GetLog()
        {
            try
            {
                if (!File.Exists(DebugLog)) return "";
                using (var fs = new FileStream(DebugLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    string all = sr.ReadToEnd();
                    if (all.Length > 200000) all = all.Substring(all.Length - 200000);
                    return all;
                }
            }
            catch { return ""; }
        }

        public static void ClearLog()
        {
            try { File.WriteAllText(DebugLog, ""); } catch { }
        }

        void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string raw = e.TryGetWebMessageAsString();
                if (raw == null) raw = e.WebMessageAsJson;
                var msg = JsonConvert.DeserializeObject<JObject>(raw);
                if (msg == null) return;
                int id = msg.Value<int>("id");
                string method = msg.Value<string>("method");
                Dbg("<- " + method + " id=" + id + " args=" + (msg["args"] ?? "").ToString(Formatting.None));
                if (method == "__jsError")
                {
                    Dbg("JS: " + (msg["args"] == null ? "" : msg["args"].ToString(Formatting.None)));
                    return;
                }
                var args = msg["args"];
                _ = DispatchAsync(id, method, args);
            }
            catch (Exception ex) { Dbg("OnWebMessage: " + ex); }
        }

        async Task<JToken> DispatchAsync(int id, string method, JToken args)
        {
            JToken result = null;
            try
            {
                switch (method)
                {
                    case "minimize": WindowState = FormWindowState.Minimized; break;
                    case "maximize": WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; break;
                    case "close": Close(); break;
                    case "startDrag": StartDrag(); break;
                    case "reportRect": PositionFlash(args); break;
                    case "flashCall":
                        _flash.FlashCall(args.Value<string>("method"), (args["params"] ?? new JArray()).ToObject<string[]>()); break;
                    case "flashQuery":
                        result = JValue.CreateString(DoFlashQuery(args.Value<string>("method"))); break;
                    case "gear":
                        DoGear(args.Value<string>("rows")); break;
                    case "background":
                        _flash.LoadBackground(args.Value<string>("name")); break;
                    case "reset":
                        _flash.ResetFlash(); break;
                    case "loadItem":
                        _flash.LoadItem(args.Value<string>("ItemType"), args.Value<string>("ItemFile"), args.Value<string>("WeaponType")); break;
                    case "__ipcStuck":
                        Dbg("IPC STUCK msgId=" + args.Value<int>("msgId") + " method=" + args.Value<string>("method"));
                        RecoverPage(); break;
                    case "loadChar":
                        result = await LoadCharAsync(args.Value<string>("name")); break;
                    case "downloadAll":
                        result = await DownloadAllAsync(args, id); break;
                    case "pickFolder":
                        result = PickFolder(); break;
                    case "getAppDir":
                        result = JValue.CreateString(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\')); break;
                    case "getLog":
                        result = JValue.CreateString(GetLog()); break;
                    case "clearLog":
                        ClearLog(); break;
                    case "saveFile":
                        result = SaveFile(args.Value<string>("data"), args.Value<string>("name")); break;
                case "openFile":
                    result = OpenFile(); break;
            }
            }
            catch (Exception ex)
            {
                Dbg("Dispatch " + method + ": " + ex);
                result = JValue.CreateString(ex.Message);
            }
            PostResult(id, result);
            return result;
        }

        void PostResult(int id, JToken result)
        {
            try
            {
                if (_core == null) return;
                var obj = new JObject { { "id", id }, { "result", result ?? JValue.CreateNull() } };
                Dbg("-> result id=" + id);
                _core.PostWebMessageAsJson(obj.ToString(Formatting.None));
            }
            catch (Exception ex) { Dbg("PostResult: " + ex); }
        }

        void PostEvent(JToken obj)
        {
            try
            {
                if (_core == null) return;
                Dbg("-> ev " + obj["ev"]);
                _core.PostWebMessageAsJson(obj.ToString(Formatting.None));
            }
            catch (Exception ex) { Dbg("PostEvent: " + ex); }
        }

        // ---- preview rect -> position the Flash sibling over the webview ----
        void PositionFlash(JToken args)
        {
            if (_flash == null) return;
            int x = args.Value<int>("x");
            int y = args.Value<int>("y");
            int w = args.Value<int>("w");
            int h = args.Value<int>("h");
            if (w <= 0 || h <= 0) { _flash.Visible = false; if (_gear != null && _gear.Visible) _gear.Hide(); if (_cos != null && _cos.Visible) _cos.Hide(); return; }
            _flash.SetBounds(x, y, w, h);
            _flash.Visible = true;
            _flash.BringToFront();
            FollowGear();
        }

        void FollowGear()
        {
            if (_flash == null || !_flash.Visible) return;
            var b = _flash.Bounds;
            if (_gear != null)
            {
                _gear.Follow(b, p => PointToScreen(p));
                if (_gear.HasRows && !_gear.Visible)
                {
                    try { if (!IsDisposed) _gear.Show(this); } catch { }
                }
            }
            // Shirt button tracks the preview's bottom-right corner
            // (CharPage parity), independent of the gear list.
            if (_cos != null)
            {
                if (!_cos.Visible)
                {
                    try { if (!IsDisposed) _cos.Show(this); } catch { try { _cos.Show(); } catch { } }
                    Dbg("cos show Visible=" + _cos.Visible);
                }
                // Always repaint: if the layered surface was dropped without
                // the handle going away (Visible stays true, nothing logs),
                // the button looks gone until the next repaint. A 52px blit
                // is cheap enough to redo on every follow + watchdog tick.
                _cos.SetState(AppDomain.CurrentDomain.BaseDirectory, _cosHas, _cosOn);
                _cosPushedHas = _cosHas; _cosPushedOn = _cosOn;
                _cos.Follow(b, p => PointToScreen(p));
            }
            // Magnifier summons the loader line; sits left of the shirt.
            if (_mag != null)
            {
                if (!_mag.Visible)
                {
                    try { if (!IsDisposed) _mag.Show(this); } catch { try { _mag.Show(); } catch { } }
                    Dbg("mag show Visible=" + _mag.Visible + " flash=" + _flash.Bounds.ToString());
                    // Static button: paint once per show, not every tick.
                    _mag.SetState(AppDomain.CurrentDomain.BaseDirectory, true, false);
                }
                _mag.Follow(b, p => PointToScreen(p));
                if (_cos != null && _cos.Visible)
                    _mag.Location = new Point(_cos.Location.X - _mag.Width - 8, _cos.Location.Y);
            }
            // Loader line stays centered while visible.
            if (_loader != null && _loader.Visible) CenterLoader();
        }

        void CenterLoader()
        {
            if (_loader == null || IsDisposed) return;
            try
            {
                // Upper third: clear of the avatar, clear of the corners.
                int x = ClientSize.Width / 2 - _loader.Width / 2;
                int y = (int)(ClientSize.Height * 0.28) - _loader.Height / 2;
                _loader.Location = PointToScreen(new Point(x, y));
            }
            catch { }
        }

        void PresentLoader()
        {
            if (_loader == null || IsDisposed) return;
            _loaderPresented = true;
            CenterLoader();
            _loader.Present();
        }

        void ToggleLoader()
        {
            if (_loader == null || IsDisposed) return;
            if (_loader.Visible)
            {
                try { _loader.Hide(); } catch { }
            }
            else PresentLoader();
        }

        void OnLoaderCommit(string name)
        {
            Dbg("Loader commit: " + name);
            lock (_loadLock) { _pendingLoad = name; }
            try { if (_loader != null) _loader.Hide(); } catch { }
        }

        public string TakePendingLoad()
        {
            lock (_loadLock) { string n = _pendingLoad; _pendingLoad = null; return n ?? ""; }
        }

        // Rounded top-level window corners (Windows 11; silent no-op older).
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // Loader focus guard: something (Flash boot/content changes) can
        // yank focus out of the typing line seconds after it presents. These
        // let the watchdog pull it back only when safe: never from another
        // app, never from the page itself (drawer inputs live there).
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")]
        static extern IntPtr GetFocus();
        [DllImport("user32.dll")]
        static extern IntPtr GetParent(IntPtr hWnd);

        static bool ForegroundIsOurs()
        {
            try
            {
                uint pid;
                GetWindowThreadProcessId(GetForegroundWindow(), out pid);
                return pid == (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            }
            catch { return false; }
        }

        bool FocusInPage()
        {
            try
            {
                if (_web == null || _web.IsDisposed) return false;
                IntPtr target = _web.Handle;
                IntPtr h = GetFocus();
                while (h != IntPtr.Zero)
                {
                    if (h == target) return true;
                    h = GetParent(h);
                }
            }
            catch { }
            return false;
        }

        void ApplyWindowRounding()
        {
            try
            {
                int pref = 2;
                DwmSetWindowAttribute(Handle, 33, ref pref, sizeof(int));
            }
            catch { }
        }

        // ---- window drag via caption trick ----
        void StartDrag()
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
        }

        // ---- wrappers callable from the FbHost COM proxy (UI thread) ----
        public void DoMinimize() { WindowState = FormWindowState.Minimized; }
        public void DoMaximize()
        {
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            PostEvent(new JObject { { "ev", "win-state" }, { "max", WindowState == FormWindowState.Maximized } });
        }
        public void DoClose() { Close(); }
        public void DoStartDrag() { StartDrag(); }
        public void DoReportRect(int x, int y, int w, int h) { PositionFlash(new JObject { { "x", x }, { "y", y }, { "w", w }, { "h", h } }); }
        // Page -> overlay: {has (character owns a cosmetic set), on}.
        public void DoCosmeticsButton(string stateJson)
        {
            if (_cos == null) return;
            bool has = false, on = false;
            try
            {
                var o = JObject.Parse(stateJson ?? "{}");
                has = o.Value<bool>("has");
                on = o.Value<bool>("on");
            }
            catch { }
            _cosHas = has; _cosOn = on;
            _cos.SetState(AppDomain.CurrentDomain.BaseDirectory, has, on);
            _cosPushedHas = has; _cosPushedOn = on;
        }
        // Overlay -> page: bumped on every shirt-button click; the page
        // polls CosmeticsSeq and toggles when it changes.
        public int CosmeticsToggleSeq { get { return _cosToggleSeq; } }
        void OnCosmeticsClicked()
        {
            Dbg("Cosmetics overlay clicked");
            _cosToggleSeq++;
        }
        public void DoFlashCall(string method, string paramsJson)
        {
            if (_flash == null) return;
            string[] ps = null;
            try { ps = JsonConvert.DeserializeObject<string[]>(paramsJson); } catch { }
            _flash.FlashCall(method, ps ?? new string[0]);
        }
        public void DoGear(string rowsJson)
        {
            if (_gear == null || _flash == null) return;
            JArray rows = null;
            try { rows = JArray.Parse(rowsJson ?? "[]"); } catch { }
            string key = "";
            if (rows != null)
            {
                foreach (var r in rows)
                {
                    var o = r as JObject;
                    if (o != null) key += (o.Value<string>("name") ?? "") + "|";
                }
            }
            if (key != _gearKey) { _gearKey = key; _hidden.Clear(); if (_flash != null) _flash.ClearHelmCache(); }
            _gear.SetRows(AppDomain.CurrentDomain.BaseDirectory, rows);
            if (!_gear.HasRows) return;
            try
            {
                if (!IsDisposed && _gear.Owner != this) _gear.Show(this);
            }
            catch { try { _gear.Show(); } catch { } }
            FollowGear();
        }

        // Slot icons shown before any character loads (dimmed, unnamed): the
        // gear icons double as SWF drop targets from the start.
        static JArray DefaultGearRows()
        {
            var rows = new JArray();
            foreach (var t in new[] { "weapon", "armor", "helm", "cape", "pet", "misc" })
            {
                rows.Add(new JObject
                {
                    { "icon", t },
                    { "name", "" },
                    { "dim", true }
                });
            }
            return rows;
        }

        readonly Dictionary<string, bool> _hidden = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        string _gearKey = "";

        static string HideMethodFor(string icon)
        {
            switch ((icon ?? "").ToLowerInvariant())
            {
                case "weapon": return "unarmed";
                case "armor": return "hideArmor";
                case "helm": return "hideHelm";
                case "cape": return "hideCape";
                case "pet": return "hidePet";
                case "misc": return "hideMisc";
                case "hair": return "hideHair";
                default: return null;
            }
        }

        void OnGearIcon(string icon)
        {
            if (_flash == null) return;
            string method = HideMethodFor(icon);
            if (method == null) return;
            bool hidden = !_hidden.TryGetValue(icon, out hidden) || !hidden;
            _hidden[icon] = hidden;
            // Player convention (hideHelm etc.): visible = (arg == "False").
            // Helm unhide goes through the reload path (backhair-safe).
            if (icon.Equals("helm", StringComparison.OrdinalIgnoreCase) && !hidden)
            {
                _flash.UnhideHelm();
                return;
            }
            _flash.FlashCall(method, new[] { hidden ? "True" : "False" });
        }
        public string DoFlashQuery(string method) { if (_flash == null) return ""; return _flash.QueryFlash(method); }
        public void DoBackground(string name) { if (_flash != null) _flash.LoadBackground(name); }
        public void DoReset() { if (_flash != null) _flash.ResetFlash(); }
        public void DoLoadItem(string itemType, string itemFile, string weaponType) { if (_flash != null) _flash.LoadItem(itemType, itemFile, weaponType); }
        public void DoDropFile(string slot, string path)
        {
            Dbg("DoDropFile/native slot=" + slot + " path=" + path);
            if (_flash == null || string.IsNullOrEmpty(path)) return;
            if (!File.Exists(path) || !path.EndsWith(".swf", StringComparison.OrdinalIgnoreCase)) return;
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch { return; }
            if (bytes.Length < 4) return;
            string wt = slot.Equals("Weapon", StringComparison.OrdinalIgnoreCase) ? FlashCore.DetectWeaponType(bytes) : null;
            _flash.LoadItemBytes(slot, bytes, Path.GetFileName(path), wt);
        }
        public void DoDropItem(string itemType, string fileName, string base64)
        {
            Dbg("DoDropItem/ui type=" + itemType + " file=" + fileName);
            if (_flash == null) return;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(base64 ?? ""); }
            catch { Dbg("DropItem bad data: " + fileName); return; }
            if (bytes.Length < 4 || bytes[1] != (byte)'W' || bytes[2] != (byte)'S' ||
                (bytes[0] != (byte)'F' && bytes[0] != (byte)'C' && bytes[0] != (byte)'Z'))
            { Dbg("DropItem not an SWF: " + fileName); return; }
            string wt = string.Equals(itemType, "Weapon", StringComparison.OrdinalIgnoreCase)
                ? FlashCore.DetectWeaponType(bytes) : null;
            _flash.LoadItemBytes(itemType, bytes, fileName ?? "dropped.swf", wt);
        }
        public string DoPickFolder() { return PickFolder(); }
        public string DoSaveFile(string data, string name) { return SaveFile(data, name); }
        public string DoOpenFile() { return OpenFile(); }

        // ---- character fetch + parse (port of main.js fb-fetch-char) ----
        async Task<JToken> LoadCharAsync(string name)
        {
            string body = null;
            foreach (var url in new[]
            {
                "https://account.aq.com/CharPage?id=" + Uri.EscapeDataString(name),
                "http://www.aq.com/character.asp?id=" + Uri.EscapeDataString(name)
            })
            {
                body = await Downloads.GetAsync(url);
                if (body != null) break;
            }
            if (body == null)
                return Error("Could not reach the character page.");
            var vars = Downloads.ParseFlashVars(body);
            if (vars == null)
                return Error("No FlashVars on that character page.");
            var obj = new JObject { { "ok", true } };
            var v = new JObject();
            foreach (var kv in vars) v[kv.Key] = kv.Value;
            obj["vars"] = v;
            return obj;
        }

        // ---- download all item SWFs (port of main.js fb-download-all) ----
        async Task<JToken> DownloadAllAsync(JToken args, int reqId)
        {
            string dir = args.Value<string>("dir");
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var vo = args["vars"] as JObject;
            if (vo != null)
                foreach (var p in vo.Properties())
                    vars[p.Name] = p.Value.Value<string>();

            var items = Downloads.BuildDownloads(vars);
            try { Directory.CreateDirectory(dir); } catch { }

            var results = new JArray();
            int total = items.Count;
            for (int i = 0; i < total; i++)
            {
                var it = items[i];
                string dest = Path.Combine(dir, it.File);
                bool ok = await Downloads.DownloadAsync(it.Url, dest);
                results.Add(new JObject
                {
                    { "type", it.Type },
                    { "file", it.File },
                    { "ok", ok },
                    { "path", ok ? dest : "" },
                    { "cosmetic", it.Cosmetic }
                });
                PostEvent(new JObject
                {
                    { "ev", "progress" },
                    { "index", i + 1 },
                    { "total", total },
                    { "type", it.Type },
                    { "ok", ok },
                    { "file", it.File },
                    { "cosmetic", it.Cosmetic }
                });
            }

            return new JObject
            {
                { "ok", true },
                { "items", results },
                { "dir", dir }
            };
        }

        static JToken Error(string message)
        {
            return new JObject { { "ok", false }, { "error", message } };
        }

        // ---- folder/file dialogs ----
        string PickFolder()
        {
            using (var dlg = new FolderBrowserDialog { Description = "Choose output folder for SWFs" })
            {
                return dlg.ShowDialog(this) == DialogResult.OK ? dlg.SelectedPath : "";
            }
        }

        string SaveFile(string data, string defaultName)
        {
            using (var dlg = new SaveFileDialog
            {
                Filter = "FlashBox project (*.fbp)|*.fbp|JSON (*.json)|*.json|All files (*.*)|*.*",
                FileName = string.IsNullOrEmpty(defaultName) ? "flashbox.fbp" : defaultName,
                AddExtension = true
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return "";
                try { File.WriteAllText(dlg.FileName, data ?? ""); return dlg.FileName; }
                catch { return ""; }
            }
        }

        string OpenFile()
        {
            using (var dlg = new OpenFileDialog
            {
                Filter = "FlashBox project (*.fbp)|*.fbp|JSON (*.json)|*.json|All files (*.*)|*.*"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return "";
                try
                {
                    string data = File.ReadAllText(dlg.FileName);
                    return JsonConvert.SerializeObject(new { path = dlg.FileName, data });
                }
                catch { return ""; }
            }
        }

        // ---- custom frame: real OS window, client covers everything ----
        // Keeping WS_CAPTION/WS_THICKFRAME gives us OS minimize/maximize
        // animations, Aero snap, taskbar thumbnails and shadow. Stripping the
        // non-client area below hides the native titlebar so only our dark
        // bar shows. Edge resizing stays native (no custom branch needed).
        const int WM_NCLBUTTONDOWN = 0xA1;
        const int HTCAPTION = 0x2;
        const int WM_NCCALCSIZE = 0x83;
        const int WM_NCHITTEST = 0x84;
        const int WM_GETMINMAXINFO = 0x24;

        [StructLayout(LayoutKind.Sequential)]
        struct MINMAXINFO
        {
            public Point reserved;
            public Size maxSize;
            public Point maxPos;
            public Size minTrack;
            public Size maxTrack;
        }

        protected override void WndProc(ref Message m)
        {
            // Borderless maximize must respect the taskbar: clamp to the
            // working area of the monitor holding the window (Chrome-style).
            if (m.Msg == WM_GETMINMAXINFO)
            {
                try
                {
                    var wa = Screen.FromHandle(Handle).WorkingArea;
                    var mi = (MINMAXINFO)Marshal.PtrToStructure(m.LParam, typeof(MINMAXINFO));
                    mi.maxPos = new Point(wa.Left, wa.Top);
                    mi.maxSize = new Size(wa.Width, wa.Height);
                    Marshal.StructureToPtr(mi, m.LParam, true);
                }
                catch { }
            }
            if (m.Msg == WM_NCHITTEST && WindowState != FormWindowState.Maximized)
            {
                // Real frame underneath: let the OS do edge resizing natively.
                base.WndProc(ref m);
                return;
            }
            // Hide the native titlebar/borders: with a real frame kept, the
            // OS still supplies animations, snap, shadow and edge resizing.
            if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        [DllImport("user32.dll")]
        static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wp, IntPtr lp);
    }
}
