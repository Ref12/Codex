using System.Collections.Immutable;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Codex.Analysis
{
    public static class DisplayFormats
    {
        public static readonly SymbolDisplayFormat ShortNameDisplayFormat = new SymbolDisplayFormat(
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters)
            .WithDefaults();

        // TODO: Figure this out. May need different format between types and members
        public static readonly SymbolDisplayFormat DeclarationNameDisplayFormat =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
                kindOptions: (SymbolDisplayKindOptions)(-1),
                delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
                propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
                genericsOptions: (SymbolDisplayGenericsOptions)(-1),
                memberOptions: (SymbolDisplayMemberOptions)(-1),
                parameterOptions: (SymbolDisplayParameterOptions)(-1),
                // Not showing the name is important because we visit parameters to display their
                // types. If we visited their types directly, we wouldn't get ref/out/params.
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseAsterisksInMultiDimensionalArrays |
                    SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName).WithDefaults();

        public static readonly SymbolDisplayFormat TypeDeclarationNameDisplayFormat =
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                kindOptions: (SymbolDisplayKindOptions) (-1),
                delegateStyle: SymbolDisplayDelegateStyle.NameAndSignature,
                propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
                genericsOptions: (SymbolDisplayGenericsOptions) (-1),
                memberOptions: (SymbolDisplayMemberOptions) (-1),
                parameterOptions: (SymbolDisplayParameterOptions) (-1),
                // Not showing the name is important because we visit parameters to display their
                // types. If we visited their types directly, we wouldn't get ref/out/params.
                miscellaneousOptions:
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.UseAsterisksInMultiDimensionalArrays |
                SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName).WithDefaults();

        public static readonly SymbolDisplayFormat TypeNameDisplayFormat = new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters).WithDefaults();

        public static readonly SymbolDisplayFormat QualifiedNameDisplayFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters).WithDefaults();

        public static readonly SymbolDisplayFormat MemberQualifiedNameDisplayFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters).WithDefaults();

        public static readonly SymbolDisplayFormat CSharpDisplayFormat = GetCSharpDisplayFormat(SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);
        public static readonly SymbolDisplayFormat CSharpMemberDisplayFormat = GetCSharpDisplayFormat(SymbolDisplayTypeQualificationStyle.NameAndContainingTypes);

        public static SymbolDisplayFormat GetCSharpDisplayFormat(SymbolDisplayTypeQualificationStyle typeQualificationStyle) =>
            new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
                typeQualificationStyle: typeQualificationStyle,
                propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                memberOptions:
                    SymbolDisplayMemberOptions.IncludeParameters |
                    SymbolDisplayMemberOptions.IncludeContainingType |
                    SymbolDisplayMemberOptions.IncludeExplicitInterface,
                parameterOptions:
                    SymbolDisplayParameterOptions.IncludeParamsRefOut |
                    SymbolDisplayParameterOptions.IncludeType,
                // Not showing the name is important because we visit parameters to display their
                // types. If we visited their types directly, we wouldn't get ref/out/params.
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                    SymbolDisplayMiscellaneousOptions.UseAsterisksInMultiDimensionalArrays |
                    SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName).WithDefaults();

        public static string ToDeclarationName(this ISymbol symbol)
        {
            var parts = ImmutableArray<SymbolDisplayPart>.Empty.ToBuilder();
            if (symbol.Kind == SymbolKind.NamedType)
            {
                AddAccessibility(symbol, parts);
                parts.AddRange(symbol.ToSymbolDisplayParts(TypeDeclarationNameDisplayFormat).ToBuilder());
            }
            else
            {
                parts.AddRange(symbol.ToSymbolDisplayParts(DeclarationNameDisplayFormat).ToBuilder());
            }


            return parts.ToImmutableArray().ToDisplayString();
        }

        private static void AddAccessibility(ISymbol symbol, ImmutableArray<SymbolDisplayPart>.Builder parts)
        {
            switch (symbol.DeclaredAccessibility)
            {
                case Accessibility.Private:
                    parts.AddKeyword(SyntaxKind.PrivateKeyword);
                    break;
                case Accessibility.Internal:
                    parts.AddKeyword(SyntaxKind.InternalKeyword);
                    break;
                case Accessibility.ProtectedAndInternal:
                case Accessibility.Protected:
                    parts.AddKeyword(SyntaxKind.ProtectedKeyword);
                    break;
                case Accessibility.ProtectedOrInternal:
                    parts.AddKeyword(SyntaxKind.ProtectedKeyword);
                    parts.AddSpace();
                    parts.AddKeyword(SyntaxKind.InternalKeyword);
                    break;
                case Accessibility.Public:
                    parts.AddKeyword(SyntaxKind.PublicKeyword);
                    break;
                default:
                    return;
            }

            parts.AddSpace();
        }

        public static void AddKeyword(this ImmutableArray<SymbolDisplayPart>.Builder parts, SyntaxKind kind)
        {
            parts.Add(new SymbolDisplayPart(SymbolDisplayPartKind.Keyword, null, SyntaxFacts.GetText(kind)));
        }

        public static void AddSpace(this ImmutableArray<SymbolDisplayPart>.Builder parts)
        {
            parts.Add(new SymbolDisplayPart(SymbolDisplayPartKind.Space, null, " "));
        }

        public static SymbolDisplayFormat WithDefaults(this SymbolDisplayFormat format)
        {
            return format
                    .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.ExpandValueTuple)
                    .AddCompilerInternalOptions(SymbolDisplayCompilerInternalOptions.UseNativeIntegerUnderlyingType);
        }

        public static SymbolDisplayFormat GetDisplayFormat(ISymbol symbol)
        {
            var specialType = symbol.As<ITypeSymbol>()?.SpecialType;
            bool isSpecialType = specialType.GetValueOrDefault(SpecialType.None) != SpecialType.None;

            if (isSpecialType)
            {
                return QualifiedNameDisplayFormat;
            }

            if (symbol.IsMemberSymbol())
            {
                Placeholder.Todo("Should we use the new display format for members?");
                // TODO: Maybe some client side processing of annotated display format would be more effective.
                //return CSharpMemberDisplayFormat;
            }

            return CSharpDisplayFormat;
        }
    }
}
