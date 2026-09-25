using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FlashBoxApp
{
    // FlashBox.exe --headless [options]
    //
    // Runs the real app (WebView2 page + Flash ActiveX + char6 player) in an
    // offscreen window with no overlays or dialogs, executes the render
    // regression suite below, writes test-results\report.json/.md plus PNG
    // snapshots, and exits 0 (all passed) / 1 (failures) / 2 (could not run).
    //
    //   --out <dir>        results folder        (default <exe>\test-results)
    //   --fixtures <dir>   item SWF cache        (default <exe>\test-fixtures)
    //   --player <swf>     player to test        (default flash\char6.swf)
    //   --only <a,b>       run only tests whose name contains one of these
    //   --offline          skip tests that need game.aq.com
    //   --timeout <sec>    whole-run watchdog    (default 600)
    static class HeadlessRunner
    {
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int pid);

        public static bool Requested(string[] args)
        {
            return args != null && args.Any(a => string.Equals(a, "--headless", StringComparison.OrdinalIgnoreCase));
        }

        public static int Run(string[] args)
        {
            // WinExe has no console of its own: borrow the parent's so the
            // summary shows up in the terminal that launched the run.
            try
            {
                AttachConsole(-1);
                var o = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                Console.SetOut(o);
            }
            catch { }

            var opt = HeadlessOptions.Parse(args);
            AppForm.Headless = true;
            if (!string.IsNullOrEmpty(opt.Player)) FlashCore.PlayerOverride = Path.GetFullPath(opt.Player);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            int code = 2;
            var form = new AppForm();
            var watchdog = new Timer { Interval = opt.TimeoutSec * 1000 };
            watchdog.Tick += (s, e) =>
            {
                watchdog.Stop();
                Console.WriteLine("HEADLESS: timed out after " + opt.TimeoutSec + "s");
                code = 2;
                form.Close();
            };
            form.Shown += async (s, e) =>
            {
                watchdog.Start();
                try { code = await new HeadlessSuite(form, opt).RunAsync(); }
                catch (Exception ex) { Console.WriteLine("HEADLESS: crashed: " + ex); code = 2; }
                watchdog.Stop();
                form.Close();
            };
            Application.Run(form);
            return code;
        }
    }

    class HeadlessOptions
    {
        public string OutDir, FixtureDir, Player;
        public string[] Only = new string[0];
        public bool Offline;
        public int TimeoutSec = 600;

        public static HeadlessOptions Parse(string[] args)
        {
            string exe = AppDomain.CurrentDomain.BaseDirectory;
            var o = new HeadlessOptions
            {
                OutDir = Path.Combine(exe, "test-results"),
                FixtureDir = Path.Combine(exe, "test-fixtures")
            };
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                string next = i + 1 < args.Length ? args[i + 1] : null;
                switch (a)
                {
                    case "--out": o.OutDir = next; i++; break;
                    case "--fixtures": o.FixtureDir = next; i++; break;
                    case "--player": o.Player = next; i++; break;
                    case "--only": o.Only = (next ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries); i++; break;
                    case "--offline": o.Offline = true; break;
                    case "--timeout": int.TryParse(next, out o.TimeoutSec); i++; break;
                }
            }
            if (o.TimeoutSec <= 0) o.TimeoutSec = 600;
            return o;
        }
    }

    class TestFailure : Exception
    {
        public TestFailure(string m) : base(m) { }
    }

    class HeadlessSuite
    {
        readonly AppForm _form;
        readonly HeadlessOptions _opt;
        readonly List<string> _log = new List<string>();
        string _snapDir;

        // Fixture items (paths relative to the game file root, exactly as a
        // CharPage lists them). Picked for what they exercise:
        const string ArmorRobeF = "classes/F/ccsephA.swf";          // Robe + RobeBack
        const string ArmorRobeOnlyF = "classes/F/BeleenSXY.swf";    // Robe, no RobeBack
        const string ArmorRobeM = "classes/M/PalidanRevamp.swf";    // Robe + RobeBack
        const string ArmorPlainM = "classes/M/warrior2a_skin.swf";  // no robe pieces
        const string ArmorF2 = "classes/F/InfinityAlphaPirateGoldDragon.swf";
        const string HairBackF = "hair/F/Alina3.swf";               // Hair + HairBack
        const string HairM = "hair/M/Normal.swf";
        const string HairMaleOnly = "hair/M/DFWarStyle.swf";        // only an MHair symbol
        const string HelmBackhair = "items/helms/cmagicianHLocksHat.swf"; // helm + _backhair
        const string HelmPlain = "items/helms/RevGenHood1.swf";
        const string Dagger = "items/daggers/mugCysero.swf";
        const string Sword = "items/swords/sword01.swf";
        const string Cape = "items/capes/PalidanRevampCape.swf";
        const string Pet = "items/pets/4up-29Aug16.swf";

        public HeadlessSuite(AppForm form, HeadlessOptions opt) { _form = form; _opt = opt; }

        FlashCore Flash { get { return _form.Flash; } }

        void Log(string s)
        {
            _log.Add(s);
            Console.WriteLine(s);
        }

        // ---------------------------------------------------------------- run

        public async Task<int> RunAsync()
        {
            Directory.CreateDirectory(_opt.OutDir);
            _snapDir = Path.Combine(_opt.OutDir, "snapshots");
            Directory.CreateDirectory(_snapDir);
            foreach (var f in Directory.GetFiles(_snapDir, "*.png")) try { File.Delete(f); } catch { }

            Log("FlashBox headless run " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Log("player: " + (FlashCore.PlayerOverride ?? "flash\\char6.swf"));
            if (!await WaitUntil(() => _form.ReadyForTests && Flash != null, 60000))
            {
                Log("HEADLESS: page/player never came up (WebView2 or Flash ActiveX missing?)");
                return 2;
            }
            if (!await WaitPlayerReady())
            {
                Log("HEADLESS: player has no isReady/getAvatarState callbacks - wrong char6.swf?");
                string boot = Flash.QueryFlash("getBootError");
                if (!string.IsNullOrEmpty(boot)) Log("HEADLESS: player boot error: " + boot);
                return 2;
            }

            var tests = new List<Tuple<string, bool, Func<Task>>>
            {
                // name, needs network (fixtures may already be cached), body
                T("armor_every_piece_attached", true, ArmorEveryPiece),
                T("armor_swap_clears_stale_robe", true, ArmorSwapClearsRobe),
                T("armor_swap_male_plain_hides_robe", true, ArmorSwapMalePlain),
                T("walk_front_foot_uses_armor", true, WalkFrontFoot),
                T("walk_front_foot_hidden_armor", true, WalkFrontFootHiddenArmor),
                T("color_dark_shade_channels", true, ColorDarkChannels),
                T("color_update_recolors_live", true, ColorUpdateLive),
                T("emote_labels_all_play", false, EmoteLabelsPlay),
                T("emote_rest_keeps_head", true, EmoteRestKeepsHead),
                T("emote_unsheath_keeps_head", true, EmoteUnsheathKeepsHead),
                T("emote_stern_loop_count_stable", false, EmoteSternLoops),
                T("hair_backhair_from_hair_file", true, HairBackhair),
                T("helm_hide_shows_hair", true, HelmHideShowsHair),
                T("helm_backhair_survives_late_hair", true, HelmBackhairLateHair),
                T("resize_keeps_facing", false, ResizeKeepsFacing),
                T("dagger_then_sword", true, DaggerThenSword),
                T("armor_back_hand_silhouette", true, BackHandSilhouette),
                T("pet_resize_keeps_facing", true, PetResizeKeepsFacing),
                T("closeuii_before_misc_name", false, CloseUiiBeforeMisc),
                T("flash_call_escapes_names", false, FlashCallEscapes),
                T("ui_hide_checkboxes_toggle", true, UiHideCheckboxes),
                T("ui_vars_apply_colors_gender", true, UiVarsColorsGender),
                T("ui_background_color_clears_scene", false, UiBackgroundColor),
                T("ui_layout_no_clipping", false, UiLayout),
                T("ui_title_bar_color_option", false, UiTitleBar),
                T("ui_copy_log_button", false, UiCopyLog),
                T("ui_gear_icons_sync_checkboxes", true, UiGearIconSync),
                T("loader_line_avoids_gear_list", false, LoaderPlacement),
                T("downloads_cache_keeps_gender_paths", false, DownloadsCachePaths),
                T("linkage_parser_prefers_gender", true, LinkagePrefersGender),
                T("live_charpage_roundtrip", true, LiveCharPage),
                T("live_load_keeps_ui_responsive", true, LiveLoadKeepsUiResponsive),
            };

            var results = new JArray();
            int pass = 0, fail = 0, skip = 0;
            foreach (var t in tests)
            {
                string name = t.Item1;
                if (_opt.Only.Length > 0 && !_opt.Only.Any(x => name.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;
                var r = new JObject { { "name", name } };
                var sw = Stopwatch.StartNew();
                _notes = new List<string>();
                if (name.StartsWith("live_") && _opt.Offline)
                {
                    r["status"] = "skip"; r["detail"] = "offline"; skip++;
                    Log("SKIP " + name + " (offline)");
                    results.Add(r);
                    continue;
                }
                try
                {
                    await ResetPlayer();
                    await t.Item3();
                    r["status"] = "pass"; pass++;
                    Log("PASS " + name + " (" + sw.ElapsedMilliseconds + " ms)");
                }
                catch (Exception ex)
                {
                    bool skipped = ex is FixtureUnavailable;
                    r["status"] = skipped ? "skip" : "fail";
                    r["detail"] = ex is TestFailure || skipped ? ex.Message : ex.ToString();
                    if (skipped) skip++; else fail++;
                    Log((skipped ? "SKIP " : "FAIL ") + name + ": " + ex.Message);
                    try { Snap("FAIL_" + name); } catch { }
                }
                r["ms"] = sw.ElapsedMilliseconds;
                if (_notes.Count > 0) r["notes"] = new JArray(_notes);
                results.Add(r);
            }

            Log(string.Format("RESULT: {0} passed, {1} failed, {2} skipped", pass, fail, skip));
            var report = new JObject
            {
                { "when", DateTime.Now.ToString("o") },
                { "player", FlashCore.PlayerOverride ?? "flash\\char6.swf" },
                { "passed", pass }, { "failed", fail }, { "skipped", skip },
                { "tests", results }
            };
            File.WriteAllText(Path.Combine(_opt.OutDir, "report.json"), report.ToString(Formatting.Indented));
            File.WriteAllText(Path.Combine(_opt.OutDir, "report.md"), ToMarkdown(report));
            return fail == 0 ? 0 : 1;
        }

        static Tuple<string, bool, Func<Task>> T(string n, bool net, Func<Task> f) { return Tuple.Create(n, net, f); }

        List<string> _notes = new List<string>();
        void Note(string s) { _notes.Add(s); }

        static string ToMarkdown(JObject rep)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# FlashBox headless test report");
            sb.AppendLine();
            sb.AppendLine("- When: " + rep["when"]);
            sb.AppendLine("- Player: `" + rep["player"] + "`");
            sb.AppendLine("- Result: **" + rep["passed"] + " passed, " + rep["failed"] + " failed, " + rep["skipped"] + " skipped**");
            sb.AppendLine();
            sb.AppendLine("| Test | Status | ms | Detail |");
            sb.AppendLine("|---|---|---|---|");
            foreach (JObject t in rep["tests"])
            {
                string d = ((string)t["detail"] ?? "").Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
                if (d.Length > 300) d = d.Substring(0, 300) + "...";
                sb.AppendLine("| " + t["name"] + " | " + t["status"] + " | " + t["ms"] + " | " + d + " |");
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ helpers

        static async Task<bool> WaitUntil(Func<bool> cond, int ms, int step = 50)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
                bool ok = false;
                try { ok = cond(); } catch { }
                if (ok) return true;
                await Task.Delay(step);
            }
            try { return cond(); } catch { return false; }
        }

        async Task<bool> WaitPlayerReady()
        {
            return await WaitUntil(() => Flash.QueryFlash("isReady") == "1" && State() != null, 20000, 100);
        }

        async Task ResetPlayer()
        {
            Flash.ResetFlash();
            if (!await WaitPlayerReady()) throw new TestFailure("player did not come back after reset");
            // Let the rig settle on its Idle frame before driving it.
            await Task.Delay(150);
        }

        JObject State()
        {
            string s = Flash.QueryFlash("getAvatarState");
            if (string.IsNullOrEmpty(s)) return null;
            try { return JObject.Parse(s); } catch { return null; }
        }

        JObject MustState()
        {
            var s = State();
            if (s == null) throw new TestFailure("getAvatarState returned nothing");
            return s;
        }

        void Call(string method, params string[] args)
        {
            string v;
            if (!Flash.TryCall(method, out v, args))
                throw new TestFailure("player call " + method + "(" + string.Join(",", args) + ") threw");
        }

        class FixtureUnavailable : Exception { public FixtureUnavailable(string m) : base(m) { } }

        async Task<string> Fixture(string rel)
        {
            string local = Path.Combine(_opt.FixtureDir, rel.Replace('/', '\\'));
            if (File.Exists(local) && new FileInfo(local).Length > 0) return local;
            if (_opt.Offline) throw new FixtureUnavailable("fixture not cached (offline): " + rel);
            Directory.CreateDirectory(Path.GetDirectoryName(local));
            if (!await Downloads.DownloadAsync(Downloads.GAME + rel, local))
                throw new FixtureUnavailable("could not download fixture " + rel);
            return local;
        }

        static string[] Kids(JObject st, string part)
        {
            var p = st["parts"][part] as JObject;
            if (p == null) return new string[0];
            return p["kids"].Select(k => (string)k).ToArray();
        }

        static bool Visible(JObject st, string part)
        {
            var p = st["parts"][part] as JObject;
            return p != null && (bool)p["v"];
        }

        static void Expect(bool cond, string msg)
        {
            if (!cond) throw new TestFailure(msg);
        }

        async Task Load(string type, string rel, string weaponType = null)
        {
            string path = await Fixture(rel);
            Flash.LoadItem(type, path, weaponType);
        }

        async Task SetGender(string g)
        {
            Flash.FlashCall("setGender", new[] { g });
            await Task.Yield();
        }

        async Task<JObject> WaitState(Func<JObject, bool> cond, string what, int ms = 8000)
        {
            JObject last = null;
            bool ok = await WaitUntil(() => { last = State(); return last != null && cond(last); }, ms, 50);
            if (!ok) throw new TestFailure("timed out waiting for " + what + "; last state: " + Short(last));
            return last;
        }

        static string Short(JObject st)
        {
            if (st == null) return "(none)";
            var s = st.ToString(Formatting.None);
            return s.Length > 1500 ? s.Substring(0, 1500) + "..." : s;
        }

        static bool Has(IEnumerable<string> kids, string prefix)
        {
            return kids.Any(k => k.StartsWith(prefix, StringComparison.Ordinal));
        }

        async Task<JObject> LoadArmor(string gender, string rel, string link)
        {
            await SetGender(gender);
            await Load("Armor", rel);
            return await WaitState(s => Has(Kids(s, "chest"), link + gender + "Chest"), "armor " + link);
        }

        void Snap(string name)
        {
            using (var bmp = Flash.Snapshot())
            {
                if (bmp == null) return;
                bmp.Save(Path.Combine(_snapDir, name + ".png"), ImageFormat.Png);
            }
        }

        // -------------------------------------------------------------- tests

        static readonly string[] PieceParts =
        {
            "chest:Chest", "hip:Hip", "idlefoot:FootIdle", "backfoot:Foot", "frontfoot:Foot",
            "frontshoulder:Shoulder", "backshoulder:Shoulder", "fronthand:Hand", "backhand:Hand",
            "frontthigh:Thigh", "backthigh:Thigh", "frontshin:Shin", "backshin:Shin",
            "robe:Robe", "backrobe:RobeBack"
        };

        // Wrong armor layering: every rig slot must hold exactly the matching
        // armor symbol (one child, no leftovers stacked on top).
        async Task ArmorEveryPiece()
        {
            var st = await LoadArmor("F", ArmorRobeF, "ccsephA");
            await Task.Delay(200);
            st = MustState();
            var bad = new List<string>();
            foreach (var pp in PieceParts)
            {
                var a = pp.Split(':');
                var kids = Kids(st, a[0]);
                string want = "ccsephAF" + a[1];
                if (kids.Length != 1 || !kids[0].StartsWith(want))
                    bad.Add(a[0] + "=[" + string.Join(",", kids) + "] want " + want);
            }
            Expect(Visible(st, "robe") && Visible(st, "backrobe"), "robe/backrobe should be visible for a robed armor");
            var head = Kids(st, "head");
            Expect(head.Length > 0 && head[0].StartsWith("ccsephAFHead"), "head face should be the armor head, got " + string.Join(",", head));
            Snap("armor_ccsephA_idle");
            Expect(bad.Count == 0, "armor pieces wrong: " + string.Join("; ", bad));
        }

        // Swapping to an armor without a RobeBack must not leave the previous
        // armor's back robe drawn behind the new one.
        async Task ArmorSwapClearsRobe()
        {
            await LoadArmor("F", ArmorRobeF, "ccsephA");
            var st = await LoadArmor("F", ArmorRobeOnlyF, "BeleenSXY");
            await Task.Delay(200);
            st = MustState();
            Expect(Has(Kids(st, "robe"), "BeleenSXYFRobe") && Visible(st, "robe"), "robe should be BeleenSXYFRobe");
            bool staleBack = Visible(st, "backrobe") && Has(Kids(st, "backrobe"), "ccsephA");
            Snap("armor_swap_beleen");
            Expect(!staleBack, "stale back robe from previous armor still visible: " + string.Join(",", Kids(st, "backrobe")));
        }

        async Task ArmorSwapMalePlain()
        {
            await LoadArmor("M", ArmorRobeM, "PalidanRevamp");
            var st = await LoadArmor("M", ArmorPlainM, "Warrior2a");
            await Task.Delay(200);
            st = MustState();
            Expect(!Visible(st, "robe"), "robe should be hidden for a robe-less armor, has " + string.Join(",", Kids(st, "robe")));
            Expect(!Visible(st, "backrobe"), "backrobe should be hidden for a robe-less armor, has " + string.Join(",", Kids(st, "backrobe")));
        }

        // Right (front) foot during Walk must be the armor's Foot symbol.
        async Task WalkFrontFoot()
        {
            await LoadArmor("F", ArmorRobeF, "ccsephA");
            Call("loadEmote", "Walk");
            var st = await WaitState(s => (string)s["label"] == "Walk" && Visible(s, "frontfoot"), "Walk with front foot shown");
            await Task.Delay(120);
            st = MustState();
            Snap("walk_ccsephA");
            var kids = Kids(st, "frontfoot");
            Expect(!Visible(st, "idlefoot"), "idle foot should be hidden while walking");
            Expect(kids.Length == 1 && kids[0].StartsWith("ccsephAFFoot"), "front foot during Walk shows [" + string.Join(",", kids) + "], want ccsephAFFoot");
            Expect(Has(Kids(st, "backfoot"), "ccsephAFFoot"), "back foot should be the armor foot too");
            // Back to idle: front foot hidden again.
            Call("loadEmote", "Idle");
            await WaitState(s => (string)s["label"] == "Idle" && !Visible(s, "frontfoot") && Visible(s, "idlefoot"), "Idle foot restored");
        }

        // hideArmor must also keep the walking front foot hidden.
        async Task WalkFrontFootHiddenArmor()
        {
            await LoadArmor("F", ArmorRobeF, "ccsephA");
            Call("hideArmor", "True");
            Call("loadEmote", "Walk");
            var st = await WaitState(s => (string)s["label"] == "Walk", "Walk");
            await Task.Delay(200);
            st = MustState();
            Expect(!Visible(st, "frontfoot"), "front foot visible while armor is hidden");
            Call("hideArmor", "False");
            await Task.Delay(150);
            st = MustState();
            Expect(Visible(st, "frontfoot") && Visible(st, "chest"), "armor/front foot should return after unhide while walking");
        }

        static int Ch(int rgb, int shift) { return (rgb >> shift) & 0xFF; }

        // Expected dye transform, per the live game's AvatarMC.changeColor.
        static int[] ExpectedOffsets(int rgb, string loc, string shade)
        {
            int r = Ch(rgb, 16), g = Ch(rgb, 8), b = Ch(rgb, 0);
            switch ((shade ?? "").ToUpperInvariant())
            {
                case "LIGHT": r += 100; g += 100; b += 100; break;
                case "DARK": r -= loc == "Skin" ? 25 : 50; g -= 50; b -= 50; break;
                case "DARKER": r -= 125; g -= 125; b -= 125; break;
            }
            return new[] { r, g, b };
        }

        List<string> CheckColors(JObject st)
        {
            var bad = new List<string>();
            var colors = st["colors"] as JObject;
            foreach (JObject c in st["colored"])
            {
                string loc = (string)c["loc"], shade = (string)c["shade"];
                if (colors == null || colors[loc] == null) { bad.Add("no color for " + loc); continue; }
                int rgb = (int)Convert.ToDouble((string)colors[loc].ToString());
                var want = ExpectedOffsets(rgb, loc, shade);
                int r = (int)(double)c["r"], g = (int)(double)c["g"], b = (int)(double)c["b"];
                if (r != want[0] || g != want[1] || b != want[2] || (double)c["m"] != 0)
                    bad.Add(loc + "/" + shade + " got (" + r + "," + g + "," + b + ") want (" + string.Join(",", want) + ")");
            }
            return bad;
        }

        // Dye channels: every colored clip must carry its location's color
        // with the game's shade offsets (Dark is -50 red except on Skin).
        // Armors that dye Base/Trim/Accessory with shades (most modern art
        // only dyes Skin/Eye); the first one with a non-skin Dark shade wins.
        static readonly string[][] DyedArmors =
        {
            new[] { "M", ArmorPlainM, "Warrior2a" },
            new[] { "M", "classes/M/zhoom_skin.swf", "Zhoom" },
            new[] { "M", "classes/M/NewCysero.swf", "NewCysero" },
            new[] { "F", ArmorRobeOnlyF, "BeleenSXY" },
            new[] { "M", ArmorRobeM, "PalidanRevamp" },
        };

        async Task ColorDarkChannels()
        {
            JObject st = null;
            List<string> shades = null;
            foreach (var a in DyedArmors)
            {
                await ResetPlayer();
                // Hair art carries the Hair/Dark shades most armors lack.
                await SetGender(a[0]);
                await Load("Hair", a[0] == "M" ? HairM : HairBackF);
                await LoadArmor(a[0], a[1], a[2]);
                await Task.Delay(300);
                foreach (var kv in new Dictionary<string, int> {
                    { "Hair", 0x336699 }, { "Skin", 0xE0B090 }, { "Eye", 0x2244AA },
                    { "Trim", 0xCC3311 }, { "Base", 0x88AA22 }, { "Accessory", 0x7722CC } })
                    Call(kv.Key, kv.Value.ToString());
                await Task.Delay(150);
                st = MustState();
                shades = st["colored"].Select(c => (string)c["loc"] + "/" + (string)c["shade"]).Distinct().ToList();
                Note(a[2] + " colored clips: " + string.Join(" ", shades));
                if (shades.Any(s => s.EndsWith("/Dark") && !s.StartsWith("Skin"))) { Snap("colors_" + a[2]); break; }
            }
            Expect(shades.Any(s => s.EndsWith("/Dark") && !s.StartsWith("Skin")), "no fixture armor has non-skin Dark clips to check");
            var bad = CheckColors(st);
            Expect(bad.Count == 0, "dye mismatch: " + string.Join("; ", bad.Distinct().Take(12)));
        }

        // A color change after load must recolor existing clips.
        async Task ColorUpdateLive()
        {
            await LoadArmor("F", ArmorRobeF, "ccsephA");
            await Task.Delay(200);
            Call("Base", 0x00FF00.ToString());
            Call("Trim", 0x0000FF.ToString());
            await Task.Delay(100);
            var st = MustState();
            var bad = CheckColors(st);
            Expect(bad.Count == 0, "after live recolor: " + string.Join("; ", bad.Distinct().Take(12)));
        }

        // Same list the Emotes panel offers.
        static readonly string[] UiEmotes = { "Idle", "Dance", "Dance2", "Laugh", "Point", "Wave", "Bow", "Cheer", "Salute", "Salute2", "Cry", "Cry2", "Sleep", "Rest", "Kneel", "Punt", "Unsheath", "Thrash", "Facepalm", "Airguitar", "Backflip", "Headbang", "Stepdance", "Samba", "Spar", "Dazed", "Dodge", "Powerup", "Jumpcheer", "Psychic1", "Psychic2", "Throw", "FireBreath", "Swordplay", "Feign", "Dead", "Stern", "Jump", "Attack1", "Attack2",
            "Use", "Danceweapon", "Useweapon",
            // Only in the game's newer skeleton (transplanted by build_char6.py):
            "Card", "WhipAttack", "GunAttack", "GunAttack2", "RifleAttack", "RifleAttack2", "RangedAttack3", "UnarmedAttack3" };

        async Task EmoteLabelsPlay()
        {
            var bad = new List<string>();
            // Everything the Emotes panel offers, plus the fixed list.
            var list = new List<string>(UiEmotes);
            try
            {
                var page = JArray.Parse(JsonConvert.DeserializeObject<string>(await Js("JSON.stringify(Object.values(EMOTE_GROUPS).flat())")));
                foreach (var e in page) if (!list.Contains((string)e)) list.Add((string)e);
            }
            catch { bad.Add("could not read the page's emote list"); }
            Note(list.Count + " emotes checked");
            foreach (var e in list)
            {
                Call("loadEmote", e);
                await Task.Delay(90);
                var a = MustState();
                string lab = (string)a["label"];
                if (lab == null || !lab.StartsWith(e)) { bad.Add(e + "->" + lab); continue; }
                if (e == "Idle") continue;
                await Task.Delay(160);
                var b = MustState();
                // Advancing (or legitimately parked on a stop() frame of the same emote).
                if ((int)b["frame"] == (int)a["frame"] && (string)b["label"] == lab && e != "Dead" && e != "Feign")
                    bad.Add(e + " frozen at " + b["frame"]);
                if (!(bool)b["headOk"]) bad.Add(e + " lost head");
            }
            Expect(bad.Count == 0, "emotes: " + string.Join(", ", bad));
        }

        async Task EmoteKeepsHead(string emote, string snapName)
        {
            await SetGender("F");
            await Load("Hair", HairBackF);
            await Load("Helm", HelmPlain);
            await LoadArmor("F", ArmorRobeF, "ccsephA");
            await WaitState(s => s["helmHead"] != null && Has(s["helmHead"]["kids"].Select(k => (string)k), "RevGenHood1"), "helm");
            Call("loadEmote", emote);
            var sw = Stopwatch.StartNew();
            var problems = new List<string>();
            bool snapped = false;
            while (sw.ElapsedMilliseconds < 2600)
            {
                var st = MustState();
                string lab = (string)st["label"];
                var face = st["parts"]["head"] as JObject;
                var faceKids = face == null ? new string[0] : face["kids"].Select(k => (string)k).ToArray();
                if (!(bool)st["headOk"]) problems.Add(lab + "@" + st["frame"] + " head detached");
                else if (!Has(faceKids, "ccsephAFHead")) problems.Add(lab + "@" + st["frame"] + " face=" + string.Join(",", faceKids));
                if (!snapped && lab == emote && sw.ElapsedMilliseconds > 250) { Snap(snapName); snapped = true; }
                await Task.Delay(30);
            }
            var end = MustState();
            Expect(problems.Count == 0, emote + ": " + string.Join("; ", problems.Distinct().Take(6)));
            // After the emote the head must still be the live, dressed one.
            Expect(end["helmHead"] != null && Has(end["helmHead"]["kids"].Select(k => (string)k), "RevGenHood1"), "helm gone after " + emote);
            Call("hideHelm", "True");
            await Task.Delay(60);
            var hid = MustState();
            Expect(!(bool)hid["helm"]["v"], "hideHelm after " + emote + " did not reach the visible head");
        }

        Task EmoteRestKeepsHead() { return EmoteKeepsHead("Rest", "emote_rest"); }
        Task EmoteUnsheathKeepsHead() { return EmoteKeepsHead("Unsheath", "emote_unsheath"); }

        // Stern loops its SternLoop section 3 times - but only if the loop
        // counter starts at 0. Measure loop-backs fresh and after Laugh.
        async Task<int> CountSternLoopBacks()
        {
            Call("loadEmote", "Stern");
            int backs = 0, last = -1;
            var sw = Stopwatch.StartNew();
            bool seenLoop = false;
            while (sw.ElapsedMilliseconds < 6000)
            {
                var st = MustState();
                string lab = (string)st["label"];
                int f = (int)st["frame"];
                if (lab == "SternLoop")
                {
                    seenLoop = true;
                    if (last >= 0 && f < last) backs++;
                    last = f;
                }
                else if (seenLoop) break;
                await Task.Delay(15);
            }
            return backs;
        }

        async Task EmoteSternLoops()
        {
            int fresh = await CountSternLoopBacks();
            Call("loadEmote", "Laugh");
            await Task.Delay(1800); // let Laugh run its own 3 loops
            int afterLaugh = await CountSternLoopBacks();
            Note("SternLoop loop-backs fresh=" + fresh + " afterLaugh=" + afterLaugh);
            Expect(fresh == 2, "fresh Stern should loop back twice (3 plays), got " + fresh);
            Expect(afterLaugh == fresh, "Stern loop count depends on previous emote: fresh " + fresh + " vs after Laugh " + afterLaugh);
        }

        async Task HairBackhair()
        {
            await SetGender("F");
            await Load("Hair", HairBackF);
            var st = await WaitState(s => s["hair"] != null && Has(s["hair"]["kids"].Select(k => (string)k), "Alina3FHair"), "hair");
            await Task.Delay(150);
            st = MustState();
            Snap("hair_alina3");
            Expect(Visible(st, "backhair") && Has(Kids(st, "backhair"), "Alina3FHairBack"),
                "back hair should be Alina3FHairBack and visible, got v=" + Visible(st, "backhair") + " [" + string.Join(",", Kids(st, "backhair")) + "]");
        }

        async Task HelmHideShowsHair()
        {
            await SetGender("F");
            await Load("Hair", HairBackF);
            await WaitState(s => Has(s["hair"]["kids"].Select(k => (string)k), "Alina3FHair"), "hair");
            await Load("Helm", HelmBackhair);
            var st = await WaitState(s => s["helmHead"] != null && Has(s["helmHead"]["kids"].Select(k => (string)k), "cmagicianHLocksHat"), "helm");
            await Task.Delay(100);
            st = MustState();
            Expect((bool)st["helm"]["v"] && !(bool)st["hair"]["v"], "helm on: helm visible, hair hidden");
            Expect(Has(Kids(st, "backhair"), "cmagicianHLocksHat_backhair") && Visible(st, "backhair"), "helm on: back hair should be the helm's locks");
            Call("hideHelm", "True");
            await Task.Delay(100);
            st = MustState();
            Snap("helm_hidden_hair");
            Expect(!(bool)st["helm"]["v"], "helm hidden");
            Expect((bool)st["hair"]["v"], "hair must show when the helm is hidden (bald head otherwise)");
            Expect(Visible(st, "backhair") && Has(Kids(st, "backhair"), "Alina3FHairBack"),
                "hair's own back hair must show when the helm is hidden, got v=" + Visible(st, "backhair") + " [" + string.Join(",", Kids(st, "backhair")) + "]");
            Call("hideHelm", "False");
            await Task.Delay(100);
            st = MustState();
            Expect((bool)st["helm"]["v"] && !(bool)st["hair"]["v"], "helm back on: helm visible, hair hidden");
            Expect(Has(Kids(st, "backhair"), "cmagicianHLocksHat_backhair") && Visible(st, "backhair"), "helm back on: helm locks restored");
        }

        // Hair finishing after the helm (download order is not guaranteed)
        // must not put the hair back over a worn helm.
        async Task HelmBackhairLateHair()
        {
            await SetGender("F");
            await Load("Helm", HelmBackhair);
            await WaitState(s => s["helmHead"] != null && Has(s["helmHead"]["kids"].Select(k => (string)k), "cmagicianHLocksHat"), "helm");
            await Load("Hair", HairBackF);
            await WaitState(s => Has(s["hair"]["kids"].Select(k => (string)k), "Alina3FHair"), "hair");
            await Task.Delay(150);
            var st = MustState();
            Expect(!(bool)st["hair"]["v"], "hair drawn over the helm after a late hair load");
            Expect(Has(Kids(st, "backhair"), "cmagicianHLocksHat_backhair") && Visible(st, "backhair"),
                "helm locks replaced by hair back after a late hair load: [" + string.Join(",", Kids(st, "backhair")) + "]");
        }

        // Walking turns the avatar (negative scaleX); resizing must keep that
        // facing instead of snapping back. setFacing drives the same flip the
        // walk code does (AvatarMC.turn), without needing a real mouse.
        async Task ResizeKeepsFacing()
        {
            Call("loadResize", "1");
            var st = MustState();
            double sx0 = (double)st["scaleX"];
            Call("setFacing", "toggle");
            st = await WaitState(s => Math.Sign((double)s["scaleX"]) != Math.Sign(sx0), "avatar to turn", 2000);
            Call("loadResize", "1.5");
            await Task.Delay(50);
            var st2 = MustState();
            Expect(Math.Sign((double)st2["scaleX"]) == Math.Sign((double)st["scaleX"]),
                "loadResize flipped the avatar: scaleX " + st["scaleX"] + " -> " + st2["scaleX"]);
            Expect(Math.Abs(Math.Abs((double)st2["scaleX"]) - 1.5) < 0.001 && Math.Abs((double)st2["scaleY"] - 1.5) < 0.001, "scale not applied");
        }

        async Task DaggerThenSword()
        {
            await Load("Weapon", Dagger, "Dagger");
            var st = await WaitState(s => Has(Kids(s, "weapon"), "mugCysero") && Has(Kids(s, "weaponOff"), "mugCysero"), "dagger in both hands");
            Expect(Visible(st, "weaponOff"), "off-hand dagger visible");
            await Load("Weapon", Sword, "Sword");
            st = await WaitState(s => !Has(Kids(s, "weapon"), "mugCysero"), "dagger removed");
            await Task.Delay(100);
            st = MustState();
            Expect(!Visible(st, "weaponOff"), "off hand should be hidden for a single sword");
            Expect(!Has(Kids(st, "weaponOff"), "mugCysero"), "stale dagger in off hand");
        }

        // The far limbs are black silhouettes; the skeleton's timeline does
        // that for all of them except the back hand, which the game blacks
        // out in code. The front hand keeps its colors.
        async Task BackHandSilhouette()
        {
            var st = await LoadArmor("F", ArmorRobeF, "ccsephA");
            await Task.Delay(100);
            st = MustState();
            double back = (double)st["parts"]["backhand"]["m0"], front = (double)st["parts"]["fronthand"]["m0"];
            Expect(back == 0, "back hand armor piece should be blacked out (red multiplier 0), got " + back);
            Expect(front == 1, "front hand must keep its colors, got multiplier " + front);
        }

        async Task PetResizeKeepsFacing()
        {
            await Load("Pet", Pet);
            var st = await WaitState(s => s["pet"] != null && s["pet"].Type == JTokenType.Object, "pet loaded");
            Call("setFacing", "toggle");
            await Task.Delay(50);
            st = MustState();
            double sx0 = (double)st["pet"]["sx"];
            Call("loadResizePet", "1.5");
            await Task.Delay(50);
            st = MustState();
            double sx1 = (double)st["pet"]["sx"];
            Expect(Math.Sign(sx1) == Math.Sign(sx0) && Math.Abs(Math.Abs(sx1) - 1.5) < 0.001,
                "pet scaleX " + sx0 + " -> " + sx1 + " after loadResizePet(1.5); facing lost");
        }

        async Task CloseUiiBeforeMisc()
        {
            string v;
            Expect(Flash.TryCall("closeUii", out v, "True"), "closeUii(True) threw before any misc name was set");
            Expect(Flash.TryCall("closeUii", out v, "False"), "closeUii(False) threw");
            await Task.Yield();
        }

        // Names with XML-special characters ("Shield & Blade") must reach the
        // player through the normal FlashCall path.
        async Task FlashCallEscapes()
        {
            string name = "Prince's Shield & Blade <of> \"Doom\"";
            Flash.FlashCall("changeWeaponName", new[] { name });
            Flash.FlashCall("changeUserName", new[] { "A&B" });
            await Task.Delay(30);
            var st = MustState();
            var names = st["names"] as JObject;
            Expect(names != null, "player exposes no name fields");
            Expect((string)names["weapon"] == name, "weapon name = '" + names["weapon"] + "', want '" + name + "'");
            Expect((string)names["user"] == "A&B", "user name = '" + names["user"] + "'");
        }

        async Task<string> Js(string js)
        {
            return await _form.EvalAsync(js);
        }

        async Task JsAwait(string promiseExpr, int ms = 60000)
        {
            await Js("window.__hl='busy';Promise.resolve().then(()=>(" + promiseExpr + ")).then(()=>{window.__hl='done'},e=>{window.__hl='err:'+(e&&e.message||e)})");
            string st = null;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms)
            {
                st = JsonConvert.DeserializeObject<string>(await Js("window.__hl"));
                if (st != "busy") break;
                await Task.Delay(100);
            }
            if (st != "done") throw new TestFailure("page call failed: " + promiseExpr + " -> " + st);
        }

        async Task UiHideCheckboxes()
        {
            await SetGender("F");
            await Load("Hair", HairBackF);
            await Load("Helm", HelmPlain);
            await Load("Cape", Cape);
            await WaitState(s => s["helmHead"] != null && Has(s["helmHead"]["kids"].Select(k => (string)k), "RevGenHood1") && Has(Kids(s, "cape"), "PalidanRevampCape"), "helm+cape");
            foreach (var box in new[] { "hideHelm", "hideCape" })
            {
                await Js("document.getElementById('" + box + "').click()");
                await Task.Delay(150);
                var on = MustState();
                bool vis = box == "hideHelm" ? (bool)on["helm"]["v"] : Visible(on, "cape");
                Expect(!vis, box + " checked: item still visible");
                await Js("document.getElementById('" + box + "').click()");
                await Task.Delay(150);
                var off = MustState();
                vis = box == "hideHelm" ? (bool)off["helm"]["v"] : Visible(off, "cape");
                Expect(vis, box + " unchecked: item stays hidden (checkbox sends lowercase true/false)");
            }
        }

        // Canned CharPage FlashVars: female character wearing a hair from the
        // male folder (real case) - gender must come from strGender, colors
        // from intColor*.
        async Task UiVarsColorsGender()
        {
            foreach (var rel in new[] { ArmorF2, HairMaleOnly, HelmPlain }) await Fixture(rel);
            var vars = new JObject
            {
                { "intColorHair", "16247807" }, { "intColorSkin", "16765338" }, { "intColorEye", "9765013" },
                { "intColorTrim", "6749952" }, { "intColorBase", "6684825" }, { "intColorAccessory", "255" },
                { "strGender", "F" },
                { "strHairFile", HairMaleOnly }, { "strHairName", "DFWarStyle" },
                { "strClassName", "Test" }, { "strClassFile", "InfinityAlphaPirateGoldDragon.swf" }, { "strClassLink", "InfinityAlphaPirateGoldDragon" },
                { "strArmorName", "Test Armor" },
                { "strHelmFile", HelmPlain }, { "strHelmLink", "RevGenHood1" }, { "strHelmName", "Hood" },
                { "strWeaponFile", "none" }, { "strCapeFile", "none" }, { "strPetFile", "none" }, { "strMiscFile", "none" }
            };
            await Js("document.getElementById('folder').value=" + JsonConvert.ToString(_opt.FixtureDir));
            await JsAwait("loadVars('Headless Warlic'," + vars.ToString(Formatting.None) + ")");
            var st = await WaitState(s => Has(Kids(s, "chest"), "InfinityAlphaPirateGoldDragon"), "armor from vars", 15000);
            await Task.Delay(300);
            st = MustState();
            Snap("ui_vars_warlic");
            Expect((string)st["gender"] == "F", "gender should be F from strGender, got " + st["gender"]);
            Expect(Has(Kids(st, "chest"), "InfinityAlphaPirateGoldDragonFChest"), "female armor pieces");
            var c = st["colors"];
            foreach (var k in new[] { "Hair", "Skin", "Eye", "Trim", "Base", "Accessory" })
            {
                string want = (string)vars["intColor" + k];
                Expect(c[k] != null && c[k].ToString() == want, "color " + k + " = " + c[k] + ", CharPage says " + want);
            }
            var bad = CheckColors(st);
            Expect(bad.Count == 0, "dye transforms: " + string.Join("; ", bad.Distinct().Take(8)));
        }

        async Task UiBackgroundColor()
        {
            await Js("project.background='Hill';sendBackground();");
            await Task.Delay(700);
            using (var a = Flash.Snapshot())
            {
                await Js("project.background='#FF00FF';sendBackground();");
                await Task.Delay(500);
                using (var b = Flash.Snapshot())
                {
                    b.Save(Path.Combine(_snapDir, "bg_color_magenta.png"), ImageFormat.Png);
                    // Top-left corner is background, never avatar.
                    Color px = b.GetPixel(4, 4);
                    Expect(px.R > 200 && px.G < 60 && px.B > 200, "background color not applied (corner pixel " + px + ")");
                }
            }
        }

        // Panel layout regressions from user screenshots: squashed chip text,
        // cut-off Browse button / horizontal scroll, oblong dye swatches.
        async Task UiLayout()
        {
            string js = @"(() => {
              const out = [];
              document.querySelectorAll('.drawer').forEach(d => d.style.transition = 'none');
              for (const tab of ['backgrounds','emotes','misc','colors','log','sizes','names','monitor']) {
                activateTab(tab);
                if (document.body.classList.contains('nopanel')) activateTab(tab);
                const inner = document.querySelector('.drawer-inner');
                if (inner.scrollWidth > inner.clientWidth + 1) out.push(tab + ': horizontal overflow ' + (inner.scrollWidth - inner.clientWidth) + 'px');
                document.querySelectorAll('#p-' + tab + ' .bg-pick, #p-' + tab + ' .emote-btn').forEach(b => {
                  if (b.offsetParent && b.scrollHeight > b.clientHeight + 1) out.push(tab + ': clipped chip ' + b.textContent + ' ' + b.clientHeight + '/' + b.scrollHeight);
                });
                document.querySelectorAll('#p-' + tab + ' .wheel-node input[type=color]').forEach(i => {
                  const r = i.getBoundingClientRect();
                  if (Math.abs(r.width - r.height) > 1) out.push(tab + ': dye swatch not round ' + r.width + 'x' + r.height);
                });
                const br = document.getElementById('btnBrowse').getBoundingClientRect(), dr = inner.getBoundingClientRect();
                if (tab === 'misc' && br.right > dr.right + 1) out.push('misc: Browse button cut off');
              }
              document.body.classList.add('nopanel');
              return JSON.stringify(out.slice(0, 12));
            })()";
            var res = JArray.Parse(JsonConvert.DeserializeObject<string>(await Js(js)));
            Expect(res.Count == 0, "layout: " + string.Join("; ", res));
        }

        async Task UiTitleBar()
        {
            string js = @"(() => {
              const t = document.querySelector('.titlebar'), cb = document.getElementById('grayTitle');
              if (cb.checked) cb.click();
              const dark = getComputedStyle(t).backgroundColor;
              cb.click();
              const gray = getComputedStyle(t).backgroundColor;
              let stored = null; try { stored = localStorage.getItem('fb.grayTitle'); } catch (e) {}
              cb.click();
              return JSON.stringify({ dark, gray, stored, rail: getComputedStyle(document.querySelector('.siderail')).backgroundColor });
            })()";
            var r = JObject.Parse(JsonConvert.DeserializeObject<string>(await Js(js)));
            Expect((string)r["dark"] == (string)r["rail"], "title bar should match the UI (" + r["rail"] + "), is " + r["dark"]);
            Expect((string)r["gray"] == "rgb(45, 45, 48)", "gray option gave " + r["gray"]);
            Expect((string)r["stored"] == "1", "gray preference not remembered (localStorage=" + r["stored"] + ")");
        }

        async Task UiCopyLog()
        {
            string marker = "headless-copy-marker-" + Guid.NewGuid().ToString("N");
            AppForm.Dbg(marker);
            Clipboard.Clear();
            await Js("document.getElementById('btnCopyLog').click()");
            string text = "";
            await WaitUntil(() => { text = Clipboard.ContainsText() ? Clipboard.GetText() : ""; return text.Contains(marker); }, 3000, 100);
            Expect(text.Contains(marker), "clipboard does not hold the debug log (" + text.Length + " chars)");
        }

        // Gear-list icon clicks and the Misc checkboxes are one state.
        async Task UiGearIconSync()
        {
            await SetGender("F");
            await Load("Helm", HelmPlain);
            await WaitState(s => s["helmHead"] != null && Has(s["helmHead"]["kids"].Select(k => (string)k), "RevGenHood1"), "helm");
            await Js("if (document.getElementById('hideHelm').checked) document.getElementById('hideHelm').click()");
            _form.OnGearIcon("helm");
            var st = await WaitState(s => !(bool)s["helm"]["v"], "helm hidden by gear icon", 3000);
            Expect(JsonConvert.DeserializeObject<bool>(await Js("document.getElementById('hideHelm').checked")), "gear icon hid the helm but the Hide helm box stayed unticked");
            _form.OnGearIcon("helm");
            await WaitState(s => (bool)s["helm"]["v"], "helm shown again by gear icon", 3000);
            Expect(!JsonConvert.DeserializeObject<bool>(await Js("document.getElementById('hideHelm').checked")), "Hide helm box still ticked after un-hiding via gear icon");
        }

        // Typing line: over the preview, never over the gear list.
        async Task LoaderPlacement()
        {
            var cases = new[]
            {
                // preview, gear list (client coords) - from the user's screenshots
                Tuple.Create(new Rectangle(264, 40, 568, 488), new Rectangle(276, 150, 240, 280)),
                Tuple.Create(new Rectangle(404, 40, 412, 449), new Rectangle(416, 120, 240, 280)),
                Tuple.Create(new Rectangle(45, 40, 771, 449), new Rectangle(57, 117, 240, 214)),
                Tuple.Create(new Rectangle(45, 40, 771, 449), Rectangle.Empty),
            };
            foreach (var c in cases)
            {
                var r = AppForm.LoaderRect(c.Item1, c.Item2, 46);
                Expect(c.Item1.Contains(r), "typing line " + r + " leaves the preview " + c.Item1);
                Expect(c.Item2.IsEmpty || !r.IntersectsWith(c.Item2), "typing line " + r + " covers the gear list " + c.Item2);
            }
            await Task.Yield();
        }

        // Character fetch/downloads used to block the UI thread for their
        // whole duration; a UI timer must keep ticking during a live load.
        async Task LiveLoadKeepsUiResponsive()
        {
            string dir = Path.Combine(_opt.FixtureDir, "live");
            await Js("document.getElementById('folder').value=" + JsonConvert.ToString(dir));
            await Js("document.getElementById('charName').value='Artix'");
            long maxGap = 0, last = 0;
            var sw = Stopwatch.StartNew();
            var t = new Timer { Interval = 25 };
            t.Tick += (s, e) => { long now = sw.ElapsedMilliseconds; if (last > 0) maxGap = Math.Max(maxGap, now - last); last = now; };
            t.Start();
            try { await JsAwait("loadCharacter()", 90000); }
            finally { t.Stop(); t.Dispose(); }
            Note("load took " + sw.ElapsedMilliseconds + " ms, longest UI stall " + maxGap + " ms");
            Expect(maxGap < 300, "UI thread stalled " + maxGap + " ms during the character load");
        }

        async Task DownloadsCachePaths()
        {
            var male = Downloads.BuildDownloads(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "strGender", "M" }, { "strHairName", "Bob" }, { "strHairFile", "hair/M/Bob.swf" },
                { "strArmorName", "A" }, { "strClassFile", "same.swf" }
            });
            var female = Downloads.BuildDownloads(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "strGender", "F" }, { "strHairName", "Bob" }, { "strHairFile", "hair/F/Bob.swf" },
                { "strArmorName", "A" }, { "strClassFile", "same.swf" }
            });
            foreach (var t in new[] { "Hair", "Armor" })
            {
                var m = male.First(x => x.Type == t);
                var f = female.First(x => x.Type == t);
                Expect(!string.Equals(m.File, f.File, StringComparison.OrdinalIgnoreCase),
                    t + ": male and female files share one cache path (" + m.File + ") - the wrong gender gets reused");
            }
            await Task.Yield();
        }

        // Gender belongs to the character (strGender / setGender), not to
        // whatever symbols an item file happens to export: a female
        // character wearing a hair from the male folder must stay female.
        async Task LinkagePrefersGender()
        {
            string hair = await Fixture(HairMaleOnly);
            await SetGender("F");
            Flash.LoadItem("Hair", hair, null);
            await Task.Delay(300);
            var st = MustState();
            Expect((string)st["gender"] == "F", "loading a male-folder hair flipped the character to " + st["gender"]);
        }

        async Task LiveCharPage()
        {
            string dir = Path.Combine(_opt.FixtureDir, "live");
            await Js("document.getElementById('folder').value=" + JsonConvert.ToString(dir));
            await Js("document.getElementById('charName').value='Alina'");
            await JsAwait("loadCharacter()", 90000);
            string varsJson = await Js("JSON.stringify(project.vars||null)");
            var vars = JObject.Parse(JsonConvert.DeserializeObject<string>(varsJson));
            string link = (string)vars["strClassLink"];
            var st = await WaitState(s => Has(Kids(s, "chest"), link), "live armor " + link, 20000);
            await Task.Delay(500);
            st = MustState();
            Snap("live_alina");
            foreach (var k in new[] { "Hair", "Skin", "Eye", "Trim", "Base", "Accessory" })
                Expect(st["colors"][k].ToString() == (string)vars["intColor" + k], "live " + k + " color not applied");
            Expect((string)st["gender"] == (string)vars["strGender"], "live gender");
            Expect(Has(Kids(st, "frontfoot"), link), "live front foot");
        }
    }
}
