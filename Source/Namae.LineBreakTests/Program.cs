using System;
using System.Globalization;
using Namae;

static float Measure(string value)
{
    float width = 0;
    var elements = StringInfo.GetTextElementEnumerator(StripTags(value));
    while (elements.MoveNext()) width++;
    return width;
}

static string StripTags(string value)
{
    var result = new System.Text.StringBuilder();
    bool tag = false;
    foreach (char character in value)
    {
        if (character == '<') tag = true;
        else if (character == '>') tag = false;
        else if (!tag) result.Append(character);
    }
    return result.ToString();
}

static void Equal(string expected, string actual, string name)
{
    if (expected != actual)
        throw new Exception($"{name}: expected [{expected}] but got [{actual}]");
}

Equal("これは試\n験です。",
    NaturalLineBreaker.Format("これは試験です。", 4, LineBreakMode.Japanese, Measure),
    "Japanese punctuation");
Equal("これは\n「試験」",
    NaturalLineBreaker.Format("これは「試験」", 4, LineBreakMode.Japanese, Measure),
    "Opening bracket");
Equal("测试文\n本。",
    NaturalLineBreaker.Format("测试文本。", 3, LineBreakMode.Chinese, Measure),
    "Chinese punctuation");
Equal("한국어\n문장입니\n다.",
    NaturalLineBreaker.Format("한국어 문장입니다.", 5, LineBreakMode.Korean, Measure),
    "Korean spaces");
Equal("日本語\nEnglish",
    NaturalLineBreaker.Format("日本語English", 7, LineBreakMode.Japanese, Measure),
    "Latin word");
Equal("<b>日本\n語。</b>",
    NaturalLineBreaker.Format("<b>日本語。</b>", 2, LineBreakMode.Japanese, Measure),
    "Rich text tags");
Equal("A👩🏽‍💻日\n本語",
    NaturalLineBreaker.Format("A👩🏽‍💻日本語", 3, LineBreakMode.Japanese, Measure),
    "Grapheme cluster");

Console.WriteLine("NaturalLineBreaker tests passed.");
