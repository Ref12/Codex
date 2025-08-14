using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Analysis.Managed.Symbols
{
    class PropertySymbolWrapper : BaseSymbolWrapper<IPropertySymbol>, IPropertySymbol
    {
        public PropertySymbolWrapper(IPropertySymbol innerSymbol) : base(innerSymbol)
        {
        }

        public override void Accept(SymbolVisitor visitor)
        {
            visitor.VisitProperty(this);
        }

        public override TResult Accept<TResult>(SymbolVisitor<TResult> visitor)
        {
            return visitor.VisitProperty(this);
        }

        public bool IsIndexer
        {
            get
            {
                return InnerSymbol.IsIndexer;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return InnerSymbol.IsReadOnly;
            }
        }

        public bool IsWriteOnly
        {
            get
            {
                return InnerSymbol.IsWriteOnly;
            }
        }

        public bool IsWithEvents
        {
            get
            {
                return InnerSymbol.IsWithEvents;
            }
        }

        public bool ReturnsByRef
        {
            get
            {
                return InnerSymbol.ReturnsByRef;
            }
        }

        public bool ReturnsByRefReadonly
        {
            get
            {
                return InnerSymbol.ReturnsByRefReadonly;
            }
        }

        public RefKind RefKind
        {
            get
            {
                return InnerSymbol.RefKind;
            }
        }

        public ITypeSymbol Type
        {
            get
            {
                return InnerSymbol.Type;
            }
        }

        public ImmutableArray<IParameterSymbol> Parameters
        {
            get
            {
                return InnerSymbol.Parameters;
            }
        }

        public IMethodSymbol GetMethod
        {
            get
            {
                return InnerSymbol.GetMethod;
            }
        }

        public IMethodSymbol SetMethod
        {
            get
            {
                return InnerSymbol.SetMethod;
            }
        }

        public IPropertySymbol OverriddenProperty
        {
            get
            {
                return InnerSymbol.OverriddenProperty;
            }
        }

        public ImmutableArray<IPropertySymbol> ExplicitInterfaceImplementations
        {
            get
            {
                return InnerSymbol.ExplicitInterfaceImplementations;
            }
        }

        public ImmutableArray<CustomModifier> RefCustomModifiers
        {
            get
            {
                return InnerSymbol.RefCustomModifiers;
            }
        }

        public ImmutableArray<CustomModifier> TypeCustomModifiers
        {
            get
            {
                return InnerSymbol.TypeCustomModifiers;
            }
        }

        IPropertySymbol IPropertySymbol.OriginalDefinition
        {
            get
            {
                return InnerSymbol.OriginalDefinition;
            }
        }

        public NullableAnnotation NullableAnnotation => InnerSymbol.NullableAnnotation;

        public bool IsRequired => InnerSymbol.IsRequired;

        public IPropertySymbol? PartialDefinitionPart => InnerSymbol.PartialDefinitionPart;

        public IPropertySymbol? PartialImplementationPart => InnerSymbol.PartialImplementationPart;

        public bool IsPartialDefinition => InnerSymbol.IsPartialDefinition;
    }
}
