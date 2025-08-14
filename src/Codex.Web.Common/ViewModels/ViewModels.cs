using System.ComponentModel;
using System.Runtime.CompilerServices;
using Codex.Sdk.Search;
using Codex.Utilities;
using Codex.Web.Common;

namespace Codex.View
{
    public record struct TextSpanSearchResult(IProjectFileScopeEntity SearchResult, ITextLineSpan Span, IReferenceSearchResult RefResult = null);

    public partial class TextSpanSearchResultViewModel : FileItemResultViewModel, INavigateItem
    {
        public TextSpanSearchResult Model { get; }
        public IProjectFileScopeEntity SearchResult => Model.SearchResult;
        public int ReferenceCount { get; set; }

        public string DebugPrefix => Model.Span is IReferenceSpan span
            ? $"[{span.Reference.ReferenceKind}]"
            : "";

        public string ProjectId => SearchResult.ProjectId;

        public int LineNumber => Model.Span.LineNumber;
        public string LineNumberText { get; }

        public ViewModelAddress NavigateAddress => ViewModelAddress.GoToSpan(SearchResult.ProjectId, SearchResult.ProjectRelativePath, targetSpan: new TargetSpan(LineNumber, SymbolId: GetSymbolId()));

        public IReadOnlyList<RichText> Spans { get; set; }

        private SymbolIdArgument GetSymbolId()
        {
            SymbolIdArgument symbolId = null;
            if (Model.Span is IReferenceSpan span)
            {
                if (span.Reference.ReferenceKind == ReferenceKind.Definition)
                {
                    symbolId = span.Reference.Id;
                }
            }

            return symbolId;
        }

        public TextSpanSearchResultViewModel(TextSpanSearchResult result, IEnumerable<RichText> spans, int lineNumberWidth)
        {
            Model = result;
            Spans = spans.ToList();
            LineNumberText = Model.Span.LineNumber.ToString().PadLeft(lineNumberWidth);
        }

        public TextSpanSearchResultViewModel(IReferenceSearchResult result)
            : this(new TextSpanSearchResult(result.File, result.ReferenceSpan, result), result.ReferenceSpan.GetRichTextSpans(), 0)
        {
        }

        public override string ToString()
        {
            return $"{string.Concat(Spans)} ({NavigateAddress})";
        }

        public override TView CreateView<TView>(ViewFactory<TView> factory)
        {
            return factory.Create(this);
        }
    }

    public abstract class ProjectItemResultViewModel : INavigateTreeNode
    {
        public abstract TView CreateView<TView>(ViewFactory<TView> factory);

        public abstract IEnumerable<INavigateItem> GetItems();

        public SymbolResultViewModel AsSymbolResult => (SymbolResultViewModel)this;

        public FileResultsViewModel AsFileResults => (FileResultsViewModel)this;
    }

    public abstract class FileItemResultViewModel
    {
        public abstract TView CreateView<TView>(ViewFactory<TView> factory);
    }

    public interface INavigateItem
    {
        ViewModelAddress NavigateAddress { get; }

        string ProjectId => NavigateAddress.rightProjectId ?? NavigateAddress.leftProjectId;
    }

    public static class NavigateTreeExtensions
    {
        public static SymbolResultViewModel AsSymbol(this INavigateItem item)
        {
            return item.As<SymbolResultViewModel>();
        }

        public static TextSpanSearchResultViewModel AsText(this INavigateItem item)
        {
            return item.As<TextSpanSearchResultViewModel>();
        }

        public static TItem As<TItem>(this INavigateItem item)
            where TItem : INavigateItem
        {
            return (TItem)item;
        }
    }

    public interface INavigateTreeNode
    {
        IEnumerable<INavigateItem> GetItems();
    }

    public partial class FileResultsViewModel : ProjectItemResultViewModel, IResultsStats, INavigateTreeNode
    {
        public Counter Counter { get; set; } = new Counter();
        public string Path { get; set; }
        public IReadOnlyList<TextSpanSearchResultViewModel> Items { get; set; }

        public string Glyph => GlyphUtilities.GetFileNameGlyph(Path);

        public override TView CreateView<TView>(ViewFactory<TView> factory)
        {
            return factory.Create(this);
        }

        public override IEnumerable<INavigateItem> GetItems()
        {
            return Items;
        }
    }

