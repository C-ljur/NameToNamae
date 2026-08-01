using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using Verse;

namespace Namae
{
    // Tracks English names with no translation in the active pack.
    public static class MissingNames
    {
        public static readonly HashSet<string> FirstMale = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> FirstFemale = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> Last = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NickMale = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NickFemale = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NickUnisex = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NewFirstMale = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NewFirstFemale = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NewLast = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NewNickMale = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NewNickFemale = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NewNickUnisex = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> AnimalMale = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> AnimalFemale = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> AnimalUnisex = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NewAnimalMale = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NewAnimalFemale = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NewAnimalUnisex = new HashSet<string>(StringComparer.Ordinal);

        public static int Total =>
            FirstMale.Count + FirstFemale.Count + Last.Count + NickMale.Count + NickFemale.Count + NickUnisex.Count;
        public static int NewTotal =>
            NewFirstMale.Count + NewFirstFemale.Count + NewLast.Count + NewNickMale.Count + NewNickFemale.Count + NewNickUnisex.Count;
        public static int AnimalTotal => AnimalMale.Count + AnimalFemale.Count + AnimalUnisex.Count;
        public static int NewAnimalTotal => NewAnimalMale.Count + NewAnimalFemale.Count + NewAnimalUnisex.Count;

        // NameBank.NamesFor is non-public; access it via reflection.
        private static readonly MethodInfo NamesForMethod =
            AccessTools.Method(typeof(NameBank), "NamesFor", new[] { typeof(PawnNameSlot), typeof(Gender) });

        // Config/Namae/ - keeps our dumps out of the shared Config folder.
        public static string OutputFolder()
        {
            string dir = Path.Combine(GenFilePaths.ConfigFolderPath, "Namae");
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception e)
            {
                Log.Error("[Namae] could not create output folder: " + e);
                return GenFilePaths.ConfigFolderPath;
            }
            return dir;
        }

        public static void ScanLoadedNames()
        {
            Clear();
            if (!NameDictionaries.Active) return;
            try
            {
                NameSourceIndex.Rebuild();
                foreach (PawnBio bio in SolidBioDatabase.allBios)
                {
                    NameSourceIndex.AddBio(bio);
                    NameTriple nt = bio?.name;
                    if (nt == null) continue;

                    bool male = bio.gender != GenderPossibility.Female;   // Male or Either
                    bool female = bio.gender != GenderPossibility.Male;   // Female or Either

                    if (male) NoteFirst(nt.First, false);
                    if (female) NoteFirst(nt.First, true);
                    NoteNick(nt.Nick, NickGenderOf(bio.gender));
                    NoteLast(nt.Last);
                }

                ScanBaseBank();
                ScanAnimalBanks();

                if (NewTotal > 0)
                {
                    Log.Warning($"[Namae] {NewTotal} new name row(s) not present in XML "
                        + $"(FM={NewFirstMale.Count} FF={NewFirstFemale.Count} Last={NewLast.Count} "
                        + $"NM={NewNickMale.Count} NF={NewNickFemale.Count} NU={NewNickUnisex.Count}).");
                }

                if (Total > 0)
                {
                    Log.Warning($"[Namae] {Total} untranslated name(s) detected in loaded content "
                        + $"(FM={FirstMale.Count} FF={FirstFemale.Count} Last={Last.Count} "
                        + $"NM={NickMale.Count} NF={NickFemale.Count} NU={NickUnisex.Count}). "
                        + "Mod settings > Name to Namae > 'Export untranslated names' to dump the list.");
                }
            }
            catch (Exception e)
            {
                Log.Error("[Namae] ScanLoadedNames failed: " + e);
            }
        }

        private static void Clear()
        {
            FirstMale.Clear(); FirstFemale.Clear(); Last.Clear(); NickMale.Clear(); NickFemale.Clear(); NickUnisex.Clear();
            NewFirstMale.Clear(); NewFirstFemale.Clear(); NewLast.Clear(); NewNickMale.Clear(); NewNickFemale.Clear(); NewNickUnisex.Clear();
            AnimalMale.Clear(); AnimalFemale.Clear(); AnimalUnisex.Clear();
            NewAnimalMale.Clear(); NewAnimalFemale.Clear(); NewAnimalUnisex.Clear();
        }

