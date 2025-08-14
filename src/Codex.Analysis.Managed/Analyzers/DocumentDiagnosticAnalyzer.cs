using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.ContractsLight;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Codex.Analysis.Managed
{
    class DocumentDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "CB0001";
        public const string Title = "Avoid using implict types with var";
        public const string Message = "var usage has non-obvious type.  Use an explicit type.";
        private const string Category = "Naming";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public static ImmutableArray<SymbolKind> DeclarationScopeSymbolKinds { get; } = ImmutableArray.Create(new[]
        {
            SymbolKind.Method,
            SymbolKind.Property,
            SymbolKind.NamedType
        });

        //public SymbolKind[] AnalyzedSymbolKinds

        private ConcurrentDictionary<string, WeakReference<DocumentAnalyzer>> AnalyzerMap { get; } = new();

        private AsyncLocal<DocumentAnalyzer> _documentAnalyzer = new AsyncLocal<DocumentAnalyzer>();

        public CompilationServices Services { get; }

        public DocumentDiagnosticAnalyzer(CompilationServices services)
        {
            Services = services;
        }

        public Registration Register(string path, DocumentAnalyzer analyzer)
        {
            _documentAnalyzer.Value = analyzer;
            var r = new WeakReference<DocumentAnalyzer>(analyzer);
            AnalyzerMap[path] = r;
            return new(path, r, this);
        }

        public record struct Registration(string path, WeakReference<DocumentAnalyzer> reference, DocumentDiagnosticAnalyzer owner) : IDisposable
        {
            public void Dispose()
            {
                owner.AnalyzerMap.TryRemove(new(path, reference));
                owner._documentAnalyzer.Value = null;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1030:Do not invoke Compilation.GetSemanticModel() method within a diagnostic analyzer", 
            Justification = "Needed as workaround for semantic model inconsistencies between Compilation analyzed and inner created compilation during analysis. Also RegisterSemanticModelAction does not work.")]
        public override void Initialize(AnalysisContext context)
        {
            //context.RegisterOperationBlockAction(ProcessOperationBlock);

            //context.RegisterOperationAction(ProcessOperation);

            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            var documentAnalyzer = _documentAnalyzer.Value;
            context.RegisterSymbolAction(context => ProcessSymbolDeclarationScope(context, documentAnalyzer), DeclarationScopeSymbolKinds);

            context.RegisterCompilationStartAction(context =>
            {
                Contract.Assert(context.Compilation == Services.Compilation, "Compilation should be reused");
                documentAnalyzer.SemanticModel = context.Compilation.GetSemanticModel(documentAnalyzer.SyntaxTree);
            });

            context.RegisterOperationBlockAction(context => ProcessOperationBlock(context, documentAnalyzer));

            //context.RegisterSemanticModelAction(semanticModelAnalysisContext =>
            //{
            //    var filePath = semanticModelAnalysisContext.SemanticModel.SyntaxTree.FilePath;
            //});
        }

        private void ProcessSymbolDeclarationScope(SymbolAnalysisContext context, DocumentAnalyzer analyzer)
        {
            Contract.Assert(context.Compilation == Services.Compilation, "Compilation should be reused");
            var documentPath = analyzer.SyntaxTree.FilePath;
            var symbol = context.Symbol;


            //analyzer.SemanticModel.SyntaxTree == context.
            var state = analyzer.State.GetState(symbol);
            var symbolDepth = symbol.GetSymbolDepth();

            lock (analyzer.FileBuilder.Classifications)
            {
                foreach (var symbolDeclaration in symbol.DeclaringSyntaxReferences)
                {
                    if (symbolDeclaration.SyntaxTree.FilePath != documentPath)
                    {
                        continue;
                    }

                    var node = symbolDeclaration.GetSyntax();
                    var span = node.FullSpan;

                    state.IsScope = true;
                    state.SymbolDepth = symbolDepth;

                    analyzer.AddSymbolScope(containingSymbolDepth: symbolDepth, span);
                }
            }
        }

        private void ProcessOperationBlock(OperationBlockAnalysisContext context, DocumentAnalyzer analyzer)
        {
            var documentPath = analyzer.SyntaxTree.FilePath;
            var visitor = analyzer.OperationVisitor;
            foreach (var block in context.OperationBlocks)
            {
                Debug.Assert(block.Syntax.SyntaxTree.FilePath == documentPath);

                lock (visitor)
                {
                    var containingState = analyzer.State.GetState(context.OwningSymbol);
                    containingState.IsScope = true;
                    visitor.SymbolDepth = containingState.SymbolDepth ??= context.OwningSymbol.GetSymbolDepth();
                    visitor.ScopeDepth = 1;
                    visitor.Visit(block, analyzer.State);
                }
            }
        }
    }
}
