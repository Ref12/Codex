using System.Diagnostics.CodeAnalysis;

namespace Codex.Utilities;

public readonly ref struct CharSpan(ReadOnlySpan<char> chars)
{
    public readonly ReadOnlySpan<char> Chars = chars;

    public override string ToString()
    {
        return Chars.ToString();
    }

    public int Length => Chars.Length;

    public bool Equals(CharSpan other)
    {
        return Chars.Equals(other.Chars, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        return string.GetHashCode(Chars);
    }

    public bool StartsWith(ReadOnlySpan<char> value) => Chars.StartsWith(value);

    public CharSpan WithChars(ReadOnlySpan<char> chars)
    {
        return new CharSpan(chars);
    }

    public static implicit operator CharSpan(string value)
    {
        value ??= string.Empty;
        return new CharSpan(value.AsSpan());
    }

    public static implicit operator CharSpan(ReadOnlySpan<char> chars) => new(chars);

    public static bool operator ==(CharSpan left, string? right)
    {
        return right != null && left.Chars.Equals(right, StringComparison.Ordinal);
    }

    public static bool operator !=(CharSpan left, string? right)
    {
        return !(left == right);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        throw new NotImplementedException();
    }
}
