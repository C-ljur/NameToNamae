using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Verse;

namespace Namae
{
    internal static class NameSourceIndex
    {
        internal sealed class Source
        {
            public string PackageId;
            public string ModName;
            public string Origin;
        }

        private static readonly Dictionary<string, List<Source>> Sources =
            new Dictionary<string, List<Source>>(StringComparer.Ordinal);
        private static readonly Source Unknown = new Source
        {
            PackageId = "unknown", ModName = "Unknown", Origin = "unknown"
        };

        internal static void Rebuild()
        {
            Sources.Clear();
            foreach (ModContentPack mod in LoadedModManager.RunningMods)
            {
                if (mod == null || mod.foldersToLoadDescendingOrder == null) continue;
                var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string folder in mod.foldersToLoadDescendingOrder)
                {
                    ScanNameFolder(mod, Path.Combine(folder, "Languages", "English", "Names"), seenFiles);
                    string active = LanguageDatabase.activeLanguage?.folderName;
                    if (!string.IsNullOrEmpty(active) && !active.Equals("English", StringComparison.OrdinalIgnoreCase))
                        ScanNameFolder(mod, Path.Combine(folder, "Languages", active, "Names"), seenFiles);
                    ScanBioFolder(mod, Path.Combine(folder, "Resources", "Backstories", "Solid"), seenFiles);
                }
            }
        }

        internal static IReadOnlyList<Source> Find(string category, string name)
        {
            return Sources.TryGetValue(Key(category, name), out List<Source> value)
                ? value : new[] { Unknown };
        }

        internal static string ScriptOf(string value)
        {
            bool ascii = false;
            bool nonAsciiLetter = false;
            foreach (char c in value ?? string.Empty)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) ascii = true;
                else if (char.IsLetter(c)) nonAsciiLetter = true;
            }
            if (ascii && nonAsciiLetter) return "Mixed";
            if (ascii) return "Latin";
            if (nonAsciiLetter) return "NonLatin";
            return "Other";
        }

        private static void ScanNameFolder(ModContentPack mod, string dir, HashSet<string> seenFiles)
        {
            if (!Directory.Exists(dir)) return;
            var categories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["First_Male"] = "FirstMale", ["First_Female"] = "FirstFemale",
                ["Last"] = "Last", ["Nick_Male"] = "NickMale",
                ["Nick_Female"] = "NickFemale", ["Nick_Unisex"] = "NickUnisex"
            };
            foreach (string file in Directory.GetFiles(dir, "*.txt", SearchOption.AllDirectories))
            {
                if (!seenFiles.Add(Path.GetFullPath(file))) continue;
                if (!categories.TryGetValue(Path.GetFileNameWithoutExtension(file), out string category)) continue;
                foreach (string line in File.ReadAllLines(file))
                {
                    string name = line.Trim();
                    if (name.Length > 0 && !name.StartsWith("#", StringComparison.Ordinal)) Add(category, name, mod);
                }
            }
        }

        private static void ScanBioFolder(ModContentPack mod, string dir, HashSet<string> seenFiles)
        {
            if (!Directory.Exists(dir)) return;
            foreach (string file in Directory.GetFiles(dir, "*.xml", SearchOption.AllDirectories))
            {
                if (!seenFiles.Add(Path.GetFullPath(file))) continue;
                try
                {
                    var doc = new XmlDocument();
                    doc.Load(file);
                    foreach (XmlNode bio in doc.SelectNodes("//*[translate(local-name(), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz')='pawnbio']"))
                    {
                        string gender = ChildText(bio, "Gender");
                        string first = ChildText(bio, "First") ?? ChildText(bio, "firstInt");
                        string nick = ChildText(bio, "Nick") ?? ChildText(bio, "nickInt");
                        string last = ChildText(bio, "Last") ?? ChildText(bio, "lastInt");
                        if (!string.IsNullOrEmpty(first))
                        {
                            if (!"Female".Equals(gender, StringComparison.OrdinalIgnoreCase)) Add("FirstMale", first, mod);
                            if (!"Male".Equals(gender, StringComparison.OrdinalIgnoreCase)) Add("FirstFemale", first, mod);
                        }
                        if (!string.IsNullOrEmpty(nick))
                        {
                            if ("Male".Equals(gender, StringComparison.OrdinalIgnoreCase)) Add("NickMale", nick, mod);
                            else if ("Female".Equals(gender, StringComparison.OrdinalIgnoreCase)) Add("NickFemale", nick, mod);
                            else Add("NickUnisex", nick, mod);
                        }
                        if (!string.IsNullOrEmpty(last)) Add("Last", last, mod);
                    }
                }
                catch (Exception e)
                {
                    Log.Warning($"[Namae] could not inspect name source file '{file}': {e.Message}");
                }
            }
        }

        private static string ChildText(XmlNode parent, string name)
        {
            foreach (XmlNode child in parent.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element) continue;
                if (child.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return child.InnerText?.Trim();
                if (child.Name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                {
                    string nested = ChildText(child, name);
                    if (!string.IsNullOrEmpty(nested)) return nested;
                }
            }
            return null;
        }

        private static void Add(string category, string name, ModContentPack mod)
        {
            string key = Key(category, name);
            if (!Sources.TryGetValue(key, out List<Source> list)) Sources[key] = list = new List<Source>();
            string packageId = mod.PackageId ?? "unknown";
            if (list.Exists(x => x.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase))) return;
            list.Add(new Source
            {
                PackageId = packageId,
                ModName = mod.Name ?? packageId,
                Origin = mod.IsOfficialMod ? "vanilla" : "mod"
            });
        }

        private static string Key(string category, string name) => category + "\0" + name;
    }
}
