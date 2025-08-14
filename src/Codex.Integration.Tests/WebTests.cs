using System.Runtime.Loader;
using Codex.Application.Verbs;
using Codex.ObjectModel;
using Codex.ObjectModel.CompilerServices;
using Codex.ObjectModel.Implementation;
using Codex.Sdk.Search;
using Codex.Storage.Store;
using Codex.Utilities;
using Codex.View;
using Codex.Web.Common;
using Codex.Web.Utilities;
using Xunit.Abstractions;

namespace Codex.Integration.Tests;

public record WebTests(ITestOutputHelper Output) : AnalyzeTestProjectBase(Output)
{
    [Fact]
    public void ParseAddresses()
    {

        var sourceControlUri = SourceControlUri.Parse("Ref12/Codex", checkRepoNameFormat: true);

        var ss = SourceControlUri.Parse(@"https://testpass@github.com/Ref12/Codex");
        var url = sourceControlUri.GetApiUrlByCommit("a3271e3fad759caec1a812c1315e1a2336cdeb01", "Common.props");

        Output.WriteLine("world " + IntPtr.Size);
        Logger.LogMessage("hello");
        ViewModelAddress address = null;

        address = ViewModelAddress.Parse("https://localhost:5000/?indexrepo=ref12/dumpmodules");

        address = ViewModelAddress.Parse("?repo=WasmTest");

        address = ViewModelAddress.Parse("https://localhost:50145/Folder/index.html?query=bro");
        address = ViewModelAddress.Parse("https://localhost:50145/Folder/?rightProjectId=Codex.Web.Wasm&file=BrowserAppContext.cs&rightMode=file");
    }

    [Fact]
    public void Apply()
    {
        var start = new ViewModelAddress();

        start = start.With(ViewModelAddress.Search("testsearch"));
        start = start.With(ViewModelAddress.Parse(
            "?rightProjectId=System.Private.CoreLib&rightSymbolId=biotdec08mnu&rightMode=symbol"));
        start = start.With(ViewModelAddress.Parse(
            "?rightProjectId=System.Private.CoreLib&rightSymbolId=biotdec08mnu&line=24&file=src%5clibraries%5cSystem.Private.CoreLib%5csrc%5cSystem%5cText%5cStringBuilder.cs&rightMode=file"));
        start = start.With(ViewModelAddress.Search("testsearch"));

    }


    [Fact]
    public void ToQueryString()
    {
        Uri query = null;
        query = WebHelper.AsQueryUri(new SearchArguments() { SearchString = "hello", ProjectScopeId = "proj" });

        query = WebHelper.AsQueryUri(new FindDefinitionLocationArguments() { SymbolId = SymbolId.UnsafeCreateWithValue("hello"), ProjectId = "world" });

    }
}