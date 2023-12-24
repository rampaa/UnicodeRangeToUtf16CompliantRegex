using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace UnicodeRangeToUtf16CompliantRegex;

// Basically a 1:1 copy of https://stackoverflow.com/a/47627127

/// <summary>
/// Patches the <see cref="Regex"/> class so it will automatically convert and interpret
/// UTF32 characters expressed like <c>\U00010000</c> or UTF32 ranges expressed
/// like <c>\U00010000-\U00010001</c>.
/// </summary>
internal sealed partial class Utf32Regex : Regex
{
    private const char MinLowSurrogate = '\uDC00';
    private const char MaxLowSurrogate = '\uDFFF';

    private const char MinHighSurrogate = '\uD800';
    private const char MaxHighSurrogate = '\uDBFF';

    // Match any character class such as [A-z]
    [GeneratedRegex(@"(?<!\\)(\[.*?(?<!\\)\])", RegexOptions.Compiled)]
    private static partial Regex CharacterClassRegex();

    // Match a UTF32 range such as \U000E01F0-\U000E0FFF
    // or an individual character such as \U000E0FFF
    [GeneratedRegex(@"(?<begin>\\U(?:00)?[0-9A-Fa-f]{6})-(?<end>\\U(?:00)?[0-9A-Fa-f]{6})|(?<begin>\\U(?:00)?[0-9A-Fa-f]{6})", RegexOptions.Compiled)]
    private static partial Regex Utf32RangeRegex();

    public Utf32Regex(string pattern)
        : base(ConvertUtf32Characters(pattern))
    {
    }

    public Utf32Regex(string pattern, RegexOptions options)
        : base(ConvertUtf32Characters(pattern), options)
    {
    }

    public Utf32Regex(string pattern, RegexOptions options, TimeSpan matchTimeout)
        : base(ConvertUtf32Characters(pattern), options, matchTimeout)
    {
    }

    private static string ConvertUtf32Characters(string regexString)
    {
        StringBuilder result = new();
        // Convert any UTF32 character ranges \U00000000-\U00FFFFFF to their
        // equivalent UTF16 characters
        ConvertUtf32CharacterClassesToUtf16Characters(regexString, result);
        // Now find all of the individual characters that were not in ranges and
        // fix those as well.
        ConvertUtf32CharactersToUtf16(result);

        return result.ToString();
    }

    private static void ConvertUtf32CharacterClassesToUtf16Characters(string regexString, StringBuilder result)
    {
        Match match = CharacterClassRegex().Match(regexString); // Reset
        int lastEnd = 0;
        if (match.Success)
        {
            do
            {
                string characterClass = match.Groups[1].Value;
                string convertedCharacterClass = ConvertUtf32CharacterRangesToUtf16Characters(characterClass);

                result.Append(regexString.AsSpan(lastEnd, match.Index - lastEnd)); // Remove the match
                result.Append(convertedCharacterClass); // Append replacement

                lastEnd = match.Index + match.Length;
            } while ((match = match.NextMatch()).Success);
        }
        result.Append(regexString.AsSpan(lastEnd)); // Append tail
    }

    private static string ConvertUtf32CharacterRangesToUtf16Characters(string characterClass)
    {
        StringBuilder result = new();
        StringBuilder chars = new();

        Match match = Utf32RangeRegex().Match(characterClass); // Reset
        int lastEnd = 0;
        if (match.Success)
        {
            do
            {
                string utf16Chars;
                string rangeBegin = match.Groups["begin"].Value[2..];

                if (!string.IsNullOrEmpty(match.Groups["end"].Value))
                {
                    string rangeEnd = match.Groups["end"].Value[2..];
                    utf16Chars = Utf32RangeToUtf16Chars(rangeBegin, rangeEnd);
                }
                else
                {
                    utf16Chars = Utf32ToUtf16Chars(rangeBegin);
                }

                result.Append(characterClass.AsSpan(lastEnd, match.Index - lastEnd)); // Remove the match
                chars.Append(utf16Chars); // Append replacement

                lastEnd = match.Index + match.Length;
            } while ((match = match.NextMatch()).Success);
        }
        result.Append(characterClass.AsSpan(lastEnd)); // Append tail of character class

        // Special case - if we have removed all of the contents of the
        // character class, we need to remove the square brackets and the
        // alternation character |
        int emptyCharClass = result.IndexOf("[]");
        if (emptyCharClass >= 0)
        {
            result.Remove(emptyCharClass, 2);
            // Append replacement ranges (exclude beginning |)
            result.Append(chars.ToString(1, chars.Length - 1));
        }
        else
        {
            // Append replacement rangess
            result.Append(chars);
        }

        return result.ToString();
}

