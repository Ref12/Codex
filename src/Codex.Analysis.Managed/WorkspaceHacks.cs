using System;
using System.Linq;
using System.Reflection;
using Codex.ObjectModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
//using System.ComponentModel.Composition.Hosting;

using Glyph = Codex.ObjectModel.Glyph;

namespace Codex.Utilities
{
    public static class WorkspaceHacks
    {
        public static Lazy<HostServices> Pack { get; private set; }

        static WorkspaceHacks()
        {
            Pack = new Lazy<HostServices>(() =>
            {
                var assemblyNames = new[]
                {
                "Microsoft.CodeAnalysis.Workspaces",
                "Microsoft.CodeAnalysis.Workspaces.Desktop",
                "Microsoft.CodeAnalysis.CSharp.Workspaces",
                //"Microsoft.CodeAnalysis.CSharp.Workspaces.Desktop",
                //"Microsoft.CodeAnalysis.VisualBasic.Workspaces",
                //"Microsoft.CodeAnalysis.VisualBasic.Workspaces.Desktop",
                "Microsoft.CodeAnalysis",
                "Microsoft.CodeAnalysis.CSharp",
                //"Microsoft.CodeAnalysis.VisualBasic",

                "Microsoft.CodeAnalysis.Workspaces",
                "Microsoft.CodeAnalysis.Workspaces.Desktop",
                "Microsoft.CodeAnalysis.CSharp.Workspaces",
                "Microsoft.CodeAnalysis.VisualBasic.Workspaces",
                "Microsoft.CodeAnalysis.Features",
                "Microsoft.CodeAnalysis.CSharp.Features",
                "Microsoft.CodeAnalysis.VisualBasic.Features"
            }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var assemblies = assemblyNames
                    .Select(n => Assembly.Load(n));
                return MefHostServices.Create(assemblies);
            });
        }

        public static T GetLanguageService<T>(this Workspace workspace, string language = LanguageNames.CSharp)
            where T : ILanguageService
        {
            var languageServices = workspace.Services.GetLanguageServices(language);
            return languageServices.GetService<T>();
        }

        public static TSymbol As<TSymbol>(this ISymbol symbol)
            where TSymbol : class, ISymbol
        {
            return symbol as TSymbol;
        }

        public static Glyph GetDisplayGlyph(this ISymbol symbol)
        {
            Glyph publicIcon;

            switch (symbol.Kind)
            {
                case SymbolKind.Alias:
                    return ((IAliasSymbol)symbol).Target.GetDisplayGlyph();

                case SymbolKind.Assembly:
                    return Glyph.Assembly;

                case SymbolKind.ArrayType:
                    return ((IArrayTypeSymbol)symbol).ElementType.GetDisplayGlyph();

                case SymbolKind.DynamicType:
                    return Glyph.ClassPublic;

                case SymbolKind.Event:
                    publicIcon = Glyph.EventPublic;
                    break;

                case SymbolKind.Field:
                    var containingType = symbol.ContainingType;
                    if (containingType != null && containingType.TypeKind == TypeKind.Enum)
                    {
                        return Glyph.EnumMember;
                    }

                    publicIcon = ((IFieldSymbol)symbol).IsConst ? Glyph.ConstantPublic : Glyph.FieldPublic;
                    break;

                case SymbolKind.Label:
                    return Glyph.Label;

                case SymbolKind.Local:
                    return Glyph.Local;

                case SymbolKind.NamedType:
                case SymbolKind.ErrorType:
                    {
                        switch (((INamedTypeSymbol)symbol).TypeKind)
                        {
                            case TypeKind.Class:
                                publicIcon = Glyph.ClassPublic;
                                break;

                            case TypeKind.Delegate:
                                publicIcon = Glyph.DelegatePublic;
                                break;

                            case TypeKind.Enum:
                                publicIcon = Glyph.EnumPublic;
                                break;

                            case TypeKind.Interface:
                                publicIcon = Glyph.InterfacePublic;
                                break;

                            case TypeKind.Module:
                                publicIcon = Glyph.ModulePublic;
                                break;

                            case TypeKind.Struct:
                                publicIcon = Glyph.StructurePublic;
                                break;

                            case TypeKind.Error:
                                return Glyph.Error;

                            default:
                                throw new ArgumentException("The symbol does not have an icon", nameof(symbol));
                        }

                        break;
                    }

                case SymbolKind.Method:
                    {
                        var methodSymbol = (IMethodSymbol)symbol;

                        if (methodSymbol.MethodKind == MethodKind.UserDefinedOperator || methodSymbol.MethodKind == MethodKind.Conversion)
                        {
                            return Glyph.Operator;
                        }
                        else if (methodSymbol.IsExtensionMethod || methodSymbol.MethodKind == MethodKind.ReducedExtension)
                        {
                            publicIcon = Glyph.ExtensionMethodPublic;
                        }
                        else
                        {
                            publicIcon = Glyph.MethodPublic;
                        }
                    }

                    break;

                case SymbolKind.Namespace:
                    return Glyph.Namespace;

                case SymbolKind.NetModule:
                    return Glyph.Assembly;

                case SymbolKind.Parameter:
                    return symbol.IsValueParameter()
                        ? Glyph.Keyword
                        : Glyph.Parameter;

                case SymbolKind.PointerType:
                    return ((IPointerTypeSymbol)symbol).PointedAtType.GetDisplayGlyph();

                case SymbolKind.Property:
                    {
                        var propertySymbol = (IPropertySymbol)symbol;

                        if (propertySymbol.IsWithEvents)
                        {
                            publicIcon = Glyph.FieldPublic;
                        }
                        else
                        {
                            publicIcon = Glyph.PropertyPublic;
                        }
                    }

                    break;

                case SymbolKind.RangeVariable:
                    return Glyph.RangeVariable;

                case SymbolKind.TypeParameter:
                    return Glyph.TypeParameter;

                default:
                    throw new ArgumentException("The symbol does not have an icon", nameof(symbol));
            }

            switch (symbol.DeclaredAccessibility)
            {
                case Accessibility.Private:
                    publicIcon += Glyph.ClassPrivate - Glyph.ClassPublic;
                    break;

                case Accessibility.Protected:
                case Accessibility.ProtectedAndInternal:
                case Accessibility.ProtectedOrInternal:
                    publicIcon += Glyph.ClassProtected - Glyph.ClassPublic;
                    break;

                case Accessibility.Internal:
                    publicIcon += Glyph.ClassInternal - Glyph.ClassPublic;
                    break;
            }

            return publicIcon;
        }

        public static bool IsValueParameter(this ISymbol symbol)
        {
            if (symbol is IParameterSymbol)
            {
                var method = symbol.ContainingSymbol as IMethodSymbol;
                if (method != null)
                {
                    if (method.MethodKind == MethodKind.EventAdd ||
                        method.MethodKind == MethodKind.EventRemove ||
                        method.MethodKind == MethodKind.PropertySet)
                    {
                        return symbol.Name == "value";
                    }
                }
            }

            return false;
        }
    }
}
