namespace Codex.Automation.Workflow
{
    /// <summary>
    /// Defines the mode for the automation workflow
    /// Maybe this is in the SDK for use by AutoIndexing?
    /// </summary>
    public enum Mode
    {
        Prepare = 1 << 0,
        UploadOnly = 1 << 1,
        //IngestOnly = 1 << 2,
        AnalyzeOnly = 1 << 3,
        BuildOnly = 1 << 4,
        GC = 1 << 5 | Prepare,
        IndexOnly = 1 << 6,
        Codex = 1 << 7,
        Test = 1 << 8,

        FullAnalyze = Prepare | AnalyzeOnly | UploadOnly,
        Ingest = Prepare | IndexOnly,
        Upload = Prepare | UploadOnly,
        Build = Prepare | BuildOnly,
        Index = Prepare | IndexOnly,

        IndexAndUpload = Index | UploadOnly,

        BuildAndAnalyze = Build | AnalyzeOnly | UploadOnly,

        TestBuildAndAnalyze = Build | AnalyzeOnly | UploadOnly | Test,

        FullIndexNoUpload = Prepare | BuildOnly | AnalyzeOnly | IndexOnly,

        FullIndex = FullIndexNoUpload | Upload,

        GetLocation,
        Cli
    }
}
