using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text.Json;
using Codex.Application;
using Codex.Application.Verbs;
using Codex.Build.Tasks;
using Codex.Configuration;
using Codex.Lucene;
using Codex.Lucene.Formats;
using Codex.Lucene.Search;
using Codex.ObjectModel;
using Codex.ObjectModel.Attributes;
using Codex.ObjectModel.Implementation;
using Codex.Sdk;
using Codex.Sdk.Search;
using Codex.Storage;
using Codex.Utilities;
using Codex.Utilities.Zip;
using Codex.View;
using Codex.Web.Common;
using CodexTestBProject;
using CodexTestProject;
using DotNext;
using DotNext.IO;
using FluentAssertions;
using LibGit2Sharp;
using Microsoft.Net.Http.Headers;
using Mono.Cecil.Cil;
using Xunit.Abstractions;
using M = Codex.ObjectModel.Implementation.SearchMappings;

namespace Codex.Integration.Tests;

using static CodexConstants;
public record AnalyzeTestProject(ITestOutputHelper Output) : AnalyzeTestProjectBase(Output)
{
    [Fact]
    public async Task TestReindexClean()
    {
        bool isValid = PathUtilities.IsValidFileName("_internal");

        await TestRelatedDefinitions(searchOnly: false, cleanIndex: true);
    }

    [Fact(Skip = "Manual only")]
    public Task TestAddToIndex()
    {
        return TestRelatedDefinitions(searchOnly: false, cleanIndex: false);
    }

    [Fact(Skip = "Manual only")]
    public Task SearchIndex()
    {
        return TestRelatedDefinitions(searchOnly: true, cleanIndex: false);
    }

    [Fact]
    public async Task EnumConstant()
    {
        var ingest = await RunAnalyzeTestProject(o =>
        {
            o.IsAllowedTestFile = p => p.EndsWithIgnoreCase("EnumConstant.cs");
            return o;
        });

        (var codex, var app, var view, var page) = CreateCodexApp(ingest);

        await page.Search("EnumConstant.2");
        //await page.ClickLeftPaneAsync(0);
    }

    [Fact]
    public async Task ProjectFile()
    {
        var isAllowed = (string p) => p.EndsWithIgnoreCase("CodexTestProject.csproj");
        using var _ = SdkFeatures.AmbientFileIndexFilter.EnableGlobal(f => isAllowed(f.ProjectRelativePath));
        var ingest = await RunAnalyzeTestProject(o =>
        {
            o.IsAllowedTestFile = isAllowed;
            return o;
        });

        (var codex, var app, var view, var page) = CreateCodexApp(ingest);

        await page.Search("CodexTestProject.csproj");
        await page.ClickLeftPaneAsync(0);

        await page.ClickAsync("<*ProvideCommandLineArgs");

        foreach (var r in page.RightSource.References)
        {

        }

    }

    [Fact]
    public async Task AnonymousTypes()
    {
        using var _ = Features.AddDefinitionForInheritedInterfaceImplementations.EnableLocal(true);

        var ingest = await RunAnalyzeTestProject(o =>
        {
            o.IsAllowedTestFile = p => p.EndsWithIgnoreCase("AnonymousTypes.cs");
            return o;
        });

        (var codex, var app, var view, var page) = CreateCodexApp(ingest);

        await page.Search("AnonymousTypes.cs");
        await page.ClickLeftPaneAsync(0);

        var localDump = page.RightSource.GetXmlDump(SourceFileViewFlags.RefInfo);
    }

    [Fact]
    public async Task DerivedTypeInterfaceImpl()
    {
        using var _ = Features.AddDefinitionForInheritedInterfaceImplementations.EnableLocal(true);

        var ingest = await RunAnalyzeTestProject(o =>
        {
            o.IsAllowedTestFile = p => p.EndsWithIgnoreCase("DerivedTypeInterfaceImpl.cs");
            return o;
        });

        (var codex, var app, var view, var page) = CreateCodexApp(ingest);
    }

