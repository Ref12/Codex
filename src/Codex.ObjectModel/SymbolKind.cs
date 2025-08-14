using System.Collections.Immutable;

namespace Codex.ObjectModel
{
    /// <summary>
    /// Defines standard set of reference kinds
    /// </summary>
    public enum SymbolKinds
    {
        Unknown,
        File,
        FileHash,
        TypescriptFile,
        MSBuildProperty,
        MSBuildItem,
        MSBuildItemMetadata,
        MSBuildTask,
        MSBuildTarget,
        MSBuildTaskParameter,
        Operator,
        Class,
        Struct,
        Interface,
        Enum,
        Delegate,
        Field,
        Method,
        Property,
        Event,
        Checksum_Sha1,
        Checksum_Sha256,
        Indexer,
        Constructor,
        Project,
        Repo,
        Namespace
    }

    public static class SymbolKindsExtensions
    {
        public static ImmutableArray<SymbolKinds> TypeKinds => Enum.GetValues<SymbolKinds>().Where(s => s.IsTypeKind()).ToImmutableArray();

        public static bool IsTypeKind(this SymbolKinds kind)
        {
            switch (kind)
            {
                case SymbolKinds.Class:
                case SymbolKinds.Struct:
                case SymbolKinds.Interface:
                case SymbolKinds.Enum:
                case SymbolKinds.Delegate:
                    return true;
                default:
                    return false;
            }
        }
    }
}
