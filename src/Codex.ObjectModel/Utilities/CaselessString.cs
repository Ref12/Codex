namespace Codex.Utilities;

public record struct CaselessString(ReadOnlyMemory<char> Chars) : IEquatable<CaselessString>, ICharString<CaselessString>
{
    public override string ToString()
    {
        return Chars.ToString();
    }

    public int Length => Chars.Length;

    public ReadOnlySpan<char> Span => Chars.Span;

    public bool Equals(CaselessString other)
    {
        return Chars.Span.Equals(other.Chars.Span, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        return string.GetHashCode(Chars.Span, StringComparison.Ordinal);
    }

    public CaselessString WithChars(ReadOnlyMemory<char> chars)
    {
        return new CaselessString(chars);
    }

    public static implicit operator CaselessString(string value)
    {
        value ??= string.Empty;
        return new CaselessString(value.AsMemory());
    }

    public static implicit operator CaselessString(ReadOnlyMemory<char> chars) => new(chars);
}