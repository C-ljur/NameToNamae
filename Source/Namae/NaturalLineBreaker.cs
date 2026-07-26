using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Namae
{
    internal enum LineBreakMode
    {
        None,
        Japanese,
        Chinese,
        Korean
    }

    internal static class NaturalLineBreaker
    {
        private sealed class Atom
        {
            internal string Raw;
            internal string Visible;
            internal AtomKind Kind;
        }

        private enum AtomKind
        {
            Other,
            Space,
            Opening,
            Closing,
            NonStarter,
            Alphabetic,
            Number,
            Hyphen,
            Cjk,
            Hangul,
            NewLine,
            Tag
        }

        internal static string Format(
            string text,
            float width,
            LineBreakMode mode,
            Func<string, float> measure)
        {
            if (string.IsNullOrEmpty(text) || width <= 0f || mode == LineBreakMode.None
                || measure == null || !ContainsRelevantText(text))
                return text;

            List<Atom> atoms = Parse(text, mode);
            if (atoms.Count == 0) return text;

            var result = new StringBuilder(text.Length + 8);
            int start = 0;
            while (start < atoms.Count)
            {
                if (atoms[start].Kind == AtomKind.NewLine)
                {
                    result.Append(atoms[start].Raw);
                    start++;
                    continue;
                }

                int end = start;
                int lastBreak = -1;
                bool overflowed = false;
                while (end < atoms.Count && atoms[end].Kind != AtomKind.NewLine)
                {
                    int candidateEnd = end + 1;
                    string candidate = JoinRaw(atoms, start, candidateEnd);
                    if (measure(candidate) > width)
                    {
                        overflowed = true;
                        break;
                    }

                    if (candidateEnd < atoms.Count && CanBreak(atoms, candidateEnd, mode))
                        lastBreak = candidateEnd;
                    end = candidateEnd;
                }

                if (!overflowed)
                {
                    AppendRange(result, atoms, start, end);
                    start = end;
                    continue;
                }

                int breakAt = lastBreak > start ? lastBreak : Math.Max(start + 1, end);
                if (lastBreak <= start && end < atoms.Count
                    && (atoms[end].Kind == AtomKind.Closing
                        || atoms[end].Kind == AtomKind.NonStarter)
                    && breakAt > start + 1)
                    breakAt--;
                while (breakAt > start + 1 && atoms[breakAt - 1].Kind == AtomKind.Opening)
                    breakAt--;

                int contentEnd = breakAt;
                while (contentEnd > start && atoms[contentEnd - 1].Kind == AtomKind.Space)
                    contentEnd--;
                AppendRange(result, atoms, start, contentEnd);
                result.Append('\n');
                start = breakAt;
                while (start < atoms.Count && atoms[start].Kind == AtomKind.Space) start++;
            }
            return result.ToString();
        }

        internal static bool ContainsRelevantText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                int value = char.ConvertToUtf32(text, i);
                if (value > 0xFFFF) i++;
                if (IsCjk(value) || IsKana(value) || IsHangul(value)) return true;
            }
            return false;
        }

        private static List<Atom> Parse(string text, LineBreakMode mode)
        {
            var atoms = new List<Atom>();
            for (int index = 0; index < text.Length;)
            {
                if (text[index] == '<')
                {
                    int close = text.IndexOf('>', index + 1);
                    if (close >= 0)
                    {
                        atoms.Add(new Atom
                        {
                            Raw = text.Substring(index, close - index + 1),
                            Visible = "",
                            Kind = AtomKind.Tag
                        });
                        index = close + 1;
                        continue;
                    }
                }

                if (text[index] == '\r' || text[index] == '\n')
                {
                    int length = text[index] == '\r' && index + 1 < text.Length
                        && text[index + 1] == '\n' ? 2 : 1;
                    atoms.Add(new Atom
                    {
                        Raw = text.Substring(index, length),
                        Visible = "",
                        Kind = AtomKind.NewLine
                    });
                    index += length;
                    continue;
                }

                string element = StringInfo.GetNextTextElement(text, index);
                atoms.Add(new Atom
                {
                    Raw = element,
                    Visible = element,
                    Kind = Classify(element, mode)
                });
                index += element.Length;
            }
            return atoms;
        }

        private static AtomKind Classify(string element, LineBreakMode mode)
        {
            int value = char.ConvertToUtf32(element, 0);
            if (char.IsWhiteSpace(element, 0)) return AtomKind.Space;
            if (IsOpening(value)) return AtomKind.Opening;
            if (IsClosing(value)) return AtomKind.Closing;
            if (mode == LineBreakMode.Japanese && IsJapaneseNonStarter(value))
                return AtomKind.NonStarter;
            if (value == '-' || value == 0x2010 || value == 0x2013 || value == '/')
                return AtomKind.Hyphen;
            if (IsKana(value) || IsCjk(value)) return AtomKind.Cjk;
            if (IsHangul(value)) return AtomKind.Hangul;
            if (char.IsDigit(element, 0)) return AtomKind.Number;
            if (char.IsLetter(element, 0)) return AtomKind.Alphabetic;
            return AtomKind.Other;
        }

        private static bool CanBreak(List<Atom> atoms, int position, LineBreakMode mode)
        {
            Atom previous = PreviousVisible(atoms, position - 1);
            Atom next = NextVisible(atoms, position);
            if (previous == null || next == null) return false;
            if (previous.Kind == AtomKind.Opening) return false;
            if (next.Kind == AtomKind.Closing || next.Kind == AtomKind.NonStarter) return false;
            if (previous.Kind == AtomKind.Space || previous.Kind == AtomKind.Hyphen) return true;
            if (IsWord(previous.Kind) && IsWord(next.Kind)) return false;

            if (mode == LineBreakMode.Korean)
                return false;

            return IsEastAsian(previous.Kind) || IsEastAsian(next.Kind);
        }

        private static bool IsWord(AtomKind kind) =>
            kind == AtomKind.Alphabetic || kind == AtomKind.Number;

        private static bool IsEastAsian(AtomKind kind) =>
            kind == AtomKind.Cjk || kind == AtomKind.Hangul;

        private static Atom PreviousVisible(List<Atom> atoms, int index)
        {
            for (int i = index; i >= 0; i--)
                if (atoms[i].Kind != AtomKind.Tag) return atoms[i];
            return null;
        }

        private static Atom NextVisible(List<Atom> atoms, int index)
        {
            for (int i = index; i < atoms.Count; i++)
                if (atoms[i].Kind != AtomKind.Tag) return atoms[i];
            return null;
        }

        private static string JoinRaw(List<Atom> atoms, int start, int end)
        {
            var value = new StringBuilder();
            AppendRange(value, atoms, start, end);
            return value.ToString();
        }

        private static void AppendRange(StringBuilder target, List<Atom> atoms, int start, int end)
        {
            for (int i = start; i < end; i++) target.Append(atoms[i].Raw);
        }

        private static bool IsOpening(int value) =>
            value == '(' || value == '[' || value == '{'
            || value == 0x2018 || value == 0x201C
            || value == 0x3008 || value == 0x300A || value == 0x300C
            || value == 0x300E || value == 0x3010 || value == 0x3014
            || value == 0x3016 || value == 0x3018 || value == 0x301A
            || value == 0xFF08 || value == 0xFF3B || value == 0xFF5B;

        private static bool IsClosing(int value) =>
            value == ')' || value == ']' || value == '}'
            || value == ',' || value == '.' || value == '!' || value == '?'
            || value == ':' || value == ';'
            || value == 0x2019 || value == 0x201D
            || value == 0x3001 || value == 0x3002
            || value == 0x3009 || value == 0x300B || value == 0x300D
            || value == 0x300F || value == 0x3011 || value == 0x3015
            || value == 0x3017 || value == 0x3019 || value == 0x301B
            || value == 0xFF01 || value == 0xFF09 || value == 0xFF0C
            || value == 0xFF0E || value == 0xFF1A || value == 0xFF1B
            || value == 0xFF1F || value == 0xFF3D || value == 0xFF5D;

        private static bool IsJapaneseNonStarter(int value) =>
            value == 0x3005 || value == 0x303B || value == 0x309D || value == 0x309E
            || value == 0x30FD || value == 0x30FE || value == 0x30FC
            || value == 0x3099 || value == 0x309A
            || (value >= 0x3041 && value <= 0x3049 && value % 2 == 1)
            || value == 0x3063 || value == 0x3083 || value == 0x3085 || value == 0x3087
            || value == 0x308E || value == 0x3095 || value == 0x3096
            || value == 0x30A1 || value == 0x30A3 || value == 0x30A5
            || value == 0x30A7 || value == 0x30A9 || value == 0x30C3
            || value == 0x30E3 || value == 0x30E5 || value == 0x30E7
            || value == 0x30EE || value == 0x30F5 || value == 0x30F6;

        private static bool IsKana(int value) =>
            (value >= 0x3040 && value <= 0x30FF)
            || (value >= 0x31F0 && value <= 0x31FF)
            || (value >= 0xFF66 && value <= 0xFF9D);

        private static bool IsCjk(int value) =>
            (value >= 0x3400 && value <= 0x4DBF)
            || (value >= 0x4E00 && value <= 0x9FFF)
            || (value >= 0xF900 && value <= 0xFAFF)
            || (value >= 0x20000 && value <= 0x323AF);

        private static bool IsHangul(int value) =>
            (value >= 0x1100 && value <= 0x11FF)
            || (value >= 0x3130 && value <= 0x318F)
            || (value >= 0xAC00 && value <= 0xD7AF);
    }
}
