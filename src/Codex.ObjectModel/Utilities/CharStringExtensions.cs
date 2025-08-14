namespace Codex.Utilities;

public static class CharStringExtensions
{
    public static TChars Substring<TChars>(this TChars str, int start, int length)
        where TChars : struct, ICharString<TChars>
    {
        return str.WithChars(str.Chars.Slice(start, length));
    }

    public static ReadOnlyMemory<char> AsMemory<TChars>(this TChars str, int start, int length)
        where TChars : struct, ICharString<TChars>
    {
        return str.Chars.Slice(start, length);
    }

    public static ReadOnlyMemory<char> AsMemory<TChars>(this TChars str, int start)
        where TChars : struct, ICharString<TChars>
    {
        return str.Chars.Slice(start);
    }

    public static TChars Substring<TChars>(this TChars str, int start)
        where TChars : struct, ICharString<TChars>
    {
        return str.WithChars(str.Chars.Slice(start));
    }

    public static TChars Trim<TChars>(this TChars str)
        where TChars : struct, ICharString<TChars>
    {
        return str.WithChars(str.Chars.Trim());
    }

    public static TChars TrimStart<TChars>(this TChars str)
        where TChars : struct, ICharString<TChars>
    {
        return str.WithChars(str.Chars.TrimStart());
    }

    public static TChars TrimEnd<TChars>(this TChars str)
        where TChars : struct, ICharString<TChars>
    {
        return str.WithChars(str.Chars.TrimEnd());
    }

    public static bool IsNullOrWhitespace<TChars>(this TChars str)
        where TChars : struct, ICharString<TChars>
    {
        return str.Chars.Length == 0 || str.Chars.TrimEnd().Length == 0;
    }

    public static CharString GetCharString(this string s, Extent e)
    {
        return new CharString(s.AsMemory(e.Start, e.Length));
    }
}