    public partial class SymbolResultViewModel : ProjectItemResultViewModel, INavigateItem
    {
        public IDefinitionSymbol Symbol { get; }
        public string ShortName { get; set; }
        public string DisplayName { get; set; }
        public string SymbolKind { get; set; }
        public string ProjectId { get; set; }

        public string GlyphIcon => Symbol.GetGlyph();

        public string ImageMoniker { get; set; }
        public int SortOrder { get; set; }
        public int IdentifierLength => ShortName.Length;

        public ViewModelAddress NavigateAddress => ViewModelAddress.GoToDefinition(Symbol);

        public ViewModelAddress FindAllReferences => ViewModelAddress.FindAllReferences(ProjectId, Symbol.Id.Value);

        public SymbolResultViewModel(IDefinitionSymbol symbol)
        {
            Symbol = symbol;
            ShortName = symbol.ShortName;
            DisplayName = symbol.DisplayName ?? GetDisplayName(symbol);
            ProjectId = symbol.ProjectId;
            SymbolKind = symbol.Kind.ToDisplayString();
        }

        private string GetDisplayName(IDefinitionSymbol symbol)
        {
            if (symbol.Kind == SymbolKinds.File && symbol.ShortName != null && symbol.ContainerQualifiedName != null)
            {
                return PathUtilities.UriCombine(symbol.ContainerQualifiedName, symbol.ShortName).AsUrlRelativePath(encode: false);
            }

            return symbol.ShortName;
        }

        public override TView CreateView<TView>(ViewFactory<TView> factory)
        {
            return factory.Create(this);
        }

        public override IEnumerable<INavigateItem> GetItems()
        {
            yield return this;
        }

        public override string ToString()
        {
            return $"({ShortName}\\{ProjectId}) [{NavigateAddress}] {SymbolKind} ({DisplayName})";
        }
    }

    public partial class ProjectGroupResultsViewModel : IResultsStats, INavigateTreeNode
    {
        public Counter Counter { get; set; } = new Counter();

        public string ProjectName { get; set; }
        public string RepositoryName { get; set; }
        public IReadOnlyList<ProjectItemResultViewModel> Items { get; set; }

        public IEnumerable<INavigateItem> GetItems()
        {
            return Items.SelectMany(i => i.GetItems());
        }
    }

    public partial class ProjectResultsViewModel : LeftPaneContent, IResultsStats
    {
        public Counter Counter { get; } = new Counter();

        public List<ProjectGroupResultsViewModel> ProjectGroups { get; set; }

        public ProjectResultsViewModel()
        {
            ProjectGroups = new List<ProjectGroupResultsViewModel>();
        }

        public ProjectResultsViewModel(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            ProjectGroups = response.Result.Hits.Select(sr => sr.Definition).OrderByRelevance(searchString).GroupBy(sr => sr.ProjectId).Select(projectGroup =>
            {
                var projectCounter = new Counter();
                return new ProjectGroupResultsViewModel()
                {
                    ProjectName = projectGroup.Key,
                    Items = projectGroup.Select(symbol => new SymbolResultViewModel(symbol).Increment(projectCounter)).ToList()
                }
                .AddFrom(projectCounter)
                .AddTo(Counter);
            }).ToList();
        }

        public override TView CreateView<TView>(ViewFactory<TView> factory)
        {
            return factory.Create(this);
        }

        public override IEnumerable<INavigateItem> GetItems()
        {
            return ProjectGroups.SelectMany(g => g.GetItems());
        }
    }

    public partial class CategoryGroupSearchResultsViewModel : IResultsStats, INavigateTreeNode
    {
        public Counter Counter { get; } = new Counter();

        //public Visibility HeaderVisibility => string.IsNullOrEmpty(Header) ? Visibility.Collapsed : Visibility.Visible;

        public string Header { get; }

        public List<SymbolResultViewModel> RelatedDefinitions { get; set; } = new();

        public ProjectResultsViewModel ProjectResults { get; set; } = new ProjectResultsViewModel();

        public CategoryGroupSearchResultsViewModel(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            var result = response.Result;

            PopulateProjectGroups(result.Hits.Select(sr => sr.TextLine), sr => new TextSpanSearchResult(sr.File, sr.TextSpan));
            Header = $"{result.Hits.Count} text search hits for '{searchString}'";
        }