    [Fact]
    public async Task TestDefinitionDisplay()
    {
        var ingest = await RunAnalyzeTestProject(o =>
        {
            o.IsAllowedTestFile = p => p.EndsWithIgnoreCase("DefinitionDisplay.cs");
            return o;
        });

        (var codex, var app, var view, var page) = CreateCodexApp(ingest);

        await page.NavigateTo(ViewModelAddress.ShowNamespaceExplorer("CodexTestProject"));
        var root = view.LeftPane.Content.AsTree.Root;
        root.Children.Count.Should().Be(1);

        var nsChild = root.Children[0];
        nsChild.Kind.Should().BeEquivalentTo(nameof(SymbolKinds.Namespace));

        await page.Search("CodexTestProject #entrypoint");
        page.LeftItems.Count.Should().Be(1);

        await page.Search("CodexTestProject #main");
        page.LeftItems.Count.Should().Be(1);

        await page.Search("testmeth");

        page.LeftItems.Count.Should().Be(1);
        var item = page.LeftItems[0].AsSymbol().Symbol;
        item.DisplayName.Should().NotContain("params");

        await page.ClickLeftPaneAsync(0);

        var xmlDump = page.RightSource.GetXmlDump(SourceFileViewFlags.RefInfo);
        var localDump = page.RightSource.GetXmlDump(SourceFileViewFlags.Locals);
    }

    [Fact]
    public async Task TestMiscellaneous()
    {
        var ingest = await RunAnalyzeTestProject(o =>
        {
            return o;
        });

        (var codex, var app, var view, var page) = CreateCodexApp(ingest);

        await page.Search("ixedoc");
        await page.ClickLeftPaneAsync(0);

        var file = page.RightSource.SourceFile;

    }

    [Fact]
    public async Task TestProjectScope()
    {
        var ingest = await RunAnalyzeTestProject(o =>
        {
            o.IncludeSecondaryProject = true;
            return o;
        });

        (var codex, var app, var view, var page) = CreateCodexApp(ingest);

        await page.Search("ProjectScopeCommonType");

        await page.Search("testproj");
        Assert.Equal(2, page.LeftItems.Count);
        Assert.All(page.LeftItems, i => Assert.Equal(SymbolKinds.Repo, i.AsSymbol().Symbol.Kind));

        await page.Search("#repo");
        Assert.Equal(2, page.LeftItems.Count);
        Assert.All(page.LeftItems, i => Assert.Equal(SymbolKinds.Repo, i.AsSymbol().Symbol.Kind));

        await page.Search("#project");
        await page.ClickLeftPaneAsync(_ => true);
        var projectTree = (TreeViewModel)view.LeftPane.Content;
        projectTree.Should().NotBeNull();

        await page.ClickLeftPaneAsync(i =>
            i.NavigateAddress.filePath?.ContainsIgnoreCase(ReferencedProjectsXmlFileName) == true);
        page.RightSource.Should().NotBeNull();

        await page.ClickAsync(" Name=\"*System.Runtime\"");
        page.RightSource.Should().NotBeNull();

        await page.Search("CodexTest");
        Assert.Equal(2, page.LeftItems.Where(i => i.AsSymbol().Symbol.Kind == SymbolKinds.Project).Count());

        await page.Search("IHaveIndexer");
        await page.Search("ProjectScopeReferencesTest");
        await page.Search("ProjectScopeCommonTyp");

        var item = await page.SelectLeftItem(0);

        var reference = await page.ClickAsync("class *ProjectScopeCommonType");

        var allReferencesCount = page.LeftItems.Count;
        Assert.NotEqual(0, allReferencesCount);

        var projectScopeLink = reference.Link with { projectScope = item.ProjectId };

        await projectScopeLink.NavigateAsync(page.App);

        var scopedReferencesCount = page.LeftItems.Count;
        Assert.Equal(page.Address.projectScope, item.ProjectId);
        Assert.NotEqual(0, scopedReferencesCount);
        Assert.True(scopedReferencesCount < allReferencesCount);

    }
    [Fact]
    public async Task TestLocals()
    {
        GetTestOutputDirectory(clean: true);
        var ingest = await RunAnalyzeTestProject(o =>
        {
            o.IsAllowedTestFile = s => s.ContainsIgnoreCase("LocalsTest.p1");
            return o;
        });

        (var codex, var app, var view, var page) = CreateCodexApp(ingest);

        await page.Search("LocalsTest.p1.cs");
        page.LeftItems.Count.Should().Be(1);
        await page.ClickLeftPaneAsync(i => true);


        var xmlDump = page.RightSource.GetXmlDump(SourceFileViewFlags.Locals).ToString();
    }

