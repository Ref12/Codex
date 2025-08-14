using Codex.Sdk.Search;
using Codex.Utilities;
using Codex.View;
using System;
using System.Collections.Generic;
using System.Text;

namespace Codex.Web.Common
{
    public interface IViewModelController : IRepositoryIndexer
    {
        ViewModelDataContext ViewModel { get; }

        void ShowProjectExplorer(GetProjectResult projectResult);

        void ShowDocumentOutline(IBoundSourceFile sourceFile);

        void SetSearchInfo(string searchInfo);

        void OnSearchResponse(string searchString, IndexQueryHitsResponse<ISearchResult> response);

        void ShowRepositoriesSummary(IndexQueryHitsResponse<ICommit> response);

        Task DisplayOverview(OverviewKind mode);

        void OnReferencesResponse(IndexQueryResponse<ReferencesResult> response);

        void ShowRightPaneError(IndexQueryResponse response);

        void GoToSource(IBoundSourceFile sourceFile, TargetSpan? targetSpan);

        void UpdateNavigationBar();

        Task IRepositoryIndexer.IndexRepository(SourceControlUri[] repoUris) => IndexRepository(repoUris);

        new Task IndexRepository(SourceControlUri[] repoUris)
        {
            return Task.CompletedTask;
        }
    }

    public interface IRepositoryIndexer
    {
        Task IndexRepository(SourceControlUri[] repoUris);
    }

    public record struct TargetSpan(int? LineNumber, ILineSpan Span = default, SymbolIdArgument SymbolId = default)
    {
        public static implicit operator TargetSpan(int lineNumber)
        {
            return new TargetSpan(lineNumber);
        }

        public static implicit operator TargetSpan(string symbolId)
        {
            return new TargetSpan(null, SymbolId: symbolId);
        }

        public static implicit operator TargetSpan(SymbolId symbolId)
        {
            return new TargetSpan(null, SymbolId: symbolId);
        }

        public static TargetSpan From(ILineSpan span)
        {
            return new TargetSpan(span.LineNumber, span);
        }
    }
}