        public CategoryGroupSearchResultsViewModel(ReferenceKind kind, string symbolName, IEnumerable<IReferenceSearchResult> references)
        {
            PopulateProjectGroups(references, sr => new TextSpanSearchResult(sr.File, sr.ReferenceSpan, sr));
            Header = ViewUtilities.GetReferencesHeader(kind, references.Count(), symbolName);
        }

        public CategoryGroupSearchResultsViewModel(string header, IEnumerable<IDefinitionSymbol> definitions)
        {
            Header = header;
            RelatedDefinitions.AddRange(definitions.Select(d => new SymbolResultViewModel(d)));
        }

        private void PopulateProjectGroups<T>(IEnumerable<T> items, Func<T, TextSpanSearchResult> modelFactory) where T : IFileSpanResult
        {
            ProjectResults.ProjectGroups.AddRange(items.GroupBy(sr => (sr.File.ProjectId, sr.File.RepositoryName)).Select(projectGroup => new ProjectGroupResultsViewModel()
            {
                Counter = Out.Var(out var projectCounter, Counter.CreateChild()),
                ProjectName = projectGroup.Key.ProjectId,
                // TODO: Show this in UI. Maybe a chip/tag element after the counter?
                RepositoryName = projectGroup.Key.RepositoryName,
                Items = projectGroup.GroupBy(sr => sr.File.ProjectRelativePath).Select(fileGroup => new FileResultsViewModel()
                {
                    Counter = projectCounter.CreateChild(),
                    Path = fileGroup.Key,
                    Items = ViewUtilities.ToMergedViews(fileGroup.Select(sr => modelFactory(sr))).ToList()
                }.Add(f => f.Items.Sum(i => i.ReferenceCount))).ToList()
            })); ;
        }

        public IEnumerable<INavigateItem> GetItems()
        {
            return RelatedDefinitions.Concat(ProjectResults.GetItems());
        }
    }

    public class TreeViewModel<T> : TreeViewModel
    {
        public TreeViewModel(T target, LeftPaneMode mode)
            : base(mode)
        {
            Target = target;
        }

        public T Target { get; }
    }

    public partial class TreeViewModel : LeftPaneContent
    {
        public LeftPaneMode Mode { get; }

        public TreeNodeViewModel Root { get; } = new TreeNodeViewModel();

        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        public TreeViewModel(LeftPaneMode mode)
        {
            Mode = mode;
        }

        public override TView CreateView<TView>(ViewFactory<TView> factory)
        {
            return factory.Create(this);
        }

        public override IEnumerable<INavigateItem> GetItems()
        {
            foreach (var item in Root.GetItems())
            {
                yield return item;
            }
        }

        public override string ToString()
        {
            return Mode == LeftPaneMode.outline ? "Document Outline" : Mode.ToString();
        }
    }

    public partial class TreeNodeViewModel : INavigateTreeNode, INavigateItem
    {
        public IDefinitionSymbol Definition { get; }

        public IProjectFileScopeEntity File { get; }

        public IProjectScopeEntity ReferencedProject { get; }

        public ViewModelAddress NavigateAddress { get; init; }

        public string Name { get; set; }

        public Glyph Glyph { set => Icon = value.GetGlyphIcon(); }

        public string Icon { get; set; }

        public string Kind { get; set; }

        public int SortGroup { get; set; }

        public bool Expanded { get; set; } = false;

        private List<TreeNodeViewModel> _children;

        public List<TreeNodeViewModel> Children => _children ??= new();

        public TreeNodeViewModel(IDefinitionSymbol definition, IProjectFileScopeEntity? file = null)
        {
            Definition = definition;
            NavigateAddress = file != null
                ? ViewModelAddress.GoToSpan(file.ProjectId, file.ProjectRelativePath, targetSpan: definition.Id)
                : ViewModelAddress.GoToDefinition(definition);
            Icon = definition.GetGlyph(file?.ProjectRelativePath);
            Kind = definition.Kind.ToDisplayString();
        }

        public TreeNodeViewModel(string projectId, IProjectFileScopeEntity file)
        {
            File = file;
            Name = PathUtilities.GetFileName(file.ProjectRelativePath);
            NavigateAddress = ViewModelAddress.GoToFile(projectId, file.ProjectRelativePath);
            Icon = GlyphUtilities.GetFileNameGlyph(file.ProjectRelativePath);
        }

