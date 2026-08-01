using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace Namae
{
    // Language name pack. Any mod can add one in its Defs/. Paths are relative to that mod.
    public class NamaeNameSetDef : Def
    {
        public string language;      // language folderName, e.g. Japanese
        public string firstMale;
        public string firstFemale;
        public string last;
        public string nickMale;
        public string nickFemale;
        public string nickUnisex;
        public string animalMale;
        public string animalFemale;
        public string animalUnisex;
    }

    public class NamaeSettings : ModSettings
    {
        public bool translateNames = true;
        public bool translateAnimalNames = true;
        public bool autoNameColonyAnimals = true;
        public bool avoidDuplicateAnimalNames = true;
        public bool disablePseudoTranslation = true;
        public bool devModeTooltips = true;
        public bool translateDevModeLabels = true;
        public bool naturalLineBreaks = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref translateNames, "translateNames", true);
            Scribe_Values.Look(ref translateAnimalNames, "translateAnimalNames", true);
            Scribe_Values.Look(ref autoNameColonyAnimals, "autoNameColonyAnimals", true);
            Scribe_Values.Look(ref avoidDuplicateAnimalNames, "avoidDuplicateAnimalNames", true);
            Scribe_Values.Look(ref disablePseudoTranslation, "disablePseudoTranslation", true);
            Scribe_Values.Look(ref devModeTooltips, "devModeTooltips", true);
            Scribe_Values.Look(ref translateDevModeLabels, "translateDevModeLabels", true);
            Scribe_Values.Look(ref naturalLineBreaks, "naturalLineBreaks", false);
            base.ExposeData();
        }
    }

    public class NamaeMod : Mod
    {
        public static NamaeSettings Settings;
        private static bool patchesApplied;
        private static readonly MethodInfo PseudoTranslatedMethod =
            AccessTools.Method(typeof(Translator), "PseudoTranslated");
        private string lastExportPath;
        private Vector2 settingsScrollPosition;
        private float settingsContentHeight;

        public NamaeMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<NamaeSettings>();
            if (patchesApplied) return;
            try
            {
                new Harmony("cljur.namae").PatchAll(Assembly.GetExecutingAssembly());
                patchesApplied = true;
            }
            catch (Exception e)
            {
                Log.Error("[Namae] patch failed: " + e);
            }
        }

        public override string SettingsCategory() => "Name to Namaé";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            float viewHeight = Math.Max(1000f, Math.Max(inRect.height, settingsContentHeight));
            var viewRect = new Rect(0f, 0f, inRect.width - 18f, viewHeight);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);

            var list = new Listing_Standard();
            list.Begin(viewRect);

            NamaeSettings s = Settings;

            Text.Font = GameFont.Medium;
            list.Label("Namae_PawnNamesHeader".Translate());
            Text.Font = GameFont.Small;
            list.CheckboxLabeled("Namae_NamesEnable".Translate(), ref s.translateNames,
                "Namae_NamesEnableDesc".Translate());
            list.Gap();
            if (Current.Game != null)
            {
                if (list.ButtonText("Namae_RetranslateButton".Translate()))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "Namae_RetranslateConfirm".Translate(),
                        () =>
                        {
                            int n = NameDictionaries.RetranslateExisting();
                            Messages.Message("Namae_RetranslateResult".Translate(n),
                                MessageTypeDefOf.TaskCompletion, false);
                        },
                        destructive: true));
                }
            }
            else
            {
                list.Label("Namae_PlayingOnly".Translate());
            }

            list.GapLine();
            Text.Font = GameFont.Medium;
            list.Label("Namae_AnimalNamesHeader".Translate());
            Text.Font = GameFont.Small;
            list.CheckboxLabeled("Namae_AnimalNamesEnable".Translate(), ref s.translateAnimalNames,
                "Namae_AnimalNamesEnableDesc".Translate());
            list.CheckboxLabeled("Namae_AutoNameColonyAnimals".Translate(), ref s.autoNameColonyAnimals,
                "Namae_AutoNameColonyAnimalsDesc".Translate());
            list.CheckboxLabeled("Namae_AvoidDuplicateAnimalNames".Translate(), ref s.avoidDuplicateAnimalNames,
                "Namae_AvoidDuplicateAnimalNamesDesc".Translate());
            list.Gap();
            if (Current.Game != null)
            {
                if (list.ButtonText("Namae_RetranslateAnimalsButton".Translate()))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "Namae_RetranslateAnimalsConfirm".Translate(),
                        () =>
                        {
                            int n = NameDictionaries.RetranslateExistingAnimals();
                            Messages.Message("Namae_RetranslateAnimalsResult".Translate(n),
                                MessageTypeDefOf.TaskCompletion, false);
                        },
                        destructive: true));
                }
            }
            else
            {
                list.Label("Namae_AnimalsPlayingOnly".Translate());
            }
            list.GapLine();
            Text.Font = GameFont.Medium;
            list.Label("Namae_NameReportsHeader".Translate());
            Text.Font = GameFont.Small;
            list.Label("Namae_Counts".Translate(MissingNames.NewTotal, MissingNames.Total));
            list.Gap();

            if (list.ButtonText("Namae_ExportNew".Translate()))
            {
                lastExportPath = MissingNames.ExportNewNames();
            }
            if (list.ButtonText("Namae_ExportUntranslated".Translate()))
            {
                lastExportPath = MissingNames.Export();
            }
            if (list.ButtonText("Namae_ExportNickAudit".Translate()))
            {
                lastExportPath = MissingNames.ExportNickAudit();
            }
            if (list.ButtonText("Namae_OpenFolder".Translate()))
            {
                Application.OpenURL(MissingNames.OutputFolder());
            }

            list.GapLine();
            Text.Font = GameFont.Medium;
            list.Label("Namae_DeveloperModeHeader".Translate());
            Text.Font = GameFont.Small;
            list.Label("Namae_PseudoTranslationHeader".Translate());
            list.CheckboxLabeled("Namae_PseudoTranslationEnable".Translate(),
                ref s.disablePseudoTranslation,
                "Namae_PseudoTranslationEnableDesc".Translate());
            list.Gap();
            list.Label("Namae_PseudoTranslationCheckDesc".Translate());
            list.Label(PseudoTranslationCheckText());

            list.Gap();
            list.Label("Namae_DevTooltipsHeader".Translate());
            list.CheckboxLabeled("Namae_DevTooltipsEnable".Translate(),
                ref s.devModeTooltips,
                "Namae_DevTooltipsEnableDesc".Translate());

            list.Gap();
            list.Label("Namae_DevLabelsHeader".Translate());
            list.CheckboxLabeled("Namae_DevLabelsEnable".Translate(),
                ref s.translateDevModeLabels,
                "Namae_DevLabelsEnableDesc".Translate());
            list.Label("Namae_DevLabelsMissing".Translate(MissingDevActions.Count));
            if (list.ButtonText("Namae_ExportDevActions".Translate()))
            {
                lastExportPath = MissingDevActions.Export();
            }

            list.GapLine();
            Text.Font = GameFont.Medium;
            list.Label("Namae_NaturalLineBreaksHeader".Translate());
            Text.Font = GameFont.Small;
            list.CheckboxLabeled("Namae_NaturalLineBreaksEnable".Translate(),
                ref s.naturalLineBreaks,
                "Namae_NaturalLineBreaksEnableDesc".Translate());
            DrawNaturalLineBreaksPreview(list, s.naturalLineBreaks);

            if (!string.IsNullOrEmpty(lastExportPath))
            {
                list.Gap();
                list.Label("Namae_ExportedTo".Translate());
                list.Label(lastExportPath);
            }

            settingsContentHeight = Math.Max(inRect.height, list.CurHeight + 40f);
            list.End();
            Widgets.EndScrollView();
        }

        private static void DrawNaturalLineBreaksPreview(Listing_Standard list, bool enabled)
        {
            list.Gap();
            list.Label(enabled
                ? "Namae_NaturalLineBreaksPreviewEnabled".Translate()
                : "Namae_NaturalLineBreaksPreviewDisabled".Translate());

            const float previewWidth = 280f;
            string sample = "Namae_NaturalLineBreaksPreviewText".Translate();
            float height = Math.Max(Text.LineHeight * 3f, Text.CalcHeight(sample, previewWidth) + 12f);
            Rect outer = list.GetRect(height);
            outer.width = Math.Min(previewWidth + 12f, outer.width);
            Widgets.DrawMenuSection(outer);
            Widgets.Label(outer.ContractedBy(6f), sample);
        }

        private static string PseudoTranslationCheckText()
        {
            const string sample = "If this sentence is readable, the setting is working.";
            try
            {
                return PseudoTranslatedMethod?.Invoke(null, new object[] { sample }) as string ?? sample;
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Namae] pseudo-translation check failed: " + e, 197438621);
                return sample;
            }
        }
    }

    [HarmonyPatch(typeof(Translator), "PseudoTranslated")]
    internal static class Patch_PseudoTranslated
    {
        static bool Prefix(string original, ref string __result)
        {
            if (NamaeMod.Settings == null || !NamaeMod.Settings.disablePseudoTranslation)
            {
                return true;
            }

            __result = original;
            return false;
        }
    }

    [HarmonyPatch(typeof(DevGUI), nameof(DevGUI.Label))]
    internal static class Patch_DevGUI_Label
    {
        static void Postfix(Rect rect, string label)
        {
            if (!ShouldAddDevTooltip(rect, label, rect.width)) return;
            TooltipHandler.TipRegion(rect, label);
        }

        internal static bool ShouldAddDevTooltip(Rect rect, string label, float availableWidth)
        {
            if (NamaeMod.Settings == null || !NamaeMod.Settings.devModeTooltips) return false;
            if (!Prefs.DevMode || string.IsNullOrEmpty(label) || availableWidth <= 0f) return false;
            return Text.CurFontStyle.CalcSize(new GUIContent(label)).x > availableWidth;
        }
    }

    [HarmonyPatch(typeof(DevGUI), nameof(DevGUI.CheckboxPinnable))]
    internal static class Patch_DevGUI_CheckboxPinnable
    {
        static void Postfix(Rect rect, string label)
        {
            Patch_DevActionTooltip.Register(rect, label, rect.width - 15f);
        }
    }

    [HarmonyPatch(typeof(DevGUI), nameof(DevGUI.ButtonDebugPinnable))]
    internal static class Patch_DevGUI_ButtonDebugPinnable
    {
        static void Postfix(Rect rect, string label)
        {
            Patch_DevActionTooltip.Register(rect, label, rect.width);
        }
    }

    [HarmonyPatch(typeof(DebugTabMenu_Actions), "GenerateCacheForMethod")]
    internal static class Patch_DebugTabMenu_Actions_GenerateCacheForMethod
    {
        static void Prefix(MethodInfo method, DebugActionAttribute attribute)
        {
            if (method == null || attribute == null) return;

            string translated = MissingDevActions.Observe(method, attribute);
            if (NamaeMod.Settings == null || !NamaeMod.Settings.translateDevModeLabels) return;
            if (!string.IsNullOrEmpty(translated)) attribute.name = translated;
        }
    }

    [HarmonyPatch(typeof(DebugTabMenu_Settings), nameof(DebugTabMenu_Settings.InitActions))]
    internal static class Patch_DebugTabMenu_Settings_InitActions
    {
        static void Postfix(DebugActionNode __result)
        {
            if (NamaeMod.Settings == null || !NamaeMod.Settings.translateDevModeLabels
                || __result == null) return;

            __result.label = "Namae_DevTab_Settings".TranslateSimple();
            if (__result.children == null) return;
            foreach (DebugActionNode child in __result.children)
            {
                FieldInfo field = child?.settingsField;
                if (field == null) continue;
                string key = "Namae_DevSetting_" + field.DeclaringType.Name + "_" + field.Name;
                string original = child.label;
                if (key.CanTranslate()) child.label = key.TranslateSimple();
                DevMenuDescriptions.Register(original, child.label, key);

                string categoryKey = "Namae_DevCategory_" + child.category;
                if (categoryKey.CanTranslate()) child.category = categoryKey.TranslateSimple();
            }
        }
    }

    [HarmonyPatch(typeof(DebugTabMenu_Output), "GenerateCacheForMethod")]
    internal static class Patch_DebugTabMenu_Output_GenerateCacheForMethod
    {
        static void Prefix(MethodInfo method, DebugOutputAttribute attribute)
        {
            if (method == null || attribute == null
                || NamaeMod.Settings == null || !NamaeMod.Settings.translateDevModeLabels) return;
            string original = string.IsNullOrEmpty(attribute.name)
                ? GenText.SplitCamelCase(method.Name)
                : attribute.name;
            string key = "Namae_DevOutput_" + method.Name;
            if (key.CanTranslate()) attribute.name = key.TranslateSimple();
            DevMenuDescriptions.Register(original, attribute.name ?? original, key);
        }
    }

    [HarmonyPatch(typeof(DebugTabMenu_Output), nameof(DebugTabMenu_Output.InitActions))]
    internal static class Patch_DebugTabMenu_Output_InitActions
    {
        static void Postfix(DebugActionNode __result)
        {
            if (NamaeMod.Settings == null || !NamaeMod.Settings.translateDevModeLabels
                || __result == null) return;

            __result.label = "Namae_DevTab_Outputs".TranslateSimple();
            if (__result.children == null) return;
            foreach (DebugActionNode child in __result.children)
            {
                if (child == null || string.IsNullOrEmpty(child.category)) continue;
                string categoryKey = "Namae_DevCategory_" + child.category.Replace(" ", "");
                if (categoryKey.CanTranslate()) child.category = categoryKey.TranslateSimple();
            }
        }
    }

    [HarmonyPatch(typeof(DebugActionNode), nameof(DebugActionNode.AddChild))]
    internal static class Patch_DebugActionNode_AddChild
    {
        static void Postfix(DebugActionNode child)
        {
            if (child == null || child.sourceAttribute != null) return;
            string translated = MissingDevActions.ObserveDynamic(child);
            if (NamaeMod.Settings == null || !NamaeMod.Settings.translateDevModeLabels) return;
            if (!string.IsNullOrEmpty(translated) && child.labelGetter == null) child.label = translated;
        }
    }

    [HarmonyPatch(typeof(DebugActionNode), "get_LabelNow")]
    internal static class Patch_DebugActionNode_LabelNow
    {
        static void Postfix(DebugActionNode __instance, ref string __result)
        {
            if (__instance == null || __instance.sourceAttribute != null || __instance.labelGetter == null) return;
            string original = __instance.label;
            string translated = MissingDevActions.ObserveDynamic(__instance);
            if (NamaeMod.Settings == null || !NamaeMod.Settings.translateDevModeLabels) return;
            if (string.IsNullOrEmpty(translated) || string.IsNullOrEmpty(__result)) return;

            if (!string.IsNullOrEmpty(original)
                && __result.StartsWith(original, StringComparison.Ordinal))
            {
                __result = translated + __result.Substring(original.Length);
            }
            else
            {
                __result = translated;
            }
        }
    }

    internal static class Patch_DevActionTooltip
    {
        private static readonly Dictionary<string, string> TooltipCache =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly HashSet<string> NoTooltip =
            new HashSet<string>(StringComparer.Ordinal);

        internal static bool Register(Rect rect, string label, float availableWidth)
        {
            if (NamaeMod.Settings == null || !NamaeMod.Settings.devModeTooltips
                || string.IsNullOrEmpty(label)) return false;

            if (TooltipCache.TryGetValue(label, out string cached))
            {
                TooltipHandler.TipRegion(rect, cached);
                return true;
            }
            if (NoTooltip.Contains(label)) return false;

            string description = MissingDevActions.DescriptionForLabel(label)
                ?? DevMenuDescriptions.ForLabel(label);
            bool truncated = availableWidth > 0f
                && Text.CurFontStyle.CalcSize(new GUIContent(label)).x > availableWidth;
            if (string.IsNullOrEmpty(description) && !truncated)
            {
                NoTooltip.Add(label);
                return false;
            }

            string tooltip = string.IsNullOrEmpty(description)
                ? label
                : description;
            TooltipCache[label] = tooltip;
            TooltipHandler.TipRegion(rect, tooltip);
            return true;
        }
    }

    internal static class DevMenuDescriptions
    {
        private static readonly Dictionary<string, string> Keys =
            new Dictionary<string, string>(StringComparer.Ordinal);

        internal static void Register(string original, string translated, string key)
        {
            if (!string.IsNullOrEmpty(original)) Keys[original] = key;
            if (!string.IsNullOrEmpty(translated)) Keys[translated] = key;
        }

        internal static string ForLabel(string label)
        {
            if (string.IsNullOrEmpty(label) || !Keys.TryGetValue(label, out string key)) return null;
            string descriptionKey;
            const string settingPrefix = "Namae_DevSetting_";
            const string outputPrefix = "Namae_DevOutput_";
            if (key.StartsWith(settingPrefix, StringComparison.Ordinal))
                descriptionKey = "Namae_DevSettingDesc_" + key.Substring(settingPrefix.Length);
            else if (key.StartsWith(outputPrefix, StringComparison.Ordinal))
                descriptionKey = "Namae_DevOutputDesc_" + key.Substring(outputPrefix.Length);
            else
                return null;
            return descriptionKey.CanTranslate() ? descriptionKey.TranslateSimple() : null;
        }
    }

    [StaticConstructorOnStartup]
    internal static class NamaeStartup
    {
        static NamaeStartup()
        {
            NameDictionaries.LoadFromDefs();
            MissingNames.ScanLoadedNames();
        }
    }

    public static class NameDictionaries
    {
        public static bool Active;
        public static readonly Dictionary<string, string> FirstMale = new Dictionary<string, string>(StringComparer.Ordinal);
        public static readonly Dictionary<string, string> FirstFemale = new Dictionary<string, string>(StringComparer.Ordinal);
        public static readonly Dictionary<string, string> Last = new Dictionary<string, string>(StringComparer.Ordinal);
        public static readonly Dictionary<string, string> NickMale = new Dictionary<string, string>(StringComparer.Ordinal);
        public static readonly Dictionary<string, string> NickFemale = new Dictionary<string, string>(StringComparer.Ordinal);
        public static readonly Dictionary<string, string> NickUnisex = new Dictionary<string, string>(StringComparer.Ordinal);
        public static readonly Dictionary<string, string> AnimalMale = new Dictionary<string, string>(StringComparer.Ordinal);
        public static readonly Dictionary<string, string> AnimalFemale = new Dictionary<string, string>(StringComparer.Ordinal);
        public static readonly Dictionary<string, string> AnimalUnisex = new Dictionary<string, string>(StringComparer.Ordinal);
        public static readonly HashSet<string> FirstMaleRows = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> FirstFemaleRows = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> LastRows = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NickMaleRows = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NickFemaleRows = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> NickUnisexRows = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> AnimalMaleRows = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> AnimalFemaleRows = new HashSet<string>(StringComparer.Ordinal);
        public static readonly HashSet<string> AnimalUnisexRows = new HashSet<string>(StringComparer.Ordinal);

        public static void LoadFromDefs()
        {
            Clear();
            try
            {
                string activeName = LanguageDatabase.activeLanguage?.folderName;
                if (string.IsNullOrEmpty(activeName)) return;

                int packs = 0;
                foreach (NamaeNameSetDef def in DefDatabase<NamaeNameSetDef>.AllDefs)
                {
                    if (!LanguageMatches(activeName, def.language)) continue;
                    string root = def.modContentPack?.RootDir;
                    if (string.IsNullOrEmpty(root)) continue;

                    MergeFile(root, def.firstMale, FirstMale, FirstMaleRows);
                    MergeFile(root, def.firstFemale, FirstFemale, FirstFemaleRows);
                    MergeFile(root, def.last, Last, LastRows);
                    MergeFile(root, def.nickMale, NickMale, NickMaleRows);
                    MergeFile(root, def.nickFemale, NickFemale, NickFemaleRows);
                    MergeFile(root, def.nickUnisex, NickUnisex, NickUnisexRows);
                    MergeFile(root, def.animalMale, AnimalMale, AnimalMaleRows);
                    MergeFile(root, def.animalFemale, AnimalFemale, AnimalFemaleRows);
                    MergeFile(root, def.animalUnisex, AnimalUnisex, AnimalUnisexRows);
                    packs++;
                }

                Active = packs > 0;
                Log.Message($"[Namae] language='{activeName}' packs={packs} "
                    + $"FM={FirstMale.Count} FF={FirstFemale.Count} Last={Last.Count} "
                    + $"NM={NickMale.Count} NF={NickFemale.Count} NU={NickUnisex.Count}");
                Log.Message($"[Namae] animal names M={AnimalMale.Count} F={AnimalFemale.Count} U={AnimalUnisex.Count}");
            }
            catch (Exception e)
            {
                Log.Error("[Namae] LoadFromDefs failed: " + e);
            }
        }

        private static void Clear()
        {
            Active = false;
            FirstMale.Clear(); FirstFemale.Clear(); Last.Clear();
            NickMale.Clear(); NickFemale.Clear(); NickUnisex.Clear();
            AnimalMale.Clear(); AnimalFemale.Clear(); AnimalUnisex.Clear();
            FirstMaleRows.Clear(); FirstFemaleRows.Clear(); LastRows.Clear();
            NickMaleRows.Clear(); NickFemaleRows.Clear(); NickUnisexRows.Clear();
            AnimalMaleRows.Clear(); AnimalFemaleRows.Clear(); AnimalUnisexRows.Clear();
        }

        // Match on the language code (folderName up to " ("), not a prefix.
        // Avoids "Chinese"/"Spanish"/"Portuguese" leaking into their regional variants.
        private static bool LanguageMatches(string activeName, string token)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(activeName)) return false;
            if (string.Equals(activeName, token, StringComparison.OrdinalIgnoreCase)) return true;
            string code = activeName;
            int p = activeName.IndexOf(" (", StringComparison.Ordinal);
            if (p > 0) code = activeName.Substring(0, p);
            return string.Equals(code, token, StringComparison.OrdinalIgnoreCase);
        }

        private static void MergeFile(string root, string relPath, Dictionary<string, string> target, HashSet<string> rows)
        {
            if (string.IsNullOrEmpty(relPath)) return;
            string path = Path.Combine(root, relPath);
            if (!File.Exists(path)) return;
            try
            {
                XDocument doc = XDocument.Load(path);
                foreach (XElement e in doc.Descendants("n"))
                {
                    string en = (string)e.Attribute("en");
                    string t = (string)e.Attribute("t");
                    if (string.IsNullOrEmpty(en)) continue;
                    rows.Add(en);
                    if (!string.IsNullOrEmpty(t)) target[en] = t;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Namae] failed to load {path}: {ex}");
            }
        }

        public static void Translate(Pawn pawn, ref Name name)
        {
            if (!Active || !(name is NameTriple nt)) return;
            if (NamaeMod.Settings != null && !NamaeMod.Settings.translateNames) return;

            bool female = pawn != null && pawn.gender == Gender.Female;
            Dictionary<string, string> firstDict = female ? FirstFemale : FirstMale;
            Dictionary<string, string> nickDict = female ? NickFemale : NickMale;

            MissingNames.Observe(nt, female);

            string first = Lookup(firstDict, nt.First);
            string last = Lookup(Last, nt.Last);
            string nick = LookupNick(nickDict, nt.Nick);

            if (first == nt.First && last == nt.Last && nick == nt.Nick) return;
            name = new NameTriple(first, nick, last);
        }

        public static bool TryGenerateAnimalName(Pawn pawn, NameStyle style, out Name name)
        {
            name = null;
            if (!Active || pawn?.RaceProps?.Animal != true || style != NameStyle.Full) return false;
            if (NamaeMod.Settings != null && !NamaeMod.Settings.translateAnimalNames) return false;

            List<string> candidates = AnimalNameCandidates(pawn,
                pawn.Faction == Faction.OfPlayer &&
                NamaeMod.Settings?.avoidDuplicateAnimalNames == true);
            if (candidates.Count == 0) return false;
            name = new NameSingle(candidates[Rand.Range(0, candidates.Count)]);
            return true;
        }

        public static void TryAutoNameColonyAnimal(Pawn pawn)
        {
            if (pawn?.RaceProps?.Animal != true || pawn.Faction != Faction.OfPlayer) return;
            if (pawn.Name == null || !pawn.Name.Numerical) return;
            if (NamaeMod.Settings == null || !NamaeMod.Settings.autoNameColonyAnimals) return;
            if (!NamaeMod.Settings.translateAnimalNames) return;
            List<string> candidates = AnimalNameCandidates(pawn,
                NamaeMod.Settings.avoidDuplicateAnimalNames);
            if (candidates.Count > 0)
                pawn.Name = new NameSingle(candidates[Rand.Range(0, candidates.Count)]);
        }

        private static List<string> AnimalNameCandidates(Pawn pawn, bool avoidDuplicates)
        {
            Dictionary<string, string> gendered =
                pawn.gender == Gender.Female ? AnimalFemale : AnimalMale;
            List<string> candidates = gendered.Values.Concat(AnimalUnisex.Values)
                .Distinct(StringComparer.Ordinal).ToList();
            if (!avoidDuplicates || candidates.Count == 0) return candidates;

            var used = new HashSet<string>(
                PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive
                    .Where(other => other != pawn && other.Faction == Faction.OfPlayer &&
                        other.def == pawn.def && other.Name != null && !other.Name.Numerical)
                    .Select(other => other.Name.ToStringFull),
                StringComparer.Ordinal);
            List<string> unused = candidates.Where(value => !used.Contains(value)).ToList();
            return unused.Count > 0 ? unused : candidates;
        }

        // One-time pass over existing pawns in the loaded game. Names are baked into
        // the save as plain strings, so pawns made before the mod stay English until this runs.
        public static int RetranslateExisting()
        {
            if (!Active) return 0;
            int changed = 0;
            foreach (Pawn p in PawnsFinder.All_AliveOrDead)
            {
                if (p == null || !(p.Name is NameTriple nt)) continue;
                bool female = p.gender == Gender.Female;
                Dictionary<string, string> firstDict = female ? FirstFemale : FirstMale;
                Dictionary<string, string> nickDict = female ? NickFemale : NickMale;

                string first = Lookup(firstDict, nt.First);
                string last = Lookup(Last, nt.Last);
                string nick = LookupNick(nickDict, nt.Nick);
                if (first == nt.First && last == nt.Last && nick == nt.Nick) continue;

                p.Name = new NameTriple(first, nick, last);
                changed++;
            }
            return changed;
        }

        public static int RetranslateExistingAnimals()
        {
            if (!Active) return 0;
            int changed = 0;
            foreach (Pawn p in PawnsFinder.All_AliveOrDead)
            {
                if (p?.RaceProps?.Animal != true || !(p.Name is NameSingle ns)) continue;

                if (p.Name.Numerical && p.Faction == Faction.OfPlayer)
                {
                    List<string> candidates = AnimalNameCandidates(p,
                        NamaeMod.Settings?.avoidDuplicateAnimalNames == true);
                    if (candidates.Count == 0) continue;
                    p.Name = new NameSingle(candidates[Rand.Range(0, candidates.Count)]);
                    changed++;
                    continue;
                }

                Dictionary<string, string> gendered =
                    p.gender == Gender.Female ? AnimalFemale : AnimalMale;
                string original = ns.Name;
                string translated = Lookup(gendered, original);
                if (translated == original) translated = Lookup(AnimalUnisex, original);
                if (translated == original)
                {
                    translated = LookupLocalizedAnimalName(p.gender, original, gendered);
                }
                if (translated == original) continue;

                p.Name = new NameSingle(translated);
                changed++;
            }
            return changed;
        }

        private static string LookupLocalizedAnimalName(
            Gender gender,
            string value,
            Dictionary<string, string> gendered)
        {
            string genderedFile =
                gender == Gender.Female ? "Names/Animal_Female" : "Names/Animal_Male";
            string translated = LookupLocalizedAnimalFile(genderedFile, value, gendered);
            return translated != value
                ? translated
                : LookupLocalizedAnimalFile("Names/Animal_Unisex", value, AnimalUnisex);
        }

        private static string LookupLocalizedAnimalFile(
            string fileName,
            string value,
            Dictionary<string, string> dictionary)
        {
            LoadedLanguage active = LanguageDatabase.activeLanguage;
            LoadedLanguage english = LanguageDatabase.defaultLanguage;
            if (active == null || english == null ||
                !active.TryGetStringsFromFile(fileName, out List<string> localized) ||
                !english.TryGetStringsFromFile(fileName, out List<string> originals))
            {
                return value;
            }

            int count = Math.Min(localized.Count, originals.Count);
            for (int i = 0; i < count; i++)
            {
                if (localized[i] != value) continue;
                string translated = Lookup(dictionary, originals[i]);
                if (translated != originals[i]) return translated;
            }
            return value;
        }

        private static string Lookup(Dictionary<string, string> dict, string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return dict.TryGetValue(value, out string t) ? t : value;
        }

        private static string LookupNick(Dictionary<string, string> gendered, string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (gendered.TryGetValue(value, out string t)) return t;
            if (NickUnisex.TryGetValue(value, out string u)) return u;
            if (Last.TryGetValue(value, out string l)) return l;
            if (FirstMale.TryGetValue(value, out string m)) return m;
            if (FirstFemale.TryGetValue(value, out string f)) return f;
            return value;
        }
    }

    [HarmonyPatch]
    internal static class Patch_GeneratePawnName
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(PawnBioAndNameGenerator), "GeneratePawnName");
        }

        static bool Prefix(Pawn pawn, NameStyle style, ref Name __result)
        {
            return !NameDictionaries.TryGenerateAnimalName(pawn, style, out __result);
        }

        static void Postfix(Pawn pawn, ref Name __result)
        {
            NameDictionaries.Translate(pawn, ref __result);
        }
    }

    [HarmonyPatch(typeof(PawnBioAndNameGenerator), "GiveAppropriateBioAndNameTo")]
    internal static class Patch_GiveAppropriateBioAndNameTo
    {
        static void Postfix(Pawn pawn)
        {
            if (pawn?.Name == null) return;
            Name name = pawn.Name;
            NameDictionaries.Translate(pawn, ref name);
            pawn.Name = name;
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn),
        new[] { typeof(PawnGenerationRequest) })]
    internal static class Patch_PawnGenerator_GeneratePawn
    {
        static void Postfix(Pawn __result)
        {
            NameDictionaries.TryAutoNameColonyAnimal(__result);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction),
        new[] { typeof(Faction), typeof(Pawn) })]
    internal static class Patch_Pawn_SetFaction
    {
        static void Postfix(Pawn __instance)
        {
            NameDictionaries.TryAutoNameColonyAnimal(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayDataLoader), nameof(PlayDataLoader.LoadAllPlayData))]
    internal static class Patch_PlayDataLoader_LoadAllPlayData
    {
        static void Postfix()
        {
            NameDictionaries.LoadFromDefs();
            LongEventHandler.ExecuteWhenFinished(MissingNames.ScanLoadedNames);
        }
    }
}
