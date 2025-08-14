using System.Text;
using Codex.Sdk.Search;
using Codex.Utilities;
using Codex.View;
using Folder = Codex.Web.Mvc.Rendering.Folder<string>;

namespace Codex.Web.Mvc.Rendering
{
    using static ViewModelAddress;

    public partial class ProjectExplorerRenderer
    {
        public GetProjectResult getProjectResult;
        private IAnalyzedProjectInfo projectContents;
        private readonly IEnumerable<string> referencingProjects;

        public ProjectExplorerRenderer(GetProjectResult getProjectResult)
        {
            this.getProjectResult = getProjectResult;
            this.projectContents = getProjectResult.Project;
            this.referencingProjects = getProjectResult.ReferencingProjects
                .Select(s => s.ProjectId)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);
        }

        public TreeViewModel GenerateViewModel()
        {
            var tree = new TreeViewModel<GetProjectResult>(getProjectResult, LeftPaneMode.project);
            var root = CreateFileHierarchyNodes(tree.Root);
            root.Expanded = true;

            CreateReferencesNode(root);
            CreateUsedByNode(root);
            CreateMetadataNode(root);

            var props = tree.Properties;

            var projectFile = projectContents.PrimaryFile?.RepoRelativePath;
            if (projectFile != null)
            {
                props["Project"] = projectFile;
            }

            props["Files"] = projectContents.Files.Count.WithThousandSeparators();
            //sb.AppendLine("Lines&nbsp;of&nbsp;code:&nbsp;" + projectContents.SourceLineCount.WithThousandSeparators() + "<br>");
            //sb.AppendLine("Bytes:&nbsp;" + BytesOfCode.WithThousandSeparators() + "<br>");
            //sb.AppendLine("Declared&nbsp;symbols:&nbsp;" + projectContents.SymbolCount.WithThousandSeparators() + "<br>");
            props["Declared symbols"] = projectContents.DefinitionCount.WithThousandSeparators();
            //sb.AppendLine("Declared&nbsp;types:&nbsp;" + namedTypes.Count().WithThousandSeparators() + "<br>");
            //sb.AppendLine("Public&nbsp;types:&nbsp;" + namedTypes.Where(t => t.DeclaredAccessibility == Accessibility.Public).Count().WithThousandSeparators() + "<br>");
            if (getProjectResult.DateUploaded != default(DateTime))
            {
                props["Indexed on"] = getProjectResult.DateUploaded.ToLocalTime().ToString("MMMM dd");
            }

            return tree;
        }

        private void CreateUsedByNode(TreeNodeViewModel root)
        {
            var trimmed = referencingProjects.Take(100).ToArray();
            var totalCount = referencingProjects.Count();
            var title = $"Used By ({totalCount})";
            if (trimmed.Length < totalCount)
            {
                title = $"Used By (displaying {trimmed.Length} of {totalCount})";
            }

            CreateReferencesNode(root, title, 1, getProjectResult.ReferencingProjects);
        }

        private void CreateReferencesNode(TreeNodeViewModel root)
        {
            CreateReferencesNode(root, "References", 0, projectContents.ProjectReferences);
        }

        private void CreateReferencesNode(TreeNodeViewModel root, string title, int index, IEnumerable<IProjectScopeEntity> references)
        {
            var folder = root.CreateFolder(title, Glyph.Assembly, index);

            foreach (var reference in references)
            {
                folder.Children.Add(new TreeNodeViewModel(reference));
            }
        }

        private void CreateMetadataNode(TreeNodeViewModel root)
        {
            var folder = root.GetOrCreateFolder("[Metadata]", Glyph.Metadata);
            folder.Name = "Metadata";
            folder.Glyph = Glyph.Metadata;
            root.Children.Remove(folder);
            root.Children.Insert(2, folder);

            if (getProjectResult.GenerateReferenceMetadata)
            {
                var refSymbolsFolder = folder.GetOrCreateFolder("Reference Symbols", Glyph.ReferenceGroup);

                foreach (var referencedProject in projectContents.ProjectReferences)
                {
                    if (referencedProject.DefinitionCount > 0) continue;

                    refSymbolsFolder.Children.Add(new TreeNodeViewModel($"{referencedProject.ProjectId}.xml", Glyph.ShowReferencedElements)
                    {
                        NavigateAddress = ViewReferenceSymbolsXml(projectContents.ProjectId, referencedProject.ProjectId)
                    });
                }

                folder.Children.Add(new TreeNodeViewModel(CodexConstants.ReferencedProjectsXmlFileName, Glyph.ShowReferencedElements)
                {
                    NavigateAddress = ViewReferenceProjectsXml(getProjectResult.Project.ProjectId)
                });
            }
        }

        private void CreateReferenceSymbolsMetadataNode(TreeNodeViewModel root, string title, int index, IEnumerable<IProjectScopeEntity> references)
        {
            var folder = root.CreateFolder(title, Glyph.Assembly, index);

            foreach (var reference in references)
            {
                folder.Children.Add(new TreeNodeViewModel(reference));
            }
        }

        private TreeNodeViewModel CreateFileHierarchyNodes(TreeNodeViewModel rootContainer)
        {
            var glyph = projectContents.PrimaryFile?.ProjectRelativePath?.EndsWithIgnoreCase(".vbproj") == true
                ? Glyph.BasicProject
                : Glyph.CSharpProject;

            var root = rootContainer.GetOrCreateFolder(projectContents.ProjectId, glyph);

            foreach (var file in projectContents.Files)
            {
                var parts = file.ProjectRelativePath.Split('\\');
                AddDocumentToFolder(root, file, parts.Take(parts.Length - 1).ToArray());
            }

            root.Sort((l, r) => StringComparer.OrdinalIgnoreCase.Compare(l.Name, r.Name));
            return root;
        }

        private void AddDocumentToFolder(TreeNodeViewModel folder, IProjectFileScopeEntity document, ReadOnlySpan<string> subfolders)
        {
            if (subfolders.Length == 0)
            {
                folder.Children.Add(new TreeNodeViewModel(projectContents.ProjectId, document)
                {
                    SortGroup = FileSortGroup
                });
                return;
            }

            if (subfolders[0].EndsWith(":"))
            {
                return;
            }

            var folderName = Paths.SanitizeFolder(subfolders[0]);
            var subfolder = folder.GetOrCreateFolder(folderName);
            if (folderName.StartsWithIgnoreCase("["))
            {
                subfolder.SortGroup = SpecialFolderSortGroup;
            }

            AddDocumentToFolder(subfolder, document, subfolders.Slice(1));
        }

        const int SpecialFolderSortGroup = -1;
        const int FolderSortGroup = 0;
        const int FileSortGroup = 2;
    }
}