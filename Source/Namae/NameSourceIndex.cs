using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Verse;
using Verse.Grammar;

namespace Namae
{
    internal static class NameSourceIndex
    {
        internal sealed class Source
        {
            public string PackageId;
            public string ModName;
            public string Origin;
            public string SourceKind;
            public string ExpandedFrom = string.Empty;
            public int ExpandedCount;
        }

        private static readonly Dictionary<string, List<Source>> Sources =
            new Dictionary<string, List<Source>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, List<Source>> SourcesByName =
            new Dictionary<string, List<Source>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, HashSet<string>> FileCandidates =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private static readonly Source Unknown = new Source
        {
            PackageId = "unknown", ModName = "Unknown", Origin = "unknown", SourceKind = "runtime-observation"
        };

        internal static void Rebuild()
        {
            Sources.Clear();
            SourcesByName.Clear();
            FileCandidates.Clear();
            foreach (ModContentPack mod in LoadedModManager.RunningMods)
            {
                if (mod == null || mod.foldersToLoadDescendingOrder == null) continue;
                var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string folder in mod.foldersToLoadDescendingOrder)
                {
                    ScanNameFolder(mod, Path.Combine(folder, "Languages", "English", "Names"), seenFiles, true);
                    ScanNameFolder(mod, Path.Combine(folder, "Languages", "English", "Strings", "Names"), seenFiles, true);
                    string active = LanguageDatabase.activeLanguage?.folderName;
                    if (!string.IsNullOrEmpty(active) && !active.Equals("English", StringComparison.OrdinalIgnoreCase))
                    {
                        ScanNameFolder(mod, Path.Combine(folder, "Languages", active, "Names"), seenFiles, false);
                        ScanNameFolder(mod, Path.Combine(folder, "Languages", active, "Strings", "Names"), seenFiles, false);
                    }
                    ScanBioFolder(mod, Path.Combine(folder, "Resources", "Backstories", "Solid"), seenFiles);
                }
            }
            ScanRulePacks();
        }

        private static void ScanRulePacks()
        {
            foreach (RulePackDef def in DefDatabase<RulePackDef>.AllDefsListForReading)
            {
                if (def?.modContentPack == null) continue;
                List<Rule> rulesForDef = def.UntranslatedRulesPlusIncludes;
                AddRules(rulesForDef, def.modContentPack);
                var rules = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                CollectRules(rulesForDef, rules);
                ExpandPawnNameRules(def.modContentPack, rules);
            }
        }

        // Rule_File.cachedStrings holds the active language's file content, so reading it mixes
        // translated lines into the English index. Resolve the paths against English instead.
        private static List<string> EnglishStringsOf(Rule_File file)
        {
            var result = new List<string>();
            LoadedLanguage english = LanguageDatabase.defaultLanguage;
            if (english == null) return result;
            if (!file.path.NullOrEmpty()) AddStringsFromFile(english, file.path, result);
            if (file.pathList != null)
                foreach (string path in file.pathList) AddStringsFromFile(english, path, result);
            return result;
        }

        private static void AddStringsFromFile(LoadedLanguage language, string path, List<string> target)
        {
            if (!language.TryGetStringsFromFile(path, out List<string> strings) || strings == null) return;
            target.AddRange(strings);
        }