        private static NickGender NickGenderOf(GenderPossibility g)
        {
            if (g == GenderPossibility.Male) return NickGender.Male;
            if (g == GenderPossibility.Female) return NickGender.Female;
            return NickGender.Unisex;
        }

        private static void ScanBaseBank()
        {
            NameBank bank = PawnNameDatabaseShuffled.BankOf(PawnNameCategory.HumanStandard);
            if (bank == null || NamesForMethod == null) return;
            foreach (string s in Names(bank, PawnNameSlot.First, Gender.Male)) { NameSourceIndex.AddBaseName("FirstMale", s); NoteFirst(s, false); }
            foreach (string s in Names(bank, PawnNameSlot.First, Gender.Female)) { NameSourceIndex.AddBaseName("FirstFemale", s); NoteFirst(s, true); }
            foreach (string s in Names(bank, PawnNameSlot.Nick, Gender.Male)) { NameSourceIndex.AddBaseName("NickMale", s); NoteNick(s, NickGender.Male); }
            foreach (string s in Names(bank, PawnNameSlot.Nick, Gender.Female)) { NameSourceIndex.AddBaseName("NickFemale", s); NoteNick(s, NickGender.Female); }
            foreach (string s in Names(bank, PawnNameSlot.Nick, Gender.None)) { NameSourceIndex.AddBaseName("NickUnisex", s); NoteNick(s, NickGender.Unisex); }
            foreach (string s in Names(bank, PawnNameSlot.Last, Gender.None)) { NameSourceIndex.AddBaseName("Last", s); NoteLast(s); }
        }

        private static List<string> Names(NameBank bank, PawnNameSlot slot, Gender gender)
        {
            return (List<string>)NamesForMethod.Invoke(bank, new object[] { slot, gender }) ?? new List<string>();
        }

        private static void ScanAnimalBanks()
        {
            LoadedLanguage english = LanguageDatabase.defaultLanguage;
            if (english == null) return;
            ScanAnimalFile(english, "Names/Animal_Male", "AnimalMale", NameDictionaries.AnimalMale,
                NameDictionaries.AnimalMaleRows, AnimalMale, NewAnimalMale);
            ScanAnimalFile(english, "Names/Animal_Female", "AnimalFemale", NameDictionaries.AnimalFemale,
                NameDictionaries.AnimalFemaleRows, AnimalFemale, NewAnimalFemale);
            ScanAnimalFile(english, "Names/Animal_Unisex", "AnimalUnisex", NameDictionaries.AnimalUnisex,
                NameDictionaries.AnimalUnisexRows, AnimalUnisex, NewAnimalUnisex);
        }

        private static void ScanAnimalFile(LoadedLanguage english, string file, string category,
            Dictionary<string, string> translated, HashSet<string> rows,
            HashSet<string> untranslated, HashSet<string> newNames)
        {
            if (!english.TryGetStringsFromFile(file, out List<string> names)) return;
            foreach (string name in names)
            {
                if (!HasAsciiLetter(name)) continue;
                NameSourceIndex.AddBaseName(category, name);
                if (!rows.Contains(name)) newNames.Add(name);
                else if (!translated.ContainsKey(name)) untranslated.Add(name);
            }
        }

        public static void Observe(NameTriple nt, bool female, Pawn pawn = null)
        {
            if (nt == null) return;
            NameSourceIndex.AddPawn(pawn, nt, female);
            NoteFirst(nt.First, female);
            NoteNick(nt.Nick, female ? NickGender.Female : NickGender.Male);
            NoteLast(nt.Last);
        }

        private static void NoteFirst(string v, bool female)
        {
            if (!HasAsciiLetter(v) || NameDictionaries.HumanTranslationValues.Contains(v)) return;
            if (female)
            {
                if (NameDictionaries.FirstFemaleValues.Contains(v)) return;
                if (!NameDictionaries.FirstFemaleRows.Contains(v)) NewFirstFemale.Add(v);
                else if (!NameDictionaries.FirstFemale.ContainsKey(v)) FirstFemale.Add(v);
            }
            else
            {
                if (NameDictionaries.FirstMaleValues.Contains(v)) return;
                if (!NameDictionaries.FirstMaleRows.Contains(v)) NewFirstMale.Add(v);
                else if (!NameDictionaries.FirstMale.ContainsKey(v)) FirstMale.Add(v);
            }
        }