        public TreeNodeViewModel(IProjectScopeEntity project)
        {
            ReferencedProject = project;
            Name = project.ProjectId;
            NavigateAddress = ViewModelAddress.ShowProjectExplorer(project.ProjectId);
            Icon = GlyphUtilities.GetGlyphIcon(Glyph.Assembly);
        }

        public override string ToString()
        {
            return $"#{Icon} ({Kind}) {Name} [{NavigateAddress}]";
        }

        public TreeNodeViewModel(string name, Glyph glyph)
        {
            Name = name;
            Icon = glyph.GetGlyphIcon();
        }

        public TreeNodeViewModel()
        {
        }

        public void Sort(Comparison<TreeNodeViewModel> compare)
        {
            SortCore((x, y) =>
            {
                return x.SortGroup.ChainCompareTo(y.SortGroup) ?? compare(x, y);
            });
        }

        protected void SortCore(Comparison<TreeNodeViewModel> compare)
        {
            Children.Sort(compare);

            foreach (var child in Children)
            {
                child.SortCore(compare);
            }
        }

        public IEnumerable<INavigateItem> GetItems()
        {
            if (NavigateAddress != null)
            {
                yield return this;
            }

            foreach (var child in Children)
            {
                foreach (var item in child.GetItems())
                {
                    yield return item;
                }
            }
        }

        public TreeNodeViewModel GetOrCreateFolder(string folderName, Glyph icon = Glyph.ClosedFolder)
        {
            return Children.FirstOrDefault(c => StringComparer.OrdinalIgnoreCase.Equals(c.Name, folderName)) 
                ?? CreateFolder(folderName, icon);
        }

        public TreeNodeViewModel CreateFolder(string folderName, Glyph icon, int? index = null)
        {
            var folder = new TreeNodeViewModel(folderName, icon);

            if (index != null) Children.Insert(index.Value, folder);
            else Children.Add(folder);

            return folder;
        }
    }

    public partial class CategorizedSearchResultsViewModel : LeftPaneContent, IResultsStats
    {
        public List<CategoryGroupSearchResultsViewModel> Categories { get; }

        public Counter Counter { get; } = new Counter();
        public IIndexQueryHits Results { get; }
        public string SearchString { get; }
        public int ResultCount => Results.HitCount;

        public CategorizedSearchResultsViewModel(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            SearchString = searchString;
            Results = response.Result;
            Categories = new List<CategoryGroupSearchResultsViewModel>()
            {
                new CategoryGroupSearchResultsViewModel(searchString, response).AddTo(Counter)
            };
        }

        public CategorizedSearchResultsViewModel(string symbolName, ReferencesResult result)
        {
            Results = result;
            Categories = new();


            Categories.AddRange(result.RelatedDefinitions.Where(r => r.ReferenceKind == ReferenceKind.Override).Take(1)
                .Select(baseDef =>
                {
                    return new CategoryGroupSearchResultsViewModel("Base", new[] { baseDef.Symbol });
                }));

            Categories.AddRange(result.RelatedDefinitions.Where(r => r.ReferenceKind == ReferenceKind.InterfaceMemberImplementation).GroupBy(g => 0)
                .Select(implementedMembers =>
                {
                    return new CategoryGroupSearchResultsViewModel("Implemented interface members", implementedMembers.Select(r => r.Symbol));
                }));

            Categories.AddRange(result.Hits.OrderBy(r => r, ViewUtilities.LeftPaneReferencesSorter).GroupBy(r => r.ReferenceSpan.Reference.ReferenceKind).Select(referenceGroup =>
            {
                return new CategoryGroupSearchResultsViewModel(referenceGroup.Key, symbolName, referenceGroup).AddTo(Counter);
            }));
        }

        public override TView CreateView<TView>(ViewFactory<TView> factory)
        {
            return factory.Create(this);
        }

        public override IEnumerable<INavigateItem> GetItems()
        {
            return Categories.SelectMany(c => c.GetItems());
        }
    }

    public abstract partial class LeftPaneContent : INavigateTreeNode
    {
        public abstract TView CreateView<TView>(ViewFactory<TView> factory);

        public abstract IEnumerable<INavigateItem> GetItems();

        public CategorizedSearchResultsViewModel AsCategorized => (CategorizedSearchResultsViewModel)this;

        public ProjectResultsViewModel AsProjects => (ProjectResultsViewModel)this;