        private static void CollectRules(List<Rule> source, Dictionary<string, List<string>> target)
        {
            if (source == null) return;
            foreach (Rule rule in source)
            {
                if (rule == null || string.IsNullOrEmpty(rule.keyword)) continue;
                if (!target.TryGetValue(rule.keyword, out List<string> values))
                    target[rule.keyword] = values = new List<string>();
                if (rule is Rule_File file)
                {
                    foreach (string value in EnglishStringsOf(file))
                        if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value.Trim())) values.Add(value.Trim());
                    continue;
                }
                string generated;
                try { generated = rule.Generate()?.Trim(); }
                catch { continue; }
                if (!string.IsNullOrEmpty(generated) && !values.Contains(generated)) values.Add(generated);
            }
        }

        private static void ExpandPawnNameRules(ModContentPack mod, Dictionary<string, List<string>> rules)
        {
            if (!rules.TryGetValue("r_name", out List<string> roots)) return;
            foreach (string root in roots)
            {
                int firstQuote = root.IndexOf('\'');
                int secondQuote = firstQuote < 0 ? -1 : root.IndexOf('\'', firstQuote + 1);
                if (firstQuote < 0 || secondQuote < 0) continue;
                AddExpandedSegment("FirstUnisex", root.Substring(0, firstQuote), mod, rules);
                AddExpandedSegment("NickUnisex", root.Substring(firstQuote + 1, secondQuote - firstQuote - 1), mod, rules);
                AddExpandedSegment("Last", root.Substring(secondQuote + 1), mod, rules);
            }
        }

        private static readonly Regex KeywordPattern = new Regex(@"\[([^\]]+)\]");

        // A composed name never shows its seams: Biotech builds names as [SylP][nameEnd], so the
        // result reads like an ordinary word. Composition is therefore decided by the expansion
        // path, and only the leaf strings a translator can act on become candidates.
        private static void AddExpandedSegment(string category, string expression, ModContentPack mod,
            Dictionary<string, List<string>> rules)
        {
            EmitLeaves(category, expression.Trim(), mod, rules,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0, null, 0);
        }

        private static void EmitLeaves(string category, string expression, ModContentPack mod,
            Dictionary<string, List<string>> rules, HashSet<string> stack, int depth,
            string composedFrom, int composedCount)
        {
            if (depth > 12) return;
            MatchCollection refs = KeywordPattern.Matches(expression);
            if (refs.Count == 0)
            {
                string name = expression.Trim();
                if (string.IsNullOrEmpty(name)) return;
                string kind = composedFrom == null ? "rule-pack-literal" : "rule-pack-part";
                Add(category, name, mod, kind, composedFrom, composedCount);
                AddCandidate(category, name);
                return;
            }

            bool composed = composedFrom != null
                || refs.Count > 1
                || KeywordPattern.Replace(expression, string.Empty).Trim().Length > 0;
            string pattern = composedFrom ?? expression;
            int count = composedCount;
            if (composed && composedFrom == null) count = CountLeaves(expression, rules,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);

            foreach (Match reference in refs)
            {
                string keyword = reference.Groups[1].Value;
                if (!rules.TryGetValue(keyword, out List<string> replacements) || !stack.Add(keyword)) continue;
                foreach (string replacement in replacements)
                    EmitLeaves(category, replacement, mod, rules, stack, depth + 1,
                        composed ? pattern : null, composed ? count : 0);
                stack.Remove(keyword);
            }
        }

        private static int CountLeaves(string expression, Dictionary<string, List<string>> rules,
            HashSet<string> stack, int depth)
        {
            if (depth > 12) return 1;
            MatchCollection refs = KeywordPattern.Matches(expression);
            if (refs.Count == 0) return 1;
            int total = 1;
            foreach (Match reference in refs)
            {
                string keyword = reference.Groups[1].Value;
                if (!rules.TryGetValue(keyword, out List<string> replacements) || !stack.Add(keyword)) continue;
                int branch = 0;
                foreach (string replacement in replacements)
                    branch += CountLeaves(replacement, rules, stack, depth + 1);
                stack.Remove(keyword);
                if (branch > 0) total *= branch;
                if (total > 1000000) return 1000000;
            }
            return total;
        }

        private static void AddCandidate(string category, string name)
        {
            if (!FileCandidates.TryGetValue(category, out HashSet<string> names))
                FileCandidates[category] = names = new HashSet<string>(StringComparer.Ordinal);
            names.Add(name);
        }

        private static void AddRules(List<Rule> rules, ModContentPack mod)
        {
            if (rules == null) return;
            foreach (Rule rule in rules)
            {
                string keyword = rule?.keyword ?? string.Empty;
                string category = CategoryForKeyword(keyword);
                if (category == null) continue;
                if (rule is Rule_File file)
                {
                    foreach (string line in EnglishStringsOf(file))
                    {
                        string name = line?.Trim();
                        if (string.IsNullOrEmpty(name) || name.IndexOf('[') >= 0 || name.IndexOf(']') >= 0) continue;
                        Add(category, name, mod, "rule-pack");
                    }
                    continue;
                }
                string value;
                try { value = rule.Generate()?.Trim(); }
                catch { continue; }
                if (string.IsNullOrEmpty(value) || value.IndexOf('[') >= 0 || value.IndexOf(']') >= 0) continue;
                Add(category, value, mod, "rule-pack");
            }
        }

        private static string CategoryForKeyword(string keyword)
        {
            string value = keyword.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
            if (value.Contains("lastname") || value.Contains("surname")) return "Last";
            if (value.Contains("nick"))
            {
                if (value.Contains("female")) return "NickFemale";
                if (value.Contains("male")) return "NickMale";
                return "NickUnisex";
            }
            if (value.Contains("female")) return "FirstFemale";
            if (value.Contains("male")) return "FirstMale";
            return null;
        }

        internal static void AddBio(RimWorld.PawnBio bio)
        {
            if (bio?.name == null) return;
            ModContentPack mod = bio.childhood?.modContentPack ?? bio.adulthood?.modContentPack ?? CoreMod();
            if (mod == null) return;
            bool male = bio.gender != RimWorld.GenderPossibility.Female;
            bool female = bio.gender != RimWorld.GenderPossibility.Male;
            if (male) Add("FirstMale", bio.name.First, mod, "pawn-bio");
            if (female) Add("FirstFemale", bio.name.First, mod, "pawn-bio");
            if (bio.gender == RimWorld.GenderPossibility.Male) Add("NickMale", bio.name.Nick, mod, "pawn-bio");
            else if (bio.gender == RimWorld.GenderPossibility.Female) Add("NickFemale", bio.name.Nick, mod, "pawn-bio");
            else Add("NickUnisex", bio.name.Nick, mod, "pawn-bio");
            Add("Last", bio.name.Last, mod, "pawn-bio");
        }

        internal static void AddBaseName(string category, string name)
        {
            string key = Key(category, name);
            if (Sources.ContainsKey(key)) return;
            ModContentPack mod = CoreMod();
            if (mod != null) Add(category, name, mod, "base-name-bank");
        }

        internal static void AddPawn(Pawn pawn, NameTriple name, bool female)
        {
            // A pawn kind identifies the pawn definition, not the code or file that generated its name.
            // File and rule-pack sources are indexed separately; unmatched values remain runtime observations.
        }

        internal static IReadOnlyList<Source> Find(string category, string name)
        {
            if (Sources.TryGetValue(Key(category, name), out List<Source> exact)) return exact;
            return SourcesByName.TryGetValue(name, out List<Source> any) ? any : new[] { Unknown };
        }

        internal static IEnumerable<KeyValuePair<string, HashSet<string>>> Candidates => FileCandidates;

        // Accented Latin is still Latin. Only report Mixed when two script families meet, so that
        // "Mixed" stays usable as a fault signal for translated content leaking into the index.
        internal static string ScriptOf(string value)
        {
            var families = new HashSet<string>(StringComparer.Ordinal);
            foreach (char c in value ?? string.Empty)
            {
                string family = FamilyOf(c);
                if (family != null) families.Add(family);
            }
            if (families.Count == 0) return "Other";
            if (families.Count > 1) return "Mixed";
            foreach (string only in families) return only;
            return "Other";
        }

        private static string FamilyOf(char c)
        {
            if (!char.IsLetter(c)) return null;
            if (c < 0x0250) return "Latin";
            if (c >= 0x0400 && c <= 0x052F) return "Cyrillic";
            if (c >= 0x0370 && c <= 0x03FF) return "Greek";
            if (c >= 0x0590 && c <= 0x05FF) return "Hebrew";
            if (c >= 0x0600 && c <= 0x06FF) return "Arabic";
            if (c >= 0x3040 && c <= 0x30FF) return "Kana";
            if (c >= 0x3400 && c <= 0x9FFF) return "Han";
            if (c >= 0xAC00 && c <= 0xD7AF || c >= 0x1100 && c <= 0x11FF) return "Hangul";
            if (c >= 0x0E00 && c <= 0x0E7F) return "Thai";
            if (c >= 0x0900 && c <= 0x097F) return "Devanagari";
            return "Other";
        }

        private static readonly Regex AlnumIdPattern = new Regex(@"^[A-Za-z]{1,3}[-_ ]?[0-9]+$");

        internal static string FormOf(string value)
        {
            if (string.IsNullOrEmpty(value)) return "empty";
            if (AlnumIdPattern.IsMatch(value)) return "alnum-id";
            bool digit = false;
            bool separator = false;
            foreach (char c in value)
            {
                if (c >= '0' && c <= '9') digit = true;
                else if (c == '-' || c == '_' || c == ' ' || c == '\'') separator = true;
            }
            if (digit) return "alnum";
            return separator ? "compound" : "word";
        }

        private static void ScanNameFolder(ModContentPack mod, string dir, HashSet<string> seenFiles, bool collectCandidates)
        {
            if (!Directory.Exists(dir)) return;
            var categories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["First_Male"] = "FirstMale", ["First_Female"] = "FirstFemale",
                ["Last"] = "Last", ["Nick_Male"] = "NickMale",
                ["Nick_Female"] = "NickFemale", ["Nick_Unisex"] = "NickUnisex",
                ["Animal_Male"] = "AnimalMale", ["Animal_Female"] = "AnimalFemale",
                ["Animal_Unisex"] = "AnimalUnisex"
            };
            foreach (string file in Directory.GetFiles(dir, "*.txt", SearchOption.AllDirectories))
            {
                if (!seenFiles.Add(Path.GetFullPath(file))) continue;
                if (!categories.TryGetValue(Path.GetFileNameWithoutExtension(file), out string category)) continue;
                foreach (string line in File.ReadAllLines(file))
                {
                    string name = line.Trim();
                    if (name.Length > 0 && !name.StartsWith("#", StringComparison.Ordinal))
                    {
                        Add(category, name, mod, "name-file");
                        if (collectCandidates)
                        {
                            AddCandidate(category, name);
                        }
                    }
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
                            if (!"Female".Equals(gender, StringComparison.OrdinalIgnoreCase)) Add("FirstMale", first, mod, "solid-bio-file");
                            if (!"Male".Equals(gender, StringComparison.OrdinalIgnoreCase)) Add("FirstFemale", first, mod, "solid-bio-file");
                        }
                        if (!string.IsNullOrEmpty(nick))
                        {
                            if ("Male".Equals(gender, StringComparison.OrdinalIgnoreCase)) Add("NickMale", nick, mod, "solid-bio-file");
                            else if ("Female".Equals(gender, StringComparison.OrdinalIgnoreCase)) Add("NickFemale", nick, mod, "solid-bio-file");
                            else Add("NickUnisex", nick, mod, "solid-bio-file");
                        }
                        if (!string.IsNullOrEmpty(last)) Add("Last", last, mod, "solid-bio-file");
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

        private static void Add(string category, string name, ModContentPack mod, string sourceKind,
            string expandedFrom = null, int expandedCount = 0)
        {
            if (string.IsNullOrEmpty(name) || mod == null) return;
            string key = Key(category, name);
            if (!Sources.TryGetValue(key, out List<Source> list)) Sources[key] = list = new List<Source>();
            string packageId = mod.PackageId ?? "unknown";
            if (list.Exists(x => x.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase))) return;
            var source = new Source
            {
                PackageId = packageId,
                ModName = mod.Name ?? packageId,
                Origin = mod.IsOfficialMod ? "vanilla" : "mod",
                SourceKind = sourceKind,
                ExpandedFrom = expandedFrom ?? string.Empty,
                ExpandedCount = expandedCount
            };
            list.Add(source);
            if (!SourcesByName.TryGetValue(name, out List<Source> byName)) SourcesByName[name] = byName = new List<Source>();
            if (!byName.Exists(x => x.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase))) byName.Add(source);
        }

        private static ModContentPack CoreMod()
        {
            foreach (ModContentPack mod in LoadedModManager.RunningMods)
                if (mod != null && "ludeon.rimworld".Equals(mod.PackageId, StringComparison.OrdinalIgnoreCase)) return mod;
            return null;
        }

        private static string Key(string category, string name) => category + "\0" + name;
    }
}
