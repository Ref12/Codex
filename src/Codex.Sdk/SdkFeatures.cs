using Codex.Logging;
using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using Codex.Storage;
using Codex.Storage.BlockLevel;
using Codex.Utilities.Zip;
using Codex.Web.Common;

namespace Codex
{
    /// <summary>
    /// Defines on/off state of experimental features
    /// </summary>
    public static class SdkFeatures
    {
        public static readonly FeatureSwitch<Parsed<ShortHash>?> DebugEntityHash = new();

        public static readonly FeatureSwitch<Parsed<ShortHash>?> DebugAddressEntryHash = new();

        public static readonly FeatureSwitch<int> WebProgramCacheLimit = 100;

        public static readonly FeatureSwitch<int?> DebugEntityStableId = new();

        public static readonly FeatureSwitch<bool> CheckBlobReassignment = false;

        public static readonly FeatureSwitch<int?> IngestParallelism = default(int?);

        public static readonly FeatureSwitch<Func<string, bool>> CanReadFilter = new();

        // For unit testing purposes only to capture the http client used to query index files
        public static readonly FeatureSwitch<AsyncOut<IBytesRetriever>?> IndexRetrieverTestHook = new();

        public static readonly FeatureSwitch<Func<HttpClientKind, HttpResponseMessage, HttpResponseMessage?>?> IndexClientResponsePreprocessor = new(); 

        public static IHttpClient HttpClient { get; set; } = new HttpClientWrapper();

        public static Func<HttpClientKind, IInnerHttpClient> GetClient { get; set; }

        public static Func<Uri, Uri>? ProcessIndexAddress { get; set; }

        public static readonly FeatureSwitch<Logger> AmbientLogger = new();

        public static readonly FeatureSwitch<Func<ICodexStore, ICodexStore>> WrapIngestStore = new();

        public static readonly FeatureSwitch<Logger> TestLogger = new();

        public static readonly FeatureSwitch<Logger> GlobalLogger = new();

        public static Logger? GetGlobalLogger() => GlobalLogger.Value ?? AmbientLogger.Value;

        public static readonly FeatureSwitch<Func<IProjectFileScopeEntity, bool>> AmbientFileAnalysisFilter = new(file => true);

        public static readonly FeatureSwitch<Func<IProjectFileScopeEntity, bool>> AmbientFileIndexFilter = new(file => true);

        public static readonly FeatureSwitch<Action<DefinitionSearchModel>> AfterDefinitionAddHandler = new();

        public static readonly FeatureSwitch<Action<IStableIdStorage, ISearchEntity>> OnRequiredEntityHandler = new();

        public static readonly FeatureSwitch<string> DefaultZipStorePasswordPublicKey = MiscUtilities.GetEnvironmentVariableOrDefault(CodexConstants.ZipPasswordPublicKeyEnvVarName);
        public static readonly FeatureSwitch<string> DefaultZipStorePasswordPrivateKey = MiscUtilities.GetEnvironmentVariableOrDefault(CodexConstants.ZipPasswordPrivateKeyEnvVarName);

        public static readonly FeatureSwitch<string> DefaultZipStorePassword = MiscUtilities.GetEnvironmentVariableOrDefault(CodexConstants.ZipPasswordEnvVarName);
    }
}