        private static void NoteNick(string v, NickGender g)
        {
            if (!HasAsciiLetter(v) || NameDictionaries.HumanTranslationValues.Contains(v)) return;
            if (g == NickGender.Male && NameDictionaries.NickMaleValues.Contains(v)) return;
            if (g == NickGender.Female && NameDictionaries.NickFemaleValues.Contains(v)) return;
            switch (g)
            {
                case NickGender.Male:
                    if (!NameDictionaries.NickMaleRows.Contains(v)) NewNickMale.Add(v);
                    else if (!NameDictionaries.NickMale.ContainsKey(v) && !NickCoveredCommon(v)) NickMale.Add(v);
                    break;
                case NickGender.Female:
                    if (!NameDictionaries.NickFemaleRows.Contains(v)) NewNickFemale.Add(v);
                    else if (!NameDictionaries.NickFemale.ContainsKey(v) && !NickCoveredCommon(v)) NickFemale.Add(v);
                    break;
                default:
                    if (!NameDictionaries.NickUnisexRows.Contains(v)) NewNickUnisex.Add(v);
                    else if (!NickCoveredCommon(v)) NickUnisex.Add(v);
                    break;
            }
        }

        // Covered without a gendered nick entry.
        private static bool NickCoveredCommon(string v)
        {
            return NameDictionaries.NickUnisex.ContainsKey(v)
                || NameDictionaries.Last.ContainsKey(v)
                || NameDictionaries.FirstMale.ContainsKey(v)
                || NameDictionaries.FirstFemale.ContainsKey(v);
        }

        private static void NoteLast(string v)
        {
            if (!HasAsciiLetter(v) || NameDictionaries.HumanTranslationValues.Contains(v)) return;
            if (!NameDictionaries.LastRows.Contains(v)) NewLast.Add(v);
            else if (!NameDictionaries.Last.ContainsKey(v)) Last.Add(v);
        }

        public static string Export()
        {
            var sb = new StringBuilder();
            AppendReportHeader(sb);
            AppendRows(sb, "FirstMale", FirstMale, "untranslated");
            AppendRows(sb, "FirstFemale", FirstFemale, "untranslated");
            AppendRows(sb, "Last", Last, "untranslated");
            AppendRows(sb, "NickMale", NickMale, "untranslated");
            AppendRows(sb, "NickFemale", NickFemale, "untranslated");
            AppendRows(sb, "NickUnisex", NickUnisex, "untranslated");

            string path = Path.Combine(OutputFolder(), "UntranslatedNames.csv");
            try
            {
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                Log.Message($"[Namae] exported {Total} untranslated names to {path}");
            }
            catch (Exception e)
            {
                Log.Error("[Namae] export failed: " + e);
            }
            return path;
        }

        public static string ExportNewNames()
        {
            var sb = new StringBuilder();
            AppendReportHeader(sb);
            AppendRows(sb, "FirstMale", NewFirstMale, "new");
            AppendRows(sb, "FirstFemale", NewFirstFemale, "new");
            AppendRows(sb, "Last", NewLast, "new");
            AppendRows(sb, "NickMale", NewNickMale, "new");
            AppendRows(sb, "NickFemale", NewNickFemale, "new");
            AppendRows(sb, "NickUnisex", NewNickUnisex, "new");

            string path = Path.Combine(OutputFolder(), "NewNames.csv");
            try
            {
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                Log.Message($"[Namae] exported {NewTotal} new name rows to {path}");
            }
            catch (Exception e)
            {
                Log.Error("[Namae] new-name export failed: " + e);
            }
            return path;
        }

        public static string ExportAnimalNames(bool untranslated)
        {
            var sb = new StringBuilder();
            AppendReportHeader(sb);
            AppendRows(sb, "AnimalMale", untranslated ? AnimalMale : NewAnimalMale,
                untranslated ? "untranslated" : "new");
            AppendRows(sb, "AnimalFemale", untranslated ? AnimalFemale : NewAnimalFemale,
                untranslated ? "untranslated" : "new");
            AppendRows(sb, "AnimalUnisex", untranslated ? AnimalUnisex : NewAnimalUnisex,
                untranslated ? "untranslated" : "new");
            string file = untranslated ? "UntranslatedAnimalNames.csv" : "NewAnimalNames.csv";
            string path = Path.Combine(OutputFolder(), file);
            try
            {
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                Log.Message($"[Namae] exported animal name report to {path}");
            }
            catch (Exception e)
            {
                Log.Error("[Namae] animal-name export failed: " + e);
            }
            return path;
        }


