using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Codex.Analysis.Managed.Symbols
{
    class OperatorMethodSymbolDisplayOverride : MethodSymbolWrapper, IMethodSymbol
    {
        public OperatorMethodSymbolDisplayOverride(IMethodSymbol innerSymbol)
            : base(innerSymbol)
        {
        }

        MethodKind IMethodSymbol.MethodKind => MethodKind.Ordinary;
    }
}
