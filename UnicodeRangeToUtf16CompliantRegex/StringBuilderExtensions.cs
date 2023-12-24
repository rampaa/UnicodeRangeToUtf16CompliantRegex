using System.Text;

namespace UnicodeRangeToUtf16CompliantRegex;

// Basically a 1:1 copy of https://stackoverflow.com/a/47627127
internal static class StringBuilderExtensions
{
    /// <summary>
    /// Searches for the index of the specified character. The search for the
    /// character starts at the specified offset and moves towards the end.
    /// </summary>
    /// <param name="text">This <see cref="StringBuilder"/>.</param>
    /// <param name="value">The string to find.</param>
    /// <param name="startIndex">The starting offset.</param>
    /// <returns>The index of the specified character, or -1 if the character isn't found.</returns>
    public static int IndexOf(this StringBuilder text, string value, int startIndex = 0)
    {
        int length = value.Length;
        int maxSearchLength = (text.Length - length) + 1;

        for (int i = startIndex; i < maxSearchLength; i++)
        {
            if (text[i] == value[0])
            {
                int index = 1;
                while (index < length && text[i + index] == value[index])
                {
                    ++index;
                }

                if (index == length)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Replaces the specified subsequence in this builder with the specified
    /// string.
    /// </summary>
    /// <param name="text">this builder.</param>
    /// <param name="start">the inclusive begin index.</param>
    /// <param name="end">the exclusive end index.</param>
    /// <param name="str">the replacement string.</param>
    /// <returns>this builder.</returns>
    /// <exception cref="IndexOutOfRangeException">
    /// if <paramref name="start"/> is negative, greater than the current
    /// <see cref="StringBuilder.Length"/> or greater than <paramref name="end"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">if <paramref name="str"/> is <c>null</c>.</exception>
    public static void Replace(this StringBuilder text, int start, int end, string str)
    {
        if (start >= 0)
        {
            if (end > text.Length)
            {
                end = text.Length;
            }

            if (end > start)
            {
                int stringLength = str.Length;
                int diff = end - start - stringLength;
                if (diff > 0)
                {
                    // replacing with fewer characters
                    text.Remove(start, diff);
                }
                else if (diff < 0)
                {
                    // replacing with more characters...need some room
                    text.Insert(start, new char[-diff]);
                }

                // copy the chars based on the new length
                for (int i = 0; i < stringLength; i++)
                {
                    text[i + start] = str[i];
                }

                return;
            }
            if (start == end)
            {

                text.Insert(start, str);
                return;
            }
        }

        throw new ArgumentOutOfRangeException(null);
    }
}
