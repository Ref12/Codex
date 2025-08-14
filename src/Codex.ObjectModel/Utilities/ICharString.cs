namespace Codex.Utilities;

public interface ICharString<TSelf>
{
    ReadOnlyMemory<char> Chars { get; }

    TSelf WithChars(ReadOnlyMemory<char> chars);
}