    [Fact]
    public async Task TestVBProject()
    {
        using var _ = SdkFeatures.AfterDefinitionAddHandler.EnableGlobal(def =>
        {
            if (def.Definition.ShortName.ContainsIgnoreCase("Equatable"))
            {

            }
        });

        GetTestOutputDirectory(clean: true);
        var ingest = await RunAnalyzeTestProject(o =>
        {
            o.Projects = new()
            {
                TestProjects.VB,
                TestProjects.A
            };

            o.IsAllowedTestFile = s => s.ContainsIgnoreCase("equatable");
            return o;
        });

        (var codex, var app, var view, var page) = CreateCodexApp(ingest);

        await page.Search("VBEq");
        page.LeftItems.Count.Should().Be(3);

        await page.Search("IEqu @all");



        // IEquatable<T> definition should unify between the two projects
        page.LeftItems.Count.Should().Be(1);
    }

    [Fact]
    public async Task TestRepoScope()
    {
        var ingest = await RunAnalyzeTestProject(o =>
        {
            o.IncludeSecondaryProject = true;
            return o;
        });

        (var codex, var app, var view, var page) = CreateCodexApp(ingest);

        var pageRequest = new PageRequest()
        {
            Url = "http://localhost/"
        };

        var pageResult = await pageRequest.NavigateAsync(app, log: true);

        await page.Search("IHaveIndexer");
        await page.Search("ProjectScopeReferencesTest");
        await page.Search("ProjectScopeCommonTyp");

        var item = await page.SelectLeftItem(0);

        var reference = await page.ClickAsync("class *ProjectScopeCommonType");

        var allReferencesCount = page.LeftItems.Count;
        Assert.NotEqual(0, allReferencesCount);

        app.CodexService = codex.ScopeToRepo(PrimaryProjectRepoName);

        await reference.Link.NavigateAsync(page.App);
        var scopedReferencesCountA = page.LeftItems.Count;
        Assert.NotEqual(0, scopedReferencesCountA);

        Assert.All(page.LeftItems, i => Assert.Equal(typeof(TestProject).Assembly.GetName().Name, i.ProjectId));
        Assert.True(scopedReferencesCountA < allReferencesCount);

        app.CodexService = codex.ScopeToRepo(SecondaryProjectRepoName);

        await reference.Link.NavigateAsync(page.App);
        var scopedReferencesCountB = page.LeftItems.Count;
        Assert.NotEqual(0, scopedReferencesCountB);

        Assert.All(page.LeftItems, i => Assert.Equal(typeof(TestBProject).Assembly.GetName().Name, i.ProjectId));
        Assert.True(scopedReferencesCountB < allReferencesCount);

        Assert.Equal(allReferencesCount, scopedReferencesCountA + scopedReferencesCountB);
    }

    [Fact]
    public async Task TestTypeForwarding()
    {
        var address = ViewModelAddress.GoToDefinition(TestProject.ProjectName, "#T:System.Xml.XmlSpace");

        var targetAddress = ViewModelAddress.GoToDefinition(TestBProject.ProjectName, $"#T:{typeof(TypeForwardedTarget).FullName}");

        var ingest = await RunAnalyzeTestProject(o => o with
        {
            IncludeSecondaryProject = true,
            IsAllowedTestFile = p => p.ContainsIgnoreCase("TypeForwards.cs")
        });
        (var codex, var app, var view, var page) = CreateCodexApp(ingest);

        await address.NavigateAsync(app, infer: false);

        page.RightSource.Should().BeNull();

        await targetAddress.NavigateAsync(app, infer: false);

        page.RightSource.Should().NotBeNull();
        var file = page.RightSource.SourceFile;
        file.ProjectId.Should().Be(TestProject.ProjectName);

    }

    [Fact]
    public async Task TextSearch()
    {
        var ingest = await RunAnalyzeTestProject(o => o with
        {
            IsAllowedTestFile = p => p.ContainsIgnoreCase("ReferenceKindsTest.cs"),
            //SearchOnly = true
        });

        (var codex, var app, var view, var page) = CreateCodexApp(ingest);

        await page.Search("`second declaration");
    }

