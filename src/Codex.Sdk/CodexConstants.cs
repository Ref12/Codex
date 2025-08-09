using Codex.Sdk.Search;

namespace Codex;

public static class CodexConstants
{
    public const int BytesInMb = 1 << 20;

    public static readonly OneOrMany<string>[] DefinitionKindByRank = new OneOrMany<string>[]
    {
        new[] { "class", "struct", "record", "interface" },
        "file",
        "property",
        "method",
        "field"
    };

    public const string IndexZipBlobName = "index.zip";

    public const string RelativeTempDirectory = ".tmp";

    public const string ObjectDirectoryName = "objects";

    public const string IndicesDirectoryName = "indices";

    public const string BuildIndexArtifactName = BuildTags.CodexIndexOutputs;

    public const string BuildAnalysisArtifactName = BuildTags.CodexOutputs;

    public const string BinLogArtifactName = "CodexBinLogOutputs";

    public const string DebugArtifactName = "CodexDebugOutputs";

    public const string ZipPasswordEnvVarName = "Codex_ZipPassword";
    public const string ZipEncryptedPasswordCommentPrefix = "ZIP_ENC_PASS=[[";
    public const string ZipPasswordPublicKeyEnvVarName = "Codex_ZipPassword_Public";
    public const string ZipPasswordPrivateKeyEnvVarName = "Codex_ZipPassword_Private";

    public const string ReferencedProjectsXmlFileName = "ReferenceProjects.xml";

    public static string GetIndicesDirectory(string rootDirectory)
    {
        return Path.Combine(rootDirectory, IndicesDirectoryName);
    }

    public static string GetProjectReferenceSymbolsPath(string referencedProjectId)
    {
        return $@"ReferenceSymbols\{referencedProjectId}.xml";
    }

    public static string GetMetadataFilePath(string fileName)
    {
        return $@"[Metadata]\{fileName}";
    }

    public static class BuildTags
    {
        public const string CodexEnabled = "CodexEnabled";

        public const string CodexIndexEnabled = "CodexIndexEnabled";

        public const string CodexOutputs = "CodexOutputs";

        public const string CodexIndexOutputs = "CodexIndexOutputs";

        public const string FormatVersion = "codexversion=2";
    }

    public class Boosts
    {
        public const float ShortNameBoost = 2;
        public const float DefaultExplicit = 10;
    }

    public static class Terms
    {
        public const string IncludeReferencedDefinitions = "@all";
        public const string IncludeExtensionMembers = "@ext";
        public const string AllRepoSearch = "@allrepos";
    }
}