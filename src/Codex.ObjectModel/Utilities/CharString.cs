using System.Numerics;

namespace Codex.Utilities;

public record struct CharString(ReadOnlyMemory<char> Chars) : IEquatable<CharString>, ICharString<CharString>
{
    public override string ToString()
    {
        return Chars.ToString();
    }

    public int Length => Chars.Length;

    public ReadOnlySpan<char> Span => Chars.Span;

    public bool Equals(CharString other)
    {
        return Chars.Span.Equals(other.Chars.Span, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        return string.GetHashCode(Chars.Span, StringComparison.Ordinal);
    }

    public CharString WithChars(ReadOnlyMemory<char> chars)
    {
        return new CharString(chars);
    }

    public static implicit operator CharString(string value)
    {
        value ??= string.Empty;
        return new CharString(value.AsMemory());
    }

    public static implicit operator CharString(ReadOnlyMemory<char> chars) => new(chars);
}
