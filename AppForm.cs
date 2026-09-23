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
                    bool ok = Downloads.DownloadAsync(it.Url, dest).GetAwaiter().GetResult();
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

        readonly List<Tuple<Rectangle, string>> _hits = new List<Tuple<Rectangle, string>>();

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
                        for (int i = 0; i < _rows.Count; i++)
                        {
                            var r = _rows[i];
                            int rh = heights[i];
                            int iy = y + (rh - IconSize) / 2;
                            if (r.Icon != null)
                            {
                                _hits.Add(Tuple.Create(new Rectangle(PadX, iy, IconSize, IconSize), r.Key));
                                using (var sil = MakeSilhouette(r.Icon))
                                {
                                    for (int ox = -1; ox <= 1; ox++)
                                        for (int oy = -1; oy <= 1; oy++)
                                            if (ox != 0 || oy != 0)
                                                g.DrawImage(sil, PadX + ox, iy + oy, IconSize, IconSize);
                                }
                                g.DrawImage(r.Icon, PadX, iy, IconSize, IconSize);
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

    public class AppForm : Form
    {
        WebView2 _web;
        CoreWebView2 _core;
        FlashCore _flash;
        HudOverlay _gear;
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
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, 680);
            MinimumSize = new Size(640, 480);
            BackColor = UiBg;

            _web = new WebView2 { Dock = DockStyle.Fill };
            Controls.Add(_web);

            Load += OnLoad;
        }

        async void OnLoad(object s, EventArgs e)
        {
            try
            {
                await _web.EnsureCoreWebView2Async(null);
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

                // FlashCore is created AFTER the WebView2 channel is up, so the
                // legacy Flash ActiveX can't corrupt WebView2 init (seen before:
                // C#->JS web messages silently never arriving).
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                _flash = new FlashCore(baseDir);
                _flash.Bounds = new Rectangle(0, 0, 0, 0);
                _flash.Visible = false;
                _flash.Ready += OnFlashReady;
                Controls.Add(_flash);
                _flash.BringToFront();
                _web.BringToFront();
                _flash.BringToFront();

                _gear = new HudOverlay();
                _gear.IconClicked += OnGearIcon;
                LocationChanged += (o, ev) => FollowGear();
                SizeChanged += (o, ev) => FollowGear();

                string html = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui", "index.html");
                _core.Navigate("file:///" + html.Replace('\\', '/'));
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 init failed: " + ex.Message, "FlashBox");
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
            if (w <= 0 || h <= 0) { _flash.Visible = false; if (_gear != null && _gear.Visible) _gear.Hide(); return; }
            _flash.SetBounds(x, y, w, h);
            _flash.Visible = true;
            _flash.BringToFront();
            FollowGear();
        }

        void FollowGear()
        {
            if (_gear == null || _flash == null || !_gear.Visible || !_flash.Visible) return;
            var b = _flash.Bounds;
            _gear.Follow(b, p => PointToScreen(p));
        }

        // ---- window drag via caption trick ----
        void StartDrag()
        {
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
        }

        // ---- wrappers callable from the FbHost COM proxy (UI thread) ----
        public void DoMinimize() { WindowState = FormWindowState.Minimized; }
        public void DoMaximize() { WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; }
        public void DoClose() { Close(); }
        public void DoStartDrag() { StartDrag(); }
        public void DoReportRect(int x, int y, int w, int h) { PositionFlash(new JObject { { "x", x }, { "y", y }, { "w", w }, { "h", h } }); }
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
            if (key != _gearKey) { _gearKey = key; _hidden.Clear(); }
            _gear.SetRows(AppDomain.CurrentDomain.BaseDirectory, rows);
            if (!_gear.HasRows) return;
            try
            {
                if (!IsDisposed && _gear.Owner != this) _gear.Show(this);
            }
            catch { try { _gear.Show(); } catch { } }
            FollowGear();
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
            _flash.FlashCall(method, new[] { hidden ? "True" : "False" });
        }
        public string DoFlashQuery(string method) { if (_flash == null) return ""; return _flash.QueryFlash(method); }
        public void DoBackground(string name) { if (_flash != null) _flash.LoadBackground(name); }
        public void DoReset() { if (_flash != null) _flash.ResetFlash(); }
        public void DoLoadItem(string itemType, string itemFile, string weaponType) { if (_flash != null) _flash.LoadItem(itemType, itemFile, weaponType); }
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

        // ---- custom resize borders (FormBorderStyle = None) ----
        const int WM_NCLBUTTONDOWN = 0xA1;
        const int HTCAPTION = 0x2;
        const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14,
                  HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        const int WM_NCHITTEST = 0x84;
        const int border = 8;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && WindowState != FormWindowState.Maximized)
            {
                var p = PointToClient(new Point(m.LParam.ToInt32()));
                int x = p.X, y = p.Y, w = ClientSize.Width, h = ClientSize.Height;
                bool l = x < border, r = x >= w - border, t = y < border, b = y >= h - border;
                if (l && t) m.Result = new IntPtr(HTTOPLEFT);
                else if (r && t) m.Result = new IntPtr(HTTOPRIGHT);
                else if (l && b) m.Result = new IntPtr(HTBOTTOMLEFT);
                else if (r && b) m.Result = new IntPtr(HTBOTTOMRIGHT);
                else if (l) m.Result = new IntPtr(HTLEFT);
                else if (r) m.Result = new IntPtr(HTRIGHT);
                else if (t) m.Result = new IntPtr(HTTOP);
                else if (b) m.Result = new IntPtr(HTBOTTOM);
                else base.WndProc(ref m);
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
