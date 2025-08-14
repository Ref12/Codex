using Codex.Lucene.Search;
using Codex.Sdk.Search;
using Codex.Storage;
using Codex.Utilities;
using Codex.Web.Mvc.Rendering;
using Codex.Web.Wasm;

namespace Codex.View
{
    public record CodexPage(ICodex Codex, MainController App, ViewModelDataContext View)
    {
        public CodexPage(ICodex codex)
            : this(codex, new MainController() { CodexService = codex })
        {
        }

        public CodexPage(ICodex codex, MainController app)
            : this(codex, app, app.Controller.ViewModel)
        {
        }

        public async Task<INavigateItem> SelectLeftItem(int index)
        {
            var item = View.LeftPane.Content.GetItems().ElementAt(index);
            await item.NavigateAddress.NavigateAsync(App);
            return item;
        }

        public IStableIdStorage StableIdStorage { get; set; }

        public Task Search(string text) => App.SearchTextChanged(text);

        public IReadOnlyList<INavigateItem> LeftItems => View.LeftPane.Content?.ItemList ?? (IReadOnlyList<INavigateItem>)Array.Empty<INavigateItem>();

        public SourceFileViewModel RightSource => View.RightPane.SourceView;

        public NavigationBarViewModel NavBar => View.NavigationBar;

        public ViewModelAddress Address => View.NavigationBar.Address;

        public LuceneCodex LuceneCodex => (LuceneCodex)Codex;

        public ReloadableCodex ReloadableCodex => (ReloadableCodex)Codex;

        public Task NavigateTo(ViewModelAddress address) => address.NavigateAsync(App);

        public void Deconstruct(out ICodex codex, out MainController app, out ViewModelDataContext view, out CodexPage page)
        {
            Deconstruct(out codex, out app, out view);
            page = this;
        }

        public void Deconstruct(out ICodex codex, out CodexPage page)
        {
            codex = Codex;
            page = this;
        }

        public static implicit operator (ICodex codex, MainController app, ViewModelDataContext view)(CodexPage page)
        {
            return (page.Codex, page.App, page.View);
        }

        public ListSegment<HtmlElementInfo> TryFind(string searchString)
        {
            return RightSource.TryFind(searchString);
        }

        public HtmlElementInfo Find(string searchString)
        {
            return TryFind(searchString)[0];
        }

        public HtmlElementInfo FindOrDefault(string searchString)
        {
            return TryFind(searchString).FirstOrDefault();
        }

        public async Task<HtmlElementInfo> ClickAsync(string searchString)
        {
            var element = Find(searchString);
            await element.Link.NavigateAsync(App);
            return element;
        }

        public async Task<INavigateItem> ClickLeftPaneAsync(int index)
        {
            var element = LeftItems[index];
            await element.NavigateAddress.NavigateAsync(App);
            return element;
        }

        public async Task<INavigateItem> ClickLeftPaneAsync(Func<INavigateItem, bool> selectLeftItem)
        {
            var element = LeftItems.First(selectLeftItem);
            await element.NavigateAddress.NavigateAsync(App);
            return element;
        }
    }
}
