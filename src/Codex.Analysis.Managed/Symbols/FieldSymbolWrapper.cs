using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Analysis.Managed.Symbols
{
    class FieldSymbolWrapper : BaseSymbolWrapper<IFieldSymbol>, IFieldSymbol
    {
        public FieldSymbolWrapper(IFieldSymbol innerSymbol) : base(innerSymbol)
        {
        }

        public override void Accept(SymbolVisitor visitor)
        {
            visitor.VisitField(this);
        }

        public override TResult Accept<TResult>(SymbolVisitor<TResult> visitor)
        {
            return visitor.VisitField(this);
        }

        public ISymbol AssociatedSymbol
        {
            get
            {
                return InnerSymbol.AssociatedSymbol;
            }
        }

        public bool IsConst
        {
            get
            {
                return InnerSymbol.IsConst;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return InnerSymbol.IsReadOnly;
            }
        }

        public bool IsVolatile
        {
            get
            {
                return InnerSymbol.IsVolatile;
            }
        }

        public ITypeSymbol Type
        {
            get
            {
                return InnerSymbol.Type;
            }
        }

        public bool HasConstantValue
        {
            get
            {
                return InnerSymbol.HasConstantValue;
            }
        }

        public object ConstantValue
        {
            get
            {
                return InnerSymbol.ConstantValue;
            }
        }

        public ImmutableArray<CustomModifier> CustomModifiers
        {
            get
            {
                return InnerSymbol.CustomModifiers;
            }
        }

        public IFieldSymbol CorrespondingTupleField
        {
            get
            {
                return InnerSymbol.CorrespondingTupleField;
            }
        }

        IFieldSymbol IFieldSymbol.OriginalDefinition
        {
            get
            {
                return InnerSymbol.OriginalDefinition;
            }
        }

        public bool IsFixedSizeBuffer => InnerSymbol.IsFixedSizeBuffer;

        public NullableAnnotation NullableAnnotation => InnerSymbol.NullableAnnotation;

        public bool IsRequired => InnerSymbol.IsRequired;

        public int FixedSize => InnerSymbol.FixedSize;

        public RefKind RefKind => InnerSymbol.RefKind;

        public ImmutableArray<CustomModifier> RefCustomModifiers => InnerSymbol.RefCustomModifiers;

        public bool IsExplicitlyNamedTupleElement => InnerSymbol.IsExplicitlyNamedTupleElement;
    }
}
