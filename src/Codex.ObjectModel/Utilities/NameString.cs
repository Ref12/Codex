namespace Codex.Utilities.Serialization;

public record struct NameString(ReadOnlyMemory<char> Chars, bool CaseSensitive = false) : IEquatable<NameString>
{
    public override string ToString()
    {
        return Chars.ToString();
    }

    public int Length => Chars.Length;

    public bool Equals(NameString other)
    {
        return Chars.Span.Equals(other.Chars.Span,
            CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return string.GetHashCode(Chars.Span, 
            CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }

    public static implicit operator NameString(string value)
    {
        value ??= string.Empty;
        return new NameString(value.AsMemory(), true);
    }

    public static implicit operator NameString(ReadOnlyMemory<char> chars) => new(chars, true);
}