    [Fact]
    public async Task TestExtensionMethods()
    {
        var ingest = await RunAnalyzeTestProject(o => o with
        {
            IsAllowedTestFile = p => p.ContainsIgnoreCase("TestExtensionMethods.cs"),
        });

        (var codex, var app, var view, var page) = CreateCodexApp(ingest);
        await page.Search("String.*");
        page.LeftItems.Count.Should().Be(0);

        await page.Search("@ext String.*");
        page.LeftItems.Count.Should().BeGreaterThan(0);

        await page.Search("@ext IList.*");
        page.LeftItems.Count.Should().BeGreaterThan(0,
            "Extension info should be added if there is a single type constraint (excluding non-type type constraints like struct or new())");

        await page.Search("@all @ext IEnumerable.Take");
        page.LeftItems.Count.Should().BeGreaterThan(0,
            "Extension info should be added for referenced definitions");

        await page.Search("@ext IDictionary.*");
        page.LeftItems.Count.Should().Be(0,
            "No extension info should be added if cannot be resolved to single type");
    }

    [Theory]
    //[InlineData(true)]
    [InlineData("ReferenceKindsTest.cs")]
    [InlineData("InstantiationTest.cs")]
    [InlineData("NonStandardRefsTest.cs")]
    public async Task TestReferenceKinds(string fileName, bool searchOnly = false)
    {
        var ingest = await RunAnalyzeTestProject(o => o with
        {
            IsAllowedTestFile = p => p.ContainsIgnoreCase(fileName),
            SearchOnly = searchOnly,
        });

        (var codex, var app, var view, var page) = CreateCodexApp(ingest, o => o with { UsePaging = true });
        var luCodex = codex as LuceneCodex;

        await page.Search(fileName);

        await page.SelectLeftItem(0);

        if (fileName == "-.cs")
        {

        }
        else if (fileName == "NonStandardRefsTest.cs")
        {
            page.TryFind("*->").Should().BeNullOrEmpty();
        }
        else if (fileName == "InstantiationTest.cs")
        {
            var instantation = page.Find("new *InstantationTest");

            var primaryRef = instantation.SourceSpan.Reference.Reference;
            primaryRef.ReferenceKind.Should().Be(ReferenceKind.Reference);
            primaryRef.Kind.Should().Be(SymbolKinds.Constructor);

            var instantationRef = instantation.SourceSpan.SpanReferences.Value.Single(r => r.Reference.ReferenceKind == ReferenceKind.Instantiation);
            instantationRef.Reference.Kind.Should().Be(SymbolKinds.Class);

            await page.ClickAsync("class *InstantationTest");

            page.LeftItems.Single(i => i.AsText()?.Model.RefResult.ReferenceSpan.Reference.ReferenceKind == ReferenceKind.Instantiation);
        }
        else if (fileName == "ReferenceKindsTest.cs")
        {
            var op = page.FindOrDefault("*++ta");
            Assert.Equal(SymbolKinds.Operator, op.Symbol.Kind);
            Assert.Equal(ReferenceKind.Reference, op.Symbol.ReferenceKind);

            op = page.FindOrDefault("ta *== tb");
            Assert.Equal(SymbolKinds.Operator, op.Symbol.Kind);
            Assert.Equal(ReferenceKind.Reference, op.Symbol.ReferenceKind);

            // No references for operators which are not explicitly defined
            page.FindOrDefault("oa *== ob")?.Symbol.Should().BeNull();
            page.FindOrDefault(")ta *== (")?.Symbol.Should().BeNull();
            page.FindOrDefault("a *< b")?.Symbol.Should().BeNull();
            page.FindOrDefault("a *== b")?.Symbol.Should().BeNull();

            page.TryFind("<see cref=\"HaveIndexer.*RecordArgParamAndProperty\"/>").Count.Should().Be(0);

            var partial = await page.ClickAsync("*partial interface IHaveIndexer :");
            Assert.Equal(SymbolKinds.Interface, partial.Symbol.Kind);
            Assert.Equal(ReferenceKind.Partial, partial.Symbol.ReferenceKind);
            page.LeftItems.Count.Should().Be(2);

            var read = await page.ClickAsync("var np = indexer.*NormalProperty;");
            Assert.Equal(SymbolKinds.Property, read.Symbol.Kind);
            Assert.Equal(ReferenceKind.Read, read.Symbol.ReferenceKind);

            var with = await page.ClickAsync("indexer *with");
            Assert.Equal(SymbolKinds.Class, with.Symbol.Kind);
            Assert.Equal(ReferenceKind.CopyWith, with.Symbol.ReferenceKind);

            var json = page.LeftItems[1].AsText().Model.RefResult.As<EntityBase>().RootEntity.As<IReferenceSearchModel>()
                .SerializeEntity(ObjectStage.Index).Replace('"', '\'');

            var targetTypeNew = await page.ClickAsync("NoArgRecord nar2 = *new()");
            var bestReference = targetTypeNew.SourceSpan.Reference;
            Assert.Equal(SymbolKinds.Constructor, targetTypeNew.Symbol.Kind);
            Assert.Equal(ReferenceKind.Reference, targetTypeNew.Symbol.ReferenceKind);

            var clickVirtual = await page.ClickAsync("virtual bool *VirtualMethod");

            var noArgRecordInstantiation = await page.ClickAsync("new *NoArgRecord");

            var info = page.Find("HaveIndexer(int *RecordArgProperty");
            Assert.Equal(ReferenceKind.Definition, info.Symbol.ReferenceKind);

            info = page.FindOrDefault("DerivedRecord(int *RecordArgProperty");
            Assert.Null(info);

            var getter = await page.ClickAsync("this[int input] { *get =>");
            Assert.Equal(ReferenceKind.Getter, getter.Symbol.ReferenceKind);
            Assert.Equal(ReferenceKind.Getter, getter.Link.refKind);

            Assert.Equal(ReferenceKind.Getter, page.Address.refKind);

            // Clicking on a normal definition should clear the refKind from the address.
            await page.ClickAsync("record *NoArgRecord");
            Assert.Null(page.Address.refKind);

            var setter = page.Find("int this[int input] { get; *set");
            Assert.Equal(ReferenceKind.Setter, setter.Symbol.ReferenceKind);
            Assert.Equal(ReferenceKind.Setter, setter.Link.refKind);

            var indexerGet = page.Find(@"*[0]");
            var definition = await codex.FindDefinitionAsync(new FindDefinitionArguments() { SymbolId = indexerGet.Symbol.Id, ProjectId = indexerGet.Symbol.ProjectId });

            Assert.Equal(ReferenceKind.Read, indexerGet.Symbol.ReferenceKind);
            Assert.Equal(SymbolKinds.Indexer, indexerGet.Symbol.Kind);

            var indexerSet = page.Find(@"[0] =");
            Assert.Equal(ReferenceKind.Write, indexerSet.Symbol.ReferenceKind);
            Assert.Equal(SymbolKinds.Indexer, indexerSet.Symbol.Kind);

            indexerGet = page.Find(@"[1]");
            Assert.Equal(ReferenceKind.Read, indexerGet.Symbol.ReferenceKind);
            Assert.Equal(SymbolKinds.Indexer, indexerGet.Symbol.Kind);

            indexerSet = page.Find(@"[1] =");
            Assert.Equal(ReferenceKind.Write, indexerSet.Symbol.ReferenceKind);
            Assert.Equal(SymbolKinds.Indexer, indexerSet.Symbol.Kind);

            foreach ((var castText, var index) in new[] {
            "intCast = (*HaveIndexer)result",
            "mustCast = (*HaveIndexer)indexer",
            "is *HaveIndexer haveIndexer",
            "indexer as *HaveIndexer" }.WithIndices())
            {
                var cast = page.Find(castText);
                var symbol = cast.SourceSpan.SpanReferences.Value.FirstOrDefault(s => s.Reference.Kind == SymbolKinds.Class);
                symbol.Should().NotBeNull();
                Assert.Equal(ReferenceKind.ExplicitCast, symbol.Reference.ReferenceKind);

                if (index == 0)
                {
                    Assert.Equal(ReferenceKind.Reference, cast.Symbol.ReferenceKind);
                    Assert.Equal(SymbolKinds.Method, cast.Symbol.Kind);
                }
            }

            await getter.Link.NavigateAsync(app);

            await indexerGet.Link.NavigateAsync(app);
        }
    }

