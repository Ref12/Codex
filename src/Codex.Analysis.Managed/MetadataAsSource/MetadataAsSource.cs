using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.MetadataAsSource;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CodeGeneration;
using Perspectil;
using Microsoft.CodeAnalysis.DecompiledSource;
using Microsoft.CodeAnalysis.Host;
using System.Composition.Hosting;
using Codex.Logging;
using Microsoft.CodeAnalysis.Editor.CSharp.DecompiledSource;
using ICSharpCode.Decompiler.Metadata;
using System.Reflection.PortableExecutable;

namespace Codex.Analysis.Managed
{
    public record MetadataAsSource(
        string AssemblyFilePath,
        Logger Logger,
        FileSystem FileSystem = null, 
        bool UseDecompilation = true)
    {
        public FileSystem FileSystem { get; } = FileSystem ?? SystemFileSystem.Instance;

        private Workspace workspace;
        private IMetadataAsSourceService metadataAsSourceService;
        private CSharpDecompiledSourceService decompiledSourceService;

        public MetadataReference CreateSourceAssemblyReference(AsyncOut<PEFile> file = null)
        {
            var documentationProvider = GetDocumentationProvider(
                AssemblyFilePath,
                Path.GetFileNameWithoutExtension(AssemblyFilePath),
                FileSystem);

            using var fileStream = FileSystem.OpenFile(AssemblyFilePath);
            using var stream = new MemoryStream();
            fileStream.CopyTo(stream);

            if (file != null)
            {
                stream.Position = 0;
                file.Value = new PEFile(AssemblyFilePath, stream, PEStreamOptions.PrefetchEntireImage | PEStreamOptions.LeaveOpen);
                stream.Position = 0;
            }

            return MetadataReference.CreateFromStream(stream, documentation: documentationProvider);
        }

        public async Task<Solution> LoadMetadataAsSourceSolution(string projectDirectory)
        {
            try
            {
                var assemblyName = Path.GetFileNameWithoutExtension(AssemblyFilePath);

                var solution = new AdhocWorkspace(GetHostServices()).CurrentSolution;
                workspace = solution.Workspace;

                if (UseDecompilation)
                {
                    decompiledSourceService = new();
                }
                else
                {
                    metadataAsSourceService = workspace.GetLanguageService<IMetadataAsSourceService>();
                }

                var project = solution.AddProject(assemblyName, assemblyName, LanguageNames.CSharp);
                var metadataReference = CreateSourceAssemblyReference(new(out var file));

                try
                {
                    project = project.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
                }
                catch
                {
                    Console.WriteLine("Could add reference to mscorlib");
                }

                try
                {
                    var generator = PerspectiveAssemblyGenerator.CreateForStream(FileSystem.OpenFile(AssemblyFilePath), close: true);
                    generator.AllowGenerateCoreLibAssembly = true;
                    generator.Generate();

                    foreach (var assemblyDefinition in generator.PerspectiveAssemblies)
                    {
                        using (var stream = new MemoryStream())
                        {
                            try
                            {
                                stream.SetLength(0);
                                assemblyDefinition.Write(stream);

                                stream.Position = 0;
                                project = project.AddMetadataReference(MetadataReference.CreateFromStream(stream));
                            }
                            catch (Exception ex)
                            {
                                Logger.LogExceptionError($"Generate perspective assembly: {assemblyDefinition.Name}.", ex);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogExceptionError($"Generate perspective assemblies.", ex);
                }

                var projectWithReference = project.AddMetadataReference(metadataReference);
                var compilation = await projectWithReference.GetCompilationAsync();
                var assemblyOrModuleSymbol = compilation.GetAssemblyOrModuleSymbol(metadataReference);
                IAssemblySymbol assemblySymbol = assemblyOrModuleSymbol as IAssemblySymbol;
                IModuleSymbol moduleSymbol = assemblyOrModuleSymbol as IModuleSymbol;
                if (moduleSymbol != null && assemblySymbol == null)
                {
                    assemblySymbol = moduleSymbol.ContainingAssembly;
                }

                INamespaceSymbol namespaceSymbol = null;
                if (assemblySymbol != null)
                {
                    namespaceSymbol = assemblySymbol.GlobalNamespace;
                }
                else if (moduleSymbol != null)
                {
                    namespaceSymbol = moduleSymbol.GlobalNamespace;
                }

                var types = GetTypes(namespaceSymbol)
                    .OfType<INamedTypeSymbol>()
                    .Where(t => t.CanBeReferencedByName).ToArray();

                var tempDocument = projectWithReference.AddDocument("temp", SourceText.From(""), null);

                var texts = new Dictionary<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
                var langServices = workspace.Services.GetLanguageServices(LanguageNames.CSharp).LanguageServices;
                var genOptions = CleanCodeGenerationOptionsProviders.GetDefault(langServices);

                List<Task<string>> sourceTextTasks = new List<Task<string>>();
                foreach (var type in types)
                {
                    Task<Document> addSourceToAsync()
                    {
                        if (UseDecompilation)
                        {
                            return decompiledSourceService.AddSourceToAsync(tempDocument,
                                compilation,
                                type,
                                metadataReference,
                                file: file,
                                formattingOptions: genOptions.CleanupOptions.FormattingOptions,
                                CancellationToken.None);
                        }
                        else
                        {
                            return metadataAsSourceService.AddSourceToAsync(tempDocument,
                                compilation,
                                type,
                                genOptions.CleanupOptions.FormattingOptions,
                                CancellationToken.None);
                        }
                    }

                    sourceTextTasks.Add(GetTextAsync(addSourceToAsync(), AssemblyFilePath));
                }

                var sourceTexts = await Task.WhenAll(sourceTextTasks);

                List<Task<SourceText>> textTasks = new List<Task<SourceText>>();
                int typeIndex = 0;
                foreach (string sourceText in sourceTexts)
                {
                    texts.Add(types[typeIndex], sourceText);
                    typeIndex++;
                }

                HashSet<string> existingFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in texts)
                {
                    var tempProject = AddDocument(project, projectDirectory, kvp, existingFileNames);

                    // tempProject can be null if the document was in an unutterable namespace
                    // we want to skip such documents
                    if (tempProject != null)
                    {
                        project = tempProject;
                    }
                }

                //const string assemblyAttributesFileName = "AssemblyAttributes.cs";
                //project = project.AddDocument(
                //    assemblyAttributesFileName,
                //    assemblyAttributesFileText,
                //    filePath: assemblyAttributesFileName).Project;

                solution = project.Solution;
                return solution;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to run metadata as source for: {AssemblyFilePath}\n{ex.ToString()}" + AssemblyFilePath);
                return null;
            }
        }

        private HostServices GetHostServices()
        {
            //var compositionConfiguration = new ContainerConfiguration()
            //    .WithAssemblies(MefHostServices.DefaultAssemblies)
            //    .WithPart<CSharpDecompiledSourceService>();
            //var container = compositionConfiguration.CreateContainer();

            return MefHostServices.DefaultHost;
        }

        public static ImmutableArray<AssemblyIdentity> GetReferences(IAssemblySymbol assemblySymbol)
        {
            return assemblySymbol.Modules
                .SelectMany(m => m.ReferencedAssemblies)
                .Distinct()
                .ToImmutableArray();
        }

        public static async Task<string> GetTextAsync(Task<Document> documentTask, string assemblyFilePath)
        {
            try
            {
                var document = await documentTask;

                var sourceText = await document.GetTextAsync();

                var text = sourceText.ToString();

                text = text.Replace(assemblyFilePath, "Metadata As Source Generated Code");

                return text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when adding a MAS document to texts: {assemblyFilePath} \n{ex.ToString()}");
            }

            return null;
        }

        /// <summary>
        /// This has to be unique, there shouldn't be a project with this name ever
        /// </summary>
        public const string GeneratedAssemblyAttributesFileName = "GeneratedAssemblyAttributes0e71257b769ef";

        private static DocumentationProvider GetDocumentationProvider(string assemblyFilePath, string assemblyName, FileSystem fileSystem)
        {
            string xmlFile = Path.ChangeExtension(assemblyFilePath, ".xml");
            if (fileSystem.FileExists(xmlFile))
            {
                return XmlDocumentationProvider.CreateFromBytes(fileSystem.OpenFile(xmlFile).ReadAllBytes());
            }

            return DocumentationProvider.Default;
        }

        private static Project AddDocument(
            Project project,
            string projectDirectory,
            KeyValuePair<INamedTypeSymbol, string> symbolAndText,
            HashSet<string> existingFileNames)
        {
            var symbol = symbolAndText.Key;
            var text = symbolAndText.Value;
            var sanitizedTypeName = Paths.SanitizeFileName(symbol.Name);
            if (symbol.IsGenericType)
            {
                sanitizedTypeName = sanitizedTypeName + "`" + symbol.TypeParameters.Length;
            }

            var fileName = sanitizedTypeName + ".cs";
            var folders = GetFolderChain(symbol);
            if (folders == null)
            {
                // There was an unutterable namespace name - abort the entire document
                return null;
            }

            var foldersString = string.Join(".", folders ?? Enumerable.Empty<string>());
            var fileNameAndFolders = foldersString + fileName;
            int index = 1;
            while (!existingFileNames.Add(fileNameAndFolders))
            {
                fileName = sanitizedTypeName + index + ".cs";
                fileNameAndFolders = foldersString + fileName;
                index++;
            }

            project = project.AddDocument(fileName, text, folders, Path.Combine(projectDirectory, fileName)).Project;
            return project;
        }

        private static string[] GetFolderChain(INamedTypeSymbol symbol)
        {
            var containingNamespace = symbol.ContainingNamespace;
            var folders = new List<string>();
            while (containingNamespace != null && !containingNamespace.IsGlobalNamespace)
            {
                if (!containingNamespace.CanBeReferencedByName)
                {
                    // namespace name is mangled - we don't want it
                    return null;
                }

                var sanitizedNamespaceName = Paths.SanitizeFolder(containingNamespace.Name);
                folders.Add(sanitizedNamespaceName);
                containingNamespace = containingNamespace.ContainingNamespace;
            }

            folders.Reverse();
            return folders.ToArray();
        }

        private static IEnumerable<ISymbol> GetTypes(INamespaceSymbol namespaceSymbol)
        {
            var results = new List<ISymbol>();
            EnumSymbols(namespaceSymbol, results.Add);
            return results;
        }

        private static void EnumSymbols(INamespaceSymbol namespaceSymbol, Action<ISymbol> action)
        {
            foreach (var subNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                EnumSymbols(subNamespace, action);
            }

            foreach (var topLevelType in namespaceSymbol.GetTypeMembers())
            {
                action(topLevelType);
            }
        }
    }
}
