using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Namae
{
    internal static class NaturalLineBreaks
    {
        private const int CacheLimit = 512;
        private static readonly Dictionary<CacheKey, string> Cache = new Dictionary<CacheKey, string>();
        private static readonly Queue<CacheKey> CacheOrder = new Queue<CacheKey>();

        internal static bool Enabled =>
            NamaeMod.Settings != null && NamaeMod.Settings.naturalLineBreaks;

        internal static LineBreakMode ActiveMode()
        {
            string folder = LanguageDatabase.activeLanguage?.folderName ?? "";
            int suffix = folder.IndexOf(" (", StringComparison.Ordinal);
            if (suffix > 0) folder = folder.Substring(0, suffix);
            if (folder.Equals("Japanese", StringComparison.OrdinalIgnoreCase))
                return LineBreakMode.Japanese;
            if (folder.Equals("ChineseSimplified", StringComparison.OrdinalIgnoreCase)
                || folder.Equals("ChineseTraditional", StringComparison.OrdinalIgnoreCase)
                || folder.Equals("Chinese", StringComparison.OrdinalIgnoreCase))
                return LineBreakMode.Chinese;
            if (folder.Equals("Korean", StringComparison.OrdinalIgnoreCase))
                return LineBreakMode.Korean;
            return LineBreakMode.None;
        }

        internal static string Format(string text, float width, GUIStyle style)
        {
            LineBreakMode mode = ActiveMode();
            if (!Enabled || mode == LineBreakMode.None || style == null || !style.wordWrap
                || string.IsNullOrEmpty(text) || !NaturalLineBreaker.ContainsRelevantText(text))
                return text;

            Vector2 probe = style.CalcSize(new GUIContent("漢W"));
            var key = new CacheKey(text, width, style.GetHashCode(),
                Mathf.RoundToInt(probe.x * 10f), Mathf.RoundToInt(probe.y * 10f), style.fontSize,
                style.richText, mode);
            if (Cache.TryGetValue(key, out string cached)) return cached;

            string formatted = NaturalLineBreaker.Format(text, width, mode,
                value => style.CalcSize(new GUIContent(value)).x);
            Cache[key] = formatted;
            CacheOrder.Enqueue(key);
            while (Cache.Count > CacheLimit && CacheOrder.Count > 0)
                Cache.Remove(CacheOrder.Dequeue());
            return formatted;
        }

        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            private readonly string text;
            private readonly int width;
            private readonly int styleHash;
            private readonly int probeWidth;
            private readonly int probeHeight;
            private readonly int fontSize;
            private readonly bool richText;
            private readonly LineBreakMode mode;

            internal CacheKey(string text, float width, int styleHash, int probeWidth,
                int probeHeight, int fontSize,
                bool richText, LineBreakMode mode)
            {
                this.text = text;
                this.width = Mathf.RoundToInt(width * 10f);
                this.styleHash = styleHash;
                this.probeWidth = probeWidth;
                this.probeHeight = probeHeight;
                this.fontSize = fontSize;
                this.richText = richText;
                this.mode = mode;
            }

            public bool Equals(CacheKey other) =>
                width == other.width && styleHash == other.styleHash
                && probeWidth == other.probeWidth && probeHeight == other.probeHeight
                && fontSize == other.fontSize
                && richText == other.richText && mode == other.mode
                && string.Equals(text, other.text, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is CacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = text?.GetHashCode() ?? 0;
                    hash = hash * 397 ^ width;
                    hash = hash * 397 ^ styleHash;
                    hash = hash * 397 ^ probeWidth;
                    hash = hash * 397 ^ probeHeight;
                    hash = hash * 397 ^ fontSize;
                    hash = hash * 397 ^ richText.GetHashCode();
                    hash = hash * 397 ^ (int)mode;
                    return hash;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) })]
    internal static class Patch_Widgets_Label_NaturalLineBreaks
    {
        static void Prefix(Rect rect, ref string label, out bool __state)
        {
            GUIStyle style = Text.CurFontStyle;
            __state = style.wordWrap;
            if (!__state || !NaturalLineBreaks.Enabled) return;
            string formatted = NaturalLineBreaks.Format(label, rect.width, style);
            if (ReferenceEquals(formatted, label) || formatted == label) return;
            label = formatted;
            style.wordWrap = false;
        }

        static void Postfix(bool __state)
        {
            if (__state) Text.CurFontStyle.wordWrap = true;
        }

        static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state) Text.CurFontStyle.wordWrap = true;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Text), nameof(Text.CalcHeight))]
    internal static class Patch_Text_CalcHeight_NaturalLineBreaks
    {
        static bool Prefix(string text, float width, ref float __result)
        {
            GUIStyle style = Text.CurFontStyle;
            if (!NaturalLineBreaks.Enabled || !style.wordWrap) return true;
            string stripped = text.StripTags();
            string formatted = NaturalLineBreaks.Format(stripped, width, style);
            if (formatted == stripped) return true;

            bool wordWrap = style.wordWrap;
            try
            {
                style.wordWrap = false;
                __result = style.CalcHeight(new GUIContent(formatted), width);
                return false;
            }
            finally
            {
                style.wordWrap = wordWrap;
            }
        }
    }
}
