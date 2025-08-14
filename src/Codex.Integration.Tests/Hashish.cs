using Codex.Utilities;

namespace Codex.Integration.Tests;

public record struct Hashish(ShortHash? Hash)
{
    public static implicit operator ShortHash?(Hashish value) => value.Hash;

    public static implicit operator Hashish(string value) => new(value == null ? null : ShortHash.Parse(value));
}
