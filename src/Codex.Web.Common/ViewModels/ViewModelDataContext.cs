using Codex.ObjectModel.Implementation;
using Codex.Sdk.Search;
using Codex.Storage.BlockLevel;
using Codex.Utilities;
using Codex.Web.Common;
using Codex.Web.Mvc.Rendering;

namespace Codex.View
{
    public partial class ViewModelDataContext
        : IViewModelController
    {
        public NavigationBarViewModel NavigationBar { get => NavigationBarBinding.Value; set => NavigationBarBinding.Value = value; }
        public Bound<NavigationBarViewModel> NavigationBarBinding { get; } = new();

        public Bound<LeftPaneViewModel> LeftPaneBinding { get; } = new Bound<LeftPaneViewModel>();
        public LeftPaneViewModel LeftPane { get => LeftPaneBinding.Value; set => LeftPaneBinding.Value = value; }

        public Bound<RightPaneViewModel> RightPaneBinding { get; } = new Bound<RightPaneViewModel>();
        public RightPaneViewModel RightPane { get => RightPaneBinding.Value; set => RightPaneBinding.Value = value; }

        public ViewModelDataContext()
        {
            LeftPane = LeftPaneViewModel.Initial;
            RightPane = new RightPaneViewModel();
            NavigationBar = new NavigationBarViewModel(new ViewModelAddress(), "ReF12");
        }

        ViewModelDataContext IViewModelController.ViewModel => this;

        public virtual Task DisplayOverview(OverviewKind mode)
        {
            RightPane = new RightPaneViewModel() { OverviewMode = mode };
            return Task.CompletedTask;
        }

        public virtual void ShowRightPaneError(IndexQueryResponse response)
        {
            RightPane = new RightPaneViewModel(response);
        }

        public virtual void OnReferencesResponse(IndexQueryResponse<ReferencesResult> response)
        {
            LeftPane = LeftPaneViewModel.FromReferencesResponse(response);

            if (response.Result?.Arguments is { } arguments)
            {
                NavigationBar = NavigationBar with
                {
                    Address = NavigationBar.Address.With(ViewModelAddress.FindAllReferences(
                        projectId: arguments.ProjectId,
                        symbolId: arguments.SymbolId,
                        projectScope: arguments.ProjectScopeId,
                        refKind: arguments.ReferenceKind))
                };
            }
        }

        public virtual void OnSearchResponse(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            LeftPane = LeftPaneViewModel.FromSearchResponse(searchString, response);
            NavigationBar = NavigationBar with
            {
                Address = NavigationBar.Address.With(ViewModelAddress.Search(searchString)),
                Title = searchString
            };
        }

        public virtual void SetSearchInfo(string searchInfo)
        {
            LeftPane = new LeftPaneViewModel()
            {
                SearchInfo = searchInfo
            };
        }

        public virtual void GoToSource(IBoundSourceFile sourceFile, TargetSpan? targetSpan)
        {
            RightPane = new RightPaneViewModel(sourceFile);
            RightPane.TargetSpan.Value = targetSpan;

            NavigationBar = NavigationBar with
            {
                Address = NavigationBar.Address.With(ViewModelAddress.GoToSpan(sourceFile?.ProjectId, sourceFile?.ProjectRelativePath, targetSpan: targetSpan)),
                Title = GetFileTitle(sourceFile.SourceFile.Info) ?? NavigationBar.Title
            };
        }

        public void ApplyAddress(ViewModelAddress address)
        {
            NavigationBar = NavigationBar with
            {
                Address = NavigationBar.Address.With(address)
            };
        }

        private string GetFileTitle(IProjectFileScopeEntity sourceFile)
        {
            if (sourceFile?.ProjectRelativePath is string projectRelativePath)
            {
                return $"{PathUtilities.GetFileName(projectRelativePath)} ({sourceFile.ProjectId})";
            }

            return null;
        }

        public void ShowProjectExplorer(GetProjectResult projectResult)
        {
            if (projectResult.GenerateReferenceMetadata)
            {
                if (projectResult.AddressKind == AddressKind.References)
                {
                    return;
                }
                else if (projectResult.AddressKind == AddressKind.Definitions)
                {
                    return;
                }
            }

            LeftPane = new LeftPaneViewModel()
            {
                Content = projectResult.AddressKind == AddressKind.TopLevelDefinitions
                    ? TreeViewRenderer.GenerateNamespaceExplorer(projectResult.Project.Definitions.AsList())
                    : new ProjectExplorerRenderer(projectResult).GenerateViewModel()
            };
        }

        public void UpdateNavigationBar()
        {
        }

        public void ShowDocumentOutline(IBoundSourceFile sourceFile)
        {
            LeftPane = new LeftPaneViewModel()
            {
                Content = TreeViewRenderer.GenerateDocumentOutline(sourceFile)
            };
        }

        public void ShowRepositoriesSummary(IndexQueryHitsResponse<ICommit> response)
        {
            RightPane = new RightPaneViewModel()
            {
                ReposSummary = response
            };
        }
    }
}
