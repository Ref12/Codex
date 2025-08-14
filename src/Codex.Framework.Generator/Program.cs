using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Codex.Utilities;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.Ide.Common;
using RazorEngineCore;
using Xunit;

namespace Codex.Framework.Generator;
public class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
    }

    private static void Run(string outputPath)
    {
        Directory.CreateDirectory(outputPath);
        var context = new GeneratorContext(outputPath);
        context.Initialize();
    }

    [Fact]
    public void TestJob()
    {
        var isInJob = JobObjectUtilities.IsProcessInJobObject(Process.GetCurrentProcess());

        var addJob = JobObjectUtilities.CreateOrGetJobObject(forceNew: true);
    }

    [Fact]
    public void RunGenerator()
    {
        Program.Run(GetRootDir());
    }

    [Fact]
    public void Test()
    {
        ReferenceKindSet kinds = new ReferenceKindSet();
        kinds.Add(ReferenceKind.Read);
        kinds.Add(ReferenceKind.Partial);

        var array = kinds.Enumerate().ToArray();

        var ml = ReferenceKindExtensions.ReferenceKindPreferenceSetMaskList;
    }

    private static string GetRootDir()
    {
        return Path.GetDirectoryName(ProjectPath);
    }

    //[Fact]
    public void GenerateWebFiles()
    {
        string razorFilePath = @$"{GetRootDir()}\Codex.Web.Common\Rendering\SearchResultsTemplate.razor";
        var doc = RazorSourceDocument.Create(File.ReadAllText(razorFilePath), @"SearchResultsTemplate.razor");

        RazorProjectEngine engine = RazorProjectEngine.Create(
                RazorConfiguration.Default,
                RazorProjectFileSystem.Create(@"."),
                (builder) =>
                {
                    builder.SetNamespace("Codex.Web.Common");
                    builder.ConfigureClass((doc, node) =>
                    {
                        node.ClassName = "SearchResultsTemplate";
                        node.BaseType = "TemplateBase<SymbolSearchResult>";
                    });
                });

        var codeDoc = engine.Process(doc, null, new List<RazorSourceDocument>(), new List<TagHelperDescriptor>());
        var csDoc = codeDoc.GetCSharpDocument();
        var generatedCode = csDoc.GeneratedCode;
        File.WriteAllText(razorFilePath + ".cs", generatedCode.Replace(
            @"public async override global::System.Threading.Tasks.Task ExecuteAsync()",
            @"public override void Execute()"));
    }

    public static string ProjectPath { get; } = GetProjectPath();

    private static string GetProjectPath([CallerFilePath] string filePath = null)
    {
        return Path.GetDirectoryName(filePath);
    }
}
