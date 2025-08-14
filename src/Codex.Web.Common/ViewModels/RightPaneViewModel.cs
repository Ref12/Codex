using Codex.Sdk.Search;
using Codex.Utilities;
using Codex.Web.Common;
using Codex.Web.Mvc.Rendering;

namespace Codex.View
{
    public partial class RightPaneViewModel : PaneViewModelBase
    {
        public string Error { get; set; }

        public string Html { get; set; }

        public IBoundSourceFile SourceFile { get; }

        public OverviewKind? OverviewMode { get; set; }

        public SourceFileViewModel SourceView { get; set; }

        public IndexQueryHitsResponse<ICommit> ReposSummary { get; set; }

        public int? LineNumber => TargetSpan.Value?.LineNumber;

        public IReadOnlyList<ClassifiedTextSpan> Classifications => SourceView.Classifications;

        public IReadOnlyList<TextSpanSearchResultViewModel> References => SourceView.References;

        public Bound<TargetSpan?> TargetSpan { get; } = new Bound<TargetSpan?>();

        public RightPaneViewModel()
        {
        }

        public RightPaneViewModel(IndexQueryResponse<IBoundSourceFile> sourceFileResponse)
            : this(sourceFileResponse.Result)
        {
            Error = sourceFileResponse.Error;
        }

        public RightPaneViewModel(IndexQueryResponse response)
        {
            Error = response.Error;
        }

        public RightPaneViewModel(IBoundSourceFile sourceFile)
        {
            SourceFile = sourceFile;
            if (sourceFile != null && !sourceFile.Flags.HasFlag(BoundSourceFlags.FileInfoOnly))
            {
                SourceView = new SourceFileViewModel(sourceFile);
            }
        }

        public TView CreateView<TView>(ViewFactory<TView> factory)
        {
            return factory.Create(this);
        }
    }
}
