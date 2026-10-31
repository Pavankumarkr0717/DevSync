using System.Text;

namespace DevSync.Domain.Common;

/// <summary>Text helpers shared by value objects. Lengths are Unicode code points, matching PostgreSQL char_length.</summary>
public static class Text
{
    public static int CodePointLength(string value)
    {
        var count = 0;
        foreach (var _ in value.EnumerateRunes())
        {
            count++;
        }

        return count;
    }

    public static bool ContainsControlCharacters(string value, ReadOnlySpan<char> allowed = default)
    {
        foreach (var rune in value.EnumerateRunes())
        {
            if (Rune.IsControl(rune) && (rune.Value > char.MaxValue || !allowed.Contains((char)rune.Value)))
            {
                return true;
            }
        }

        return false;
    }
}
