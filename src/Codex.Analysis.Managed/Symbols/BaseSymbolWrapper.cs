using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Codex.Analysis.Managed.Symbols
{
    public class BaseSymbolWrapper
    {
        public static ISymbol WrapWithOverrideContainer(ISymbol symbol, ISymbol overrideContainerSymbol)
        {
            switch (symbol)
            {
                case IFieldSymbol field:
                    return new FieldSymbolWrapper(field) { OverrideContainerSymbol = overrideContainerSymbol };
                case IMethodSymbol method:
                    return new MethodSymbolWrapper(method) { OverrideContainerSymbol = overrideContainerSymbol };
                case IPropertySymbol property:
                    return new PropertySymbolWrapper(property) { OverrideContainerSymbol = overrideContainerSymbol };
                case IEventSymbol ev:
                    return new EventSymbolWrapper(ev) { OverrideContainerSymbol = overrideContainerSymbol };
                default:
                    throw new ArgumentException();
            }
        }
    }

    internal abstract class BaseSymbolWrapper<TSymbol> : ISymbol
        where TSymbol : ISymbol
    {
        protected TSymbol InnerSymbol { get; }

        public SymbolKind Kind => InnerSymbol.Kind;

        public string Language => InnerSymbol.Language;

        public string Name => InnerSymbol.Name;

        /// <summary>
        /// Set override container to create synthetic symbol for usages of members on specific implementations
        /// such as TextWriter.Write(int) on a StringWriter since StringWriter does not define an override
        /// </summary>
        public ISymbol OverrideContainerSymbol { get; set; }

        public string MetadataName
        {
            get
            {
                return InnerSymbol.MetadataName;
            }
        }

        public ISymbol ContainingSymbol
        {
            get
            {
                return OverrideContainerSymbol ?? InnerSymbol.ContainingSymbol;
            }
        }

        public ISymbol OriginalDefinition
        {
            get
            {
                return Wrap(InnerSymbol.OriginalDefinition, OverrideContainerSymbol.OriginalDefinition);
            }
        }

        public virtual ISymbol Wrap(ISymbol symbol, ISymbol overrideContainerSymbol)
        {
            if (symbol == (ISymbol)InnerSymbol && overrideContainerSymbol == OverrideContainerSymbol)
            {
                return this;
            }

            return WrapCore(symbol, overrideContainerSymbol);
        }

        public virtual ISymbol WrapCore(ISymbol symbol, ISymbol overrideContainerSymbol)
        {
            return BaseSymbolWrapper.WrapWithOverrideContainer(symbol, overrideContainerSymbol);
        }

        public IAssemblySymbol ContainingAssembly
        {
            get
            {
                return OverrideContainerSymbol?.ContainingAssembly ?? InnerSymbol.ContainingAssembly;
            }
        }

        public IModuleSymbol ContainingModule
        {
            get
            {
                return OverrideContainerSymbol?.ContainingModule ?? InnerSymbol.ContainingModule;
            }
        }

        public INamedTypeSymbol ContainingType
        {
            get
            {
                if (OverrideContainerSymbol is INamedTypeSymbol overrideContainingType)
                {
                    return overrideContainingType;
                }

                return OverrideContainerSymbol?.ContainingType ?? InnerSymbol.ContainingType;
            }
        }

        public INamespaceSymbol ContainingNamespace
        {
            get
            {
                return OverrideContainerSymbol?.ContainingNamespace ?? InnerSymbol.ContainingNamespace;
            }
        }

        public bool IsDefinition
        {
            get
            {
                return InnerSymbol.IsDefinition;
            }
        }

        public bool IsStatic
        {
            get
            {
                return InnerSymbol.IsStatic;
            }
        }

        public bool IsVirtual
        {
            get
            {
                return InnerSymbol.IsVirtual;
            }
        }

        public bool IsOverride
        {
            get
            {
                return InnerSymbol.IsOverride;
            }
        }

        public bool IsAbstract
        {
            get
            {
                return InnerSymbol.IsAbstract;
            }
        }

        public bool IsSealed
        {
            get
            {
                return InnerSymbol.IsSealed;
            }
        }

        public bool IsExtern
        {
            get
            {
                return InnerSymbol.IsExtern;
            }
        }

        public bool IsImplicitlyDeclared
        {
            get
            {
                return InnerSymbol.IsImplicitlyDeclared;
            }
        }

        public bool CanBeReferencedByName
        {
            get
            {
                return InnerSymbol.CanBeReferencedByName;
            }
        }

        public ImmutableArray<Location> Locations
        {
            get
            {
                return InnerSymbol.Locations;
            }
        }

        public ImmutableArray<SyntaxReference> DeclaringSyntaxReferences
        {
            get
            {
                return InnerSymbol.DeclaringSyntaxReferences;
            }
        }

        public Accessibility DeclaredAccessibility
        {
            get
            {
                return InnerSymbol.DeclaredAccessibility;
            }
        }

        public bool HasUnsupportedMetadata
        {
            get
            {
                return InnerSymbol.HasUnsupportedMetadata;
            }
        }

        public int MetadataToken => InnerSymbol.MetadataToken;

        public BaseSymbolWrapper(TSymbol innerSymbol)
        {
            InnerSymbol = innerSymbol;
        }

        public ImmutableArray<AttributeData> GetAttributes()
        {
            return InnerSymbol.GetAttributes();
        }

        public abstract void Accept(SymbolVisitor visitor);

        public abstract TResult Accept<TResult>(SymbolVisitor<TResult> visitor);

        public string GetDocumentationCommentId()
        {
            return InnerSymbol.GetDocumentationCommentId();
        }

        public string GetDocumentationCommentXml(CultureInfo preferredCulture = null, bool expandIncludes = false, CancellationToken cancellationToken = default)
        {
            return InnerSymbol.GetDocumentationCommentXml(preferredCulture, expandIncludes, cancellationToken);
        }

        public string ToDisplayString(SymbolDisplayFormat format = null)
        {
            return CS.SymbolDisplay.ToDisplayString(this, format);
        }

        public ImmutableArray<SymbolDisplayPart> ToDisplayParts(SymbolDisplayFormat format = null)
        {
            return CS.SymbolDisplay.ToDisplayParts(this, format);
        }

        public string ToMinimalDisplayString(SemanticModel semanticModel, int position, SymbolDisplayFormat format = null)
        {
            return CS.SymbolDisplay.ToMinimalDisplayString(this, semanticModel, position, format);
        }

        public ImmutableArray<SymbolDisplayPart> ToMinimalDisplayParts(SemanticModel semanticModel, int position, SymbolDisplayFormat format = null)
        {
            return CS.SymbolDisplay.ToMinimalDisplayParts(this, semanticModel, position, format);
        }

        public bool Equals(ISymbol other)
        {
            throw new InvalidOperationException("Equality comparison is not allowed in wrapper symbol");
        }

        public override string ToString()
        {
            return this.ToDisplayString();
        }

        public bool Equals(ISymbol other, SymbolEqualityComparer equalityComparer)
        {
            throw new InvalidOperationException("Equality comparison is not allowed in wrapper symbol");
        }

        public TResult Accept<TArgument, TResult>(SymbolVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return InnerSymbol.Accept(visitor, argument);
        }
    }
}