    [Theory]
    //[InlineData(true)]
    [InlineData(false)]
    public async Task TestFullNameSearch(bool searchOnly)
    {
        Features.AddReferenceDefinitions.EnableLocal(enabled: false);

        var ingest = await RunAnalyzeTestProject(o => o with
        {
            SearchOnly = searchOnly,
            IsAllowedTestFile = p => p.ContainsIgnoreCase("FullNameSearch.cs")
        });
        (var codex, var app, var view, var page) = CreateCodexApp(ingest);

        await app.SearchTextChanged("TestCases/Full");
        Assert.Equal(1, view.LeftPane.Content.ItemList.Count);

        var luceneCodex = page.LuceneCodex;
        var reader = luceneCodex.Client.DefinitionIndex.As<ILuceneIndex>().Reader;
        var lr = reader.Leaves[0];
        var qterms = lr.AtomicReader.GetTerms(M.Definition.ContainerQualifiedName.Name);

        var qtermsenum = qterms?.Enumerate().Select(b => b.ToString());


        await app.SearchTextChanged("FullNameSearch.GenericType.Gen");
        Assert.Equal(1, view.LeftPane.Content.ItemList.Count);

        var json = new DefinitionSearchModel()
        {
            Definition = (DefinitionSymbol)view.LeftPane.Content.ItemList[0].AsSymbol().Symbol
        }.SerializeEntity(ObjectStage.Index).Replace('"', '\'');

        await app.SearchTextChanged("FullNameSearch.GenericType<T,U,V>.Gen");
        Assert.Equal(1, view.LeftPane.Content.ItemList.Count);

        await app.SearchTextChanged("Subns.TestType<T,U,V>.Nested<>.Met");
        Assert.Equal(1, view.LeftPane.Content.ItemList.Count);

        await app.SearchTextChanged("Subns.TestType.Nested.Met");
        Assert.Equal(1, view.LeftPane.Content.ItemList.Count);

        await app.SearchTextChanged("GenericType<Not,Correct,TypeParams");
        Assert.Equal(1, view.LeftPane.Content.ItemList.Count);

        await app.SearchTextChanged("GenericType<Incomplete,TypeParams");
        Assert.Equal(1, view.LeftPane.Content.ItemList.Count);

        await app.SearchTextChanged("Gen");
        Assert.Equal(2, view.LeftPane.Content.ItemList.Count);

        await app.SearchTextChanged("FullNameSearch.Gen");
        Assert.Equal(1, view.LeftPane.Content.ItemList.Count);
    }