        public static string ExportNickAudit()
        {
            var male = new HashSet<string>(StringComparer.Ordinal);
            var female = new HashSet<string>(StringComparer.Ordinal);
            var none = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (PawnBio bio in SolidBioDatabase.allBios)
                {
                    string nick = bio?.name?.Nick;
                    if (string.IsNullOrEmpty(nick)) continue;
                    if (bio.gender == GenderPossibility.Male) male.Add(nick);
                    else if (bio.gender == GenderPossibility.Female) female.Add(nick);
                    else none.Add(nick);
                }

                NameBank bank = PawnNameDatabaseShuffled.BankOf(PawnNameCategory.HumanStandard);
                if (bank != null && NamesForMethod != null)
                {
                    foreach (string s in Names(bank, PawnNameSlot.Nick, Gender.Male)) male.Add(s);
                    foreach (string s in Names(bank, PawnNameSlot.Nick, Gender.Female)) female.Add(s);
                    foreach (string s in Names(bank, PawnNameSlot.Nick, Gender.None)) none.Add(s);
                }
            }
            catch (Exception e)
            {
                Log.Error("[Namae] nick audit scan failed: " + e);
            }

            var unisex = new HashSet<string>(none, StringComparer.Ordinal);
            foreach (string m in male) if (female.Contains(m)) unisex.Add(m);

            var onlyMale = new HashSet<string>(StringComparer.Ordinal);
            foreach (string m in male) if (!unisex.Contains(m)) onlyMale.Add(m);
            var onlyFemale = new HashSet<string>(StringComparer.Ordinal);
            foreach (string f in female) if (!unisex.Contains(f)) onlyFemale.Add(f);

            NameSourceIndex.Rebuild();
            var sb = new StringBuilder();
            AppendReportHeader(sb);
            AppendRows(sb, "NickMale", onlyMale, "audit");
            AppendRows(sb, "NickFemale", onlyFemale, "audit");
            AppendRows(sb, "NickUnisex", unisex, "audit");