        public TreeViewModel AsTree => (TreeViewModel)this;

        public List<INavigateItem> ItemList => GetItems().ToList();

        public List<SymbolResultViewModel> SearchResults => GetItems().OfType<SymbolResultViewModel>().ToList();
    }

    public record NavigationBarViewModel(ViewModelAddress Address, string Title);

    public partial class LeftPaneViewModel : PaneViewModelBase
    {
        public ViewModelAddress SearchInfoAddress { get; set; }

        public string SearchInfo { get => SearchInfoBinding.Value; set => SearchInfoBinding.Value = value; }

        public Bound<string> SearchInfoBinding { get; } = new Bound<string>();

        public LeftPaneContent Content { get => ContentBinding.Value; set => ContentBinding.Value = value; }

        public Bound<LeftPaneContent> ContentBinding { get; set; } = new Bound<LeftPaneContent>();

        public static readonly LeftPaneViewModel Initial = new LeftPaneViewModel()
        {
            SearchInfo = "Enter a search string. Start with ` for full text search results only."
        };

        public TView CreateView<TView>(ViewFactory<TView> factory)
        {
            return factory.Create(this);
        }

        public static LeftPaneViewModel FromReferencesResponse(IndexQueryResponse<ReferencesResult> response)
        {
            if (response.Error != null)
            {
                return new LeftPaneViewModel()
                {
                    SearchInfo = response.Error
                };
            }
            else if (response.Result?.Hits == null || response.Result.Hits.Count == 0)
            {
                return new LeftPaneViewModel()
                {
                    SearchInfo = $"No references found."
                };
            }

            var result = response.Result;
            var symbolDisplayName = response.Result.SymbolDisplayName ?? response.Result.Hits[0].ReferenceSpan.Reference.Id.Value;
            return new LeftPaneViewModel()
            {
                Content = new CategorizedSearchResultsViewModel(
                    symbolDisplayName,
                    response.Result),
                SearchInfo = result.Arguments?.IsFallback == true ? $"Could not find definition location for '{symbolDisplayName}'. Showing references." : null
            };
        }

        public static LeftPaneViewModel FromSearchResponse(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            if (response.Result?.Hits.IsNullOrEmpty() != false)
            {
                return new LeftPaneViewModel()
                {
                    SearchInfo = response.Error ?? "No results found."
                };
            }

            var result = response.Result;
            bool isDefinitionsResult = result.Hits[0].Definition != null;
            return new LeftPaneViewModel()
            {
                Content = isDefinitionsResult ?
                    (LeftPaneContent)new ProjectResultsViewModel(searchString, response) :
                    new CategorizedSearchResultsViewModel(searchString, response),
                SearchInfo = isDefinitionsResult ?
                    (result.Hits.Count < result.Total ?
                        $"Displaying top {result.Hits.Count} results out of {result.Total}:" :
                        $"{result.Hits.Count} results found:")
                    : string.Empty,
                SearchInfoAddress = ViewModelAddress.Search(searchString)
            };
        }
    }

    public class BindableValue<T> : NotifyPropertyChangedBase
    {
        private T value;

        public T Value
        {
            get
            {
                return value;
            }

            set
            {
                this.value = value;
                OnPropertyChanged();
            }
        }
    }


    public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        protected void OnPropertyChanged([CallerMemberName] string memberName = null)
        {
            propertyChanged?.Invoke(this, new PropertyChangedEventArgs(memberName));
        }

        private event PropertyChangedEventHandler propertyChanged;
        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                propertyChanged += value;
            }

            remove
            {
                propertyChanged -= value;
            }
        }
    }

    public class PaneViewModelBase : NotifyPropertyChangedBase
    {
        public ViewModelDataContext DataContext { get; set; }

        public PaneViewModelBase()
        {
            Initialize();
        }

        protected virtual void Initialize() { }
    }

    public interface IResultsStats
    {
        Counter Counter { get; }
    }

    public class Counter
    {
        public Counter Parent;
        public int Count;

        public Counter CreateChild()
        {
            return new Counter() { Parent = this };
        }

        public void Increment()
        {
            Count++;
            Parent?.Increment();
        }

        public void Add(int value)
        {
            Count += value;
            Parent?.Add(value);
        }

        public override string ToString()
        {
            return Count.ToString();
        }
    }
}