    [Theory]
    //[InlineData(true)]
    [InlineData(false)]
    public async Task TestSuffixMatchSearch(bool searchOnly)
    {
        Features.AddReferenceDefinitions.EnableLocal(enabled: false);

        var ingest = await RunAnalyzeTestProject(o => o with
        {
            SearchOnly = searchOnly,
            IsAllowedTestFile = p => p.ContainsIgnoreCase("SuffixMatchSearch.cs")
        });
        (var codex, var app, var view, var page) = CreateCodexApp(ingest);


        await app.SearchTextChanged("dgr");
        Assert.Equal(1, page.LeftItems.Count);

        await app.SearchTextChanged("RowSpan.");
        Assert.Equal(0, page.LeftItems.Count);

        // Find all members of a type
        await app.SearchTextChanged("RowSpan.*");
        Assert.Equal(4, page.LeftItems.Count);

        await app.SearchTextChanged("RowSpan.*As");
        Assert.Equal(3, page.LeftItems.Count);

        await app.SearchTextChanged("DataGrid$");
        Assert.Equal(1, view.LeftPane.Content.ItemList.Count);

        await app.SearchTextChanged("\"DataGrid\"");
        Assert.Equal(1, view.LeftPane.Content.ItemList.Count);

        await app.SearchTextChanged("Data");
        Assert.Equal(3, view.LeftPane.Content.ItemList.Count);

        await app.SearchTextChanged("*Data");
        Assert.Equal(4, view.LeftPane.Content.ItemList.Count);

        await app.SearchTextChanged("Row");
        Assert.Equal(1, view.LeftPane.Content.ItemList.Count);

        await app.SearchTextChanged("*Row");
        Assert.Equal(2, view.LeftPane.Content.ItemList.Count);
    }

