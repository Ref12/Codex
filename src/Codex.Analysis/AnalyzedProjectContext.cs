using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Codex.Import;
using Codex.Sdk;
using Codex.Sdk.Utilities;
using Codex.Utilities;
using DotNext;
using Microsoft.CodeAnalysis;
using static Codex.Analysis.BoundSourceFileBuilder;
using static Codex.Analysis.Xml.Linq.XElementExtensions;

namespace Codex.Analysis
{
    using static CodexConstants;

    public class AnalyzedProjectContext
    {
        public readonly AnalyzedProjectInfo Project;

        public AnalyzedProjectContext(AnalyzedProjectInfo project)
        {
            Project = project;
        }

        public ConcurrentDictionary<ICodeSymbol, DefinitionSymbol> ReferenceDefinitionMap = new(CodeSymbol.SymbolEqualityComparer);

        public bool TryGetDefinition(ICodeSymbol symbol, out IDefinitionSymbol definition)
        {
            return Out.VarIf(ReferenceDefinitionMap.TryGetValue(symbol, out var ds), ds, out definition);
        }

        public ConcurrentDictionary<string, ReferencedProject> ReferencedProjects = new();

        private ConcurrentDictionary<string, NamespaceExtensionData> m_extensionData = new();

        public void ReportDocument(BoundSourceFile boundSourceFile, RepoFile file)
        {
            foreach (var reference in boundSourceFile.References)
            {

            }
        }

        public class NamespaceExtensionData : ExtensionData, ICodeSymbol
        {
            public string Namespace;
            public string Qualifier;
            private XElement NamespaceElement;

            public string ProjectId => Namespace;

            public SymbolId Id => default;

            public StringEnum<SymbolKinds> Kind => throw new NotImplementedException();

            public XElement GetNamespaceElement(XElement parent)
            {
                if (NamespaceElement?.Parent != parent)
                {
                    NamespaceElement = null;
                }

                NamespaceElement = NamespaceElement ?? parent.CreateChild("Namespace", el => el.AddAttribute("Name", Namespace));
                return NamespaceElement;
            }
        }

        public NamespaceExtensionData GetReferenceNamespaceData(ICodeSymbol key)
        {
            var namespaceSymbol = ReferenceDefinitionMap[key];
            NamespaceExtensionData extData = namespaceSymbol.ExtData as NamespaceExtensionData;
            if (extData == null)
            {
                extData = m_extensionData.GetOrAdd(key.Id.Value, k => new NamespaceExtensionData());
                extData.Namespace = namespaceSymbol.DisplayName;
                extData.Qualifier = namespaceSymbol.DisplayName + ".";
                namespaceSymbol.ExtData = extData;
            }

            return extData;
        }

        public async Task Finish(RepoProject repoProject)
        {
            foreach (var entry in ReferenceDefinitionMap)
            {
                var def = entry.Value;
                var project = ReferencedProjects.GetOrAdd(def.ProjectId, id => new ReferencedProject()
                {
                    ProjectId = id,
                    DisplayName = id,
                });

                project.Definitions.Add(def);
            }

            foreach (var referencedProject in ReferencedProjects.Values.OrderBy(rp => rp.ProjectId, StringComparer.OrdinalIgnoreCase))
            {
                if (referencedProject.ProjectId != Project.ProjectId)
                {
                    Project.ProjectReferences.Add(referencedProject);
                }
                else
                {
                    Project.Definitions.AddRange(referencedProject.Definitions);
                }

                // Remove all the namespaces
                referencedProject.Definitions.RemoveAll(ds => ds.Kind == SymbolKinds.Namespace);

                // Sort the definitions by qualified name
                referencedProject.Definitions.Sort((d1, d2) => d1.DisplayName.CompareTo(d2.DisplayName));
            }

            //CreateNamespaceFile();
            await CreateReferencedProjectFiles(repoProject);
        }

