using System;
using System.Web;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Search;
using Codex.Utilities;
using Codex.View;
using Codex.Web.Common;
using Codex.Web.Mvc.Rendering;

namespace Codex.Web.Wasm
{
    public class WebViewModelController : IViewModelController
    {
        public ViewModelDataContext ViewModel { get; } = new ViewModelDataContext();

        private PageResult _result = new PageResult();
        private AsyncLocal<PageResult> _contextualResult { get; } = new AsyncLocal<PageResult>();
        public PageResult Result => _contextualResult.Value ?? _result;
        public string LeftPaneHtml => Result.LeftPaneHtml;
        public string RightPaneHtml => Result.RightPaneHtml;

        public bool TrackSourceReferenceHtml { get; set; }

        public IRepositoryIndexer RepositoryIndexer { get; set; }

        public WebViewModelController(MainController app)
            : this()
        {
            app.Controller = this;
        }

        public WebViewModelController()
        {
            ViewModel.NavigationBarBinding.OnUpdate(_ => UpdateNavigationBar(), skipCurrentValue: true);
            ViewModel.LeftPaneBinding.OnUpdate(_ => RenderLeftPane(), skipCurrentValue: true);
            ViewModel.RightPaneBinding.OnUpdate(_ => RenderRightPane(), skipCurrentValue: true);
            TrackTargetSpan();
        }

        private void RenderRightPane()
        {
            if (TrackSourceReferenceHtml && ViewModel.RightPane?.SourceView is { } sourceView)
            {
                sourceView.ReferenceHtml ??= new();
            }

            var html = ViewModel.RightPane?.CreateView(new HtmlViewFactory()).ToString();
            SetRightPane(html);

            TrackTargetSpan();
        }

        private void TrackTargetSpan()
        {
            ViewModel.RightPane?.TargetSpan.OnUpdate(OnTargetSpanUpdated);
        }

        private void OnTargetSpanUpdated(TargetSpan? targetSpan)
        {
            if (targetSpan != null)
            {
                GoToLine(targetSpan.Value);
            }
        }

        private void RenderLeftPane()
        {
            var html = ViewModel.LeftPane?.CreateView(new HtmlViewFactory()).ToString();
            SetLeftPane(html);
        }

        public void SetContextualResult(PageResult result)
        {
            _contextualResult.Value = result;
        }

        public virtual string GetBaseUrl()
        {
            return "";
        }

        public void SetSearchInfo(string searchInfo)
        {
            ViewModel.SetSearchInfo(searchInfo);
        }

        public void OnSearchResponse(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            ViewModel.OnSearchResponse(searchString, response);
        }

        public async Task DisplayOverview(OverviewKind mode)
        {
            await ViewModel.DisplayOverview(mode);

            SetRightPane(null, mode.GetOverviewFileName());
        }

        public void OnReferencesResponse(IndexQueryResponse<ReferencesResult> response)
        {
            ViewModel.OnReferencesResponse(response);
        }

        public void ShowRightPaneError(IndexQueryResponse response)
        {
            ViewModel.ShowRightPaneError(response);
        }

        public void GoToSource(IBoundSourceFile sourceFile, TargetSpan? targetSpan)
        {
            var currentSource = ViewModel.RightPane?.SourceFile;
            if (currentSource?.ProjectId == sourceFile.ProjectId && currentSource?.ProjectRelativePath == sourceFile.ProjectRelativePath)
            {
                if (targetSpan != null)
                {
                    ViewModel.RightPane.TargetSpan.Value = targetSpan.Value;
                }

                return;
            }

            ViewModel.GoToSource(sourceFile, targetSpan);
        }

        public void ShowProjectExplorer(GetProjectResult projectResult)
        {
            ViewModel.ShowProjectExplorer(projectResult);
        }

        public void ShowDocumentOutline(IBoundSourceFile sourceFile)
        {
            ViewModel.ShowDocumentOutline(sourceFile);
        }

        public void UpdateState()
        {
            Result.SearchString = ViewModel.NavigationBar.Address.searchText;
            Result.PageState.RightSource = ViewModel.RightPane.SourceFile?.SourceFile?.Info;
            Result.PageState.IsInitialized = true;
        }

        public virtual void SetLeftPane(string html)
        {
            Result.LeftPaneHtml = html;
        }

        public virtual void SetRightPane(string html, string link = null)
        {
            Result.RightPaneHtml = html;
            Result.RightPaneHtmlLink = link;
        }

        public virtual void UpdateNavigationBar()
        {
            Result.Title = ViewModel.NavigationBar.Title;
            Result.PageState.Address = (GetBaseUrl() ?? "") + ViewModel.NavigationBar.Address.ToString();
        }

        public virtual void GoToLine(TargetSpan targetSpan)
        {
            Result.Line = targetSpan.LineNumber;
            Result.Symbol = targetSpan.SymbolId.Value.Value;
        }

        public void ShowRepositoriesSummary(IndexQueryHitsResponse<ICommit> response)
        {
            if (response.Error != null)
            {
                ShowRightPaneError(response);
                return;
            }

            var table = new DisplayTable<RepoSummaryField>();

            foreach (var hit in response.Result.Hits.OrderBy(h => h.RepositoryName))
            {
                table.NextRow();
                table[RepoSummaryField.Name] = hit.RepositoryName;
                table[RepoSummaryField.CommittedOn] = hit.DateCommitted.ToString("u");
                table[RepoSummaryField.IndexedOn] = hit.DateUploaded.ToString("u");
                table[RepoSummaryField.CommitId] = hit.CommitId;
            }

            var sw = new StringWriter();
            table.Write(sw);

            SetRightPane($"<pre>{HttpUtility.HtmlEncode(sw.ToString())}</pre>");
        }

        public virtual async Task IndexRepository(SourceControlUri[] repoUris)
        {
            string explanation = string.Empty;
            if (RepositoryIndexer != null)
            {
                try
                {
                    await RepositoryIndexer.IndexRepository(repoUris);
                    return;
                }
                catch (NotSupportedException ex)
                {
                    explanation = $": {ex.Message}";
                }
            }

            SetSearchInfo($"Indexing repositories not supported{explanation}.");
        }
    }
}