    [Fact]
    public async Task SearchIndexWithStoredFiltersSnapshots()
    {
        int index = 0;
        IngestOperation ingest = null;

        var outputDir = GetTestOutputDirectory(clean: true);

        //SdkFeatures.OnRequiredEntityHandler.EnableGlobal((storage, entity) =>
        //{
        //    if (entity is IBoundSourceSearchModel bs)
        //    {
        //        if (bs.Files.Info is { } info &&
        //            info.ProjectRelativePath.EqualsIgnoreCase("TestCases\\TemplateCode.cs"))
        //        {
        //            var values = ((ZoneTreeStableIdStorage)storage).Enumerate();
        //            var entry = values.Where(kvp => kvp.Key == entity.Uid).ToArray();
        //            var map = values.ToImmutableDictionary();

        //            Files.WriteAllText(@$"{outputDir}\zfile.{index}.json", JsonSerializationUtilities.SerializeEntity(
        //                bs,
        //                ObjectStage.Index,
        //                JsonFlags.Indented));
        //        }
        //    }
        //});


        await runAsync("apple", "prune", clean: true);
        await runAsync("apple", "prune", clean: false);
        await runAsync("prune", "apple", clean: false);
        await runAsync("berry", "prune", clean: false);

        async Task runAsync(string resultPrefix, string unexpected, bool clean = false)
        {
            Logger.LogMessage($"--- RUN {index} ---");

            index++;
            var priorIngest = ingest;
            ingest = await RunAnalyzeTestProject(o => o with
            {
                SearchOnly = false,
                CleanIndex = clean,
                TemplateReplacement = resultPrefix,
                AnalyzedDirectoryQualifier = index.ToString(),
                IsAllowedTestFile = f =>
                {
                    return f.ContainsIgnoreCase("template");
                }
            });

            (var codex, var app, var view) = CreateCodexApp(ingest);

            await app.SearchTextChanged(resultPrefix);

            var itemCount = view.LeftPane.Content?.ItemList.Count ?? -1;
            itemCount.Should().BeGreaterThan(0);

            await app.SearchTextChanged("CodexTestProject.csproj");

            itemCount = view.LeftPane.Content?.ItemList.Count ?? -1;
            itemCount.Should().BeGreaterThan(0);

            await app.SearchTextChanged(unexpected);

            itemCount = view.LeftPane.Content?.ItemList.Count ?? -1;
            itemCount.Should().Be(-1);
        }
    }