    private static void ConvertUtf32CharactersToUtf16(StringBuilder result)
    {
        int where = result.IndexOf("\\U00");
        while (where >= 0)
        {
            string cp = Utf32ToUtf16Chars(result.ToString(where + 2, 8));
            result.Replace(where, where + 10, cp);

            where = result.IndexOf("\\U00");
        }
    }

    private static string Utf32RangeToUtf16Chars(string hexBegin, string hexEnd)
    {
        StringBuilder result = new();
        int beginCodePoint = int.Parse(hexBegin, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        int endCodePoint = int.Parse(hexEnd, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        string beginChars = char.ConvertFromUtf32(beginCodePoint);
        string endChars = char.ConvertFromUtf32(endCodePoint);
        int beginDiff = endChars[0] - beginChars[0];

        if (beginDiff == 0)
        {
            // If the begin character is the same, we can just use the syntax \uD807[\uDDEF-\uDFFF]
            result.Append('|');
            AppendUtf16Character(result, beginChars[0]);
            result.Append('[');
            AppendUtf16Character(result, beginChars[1]);
            result.Append('-');
            AppendUtf16Character(result, endChars[1]);
            result.Append(']');
        }
        else
        {
            // If the begin character is not the same, create 3 ranges
            // 1. The remainder of the first
            // 2. A range of all of the middle characters
            // 3. The beginning of the last

            result.Append('|');
            AppendUtf16Character(result, beginChars[0]);
            result.Append('[');
            AppendUtf16Character(result, beginChars[1]);
            result.Append('-');
            AppendUtf16Character(result, MaxLowSurrogate);
            result.Append(']');

            // We only need a middle range if the ranges are not adjacent
            if (beginDiff > 1)
            {
                result.Append('|');
                // We only need a character class if there are more than 1
                // characters in the middle range
                if (beginDiff > 2)
                {
                    result.Append('[');
                }
                AppendUtf16Character(result, (char)Math.Min(beginChars[0] + 1, MaxHighSurrogate));
                if (beginDiff > 2)
                {
                    result.Append('-');
                    AppendUtf16Character(result, (char)Math.Max(endChars[0] - 1, MinHighSurrogate));
                    result.Append(']');
                }
                result.Append('[');
                AppendUtf16Character(result, MinLowSurrogate);
                result.Append('-');
                AppendUtf16Character(result, MaxLowSurrogate);
                result.Append(']');
            }

            result.Append('|');
            AppendUtf16Character(result, endChars[0]);
            result.Append('[');
            AppendUtf16Character(result, MinLowSurrogate);
            result.Append('-');
            AppendUtf16Character(result, endChars[1]);
            result.Append(']');
        }
        return result.ToString();
    }

    private static string Utf32ToUtf16Chars(string hex)
    {
        int codePoint = int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Utf32ToUtf16Chars(codePoint);
    }

    private static string Utf32ToUtf16Chars(int codePoint)
    {
        StringBuilder result = new();
        Utf32ToUtf16Chars(codePoint, result);
        return result.ToString();
    }

    private static void Utf32ToUtf16Chars(int codePoint, StringBuilder result)
    {
        // Use regex alteration to on the entire range of UTF32 code points
        // to ensure each one is treated as a group.
        result.Append('|');
        AppendUtf16CodePoint(result, codePoint);
    }

    private static void AppendUtf16CodePoint(StringBuilder text, int cp)
    {
        string chars = char.ConvertFromUtf32(cp);
        AppendUtf16Character(text, chars[0]);
        if (chars.Length == 2)
        {
            AppendUtf16Character(text, chars[1]);
        }
    }

    private static void AppendUtf16Character(StringBuilder text, char c)
    {
        text.Append(@"\u");
        text.Append(Convert.ToString(c, 16).ToUpperInvariant());
    }
}
