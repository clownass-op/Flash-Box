using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FlashBoxApp
{
    // Downloads: character-page fetch, FlashVars parsing, and item SWF downloads
    // (port of the Electron main.js network logic).
    class Downloads
    {
        const string UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) FlashBox/1.0";
        const string GAME = "https://game.aq.com/game/gamefiles/";

        static readonly string[] INVALID = { "none", "undefined" };
        static bool Valid(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string s = name.ToLowerInvariant();
            foreach (var b in INVALID) if (s == b) return false;
            return true;
        }
        static string BaseOf(string u)
        {
            string[] parts = (u ?? "").Split('/');
            for (int i = parts.Length - 1; i >= 0; i--)
                if (parts[i].Length > 0) return parts[i];
            return "";
        }

        // Fetch a character page (follows redirects), returns raw HTML body.
        public static async Task<string> GetAsync(string url)
        {
            for (int hop = 0; hop < 8; hop++)
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = UA;
                req.Timeout = 60000;
                req.AllowAutoRedirect = false;
                try
                {
                    using (var resp = (HttpWebResponse)await req.GetResponseAsync())
                    {
                        if ((int)resp.StatusCode >= 300 && (int)resp.StatusCode < 400 && resp.Headers["Location"] != null)
                        {
                            url = resp.Headers["Location"];
                            resp.Close();
                            continue;
                        }
                        if ((int)resp.StatusCode != 200)
                        {
                            resp.Close();
                            return null;
                        }
                        using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                            return await sr.ReadToEndAsync();
                    }
                }
                catch { return null; }
            }
            return null;
        }

        // Download a binary file (follows redirects).
        public static async Task<bool> DownloadAsync(string url, string dest)
        {
            for (int hop = 0; hop < 8; hop++)
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = UA;
                req.Timeout = 60000;
                req.AllowAutoRedirect = false;
                try
                {
                    using (var resp = (HttpWebResponse)await req.GetResponseAsync())
                    {
                        if ((int)resp.StatusCode >= 300 && (int)resp.StatusCode < 400 && resp.Headers["Location"] != null)
                        {
                            url = resp.Headers["Location"];
                            resp.Close();
                            continue;
                        }
                        if ((int)resp.StatusCode != 200)
                        {
                            resp.Close();
                            return false;
                        }
                        using (var src = resp.GetResponseStream())
                        using (var dst = File.Create(dest))
                        {
                            await src.CopyToAsync(dst);
                        }
                        return true;
                    }
                }
                catch
                {
                    try { if (File.Exists(dest)) File.Delete(dest); } catch { }
                    return false;
                }
            }
            return false;
        }

        // Parse modern character-page flashvars.
        public static Dictionary<string, string> ParseFlashVars(string html)
        {
            if (html == null) return null;
            var m = Regex.Match(html, "flashvars=\"([^\"]*)\"");
            if (!m.Success) return null;
            string s = m.Groups[1].Value
                .Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
            if (s.StartsWith("?")) s = s.Substring(1);
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in s.Split('&'))
            {
                if (kv.Length == 0) continue;
                int i = kv.IndexOf('=');
                string k, v;
                if (i == -1) { k = kv; v = ""; }
                else { k = kv.Substring(0, i); v = kv.Substring(i + 1); }
                try { k = Uri.UnescapeDataString(k); } catch { }
                try { v = Uri.UnescapeDataString(v); } catch { }
                if (k.Length > 0 && !vars.ContainsKey(k)) vars[k] = v;
            }
            return vars;
        }

        public class ItemDownload
        {
            public string Type;
            public string Url;
            public string File;
            public bool Cosmetic;
        }

        public static List<ItemDownload> BuildDownloads(Dictionary<string, string> vars)
        {
            var outList = new List<ItemDownload>();
            string gender = vars.ContainsKey("strGender") ? vars["strGender"] : "";
            Add(outList, vars, "Hair", "strHairName", "strHairFile", GAME, false);
            Add(outList, vars, "Helm", "strHelmName", "strHelmFile", GAME, false);
            Add(outList, vars, "Cape", "strCapeName", "strCapeFile", GAME, false);
            string armorName = vars.ContainsKey("strArmorName") ? vars["strArmorName"] : "";
            string armorFile = vars.ContainsKey("strClassFile") ? vars["strClassFile"] : "";
            if (Valid(armorName) && Valid(armorFile))
                outList.Add(new ItemDownload { Type = "Armor", Url = GAME + "classes/" + gender + "/" + armorFile, File = BaseOf(armorFile), Cosmetic = false });
            Add(outList, vars, "Weapon", "strWeaponName", "strWeaponFile", GAME, false);
            Add(outList, vars, "Pet", "strPetName", "strPetFile", GAME, false);
            Add(outList, vars, "Misc", "strMiscName", "strMiscFile", GAME, false);
            // Cosmetic (custom) outfit: same slots, shown by the CharPage
            // "Cosmetics" toggle. Armor follows the same classes/{gender}/ rule.
            Add(outList, vars, "Helm", "strCustHelmName", "strCustHelmFile", GAME, true);
            Add(outList, vars, "Cape", "strCustCapeName", "strCustCapeFile", GAME, true);
            Add(outList, vars, "Weapon", "strCustWeaponName", "strCustWeaponFile", GAME, true);
            string custArmorName = vars.ContainsKey("strCustArmorName") ? vars["strCustArmorName"] : "";
            string custArmorFile = vars.ContainsKey("strCustArmorFile") ? vars["strCustArmorFile"] : "";
            if (Valid(custArmorName) && Valid(custArmorFile))
                outList.Add(new ItemDownload { Type = "Armor", Url = GAME + "classes/" + gender + "/" + custArmorFile, File = BaseOf(custArmorFile), Cosmetic = true });
            return outList;
        }

        static void Add(List<ItemDownload> list, Dictionary<string, string> vars, string type, string nameKey, string fileKey, string prefix, bool cosmetic)
        {
            string name = vars.ContainsKey(nameKey) ? vars[nameKey] : "";
            string file = vars.ContainsKey(fileKey) ? vars[fileKey] : "";
            if (Valid(name) && Valid(file))
                list.Add(new ItemDownload { Type = type, Url = prefix + file, File = BaseOf(file), Cosmetic = cosmetic });
        }
    }
}