    //[Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)]
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SearchIndexWithStoredFiltersSnapshotsMultiIngest(bool shouldCheckReload)
    {
        GetTestOutputDirectory(clean: true);
        int index = -1;

        bool stage = true;

        var stableIdMap = new ConcurrentDictionary<string, int>();

        var eval = Eval.Create(() => stableIdMap.OrderBy(k => k.Key).ToList());
        var idEval = Eval.Create(() => stableIdMap.OrderBy(k => k.Value).ToList());

        using var _ = SdkFeatures.AfterDefinitionAddHandler.EnableLocal(dss =>
        {
            if (dss.Definition.ShortName is string shortName)
            {
                var currentId = stableIdMap.GetOrAdd(shortName + dss.EntityContentId, dss.StableId);
                if (currentId != dss.StableId)
                {

                }
            }
        });

        DateTimeOffset lastModifiedTimeStart = DateTimeOffset.UtcNow;
        TimeSpan lastModifiedTimeOffset = default;
        IndexSourceLocation indexSource = default;
        IBytesRetriever indexClient = default;
        using var _1 = SdkFeatures.WebProgramCacheLimit.EnableLocal(3);

        using var _2 = SdkFeatures.IndexClientResponsePreprocessor.EnableLocal((kind, rsp) =>
        {
            if (kind == HttpClientKind.Index && rsp.IsSuccessStatusCode)
            {
                rsp.Content.Headers.LastModified = lastModifiedTimeStart + lastModifiedTimeOffset;
            }

            return null;
        });


        CodexPage webProgramReloadablePage = default;

        ValueTask? emitDebugInfoAsync()
        {
            ValueTask? task = default;
            return task;
        }

        await runAsync("apple", "prune", clean: true, shouldIngest: stage);

        await emitDebugInfoAsync();

        await runAsync("prune", "apple", clean: true, shouldIngest: stage);

        await emitDebugInfoAsync();

        await runAsync("berry", "prune", clean: true, shouldIngest: true);

        await emitDebugInfoAsync();

        async Task runAsync(string resultPrefix, string unexpected, bool clean = false, bool shouldIngest = false)
        {
            lastModifiedTimeOffset += TimeSpan.FromMinutes(1);

            index++;
            var analyze = await RunAnalyzeTestProjectAnalysis(o => o with
            {
                ZipEncryptAnalysisOutput = true,
                SearchOnly = false,
                CleanIndex = clean,
                TemplateReplacement = resultPrefix,
                AnalyzedDirectoryQualifier = index.ToString(),
            },
            AsyncOut.Var<AnalyzeTestProjectOptions>(out var options));

            options.Value.ConfigureIngest = ingest =>
            {
                // Only clean on first iteration
                ingest.Clean = index == 0;

                if (stage)
                {
                    ingest.StagingDirectory = Path.Combine(options.Value.OutputPath, ".staging" + index);
                }
                else
                {
                    ingest.InputPath = Path.GetDirectoryName(ingest.InputPath);
                    ingest.Scan = true;
                }
            };

            if (shouldIngest)
            {
                var ingest = await RunAnalyzeTestProjectIngest(analyze, options);

                //if (stage) return;

                var pages = new List<CodexPage>();

                if (shouldCheckReload)
                {
                    using var _0 = SdkFeatures.IndexRetrieverTestHook.EnableLocal(new(out var indexClientOut));
                    webProgramReloadablePage ??= await CreateWebProgram(ingest, updateArguments: args =>
                    {
                        indexSource ??= args.Value.IndexSource;
                        indexSource.ReloadHeader = HeaderNames.LastModified;
                    }).SelectAsync(w => w.GetPage());

                    pages.Add(webProgramReloadablePage);


                    await webProgramReloadablePage.ReloadableCodex.CustomRunAsync(new ContextCodexArgumentsBase(), async c =>
                    {
                        indexSource.Url = GetIndexUrl(ingest);
                        indexClient ??= indexClientOut.Value;
                        ReloadableCodex.TryGetToken(out var reloadToken);
                        var response = await indexClient.GetBytesAsync(PagingDirectoryInfo.DirectoryInfoFileName).SelectAsync(b => b.AsStream().ReadAllText());
                        return new IndexQueryResponse();
                    });

                }

                pages.Add(CreateCodexApp(ingest));

                foreach (var page in pages)
                {
                    (var codex, var app, var view) = page;

                    //var results = await codex.SearchAsync(new SearchArguments()
                    //{
                    //    SearchString = "ixe",
                    //    DisableStoredFilter = true
                    //});

                    await app.SearchTextChanged(unexpected);

                    var localEval = (eval, idEval);

                    await app.SearchTextChanged(resultPrefix);

                    var itemCount = view.LeftPane.Content?.ItemList.Count ?? 0;
                    Assert.True(itemCount > 0);

                    await app.SearchTextChanged(unexpected);

                    itemCount = view.LeftPane.Content?.ItemList.Count ?? 0;
                    Assert.True(itemCount == 0);
                }
            }
        }
    }

    [Theory]
    [InlineData(false, true)]
    //[InlineData(false, false)]
    //[InlineData(true, false)]
    public async Task TestRelatedDefinitions(bool searchOnly, bool cleanIndex)
    {
        var ingest = await RunAnalyzeTestProject(searchOnly, cleanIndex);
        (var codex, var app, var view, var page) = CreateCodexApp(ingest);

        await app.SearchTextChanged("`searching");
        page.LeftItems.Should().NotBeEmpty();

        await app.SearchTextChanged("`search*");

        await app.SearchTextChanged("ixedoc");

        var symbolResult = view.LeftPane.Content.ItemList.Single().AsSymbol();

        await symbolResult.FindAllReferences.NavigateAsync(app, infer: false);

        await symbolResult.NavigateAddress.NavigateAsync(app);

        await app.SearchTextChanged("value");

        var result = view.LeftPane.Content.SearchResults.Where(i => i.DisplayName.Contains("XedocImpl.Value")).First();

        await app.FindAllReferencesExecuted(result.Symbol);
        await result.NavigateAddress.NavigateAsync(app, infer: false);

        var categories = view.LeftPane.Content.AsCategorized.Categories;

        var relatedDefCategories = categories.Where(e => e.RelatedDefinitions?.Count > 0).ToList();

        relatedDefCategories.Count.Should().Be(2);

        var refs = view.RightPane.SourceFile.References;

        await page.Search("CodexTestProject.csproj");

        await page.ClickLeftPaneAsync(i => true);

        await page.ClickAsync("<*ProvideCommandLineArgs>");

        view.LeftPane.Content.AsCategorized.Categories[0].Header.Should().Contain("ProvideCommandLineArgs");

        await page.Search("#project");

    }
}