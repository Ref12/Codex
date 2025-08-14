using System;
using System.Linq;
using System.Text;
using System.Web;
using Codex;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Storage;
using Codex.Utilities;
using Codex.View;

namespace Codex.Web.Mvc.Rendering
{
    public static class TreeViewRenderer
    {
        private static Comparer<IDefinitionSymbol> DisplayNameSymbolComparer = new ComparerBuilder<IDefinitionSymbol>()
            .CompareByAfter(r => r.ContainerQualifiedName)
            .CompareByAfter(r => r.DisplayName);

        public static TreeViewModel GenerateNamespaceExplorer(List<IDefinitionSymbol> projectDefinitions)
        {
            var tree = new TreeViewModel(LeftPaneMode.namespaces);

            projectDefinitions.Sort(DisplayNameSymbolComparer);
            projectDefinitions.SortedDedupe(DisplayNameSymbolComparer);

            var tempRoot = new TreeNodeViewModel();

            int current = 0;
            GenerateViewModelCore(tempRoot, maxExpansionDepth: 1, projectDefinitions.AsSegment(), ref current, -1, static s => s.ContainerQualifiedName);

            foreach (var (ns, defs) in tempRoot.Children.SortedBufferGroupBy(d => d.Definition.ContainerQualifiedName))
            {
                var namespaceNode = new TreeNodeViewModel(ns, Glyph.Namespace)
                {
                    Kind = SymbolKinds.Namespace.ToStringEnum().ToDisplayString(),
                    Expanded = true
                };

                tree.Root.Children.Add(namespaceNode);
                namespaceNode.Children.Add(defs);
            }

            return tree;
        }

        public static TreeViewModel GenerateDocumentOutline(IBoundSourceFile boundSourceFile)
            => GenerateDocumentOutline(boundSourceFile.Definitions, boundSourceFile.SourceFile.Info);

        public static TreeViewModel GenerateDocumentOutline(IReadOnlyList<IDefinitionSpan> Definitions, IProjectFileScopeEntity? File)
        {
            var tree = new TreeViewModel(LeftPaneMode.outline);

            int current = 0;
            GenerateViewModelCore(
                tree.Root,
                maxExpansionDepth: int.MaxValue,
                Definitions.SelectList(ds => ds.Definition).AsSegment(),
                ref current,
                -1,
                file: File,
                getParentPrefix: static s => s.ContainerQualifiedName);

            return tree;
        }

        private static void GenerateViewModelCore(
            TreeNodeViewModel parent,
            int maxExpansionDepth,
            ListSegment<IDefinitionSymbol> definitions,
            ref int current,
            int parentDepth,
            Func<IDefinitionSymbol, string> getParentPrefix = null,
            IProjectFileScopeEntity? file = null)
        {
            for (; current < definitions.Count; current++)
            {
                int nextIndex = current + 1;
                var symbol = definitions[current];

                if (string.IsNullOrEmpty(symbol.DisplayName))
                {
                    continue;
                }

                var depth = symbol.SymbolDepth;

                if (depth <= parentDepth)
                {
                    current--;
                    return;
                }

                var text = symbol.DisplayName;
                var parentPrefix = getParentPrefix?.Invoke(symbol) ?? "";

                if (text.StartsWith(parentPrefix) && text.CharAt(parentPrefix.Length) == '.')
                {
                    text = text.Substring(parentPrefix.Length + 1);
                }

                bool hasChildren = nextIndex != definitions.Count &&
                    definitions[nextIndex].SymbolDepth > depth;

                var node = new TreeNodeViewModel(symbol, file)
                {
                    Name = text,
                    Expanded = depth < maxExpansionDepth
                };

                parent.Children.Add(node);

                if (hasChildren)
                {
                    current++;
                    GenerateViewModelCore(node, maxExpansionDepth, definitions, ref current, depth, static s => s.ContainerQualifiedName, file);
                }
            }
        }
    }
}