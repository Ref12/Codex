using System;
using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Codex.Analysis.Managed;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using DocumentDiagnosticAnalyzer = Codex.Analysis.Managed.DocumentDiagnosticAnalyzer;

namespace Codex.Analysis
{
    public class CompilationServices
    {
        public Lazy<IMethodSymbol> IDisposable_Dispose;
        public Lazy<IMethodSymbol> TypeForwarderConstructor;
        public Lazy<IMethodSymbol?> EntryPoint;

        public Compilation Compilation { get; }

        private AnalysisReuseCompilationWrapper CompilationWrapper { get; }
        private CompilationWithAnalyzers CompilationWithAnalyzers { get; }
        internal DocumentDiagnosticAnalyzer Analyzer { get; }

        public CompilationServices(Compilation compilation)
        {
            Analyzer = new DocumentDiagnosticAnalyzer(this);

            var wrapper = new AnalysisReuseCompilationWrapper(compilation);
            CompilationWithAnalyzers = wrapper
                .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(Analyzer));

            CompilationWrapper = (AnalysisReuseCompilationWrapper)CompilationWithAnalyzers.Compilation;
            Contract.Assert(CompilationWrapper != wrapper, "Wrapper should be modified.");

            CompilationWrapper.IsFinal = true;

            Compilation = CompilationWrapper.Inner;
            Contract.Assert(Compilation is not AnalysisReuseCompilationWrapper,
                "Inner compilation should not be a wrapper.");

            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            EntryPoint = Lazy.Create(() =>
            {
                try
                {
                    return Compilation.GetEntryPoint(cts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    return null;
                }
            });

            IDisposable_Dispose = new Lazy<IMethodSymbol>(() => compilation.GetTypeByMetadataName(typeof(IDisposable).FullName)?.GetMembers()
                .Where(s => s.Kind == SymbolKind.Method)
                .OfType<IMethodSymbol>()
                .Where(m => m.Name == nameof(IDisposable.Dispose))
                .FirstOrDefault());

            TypeForwarderConstructor = new Lazy<IMethodSymbol>(() => compilation.GetTypeByMetadataName(typeof(TypeForwardedToAttribute).FullName)?.GetMembers()
                .Where(s => s.Kind == SymbolKind.Method)
                .OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Constructor)
                .FirstOrDefault());
        }

        internal async Task RunDiagnosticAnalyzerAsync(DocumentAnalyzer analyzer)
        {
            using var registration = Analyzer.Register(analyzer.SyntaxTree.FilePath, analyzer);
            var semanticModel = Compilation.GetSemanticModel(analyzer.SyntaxTree);
            var analysisResult = await CompilationWithAnalyzers.GetAnalysisResultAsync(semanticModel, default, default);

            if (Compilation.SemanticModelProvider is CachingSemanticModelProvider provider)
            {
                provider.ClearCache(analyzer.SyntaxTree, Compilation);
            }
        }
    }
}
