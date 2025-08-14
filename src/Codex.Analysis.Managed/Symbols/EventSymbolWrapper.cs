using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Analysis.Managed.Symbols
{
    class EventSymbolWrapper : BaseSymbolWrapper<IEventSymbol>, IEventSymbol
    {
        public EventSymbolWrapper(IEventSymbol innerSymbol) : base(innerSymbol)
        {
        }

        public override void Accept(SymbolVisitor visitor)
        {
            visitor.VisitEvent(this);
        }

        public override TResult Accept<TResult>(SymbolVisitor<TResult> visitor)
        {
            return visitor.VisitEvent(this);
        }

        public ITypeSymbol Type
        {
            get
            {
                return InnerSymbol.Type;
            }
        }

        public bool IsWindowsRuntimeEvent
        {
            get
            {
                return InnerSymbol.IsWindowsRuntimeEvent;
            }
        }

        public IMethodSymbol AddMethod
        {
            get
            {
                return InnerSymbol.AddMethod;
            }
        }

        public IMethodSymbol RemoveMethod
        {
            get
            {
                return InnerSymbol.RemoveMethod;
            }
        }

        public IMethodSymbol RaiseMethod
        {
            get
            {
                return InnerSymbol.RaiseMethod;
            }
        }

        public IEventSymbol OverriddenEvent
        {
            get
            {
                return InnerSymbol.OverriddenEvent;
            }
        }

        public ImmutableArray<IEventSymbol> ExplicitInterfaceImplementations
        {
            get
            {
                return InnerSymbol.ExplicitInterfaceImplementations;
            }
        }

        IEventSymbol IEventSymbol.OriginalDefinition
        {
            get
            {
                return InnerSymbol.OriginalDefinition;
            }
        }

        public NullableAnnotation NullableAnnotation => InnerSymbol.NullableAnnotation;
    }
}
