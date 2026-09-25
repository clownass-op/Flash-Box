using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using AxShockwaveFlashObjects;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace FlashBoxApp
{
    // FlashCore: hosts the real Flash ActiveX and the char6 avatar player
    // in-process. This is the combined-app version of FlashHost2 -- no pipe,
    // no external process. The control is positioned by AppForm over the UI's
    // preview region.
    class FlashCore : Panel
    {
        static readonly string DebugLog = Path.Combine(Path.GetTempPath(), "fb-debug.log");

        static void Dbg(string line)
        {
            try { System.IO.File.AppendAllText(DebugLog, DateTime.Now.ToString("HH:mm:ss.fff") + " [flash] " + line + Environment.NewLine); } catch { }
        }

        AxShockwaveFlash _flash;
        string _baseDir;
        byte[] _char6;
        Dictionary<string, byte[]> _backgrounds = new Dictionary<string, byte[]>();

        public event Action Ready;

        public FlashCore(string baseDir)
        {
            _baseDir = baseDir;
            BackColor = Color.FromArgb(0x2D, 0x2D, 0x30);
            CreateFlash();
        }

        bool _started;

        void CreateFlash()
        {
            _flash = new AxShockwaveFlash();
            ((System.ComponentModel.ISupportInitialize)_flash).BeginInit();
            SuspendLayout();
            _flash.Dock = DockStyle.Fill;
            _flash.Enabled = true;
            _flash.AllowDrop = true;
            Controls.Add(_flash);
            ((System.ComponentModel.ISupportInitialize)_flash).EndInit();
            ResumeLayout(true);
            _flash.HandleCreated += OnFlashHandleCreated;
            if (_flash.IsHandleCreated) OnFlashHandleCreated(this, EventArgs.Empty);
        }

        void OnFlashHandleCreated(object sender, EventArgs e)
        {
            if (_started) return;
            _started = true;
            LoadPlayer();
            var r = Ready;
            if (r != null) r();
        }

        void LoadPlayer()
        {
            // Always re-read char6.swf from disk (not cached): a rebuild/repatch
            // must take effect on the next reset without restarting the app.
            {
                // char6.swf now has native loadMisc/hideMisc (ground runes).
                // char6-orig.swf is the untouched backup.
                string p = Path.Combine(_baseDir, "char6.swf");
                if (!File.Exists(p)) p = Path.Combine(_baseDir, "flash", "char6.swf");
                if (File.Exists(p)) _char6 = File.ReadAllBytes(p);
                Dbg("player loaded: " + (_char6 != null ? _char6.Length + " bytes from " + p : "NOT FOUND"));
                // Background scenes: every *.swf in flash\ (or the app dir)
                // except the player itself. Drop a new cp-*.swf in and it
                // just shows up in the list.
                _backgrounds.Clear();
                foreach (var dir in new[] { Path.Combine(_baseDir, "flash"), _baseDir })
                {
                    if (!Directory.Exists(dir)) continue;
                    string[] files;
                    try { files = Directory.GetFiles(dir, "*.swf"); }
                    catch { continue; }
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    foreach (var fp in files)
                    {
                        string key = Path.GetFileNameWithoutExtension(fp);
                        if (key.Equals("char6", StringComparison.OrdinalIgnoreCase) ||
                            key.Equals("char6-orig", StringComparison.OrdinalIgnoreCase)) continue;
                        if (_backgrounds.ContainsKey(key)) continue;
                        try { _backgrounds[key] = File.ReadAllBytes(fp); } catch { }
                    }
                }
                Dbg("backgrounds loaded: " + _backgrounds.Count);
            }
            if (_char6 == null) return;
            try { LoadMovie(_char6); Dbg("LoadMovie done, player running"); } catch (Exception ex) { Dbg("LoadMovie EX: " + ex.Message); }
        }

        // LoadMovie via OcxState (mirrors FlashBox.Extensions.LoadMovie)
        void LoadMovie(byte[] swf)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(8 + swf.Length);
                bw.Write(1432769894);
                bw.Write(swf.Length);
                bw.Write(swf);
                ms.Seek(0, SeekOrigin.Begin);
                ((AxHost)_flash).OcxState = new AxHost.State(ms, 1, false, null);
            }
        }

        // ---- commands driven by the UI ----
        public void FlashCall(string method, string[] args)
        {
            if (_flash == null) return;
            try { Call(method, args ?? new string[0]); } catch { }
        }

        public void LoadItem(string itemType, string itemFile, string weaponType)
        {
            if (string.IsNullOrEmpty(itemFile) || !File.Exists(itemFile))
            {
                Dbg("LoadItem " + itemType + " MISSING file: " + itemFile);
                return;
            }
            ItemType type;
            if (!Enum.TryParse(itemType, true, out type)) { Dbg("LoadItem unknown type: " + itemType); return; }
            byte[] bytes;
            try { bytes = File.ReadAllBytes(itemFile); }
            catch (Exception ex) { Dbg("LoadItem read fail: " + ex.Message); return; }
            LoadItemBytes(itemType, bytes, Path.GetFileName(itemFile), weaponType);
        }

        // Same as LoadItem but from in-memory bytes (drag-and-drop SWFs that
        // were never saved to disk).
        public void LoadItemBytes(string itemType, byte[] bytes, string name, string weaponType)
        {
            if (bytes == null || bytes.Length < 4) return;
            ItemType type;
            if (!Enum.TryParse(itemType, true, out type)) { Dbg("LoadItem unknown type: " + itemType); return; }
            try
            {
                string linkage = LinkageParser.Parse(bytes, type, this);
                string b64 = Convert.ToBase64String(bytes);
                Dbg("LoadItem " + itemType + " " + name + " size=" + bytes.Length + " base64=" + b64.Length + " linkage=" + (linkage ?? "NULL"));
                if (type == ItemType.Helm)
                {
                    _helmB64 = b64;
                    _helmLink = linkage ?? "";
                    _helmLoaded = true;
                }
                if (type == ItemType.Weapon)
                {
                    // Dual sets (Dagger) split across weapon/weaponOff and leave
                    // the player in dual mode: a later single weapon would then
                    // mirror into the off hand too. Sync daggerMode to the
                    // incoming weapon FIRST so the load attaches correctly.
                    bool dual = string.Equals(weaponType, "Dagger", StringComparison.OrdinalIgnoreCase);
                    Call("daggerMode", dual ? "True" : "False");
                    if (!string.IsNullOrEmpty(weaponType))
                        Call("loadWeapon", b64, linkage ?? "", weaponType);
                    else
                        Call("loadWeapon", b64, linkage ?? "");
                }
                else
                    Call("load" + type.ToString(), b64, linkage ?? "");
            }
            catch (Exception ex) { Dbg("LoadItem fail: " + ex.Message); }
        }

        string _helmB64;
        string _helmLink;
        bool _helmLoaded;

        public void ClearHelmCache() { _helmB64 = null; _helmLink = null; _helmLoaded = false; }

        // Unhide by reloading the current helm instead of a bare
        // hideHelm("False"): the player's unhide unconditionally re-shows
        // the backhair clip, which pops a stale template backhair behind
        // helms that define no "<link>_backhair" symbol (e.g. full-head
        // morphs). Reloading re-runs the load completion logic, which only
        // shows backhair when the symbol exists. Falls back to the plain
        // unhide when no helm is cached for this outfit.
        public void UnhideHelm()
        {
            if (_helmLoaded && _helmB64 != null)
            {
                Dbg("UnhideHelm via reload link=" + _helmLink);
                try { Call("hideHelm", "False"); } catch { }
                try { Call("loadHelm", _helmB64, _helmLink ?? ""); } catch { }
            }
            else
            {
                FlashCall("hideHelm", new[] { "False" });
            }
        }

        // Split items (sword+shield sets) carry the dual-wield parent check
        // referencing "weaponOff" in their bytecode; plain weapons don't.
        // Used to pick the CharPage-style attach path for dropped weapons.
        public static string DetectWeaponType(byte[] swf)
        {
            try
            {
                byte[] data = swf;
                if (data.Length > 8 && data[0] == (byte)'C' && data[1] == (byte)'W' && data[2] == (byte)'S')
                {
                    using (var ms = new MemoryStream(data, 8, data.Length - 8))
                    using (var inf = new InflaterInputStream(ms))
                    using (var outMs = new MemoryStream())
                    {
                        inf.CopyTo(outMs);
                        data = outMs.ToArray();
                    }
                }
                byte[] needle = Encoding.ASCII.GetBytes("weaponOff");
                for (int i = 0; i + needle.Length <= data.Length; i++)
                {
                    int j = 0;
                    while (j < needle.Length && data[i + j] == needle[j]) j++;
                    if (j == needle.Length) return "Dagger";
                }
            }
            catch { }
            return "";
        }

        // Synchronous no-arg query (used for isReady/getStatus polling).
        // Returns "" when the player has no such callback yet (still loading).
        public string QueryFlash(string method)
        {
            if (_flash == null || string.IsNullOrEmpty(method)) return "";
            try
            {
                string res = _flash.CallFunction(
                    "<invoke name=\"" + method + "\" returntype=\"xml\"></invoke>");
                var node = XElement.Parse(res).FirstNode;
                return node != null ? System.Net.WebUtility.HtmlDecode(node.ToString()) : "";
            }
            catch { return ""; }
        }

        public void LoadBackground(string name)
        {
            byte[] bg;
            if (_backgrounds.TryGetValue(name, out bg))
                try { Call("loadBackground", Convert.ToBase64String(bg)); } catch { }
        }

        public void ResetFlash()
        {
            try
            {
                _flash.Dispose();
                Controls.Remove(_flash);
                _flash = null;
                _started = false;
                CreateFlash();
                var h = _flash.Handle; // force handle creation so the rebuilt control is ready immediately
            }
            catch { }
        }

        // ---- Flash invoke (mirrors FlashBox.Flash.Call) ----
        void Call(string function, params string[] args)
        {
            var sb = new StringBuilder();
            sb.Append("<invoke name=\"").Append(function).Append("\" returntype=\"xml\">");
            if (args != null && args.Length != 0)
            {
                sb.Append("<arguments>");
                foreach (var a in args) sb.Append("<string>").Append(a).Append("</string>");
                sb.Append("</arguments>");
            }
            sb.Append("</invoke>");
            int n = args == null ? 0 : args.Length;
            Dbg("Call " + function + " args=" + n + (n > 0 && args[0] != null ? " firstLen=" + args[0].Length : ""));
            try
            {
                var res = _flash.CallFunction(sb.ToString());
                var node = XElement.Parse(res).FirstNode;
                string val = node != null ? System.Net.WebUtility.HtmlDecode(node.ToString()) : "";
                Dbg("Call " + function + " -> " + (val.Length > 200 ? val.Substring(0, 200) : val));
            }
            catch (Exception ex)
            {
                // The player's ExternalInterface may not be up yet right after
                // reset/LoadMovie; give it one beat and retry once.
                if (ex.Message.Contains("E_FAIL"))
                {
                    System.Threading.Thread.Sleep(150);
                    try
                    {
                        var res = _flash.CallFunction(sb.ToString());
                        var node = XElement.Parse(res).FirstNode;
                        string val = node != null ? System.Net.WebUtility.HtmlDecode(node.ToString()) : "";
                        Dbg("Call " + function + " (retry) -> " + (val.Length > 200 ? val.Substring(0, 200) : val));
                        return;
                    }
                    catch (Exception ex2) { Dbg("Call " + function + " EX: " + ex2.Message); return; }
                }
                Dbg("Call " + function + " EX: " + ex.Message);
            }
        }
    }

    enum ItemType : byte
    {
        Hair, Helm, Armor, Cape, Weapon, Pet, Misc
    }

    // LinkageParser: scans SWF tags (SymbolClass tag 76) for item linkage names.
    static class LinkageParser
    {
        public static string Parse(byte[] swf, ItemType type, FlashCore host)
        {
            switch (type)
            {
                case ItemType.Hair: return ParseHair(swf, host);
                case ItemType.Helm: return ParseHelm(swf);
                case ItemType.Armor: return ParseArmor(swf, host);
                case ItemType.Misc: return ParseMisc(swf);
                default: return ParseFirst(swf);
            }
        }

        static string ParseFirst(byte[] swf)
        {
            var r = Open(swf);
            if (r == null) return null;
            try
            {
                while (r.BaseStream.Position < r.BaseStream.Length)
                {
                    var h = new RecordHeader(r);
                    if (h.TagCode == 76)
                    {
                        r.ReadUInt16();
                        r.ReadUInt16();
                        return ReadString(r);
                    }
                    r.ReadBytes((int)h.TagLength);
                }
                return null;
            }
            finally { r.Close(); }
        }

        static string ParseHair(byte[] swf, FlashCore host)
        {
            var r = Open(swf);
            if (r == null) return null;
            try
            {
                while (r.BaseStream.Position < r.BaseStream.Length)
                {
                    var h = new RecordHeader(r);
                    if (h.TagCode == 76)
                    {
                        ushort count = r.ReadUInt16();
                        for (ushort i = 0; i < count; i++)
                        {
                            r.ReadUInt16();
                            string name = ReadString(r);
                            if (name == null) continue;
                            if (name.EndsWith("MHairBack") || name.EndsWith("FHairBack"))
                            {
                                host.FlashCall("setGender", new[] { name.EndsWith("MHairBack") ? "M" : "F" });
                                return name.Substring(0, name.Length - 9);
                            }
                            if (name.EndsWith("MHair") || name.EndsWith("FHair"))
                            {
                                host.FlashCall("setGender", new[] { name.EndsWith("MHair") ? "M" : "F" });
                                return name.Substring(0, name.Length - 5);
                            }
                        }
                    }
                    else
                    {
                        r.ReadBytes((int)h.TagLength);
                    }
                }
                return null;
            }
            finally { r.Close(); }
        }

        // Helms with hair (e.g. cmagicianHLocksHat) export an extra
        // "<base>_backhair" symbol first; attaching that as the helm puts a
        // locks blob on the face. The CharPage player instead takes the base
        // helm symbol and derives base+"_backhair" for the backhair clip, so
        // prefer the first linkage that is neither _fla nor *_backhair.
        static string ParseHelm(byte[] swf)
        {
            var r = Open(swf);
            if (r == null) return null;
            try
            {
                string fallback = null;
                while (r.BaseStream.Position < r.BaseStream.Length)
                {
                    var h = new RecordHeader(r);
                    if (h.TagCode == 76)
                    {
                        ushort count = r.ReadUInt16();
                        for (ushort i = 0; i < count; i++)
                        {
                            r.ReadUInt16();
                            string name = ReadString(r);
                            if (name == null || name.Contains("_fla")) continue;
                            if (fallback == null) fallback = name;
                            if (!name.EndsWith("_backhair", StringComparison.OrdinalIgnoreCase)
                                && !name.EndsWith("backhair", StringComparison.OrdinalIgnoreCase)
                                && !name.EndsWith("hairback", StringComparison.OrdinalIgnoreCase))
                                return name;
                        }
                    }
                    else
                    {
                        r.ReadBytes((int)h.TagLength);
                    }
                }
                return fallback;
            }
            finally { r.Close(); }
        }

        // Ground runes (items/grounds/*.swf) export the rune name plus a
        // "<name>_fla.*" artifact (e.g. DSVGroundSymbol + DSVGroundSymbol_fla.gcc_4).
        // Same skip-_fla rule as helms.
        static string ParseMisc(byte[] swf)
        {
            var r = Open(swf);
            if (r == null) return null;
            try
            {
                while (r.BaseStream.Position < r.BaseStream.Length)
                {
                    var h = new RecordHeader(r);
                    if (h.TagCode == 76)
                    {
                        ushort count = r.ReadUInt16();
                        for (ushort i = 0; i < count; i++)
                        {
                            r.ReadUInt16();
                            string name = ReadString(r);
                            if (name != null && !name.Contains("_fla"))
                                return name;
                        }
                    }
                    else
                    {
                        r.ReadBytes((int)h.TagLength);
                    }
                }
                return null;
            }
            finally { r.Close(); }
        }

        static string ParseArmor(byte[] swf, FlashCore host)
        {
            string[] suffixes = { "MShin", "FShin", "MChest", "FChest", "MHand", "FHand",
                                  "MShoulder", "FShoulder", "MThigh", "FThigh", "MFoot", "FFoot",
                                  "MFootIdle", "FFootIdle", "MHip", "FHip" };
            var r = Open(swf);
            if (r == null) return null;
            try
            {
                while (r.BaseStream.Position < r.BaseStream.Length)
                {
                    var h = new RecordHeader(r);
                    if (h.TagCode == 76)
                    {
                        ushort count = r.ReadUInt16();
                        for (ushort i = 0; i < count; i++)
                        {
                            r.ReadUInt16();
                            string name = ReadString(r);
                            if (name == null) continue;
                            foreach (var suf in suffixes)
                            {
                                if (name.EndsWith(suf))
                                {
                                    host.FlashCall("setGender", new[] { suf[0] == 'M' ? "M" : "F" });
                                    return name.Substring(0, name.Length - suf.Length);
                                }
                            }
                        }
                    }
                    else
                    {
                        r.ReadBytes((int)h.TagLength);
                    }
                }
                return null;
            }
            finally { r.Close(); }
        }

        static BinaryReader Open(byte[] swf)
        {
            try
            {
                byte[] data = swf;
                if (data.Length > 0 && data[0] == (byte)'C')
                    data = Inflate(swf);
                var ms = new MemoryStream(data);
                ms.Position = FirstTagOffset(data);
                return new BinaryReader(ms, Encoding.UTF8);
            }
            catch { return null; }
        }

        // Walk the SWF header (signature/version/length + bit-packed RECT +
        // frame rate/count) to find where the first tag record begins.
        static int FirstTagOffset(byte[] data)
        {
            if (data.Length < 8) return 8;
            int bitPos = 8 * 8;
            int nBits = ReadBits(data, ref bitPos, 5);
            for (int i = 0; i < 4; i++) ReadBits(data, ref bitPos, nBits);
            bitPos = (bitPos + 7) & ~7;
            int pos = (bitPos >> 3) + 4;
            if (pos < 0 || pos > data.Length) pos = 8;
            return pos;
        }

        static int ReadBits(byte[] d, ref int bitPos, int n)
        {
            int v = 0;
            for (int i = 0; i < n; i++)
            {
                int byteIdx = bitPos >> 3;
                int bitIdx = 7 - (bitPos & 7);
                v = (v << 1) | (byteIdx < d.Length ? ((d[byteIdx] >> bitIdx) & 1) : 0);
                bitPos++;
            }
            return v;
        }

        static byte[] Inflate(byte[] swf)
        {
            int fileLength = BitConverter.ToInt32(swf, 4);
            byte[] outBuf = new byte[fileLength];
            Array.Copy(swf, 0, outBuf, 0, 8);
            using (var ms = new MemoryStream(swf, 8, swf.Length - 8))
            using (var inf = new InflaterInputStream(ms))
            {
                inf.Read(outBuf, 8, fileLength - 8);
            }
            outBuf[0] = (byte)'F';
            return outBuf;
        }

        static string ReadString(BinaryReader r)
        {
            var list = new List<byte>();
            while (true)
            {
                byte b = r.ReadByte();
                if (b == 0) break;
                list.Add(b);
            }
            return Encoding.UTF8.GetString(list.ToArray());
        }

        struct RecordHeader
        {
            public readonly int TagCode;
            public readonly uint TagLength;

            public RecordHeader(BinaryReader r)
            {
                ushort n = r.ReadUInt16();
                TagCode = n >> 6;
                TagLength = (uint)(n - (TagCode << 6));
                if (TagLength == 63)
                    TagLength = r.ReadUInt32();
            }
        }
    }
}
