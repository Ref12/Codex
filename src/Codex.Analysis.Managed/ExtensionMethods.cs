using Codex.Analysis.Managed;
using Codex.Analysis.Projects;
using Codex.Utilities;
using Codex.Utilities.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Codex.Analysis
{
    public static class ExtensionMethods
    {
        private static readonly ClassificationName[] _symbolDisplayClassificationMap = CreateSymbolDisplayClassificationMap();

        public static ReadOnlySpan<ClassificationName> SymbolDisplayClassificationMap => _symbolDisplayClassificationMap;

        private static ClassificationName[] CreateSymbolDisplayClassificationMap()
        {
            var kinds = Enum.GetValues<SymbolDisplayPartKind>();

            var map = new ClassificationName[kinds.Select(k => (int)k).Max() + 1];

            foreach (var kind in kinds)
            {
                StringEnum<ClassificationName> classification = kind.ToString();
                map[(int)kind] = classification.Value ?? default;
            }

            return map;
        }

        public static Extent GetDeclarationExtent(this SyntaxNode node)
        {
            var span = node.Span.ToExtent();
            if (node.HasLeadingTrivia)
            {
                int start = span.Start;
                foreach (var trivia in node.GetLeadingTrivia())
                {
                    if (!(trivia.IsEquivalentKind(CS.SyntaxKind.WhitespaceTrivia) || trivia.IsEquivalentKind(CS.SyntaxKind.EndOfLineTrivia)))
                    {
                        start = trivia.SpanStart;
                        break;
                    }
                }

                span.SetStart(Math.Min(start, span.Start));
            }

            return span;
        }

        public static Extent ToExtent(this TextSpan span)
        {
            return new Extent(span.Start, span.Length);
        }

        public static ClassificationName GetClassification(this SymbolDisplayPart part)
        {
            return SymbolDisplayClassificationMap[(int)part.Kind];
        }

        public static IReadOnlyList<ClassifiedExtent> GetClassifications(this ImmutableArray<SymbolDisplayPart> parts)
        {
            return parts.SelectArray(p => new ClassifiedExtent(p.GetClassification(), p.ToString().Length));
        }

        public static string GetDisplayString(this ISymbol symbol)
        {
            return symbol.GetDisplayParts().ToDisplayString();
        }

        public static ImmutableArray<SymbolDisplayPart> GetDisplayParts(this ISymbol symbol)
        {
            return symbol.ToSymbolDisplayParts(DisplayFormats.GetDisplayFormat(symbol));
        }

        public static string ToSymbolDisplayString(this ISymbol symbol, SymbolDisplayFormat format = null)
        {
            return CS.SymbolDisplay.ToDisplayString(symbol, format);
        }

        public static ImmutableArray<SymbolDisplayPart> ToSymbolDisplayParts(this ISymbol symbol, SymbolDisplayFormat format = null)
        {
            const string Params = "params";
            var parts = CS.SymbolDisplay.ToDisplayParts(symbol, format);
            if (parts.Any(static part => part.ToString() == Params))
            {
                using var _ = ArrayPool<SymbolDisplayPart>.Shared.Lease(parts.Length, out var array);
                SpanBuilder<SymbolDisplayPart> builder = new(array);

                bool lastWasParams = false;
                foreach (var part in parts)
                {
                    if (part.ToString() == Params)
                    {
                        lastWasParams = true;
                        continue;
                    }

                    if (lastWasParams && part.Kind == SymbolDisplayPartKind.Space)
                    {
                        lastWasParams = false;
                        continue;
                    }

                    builder.Add(part);
                    lastWasParams = false;
                }

                parts = ImmutableArray.ToImmutableArray(builder.Span);
            }

            return parts;
        }

        public static string GetQualifiedName(this ISymbol symbol)
        {
            return symbol.ToSymbolDisplayString(DisplayFormats.QualifiedNameDisplayFormat);
        }

        public static string GetProjectId(this ISymbol symbol)
        {
            return symbol.ContainingAssembly?.Name ?? "Unknown_Project";
        }

        public static SymbolSpec GetSymbolSpec(this SyntaxToken token, bool isThis)
        {
            var spec = SymbolSpec.None;
            if (!isThis)
            {
                if (token.IsEquivalentKind(CS.SyntaxKind.ThisKeyword))
                {
                    spec = SymbolSpec.This;
                }
                else if (token.IsEquivalentKind(CS.SyntaxKind.BaseKeyword))
                {
                    spec = SymbolSpec.Base;
                }
            }

            return spec;
        }

        public static bool SymbolEquals(this ISymbol symbol, ISymbol other, bool includeNullability = false)
        {
            if (other == null)
            {
                return symbol == null;
            }

            return includeNullability
                ? SymbolEqualityComparer.IncludeNullability.Equals(symbol, other)
                : SymbolEqualityComparer.Default.Equals(symbol, other);
        }

        public static bool TryGetTargetFramework(this Project project, out ProjectTargetFramework framework)
        {
            return project.ParseOptions.TryGetTargetFramework(out framework);
        }

        public static bool TryGetTargetFramework(this ProjectInfo project, out ProjectTargetFramework framework)
        {
            return project.ParseOptions.TryGetTargetFramework(out framework);
        }

        public static bool TryGetTargetFramework(this ParseOptions? options, out ProjectTargetFramework framework)
        {
            framework = null;
            foreach (var symbolNames in options?.PreprocessorSymbolNames ?? Array.Empty<string>())
            {
                if (ProjectTargetFramework.All.TryGetValue(symbolNames, out var match))
                {
                    if (framework == null || match.Priority > framework.Priority)
                    {
                        framework = match;
                    }
                }
            }

            return framework != null;
        }

        public static bool IsMemberSymbol(this ISymbol symbol)
        {
            switch (symbol.Kind)
            {
                case SymbolKind.Method:
                case SymbolKind.Event:
                case SymbolKind.Field:
                case SymbolKind.Property:
                    return true;
            }

            return false;
        }

        public static bool TryGetIdentifier(this SyntaxNode name, out SyntaxToken identifier)
        {
            if (name.IsVB())
            {
                return TrySelect(name, out identifier, (VBS.IdentifierNameSyntax node) => node.Identifier)
                    || TrySelect(name, out identifier, (VBS.QualifiedNameSyntax node) => node.Right.Identifier);
            }
            else
            {
                if (name.IsKind(CS.SyntaxKind.NullableType)
                    && TrySelect(name, out var typeSyntax, (CSS.NullableTypeSyntax node) => node.ElementType))
                {
                    name = typeSyntax;
                }

                return TrySelect(name, out identifier, (CSS.IdentifierNameSyntax node) => node.Identifier)
                    || TrySelect(name, out identifier, (CSS.QualifiedNameSyntax node) => node.Right.Identifier);
            }
        }

        public static SyntaxWrapper<TSyntax> WrapAs<TSyntax>(this SyntaxNode node)
        {
            return new(node);
        }

        public static bool TrySelect<TCS, TVB, TResult>(this SyntaxNode node, out TResult result, Func<TCS, TResult> csSelect, Func<TVB, TResult> vbSelect)
        {
            if (node.IsVB())
            {
                return node.TrySelect(out result, vbSelect);
            }
            else
            {
                return node.TrySelect(out result, csSelect);
            }
        }

        public static bool TrySelect<TNode, TResult>(this SyntaxNode node, out TResult result, Func<TNode, TResult> select)
        {
            if (SyntaxMappings.IsPossibly<TNode>(node) && node is TNode typedNode)
            {
                result = select(typedNode);
                return true;
            }

            result = default;
            return false;
        }

        public static bool TrySelectCS<TNode, TResult>(this SyntaxNode node, out TResult result, Func<TNode, TResult> select)
        {
            if (node.IsCS() && SyntaxMappings.IsPossibly<TNode>(node) && node is TNode typedNode)
            {
                result = select(typedNode);
                return true;
            }

            result = default;
            return false;
        }

        public static bool HasAncestorOrSelf<TCS, TVB>(this SyntaxNode node)
            where TCS : CS.CSharpSyntaxNode
            where TVB : VB.VisualBasicSyntaxNode
        {
            return TryGetAncestorOrSelf<TCS, TVB>(node, out _);
        }

        public static bool TryGetAncestorOrSelf<TCS, TVB>(this SyntaxNode node, out SyntaxNode result)
            where TCS : CS.CSharpSyntaxNode
            where TVB : VB.VisualBasicSyntaxNode
        {
            if (node.IsVB())
            {
                result = node.FirstAncestorOrSelf<TVB>();
            }
            else
            {
                result = node.FirstAncestorOrSelf<TCS>();
            }

            return result != null;
        }

        public static bool TryGetAncestorOrSelf<TCS>(this SyntaxNode node, out TCS result)
            where TCS : CS.CSharpSyntaxNode
        {
            if (node.IsCS())
            {
                result = node.FirstAncestorOrSelf<TCS>();
                return result != null;
            }

            result = default;
            return false;
        }

        public static bool TryGetAncestorOrSelfVB<TVB>(this SyntaxNode node, out SyntaxNode result)
            where TVB : VB.VisualBasicSyntaxNode
        {
            if (node.IsVB())
            {
                result = node.FirstAncestorOrSelf<TVB>();
                return result != null;
            }

            result = default;
            return false;
        }

        public static bool IsEquivalentKind(this SyntaxToken node, CS.SyntaxKind kind)
        {
            return IsEquivalentKind(node.RawKind, kind, node.IsVB());
        }

        public static bool IsEquivalentKind(this SyntaxTrivia node, CS.SyntaxKind kind)
        {
            return IsEquivalentKind(node.RawKind, kind, isVb: LanguageName.VisualBasic == node.Language);
        }

        public static bool IsEquivalentKind(this SyntaxNode node, CS.SyntaxKind kind)
        {
            if (node == null)
            {
                return false;
            }

            return IsEquivalentKind(node.RawKind, kind, node.IsVB());
        }

        private static bool IsEquivalentKind(int nodeKind, CS.SyntaxKind kind, bool isVb)
        {
            int rawKind = (int)kind;
            if (isVb)
            {
                rawKind = (int)GetVBSyntaxKind(kind);
            }

            return nodeKind == rawKind;
        }

        public static bool IsVB(this SyntaxToken node) => LanguageName.VisualBasic == node.Language;
        public static bool IsCS(this SyntaxToken node) => LanguageName.CSharp == node.Language;
        public static bool IsVB(this SyntaxNode node) => LanguageName.VisualBasic == node.Language;
        public static bool IsCS(this SyntaxNode node) => LanguageName.CSharp == node.Language;

        public static VB.SyntaxKind GetVBSyntaxKind(this CS.SyntaxKind kind)
        {
            switch (kind)
            {
                case CS.SyntaxKind.SimpleMemberAccessExpression:
                    return VB.SyntaxKind.SimpleMemberAccessExpression;
                case CS.SyntaxKind.ObjectCreationExpression:
                    return VB.SyntaxKind.ObjectCreationExpression;
                case CS.SyntaxKind.OverrideKeyword:
                    return VB.SyntaxKind.OverridesKeyword;
                case CS.SyntaxKind.NewKeyword:
                    return VB.SyntaxKind.NewKeyword;
                case CS.SyntaxKind.PartialKeyword:
                    return VB.SyntaxKind.PartialKeyword;
                case CS.SyntaxKind.ThisKeyword:
                    return VB.SyntaxKind.MeKeyword;
                case CS.SyntaxKind.BaseKeyword:
                    return VB.SyntaxKind.MyBaseKeyword;
                case CS.SyntaxKind.WhitespaceTrivia:
                    return VB.SyntaxKind.WhitespaceTrivia;
                case CS.SyntaxKind.EndOfLineTrivia:
                    return VB.SyntaxKind.EndOfLineTrivia;
                default:
                    throw new ArgumentException($"Can't convert {kind} to VB Syntax Kind");
            }
        }


        public static int GetSymbolDepth(this ISymbol symbol)
        {
            ISymbol current = symbol.ContainingSymbol;
            int depth = 0;
            while (current != null
                // we don't want namespaces to add to our "depth" because they won't be displayed in the tree
                && current is not INamespaceSymbol)
            {
                depth++;

                current = current.ContainingSymbol;
            }

            return depth;
        }

        public record struct SyntaxWrapper<TNode>(SyntaxNode Node)
        {
            public bool TrySelect<TResult>(Func<TNode, TResult> select, out TResult result)
            {
                return Node.TrySelect(out result, select);
            }

            public TResult? Select<TResult>(Func<TNode, TResult> select)
            {
                Node.TrySelect(out var result, select);
                return result;
            }
        }

        public record struct LanguageName(bool IsCSharp)
        {
            public static readonly LanguageName VisualBasic = new(false);
            public static readonly LanguageName CSharp = new(true);

            public string Name { get; } = IsCSharp
                ? LanguageNames.CSharp
                : LanguageNames.VisualBasic;

            public static implicit operator LanguageName(string value)
            {
                return new(IsCSharp: !ReferenceEquals(value, LanguageNames.VisualBasic));
            }

            public static bool operator ==(LanguageName name, string value)
            {
                return object.ReferenceEquals(name.Name, value);
            }

            public static bool operator !=(LanguageName name, string value)
            {
                return !object.ReferenceEquals(name.Name, value);
            }
        }

        public static class SyntaxMappings
        {
            static SyntaxMappings()
            {
                SyntaxMapping<CSS.EqualsValueClauseSyntax>.CSKind = CS.SyntaxKind.EqualsValueClause;
                SyntaxMapping<CSS.NullableTypeSyntax>.CSKind = CS.SyntaxKind.NullableType;
                SyntaxMapping<CSS.WithExpressionSyntax>.CSKind = CS.SyntaxKind.WithExpression;
                SyntaxMapping<CSS.IdentifierNameSyntax>.CSKind = CS.SyntaxKind.IdentifierName;
                SyntaxMapping<CSS.QualifiedNameSyntax>.CSKind = CS.SyntaxKind.QualifiedName;
                SyntaxMapping<CSS.CastExpressionSyntax>.CSKind = CS.SyntaxKind.CastExpression;
                SyntaxMapping<VBS.DirectCastExpressionSyntax>.VBKind = VB.SyntaxKind.DirectCastExpression;
            }

            public static bool IsPossibly<T>(SyntaxNode node)
            {
                return SyntaxMapping<T>.RawKind == 0 || node.RawKind == SyntaxMapping<T>.RawKind;
            }

            private static class SyntaxMapping<TSyntax>
            {
                public static int RawKind;
                public static VB.SyntaxKind VBKind { get => (VB.SyntaxKind)RawKind; set => RawKind = (int)value; }
                public static CS.SyntaxKind CSKind { get => (CS.SyntaxKind)RawKind; set => RawKind = (int)value; }
            }
        }
    }
}
