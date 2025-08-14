using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Analysis.Managed.Symbols
{
    class MethodSymbolWrapper : BaseSymbolWrapper<IMethodSymbol>, IMethodSymbol
    {
        public MethodSymbolWrapper(IMethodSymbol innerSymbol) : base(innerSymbol)
        {
        }

        public MethodKind MethodKind
        {
            get
            {
                return InnerSymbol.MethodKind;
            }
        }

        public int Arity
        {
            get
            {
                return InnerSymbol.Arity;
            }
        }

        public bool IsGenericMethod
        {
            get
            {
                return InnerSymbol.IsGenericMethod;
            }
        }

        public bool IsExtensionMethod
        {
            get
            {
                return InnerSymbol.IsExtensionMethod;
            }
        }

        public bool IsAsync
        {
            get
            {
                return InnerSymbol.IsAsync;
            }
        }

        public bool IsVararg
        {
            get
            {
                return InnerSymbol.IsVararg;
            }
        }

        public bool IsCheckedBuiltin
        {
            get
            {
                return InnerSymbol.IsCheckedBuiltin;
            }
        }

        public bool HidesBaseMethodsByName
        {
            get
            {
                return InnerSymbol.HidesBaseMethodsByName;
            }
        }

        public bool ReturnsVoid
        {
            get
            {
                return InnerSymbol.ReturnsVoid;
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

        public ITypeSymbol ReturnType
        {
            get
            {
                return InnerSymbol.ReturnType;
            }
        }

        public ImmutableArray<ITypeSymbol> TypeArguments
        {
            get
            {
                return InnerSymbol.TypeArguments;
            }
        }

        public ImmutableArray<ITypeParameterSymbol> TypeParameters
        {
            get
            {
                return InnerSymbol.TypeParameters;
            }
        }

        public ImmutableArray<IParameterSymbol> Parameters
        {
            get
            {
                return InnerSymbol.Parameters;
            }
        }

        public IMethodSymbol ConstructedFrom
        {
            get
            {
                return InnerSymbol.ConstructedFrom;
            }
        }

        public IMethodSymbol OverriddenMethod
        {
            get
            {
                return InnerSymbol.OverriddenMethod;
            }
        }

        public ITypeSymbol ReceiverType
        {
            get
            {
                return InnerSymbol.ReceiverType;
            }
        }

        public IMethodSymbol ReducedFrom
        {
            get
            {
                return InnerSymbol.ReducedFrom;
            }
        }

        public ImmutableArray<IMethodSymbol> ExplicitInterfaceImplementations
        {
            get
            {
                return InnerSymbol.ExplicitInterfaceImplementations;
            }
        }

        public ImmutableArray<CustomModifier> ReturnTypeCustomModifiers
        {
            get
            {
                return InnerSymbol.ReturnTypeCustomModifiers;
            }
        }

        public ImmutableArray<CustomModifier> RefCustomModifiers
        {
            get
            {
                return InnerSymbol.RefCustomModifiers;
            }
        }

        public ISymbol AssociatedSymbol
        {
            get
            {
                return InnerSymbol.AssociatedSymbol;
            }
        }

        public IMethodSymbol PartialDefinitionPart
        {
            get
            {
                return InnerSymbol.PartialDefinitionPart;
            }
        }

        public IMethodSymbol PartialImplementationPart
        {
            get
            {
                return InnerSymbol.PartialImplementationPart;
            }
        }

        public INamedTypeSymbol AssociatedAnonymousDelegate
        {
            get
            {
                return InnerSymbol.AssociatedAnonymousDelegate;
            }
        }

        IMethodSymbol IMethodSymbol.OriginalDefinition
        {
            get
            {
                return InnerSymbol.OriginalDefinition;
            }
        }

        public NullableAnnotation ReturnNullableAnnotation => InnerSymbol.ReturnNullableAnnotation;

        public ImmutableArray<NullableAnnotation> TypeArgumentNullableAnnotations => InnerSymbol.TypeArgumentNullableAnnotations;

        public bool IsReadOnly => InnerSymbol.IsReadOnly;

        public NullableAnnotation ReceiverNullableAnnotation => InnerSymbol.ReceiverNullableAnnotation;

        public bool IsInitOnly => InnerSymbol.IsInitOnly;

        public SignatureCallingConvention CallingConvention => InnerSymbol.CallingConvention;

        public ImmutableArray<INamedTypeSymbol> UnmanagedCallingConventionTypes => InnerSymbol.UnmanagedCallingConventionTypes;

        public bool IsConditional => InnerSymbol.IsConditional;

        public MethodImplAttributes MethodImplementationFlags => InnerSymbol.MethodImplementationFlags;

        public bool IsPartialDefinition => InnerSymbol.IsPartialDefinition;

        public IMethodSymbol Construct(params ITypeSymbol[] typeArguments)
        {
            return InnerSymbol.Construct(typeArguments);
        }

        public DllImportData GetDllImportData()
        {
            return InnerSymbol.GetDllImportData();
        }

        public ImmutableArray<AttributeData> GetReturnTypeAttributes()
        {
            return InnerSymbol.GetReturnTypeAttributes();
        }

        public ITypeSymbol GetTypeInferredDuringReduction(ITypeParameterSymbol reducedFromTypeParameter)
        {
            return InnerSymbol.GetTypeInferredDuringReduction(reducedFromTypeParameter);
        }

        public IMethodSymbol ReduceExtensionMethod(ITypeSymbol receiverType)
        {
            return InnerSymbol.ReduceExtensionMethod(receiverType);
        }

        public override void Accept(SymbolVisitor visitor)
        {
            visitor.VisitMethod(this);
        }

        public override TResult Accept<TResult>(SymbolVisitor<TResult> visitor)
        {
            return visitor.VisitMethod(this);
        }

        public IMethodSymbol Construct(ImmutableArray<ITypeSymbol> typeArguments, ImmutableArray<NullableAnnotation> typeArgumentNullableAnnotations)
        {
            return InnerSymbol.Construct(typeArguments, typeArgumentNullableAnnotations);
        }
    }
}
