namespace Codex.Storage.BlockLevel;

public enum AddressKind : byte
{
    Default = 0,
    Content,
    InfoHeader,
    Definitions,
    References,
    TopLevelDefinitions,
}