            string path = Path.Combine(OutputFolder(), "NickAudit.csv");
            try
            {
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                Log.Message($"[Namae] wrote nick audit to {path}");
            }
            catch (Exception e)
            {
                Log.Error("[Namae] nick audit write failed: " + e);
            }
            return path;
        }

        private static void AppendSection(StringBuilder sb, string title, HashSet<string> set)
        {
            sb.AppendLine();
            sb.AppendLine($"[{title}] ({set.Count})");
            var list = new List<string>(set);
            list.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string s in list) sb.AppendLine(s);
        }

        private static void AppendReportHeader(StringBuilder sb)
        {
            sb.AppendLine("category,name,script,packageId,modName,origin,sourceKind,status");
        }

        private static void AppendRows(StringBuilder sb, string category, HashSet<string> set, string status)
        {
            var names = new List<string>(set);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string name in names)
            {
                IReadOnlyList<NameSourceIndex.Source> sources = NameSourceIndex.Find(category, name);
                foreach (NameSourceIndex.Source source in sources)
                {
                    sb.Append(Csv(category)).Append(',')
                        .Append(Csv(name)).Append(',')
                        .Append(Csv(NameSourceIndex.ScriptOf(name))).Append(',')
                        .Append(Csv(source.PackageId)).Append(',')
                        .Append(Csv(source.ModName)).Append(',')
                        .Append(Csv(source.Origin)).Append(',')
                        .Append(Csv(source.SourceKind)).Append(',')
                        .AppendLine(Csv(status));
                }
            }
        }

        private static string Csv(string value)
        {
            string sanitized = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
            return "\"" + sanitized.Replace("\"", "\"\"") + "\"";
        }

        private static bool HasAsciiLetter(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char c in value)
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) return true;
            return false;
        }

        private enum NickGender { Male, Female, Unisex }
    }

    public static class MissingDevActions
    {
        private sealed class Entry
        {
            public string PackageId;
            public string AssemblyName;
            public string TypeName;
            public string MethodName;
            public string Key;
            public string Label;
            public string Category;
        }

        private static readonly Dictionary<string, Entry> Entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static readonly Dictionary<DebugActionAttribute, string> AttributeKeys =
            new Dictionary<DebugActionAttribute, string>();
        private static readonly Dictionary<DebugActionNode, string> NodeKeys =
            new Dictionary<DebugActionNode, string>();
        private static readonly Dictionary<string, string> LabelKeys =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public static int Count => Entries.Count;

        public static string Observe(MethodInfo method, DebugActionAttribute attribute)
        {
            if (method == null || attribute == null) return null;

            string originalLabel = string.IsNullOrEmpty(attribute.name)
                ? GenText.SplitCamelCase(method.Name)
                : attribute.name;
            string specificKey = MakeKey(method);
            string legacyKey = "Namae_DevAction_" + method.Name;

            if (specificKey.CanTranslate())
            {
                AttributeKeys[attribute] = specificKey;
                string translated = specificKey.TranslateSimple();
                RegisterLabelKey(originalLabel, translated, specificKey);
                return translated;
            }
            if (method.DeclaringType?.Assembly?.GetName().Name == "Assembly-CSharp"
                && legacyKey.CanTranslate())
            {
                AttributeKeys[attribute] = legacyKey;
                string translated = legacyKey.TranslateSimple();
                RegisterLabelKey(originalLabel, translated, legacyKey);
                return translated;
            }

            AttributeKeys[attribute] = specificKey;

            if (!Entries.ContainsKey(specificKey))
            {
                Assembly assembly = method.DeclaringType?.Assembly;
                Entries.Add(specificKey, new Entry
                {
                    PackageId = PackageIdFor(assembly),
                    AssemblyName = assembly?.GetName().Name ?? "unknown",
                    TypeName = method.DeclaringType?.FullName ?? "unknown",
                    MethodName = method.Name,
                    Key = specificKey,
                    Label = originalLabel,
                    Category = attribute.category ?? "General"
                });
            }
            return null;
        }

        public static string ObserveDynamic(DebugActionNode node)
        {
            if (node == null || string.IsNullOrEmpty(node.label)) return null;
            if (!HasAsciiLetter(node.label)) return null;
            MethodInfo method = node.action?.Method
                ?? node.pawnAction?.Method
                ?? node.childGetter?.Method;
            if (method == null) return null;

            string key = MakeKey(method) + "_Label_" + StableHash(node.label).ToString("X8");
            NodeKeys[node] = key;
            if (key.CanTranslate())
            {
                string translated = key.TranslateSimple();
                RegisterLabelKey(node.label, translated, key);
                return translated;
            }

            if (!Entries.ContainsKey(key))
            {
                Assembly assembly = method.DeclaringType?.Assembly;
                Entries.Add(key, new Entry
                {
                    PackageId = PackageIdFor(assembly),
                    AssemblyName = assembly?.GetName().Name ?? "unknown",
                    TypeName = method.DeclaringType?.FullName ?? "unknown",
                    MethodName = method.Name,
                    Key = key,
                    Label = node.label,
                    Category = node.category ?? "Dynamic"
                });
            }
            return null;
        }

        public static string DescriptionForLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return null;

            string normalized = label;
            if (normalized.StartsWith("T: ", StringComparison.Ordinal)) normalized = normalized.Substring(3);
            if (normalized.EndsWith("...", StringComparison.Ordinal))
                normalized = normalized.Substring(0, normalized.Length - 3);

            if (!LabelKeys.TryGetValue(normalized, out string key)) return null;
            const string prefix = "Namae_DevAction_";
            if (!key.StartsWith(prefix, StringComparison.Ordinal)) return null;

            string descriptionKey = "Namae_DevActionDesc_" + key.Substring(prefix.Length);
            return descriptionKey.CanTranslate() ? descriptionKey.TranslateSimple() : null;
        }

        private static void RegisterLabelKey(string original, string translated, string key)
        {
            if (!string.IsNullOrEmpty(original)) LabelKeys[original] = key;
            if (!string.IsNullOrEmpty(translated)) LabelKeys[translated] = key;
        }

        public static string TranslationFor(DebugActionNode node)
        {
            if (node == null) return null;
            string key = null;
            if (node.sourceAttribute != null) AttributeKeys.TryGetValue(node.sourceAttribute, out key);
            if (string.IsNullOrEmpty(key) && node.settingsField != null)
                key = "Namae_DevSetting_" + node.settingsField.DeclaringType.Name + "_" + node.settingsField.Name;
            if (string.IsNullOrEmpty(key) && node.action?.Method != null)
            {
                string outputKey = "Namae_DevOutput_" + node.action.Method.Name;
                if (outputKey.CanTranslate()) key = outputKey;
            }
            if (string.IsNullOrEmpty(key)) NodeKeys.TryGetValue(node, out key);
            return !string.IsNullOrEmpty(key) && key.CanTranslate() ? key.TranslateSimple() : null;
        }

        public static string DescriptionFor(DebugActionNode node)
        {
            if (node == null) return null;
            string key = null;
            if (node.sourceAttribute != null) AttributeKeys.TryGetValue(node.sourceAttribute, out key);
            if (string.IsNullOrEmpty(key)) NodeKeys.TryGetValue(node, out key);
            if (string.IsNullOrEmpty(key)) return null;

            const string prefix = "Namae_DevAction_";
            string specific = null;
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                string descriptionKey = "Namae_DevActionDesc_" + key.Substring(prefix.Length);
                if (descriptionKey.CanTranslate()) specific = descriptionKey.TranslateSimple();
            }

            string interaction = InteractionDescriptionFor(node);
            if (string.IsNullOrEmpty(specific)) return interaction;
            return string.IsNullOrEmpty(interaction) ? specific : specific + "\n\n" + interaction;
        }

        private static string InteractionDescriptionFor(DebugActionNode node)
        {
            if (node.settingsField != null) return "Namae_DevActionHelp_Toggle".TranslateSimple();
            if (node.childGetter != null || (node.children != null && node.children.Count > 0))
                return "Namae_DevActionHelp_Submenu".TranslateSimple();
            if (node.pawnAction != null || node.actionType == DebugActionType.ToolMapForPawns)
                return "Namae_DevActionHelp_SelectPawn".TranslateSimple();

            switch (node.actionType)
            {
                case DebugActionType.ToolMap:
                    return "Namae_DevActionHelp_SelectMap".TranslateSimple();
                case DebugActionType.ToolWorld:
                    return "Namae_DevActionHelp_SelectWorld".TranslateSimple();
                default:
                    return "Namae_DevActionHelp_Immediate".TranslateSimple();
            }
        }

        public static string Export()
        {
            var rows = new List<Entry>(Entries.Values);
            rows.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));

            var sb = new StringBuilder();
            sb.AppendLine("packageId,assembly,type,method,key,englishLabel,category,translation");
            foreach (Entry e in rows)
            {
                sb.Append(Csv(e.PackageId)).Append(',')
                    .Append(Csv(e.AssemblyName)).Append(',')
                    .Append(Csv(e.TypeName)).Append(',')
                    .Append(Csv(e.MethodName)).Append(',')
                    .Append(Csv(e.Key)).Append(',')
                    .Append(Csv(e.Label)).Append(',')
                    .Append(Csv(e.Category)).Append(',')
                    .AppendLine(Csv(string.Empty));
            }

            string path = Path.Combine(MissingNames.OutputFolder(), "NewDevActions.csv");
            try
            {
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
                Log.Message($"[Namae] exported {rows.Count} untranslated developer actions to {path}");
            }
            catch (Exception e)
            {
                Log.Error("[Namae] developer-action export failed: " + e);
            }
            return path;
        }

        private static string MakeKey(MethodInfo method)
        {
            Assembly assembly = method.DeclaringType?.Assembly;
            return "Namae_DevAction_"
                + Safe(assembly?.GetName().Name ?? "unknown") + "_"
                + Safe(method.DeclaringType?.FullName ?? "unknown") + "_"
                + Safe(method.Name);
        }

        private static string PackageIdFor(Assembly assembly)
        {
            if (assembly == null) return "unknown";
            if (assembly.GetName().Name == "Assembly-CSharp") return "ludeon.rimworld";
            foreach (ModContentPack mod in LoadedModManager.RunningMods)
            {
                if (mod?.assemblies?.loadedAssemblies != null
                    && mod.assemblies.loadedAssemblies.Contains(assembly))
                {
                    return mod.PackageId;
                }
            }
            return "unknown";
        }

        private static string Safe(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            }
            return sb.ToString();
        }

        private static uint StableHash(string value)
        {
            uint hash = 2166136261;
            foreach (char c in value)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }

        private static bool HasAsciiLetter(string value)
        {
            foreach (char c in value)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) return true;
            }
            return false;
        }

        private static string Csv(string value)
        {
            string sanitized = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
            return "\"" + sanitized.Replace("\"", "\"\"") + "\"";
        }
    }
}
