namespace Codex.Analysis.Managed
{
    public class ManagedAnalysisHost
    {
        public static readonly ManagedAnalysisHost Default = new ManagedAnalysisHost();

        public static ManagedAnalysisHost Instance { get; set; } = Default;

        public virtual bool IncludeDocument(string projectId, string documentPath)
        {
            return true;
        }

        public virtual void OnDocumentFinished(IBoundSourceFile boundSourceFile)
        {
        }
    }
}