        private async Task CreateReferencedProjectFiles(RepoProject repoProject)
        {
            var projectToRefFileSymbol = new Dictionary<string, ReferenceSymbol>(StringComparer.OrdinalIgnoreCase);
            foreach (var project in ReferencedProjects.Values)
            {
                if (project.Definitions.Count == 0)
                {
                    continue;
                }

                var file = await CreateMetadataFile(
                    repoProject,
                    GetProjectReferenceSymbolsPath(project.ProjectId),
                    CreateReferenceProjectSymbolsXml(project, d => d.ExtData?.TryCast<NamespaceExtensionData>()?.Namespace ?? string.Empty));

                if (file != null)
                {
                    projectToRefFileSymbol[project.ProjectId] = new ReferenceSymbol(file.FileDefinitionSymbol);
                }
            }

            if (ReferencedProjects.Count != 0)
            {
                await CreateMetadataFile(
                    repoProject,
                    ReferencedProjectsXmlFileName,
                    CreateReferenceProjectsXml(ReferencedProjects.Values, project => projectToRefFileSymbol.TryGetValue(project.ProjectId, out var fileRef) ? fileRef : null));
            }
        }

        private static readonly char[] MemberSeparators = ['.', ':'];

        public static XElement CreateReferenceProjectSymbolsXml(ReferencedProject project, Func<DefinitionSymbol, string> getNamespace)
        {
            return Element("ReferenceSymbols")
            .AddAttribute("Count", project.Definitions.Count)
            .ForEach(project.Definitions.SortedBufferGroupBy(getNamespace), (el, group) =>
            {
                var (ns, defs) = group;
                var container = new XElement("Namespace").AddAttribute("Name", ns);
                el.Add(container);

                foreach (var definition in defs)
                {
                    ReferenceSymbol reference = new ReferenceSymbol(definition);
                    reference.ReferenceKind = ReferenceKind.ProjectLevelReference;
                    reference.ExcludeFromSearch = true;
                    container.AddElement("Symbol", symbolElement =>
                        symbolElement
                        .AddAttribute("Name", definition.DisplayName
                            .TrimStart(ns).TrimStart(MemberSeparators), reference)
                        .AddAttribute("ReferenceCount", definition.ReferenceCount.ToString()));
                }
            });
        }

        public static XElement CreateReferenceProjectsXml(IEnumerable<IReferencedProject> referencedProjects, Func<IReferencedProject, ReferenceSymbol?> getProjectAnnotation)
        {
            return Element("ReferenceProjects").ForEach(referencedProjects
                .OrderBy(rp => rp.ProjectId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(rp => rp.DisplayName, StringComparer.OrdinalIgnoreCase), (el, project) =>
                {
                    var fileRef = getProjectAnnotation(project);

                    el.AddElement("Project", projElement =>
                        projElement
                        .AddAttribute("ReferenceCount", project.Definitions.Count.ToString(), fileRef)
                        .AddAttribute("Name", project.ProjectId, fileRef)
                        .AddAttribute("FullName", project.DisplayName));
                });
        }

        public static BoundSourceFileBuilder CreateMetadataFile(string projectId, string fileName, XElement annotatedFileContent, bool analyze = false)
        {
            // Maybe create a .cdx folder under the project with metadata files.
            // .cdx folder's repo relative path would be {Project.RepoRelativePath}\.cdx
            Placeholder.Todo("Figure out what to do about repo/project relative path for metadata files.");
            var metadataFileBuilder = annotatedFileContent.CreateAnnotatedSourceBuilder(
                new SourceFileInfo()
                {
                    RepoRelativePath = $@"\\Codex\ProjectMetadata\{projectId}\{fileName}",
                    ProjectRelativePath = GetMetadataFilePath(fileName),
                    Language = "xml",
                }, projectId);

            metadataFileBuilder.BoundSourceFile.Flags |= BoundSourceFlags.DisableTextIndexing;
            metadataFileBuilder.BoundSourceFile.Flags |= BoundSourceFlags.GeneratedMetadataFile;
            metadataFileBuilder.BoundSourceFile.ExcludeFromSearch = true;

            if (analyze)
            {
                XmlAnalyzer.Analyze(metadataFileBuilder);
            }

            return metadataFileBuilder;
        }

        private async Task<BoundSourceFileBuilder> CreateMetadataFile(RepoProject project, string fileName, XElement annotatedFileContent)
        {
            var metadataFileBuilder = CreateMetadataFile(project.ProjectId, fileName, annotatedFileContent);

            var repoFile = project.AddFile(
                metadataFileBuilder.SourceFile.Info.RepoRelativePath,
                metadataFileBuilder.SourceFile.Info.ProjectRelativePath);

            repoFile.InMemorySourceFileBuilder = metadataFileBuilder;

            await repoFile.Analyze();

            repoFile.InMemorySourceFileBuilder = null;

            return metadataFileBuilder;
        }
    }
}