using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.ContractsLight;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codex.Analysis.Managed.Symbols;
using Codex.ObjectModel;
using Codex.Sdk;
using Codex.Storage.BlockLevel;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;

namespace Codex.Analysis.Managed
{
    class DocumentAnalyzer
    {
        private Document _document;
        internal CompilationServices CompilationServices;
        internal AnalysisOperationVisitor OperationVisitor { get; }

        public SemanticModel SemanticModel { private get; set; }
        public SyntaxTree SyntaxTree { get; private set; }
        private AnalyzedProjectInfo _analyzedProject;
        private BoundSourceFileBuilder boundSourceFile;
        private List<ReferenceSpan> references;

        public BoundSourceFileBuilder FileBuilder => boundSourceFile;

        private AnalyzedProjectContext context;
        private SourceText DocumentText;
        private readonly SemanticServices semanticServices;

        public AnalysisState State { get; }

        public DocumentAnalyzer(
            SemanticServices semanticServices,
            Document document,
            CompilationServices compilationServices,
            string logicalPath,
            AnalyzedProjectContext context,
            BoundSourceFileBuilder boundSourceFile)
        {
            _document = document;
            CompilationServices = compilationServices;
            _analyzedProject = context.Project;
            this.semanticServices = semanticServices;
            this.context = context;
            OperationVisitor = new(this);

            references = new List<ReferenceSpan>();

            this.boundSourceFile = boundSourceFile;
            boundSourceFile.BoundSourceFile.Flags |= BoundSourceFlags.RemapLocalIds;
            State = new AnalysisState(this);
        }

        private string Language => _document.Project.Language;

        public string GetText(ClassifiedSpan span)
        {
            return DocumentText.ToString(span.TextSpan);
        }

        public async Task<BoundSourceFile> CreateBoundSourceFile()
        {
            await PopulateBoundSourceFileAsync();
            var result = boundSourceFile.Build();
            return result;
        }

        private void PostProcessClassifications(List<SymbolicClassificationSpan> spans)
        {
            State.ScopeClassificationLocalIds(spans);

            //var maxSymbolDepth = spans.Max(c => c.SymbolDepth);
            //spans.Sort(BoundSourceFileBuilder.ClassificationSorter);

            //var expected = spans
            //    .Where(c => c.State?.LocalSymbolStateIndex >= 0)
            //    .GroupBy(c => -1 - c.State.LocalSymbolStateIndex).ToDictionary(c => c.First());

            //var tracker = new ScopeTracker();
            //var localIds = spans.Select(s => s.LocalGroupId).ToHashSet();
            //var trackerLocalIds = spans.Select(s => s.TrackerLocalId = tracker.GetLocalId(s)).ToHashSet();

            //var actual = spans.Where(c => c.LocalGroupId != 0).GroupBy(c => c.TrackerLocalId).ToDictionary(c => c.First());

            //var comparer = expected.Concat(actual).ToLookup(kvp => kvp.Key, kvp => kvp.Value)
            //    .Select(g => new CompareLocals(g))
            //    .ToArray();
        }

        public record struct CompareLocals(IEnumerable<IGrouping<int, ClassificationSpan>> Spans)
        {
            public IGrouping<int, ClassificationSpan> Expected = Spans.Where(g => g.Key < 0).FirstOrDefault();
            public IGrouping<int, ClassificationSpan> Actual = Spans.Where(g => g.Key > 0).FirstOrDefault();

            public bool IsDifferent => Expected == null || Actual == null || Expected.Count() != Actual.Count();

            public override string ToString()
            {
                return $"IsDiff:{IsDifferent} Exp({Print(Expected)}) Act({Print(Actual)})";
            }

            private string Print(IGrouping<int, ClassificationSpan> g)
            {
                if (g == null) return "null";

                return $"{g.Key} => {g.Count()}";
            }
        }

        public async Task PopulateBoundSourceFileAsync()
        {
            // When re-enabling custom analyzer, be sure that no other analyzers are getting run to collect diagnostics below
            //var compilationWithAnalyzer = _compilation.WithAnalyzers(new DiagnosticAnalyzer[] { new DocumentDiagnosticAnalyzer() }.ToImmutableArray());
            //var diagnostics = await compilationWithAnalyzer.GetAnalyzerDiagnosticsAsync();
            var syntaxRoot = await _document.GetSyntaxRootAsync();
            var syntaxTree = syntaxRoot.SyntaxTree;
            SyntaxTree = syntaxTree;
            //SemanticModel = _compilation.GetSemanticModel(syntaxTree);
            DocumentText = await _document.GetTextAsync();

            await CompilationServices.RunDiagnosticAnalyzerAsync(this);
            Contract.Assert(SemanticModel != null);

            if (syntaxRoot.Language == LanguageNames.CSharp)
            {
                new DocumentAnalyzerSyntaxVisitor(this, SemanticModel).Visit(syntaxRoot);
            }

            boundSourceFile.SourceFile.Info.Lines = DocumentText.Lines.Count;
            boundSourceFile.SourceFile.Info.Size = DocumentText.Length;

            // Cancel classification after a given interval
            using var classificationToken = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // Using this obsolete overload which avoid embedded language (json, regex)
            // classification which can take a long time in certain files with extremely long strings
#pragma warning disable CS0618 // Type or member is obsolete
            var classificationSpans = (IReadOnlyList<ClassifiedSpan>)Classifier
                .GetClassifiedSpans(
                    SemanticModel,
                    syntaxRoot.FullSpan,
                    _document.Project.Solution.Workspace,
                    classificationToken.Token);
#pragma warning restore CS0618 // Type or member is obsolete

            var text = await _document.GetTextAsync();

            var originalClassificationSpans = classificationSpans;
            classificationSpans = MergeSpans(classificationSpans).ToList();
            var fileClassificationSpans = new List<SymbolicClassificationSpan>();

            foreach (var span in classificationSpans)
            {
                if (SkipSpan(span))
                {
                    continue;
                }

                var classificationSpan = new SymbolicClassificationSpan();
                fileClassificationSpans.Add(classificationSpan);

                classificationSpan.Start = span.TextSpan.Start;
                classificationSpan.Length = span.TextSpan.Length;
                classificationSpan.Classification = span.ClassificationType;

                if (!IsSemanticSpan(span))
                {
                    continue;
                }

                var token = syntaxRoot.FindToken(span.TextSpan.Start, findInsideTrivia: true);

                if (!semanticServices.IsPossibleSemanticToken(token))
                {
                    continue;
                }

                ISymbol declaredSymbol = null;
                SyntaxNode bindableParentNode = null;
                bool isThis = false;

                if (span.ClassificationType != ClassificationTypeNames.Keyword)
                {
                    declaredSymbol = SemanticModel.GetDeclaredSymbol(token.Parent);
                }

                var usingExpression = semanticServices.TryGetUsingExpressionFromToken(token);
                if (usingExpression != null)
                {
                    var disposeSymbol = CompilationServices.IDisposable_Dispose.Value;
                    if (disposeSymbol != null)
                    {
                        var typeInfo = SemanticModel.GetTypeInfo(usingExpression);
                        var disposeImplSymbol = typeInfo.Type?.FindImplementationForInterfaceMember(disposeSymbol);
                        if (disposeImplSymbol != null)
                        {
                            SymbolSpan usingSymbolSpan = CreateSymbolSpan(classificationSpan);
                            references.Add(usingSymbolSpan.CreateReference(GetReferenceSymbol(disposeImplSymbol, ReferenceKind.UsingDispose)));
                        }
                    }
                }

                if (Out.Var(out var isOverride, semanticServices.IsOverrideKeyword(token))
                    || semanticServices.IsPartialKeyword(token))
                {
                    var bindableNode = token.Parent;
                    bindableNode = semanticServices.GetEventField(bindableNode);

                    var parentSymbol = SemanticModel.GetDeclaredSymbol(bindableNode);

                    if (isOverride)
                    {
                        SymbolSpan parentSymbolSpan = CreateSymbolSpan(classificationSpan);

                        // Don't allow this to show up in search. It's only added for go to definition navigation
                        // on override keyword.
                        AddReferencesToOverriddenMembers(parentSymbolSpan, parentSymbol, excludeFromSearch: true);
                    }
                    else
                    {
                        // Partial keyword
                        AddSymbolSpan(parentSymbol, token, configureState: s =>
                        {
                            s.ReferenceKind ??= ReferenceKind.Partial;
                        });
                    }
                }

                ISymbol symbol = declaredSymbol;

                if (declaredSymbol == null)
                {
                    bindableParentNode = GetBindableParent(token);
                    if (bindableParentNode != null)
                    {
                        symbol = GetSymbol(bindableParentNode, out isThis, token);
                    }
                }

                if (symbol == null || symbol.ContainingAssembly == null)
                {
                    continue;
                }

                bool isLocalFunction() => symbol.Kind == SymbolKind.Method && symbol is IMethodSymbol m && m.MethodKind == MethodKind.LocalFunction;

                bool isAnonymousTypeOrMember() => symbol.IsAnonymousType() || symbol.IsAnonymousTypeProperty();

                if (symbol.Kind == SymbolKind.Local ||
                    symbol.Kind == SymbolKind.Parameter ||
                    symbol.Kind == SymbolKind.TypeParameter ||
                    symbol.Kind == SymbolKind.RangeVariable ||
                    isThis ||
                    isLocalFunction() ||
                    isAnonymousTypeOrMember())
                {
                    if (symbol.ContainingSymbol is { } containingSymbol)
                    {
                        var containingState = State.GetState(containingSymbol);
                        var spec = token.GetSymbolSpec(isThis);
                        var symbolState = State.GetState(symbol, spec);
                        if (symbolState.LocalSymbolStateIndex < 0)
                        {
                            containingState.SymbolDepth ??= containingSymbol.GetSymbolDepth();
                            symbolState.SymbolDepth ??= containingState.SymbolDepth + 1;
                            symbolState.LocalName = token.ToString();

                            //  Just generate group ids rather than full ref/def
                            State.AddLocalSymbolState(symbolState);
                        }

                        Contract.Assert(symbolState.LocalSymbolStateIndex >= 0);
                        classificationSpan.Associate(symbolState);
                    }
                    else
                    {
                    }
                    //var symbolDepth = localSymbolIdMap.GetOrAdd(symbol, 0, (s, _) => symbol.GetSymbolDepth());
                    //classificationSpan.LocalSymbolDepth = symbolDepth;
                    continue;
                }

                AddSymbolSpan(
                    symbol,
                    token,
                    declaredSymbol: declaredSymbol//,
                    //arg: (1, 0),
                    //static (state, args) =>
                    //{
                    //    //if (args.isLocalFunction)
                    //    //{
                    //    //    state.ExcludeFromSearch = true;
                    //    //}
                    //}
                    );
            }
            PostProcessClassifications(fileClassificationSpans);

            boundSourceFile.AddClassifications(fileClassificationSpans);
            boundSourceFile.AddReferences(references);
        }

        internal void AddSymbolScope(int containingSymbolDepth, TextSpan span)
        {
            //Span<int> starts = stackalloc[] { span.Start, span.End };
            //for (int i = 0; i < starts.Length; i++)
            //{
            //    FileBuilder.Classifications.Add(new ClassificationSpan()
            //    {
            //        Start = starts[i],
            //        Classification = i == 0
            //            ? ClassificationName.OutliningRegionStart
            //            : ClassificationName.OutliningRegionEnd,
            //        Length = 0,
            //        SymbolDepth = containingSymbolDepth + 1
            //    });
            //}
        }

        public void AddSymbolSpan(
            ISymbol symbol,
            SyntaxToken token,
            ISymbol declaredSymbol = default,
            Action<SpanAnalysisState> configureState = null)
        {
            AddSymbolSpan(
                symbol,
                token,
                declaredSymbol,
                configureState,
                configureState == null ? null : static (state, configureState) => configureState(state));
        }

        public void AddSymbolSpan<TArg>(
            ISymbol symbol,
            SyntaxToken token,
            ISymbol declaredSymbol = default,
            TArg arg = default,
            Action<SpanAnalysisState, TArg> configureState = null)
        {
            var state = State.GetState(token);
            configureState?.Invoke(state, arg);

            if (state.Skip) return;
            state.Skip = true;

            symbol = symbol.OriginalDefinition ?? symbol;

            if ((symbol.Kind == SymbolKind.Event ||
                 symbol.Kind == SymbolKind.Field ||
                 symbol.Kind == SymbolKind.Method ||
                 symbol.Kind == SymbolKind.NamedType ||
                 symbol.Kind == SymbolKind.Property))
            {
                if (symbol.IsAnonymousFunction())
                {
                    return;
                }

                if (symbol.Locations.Length == 0)
                {
                    return;
                }

                var documentationId = GetDocumentationCommentId(symbol);
                if (string.IsNullOrEmpty(documentationId))
                {
                    return;
                }

                SymbolSpan symbolSpan = CreateSymbolSpan(state?.Span ?? token.Span);

                if (declaredSymbol != null)
                {
                    // This is a definition
                    var definitionSymbol = GetDefinitionSymbol(symbol, documentationId);
                    definitionSymbol.ReferenceKind = state.ReferenceKind ?? definitionSymbol.ReferenceKind;

                    bool addReferenceDefinitions = false;
                    if (definitionSymbol.ProjectId == _analyzedProject.ProjectId)
                    {
                        // Definitions can only be added for the current project. This is mainly targeted at value tuples
                        var definitionSpan = symbolSpan.CreateDefinition(definitionSymbol);
                        definitionSpan.FullSpan = GetBindableParent(token, state)?.GetDeclarationExtent() ?? default;
                        boundSourceFile.AddDefinition(definitionSpan);
                        addReferenceDefinitions = true;
                    }

                    definitionSymbol.ExcludeFromSearch |= state.ExcludeFromSearch;

                    // A reference symbol for the definition is added so the definition is found in find all references
                    var definitionReferenceSymbol = new ReferenceSymbol(definitionSymbol);
                    AddReferenceDefinitions(definitionReferenceSymbol, symbol, addReferenceDefinitions: addReferenceDefinitions);
                    references.Add(symbolSpan.CreateReference(definitionReferenceSymbol));

                    ProcessDefinitionAndAddAdditionalReferenceSymbols(symbol, symbolSpan, definitionSymbol, token);
                }
                else
                {
                    // This is a reference
                    var referenceSpan = GetReferenceSpan(symbolSpan, symbol, documentationId, token, state);

                    // This parameter should not show up in find all references search
                    // but should navigate to type for go to definition
                    Placeholder.Todo("Should probably remove this logic. 'this' references can be handled like locals");
                    referenceSpan.Reference.ExcludeFromSearch |= IsThisParameter(symbol)
                        || state.ExcludeFromSearch;// token.IsKind(SyntaxKind.ThisKeyword) || token.IsKind(SyntaxKind.BaseKeyword);
                    references.Add(referenceSpan);

                    AddAdditionalReferenceSymbols(symbol, referenceSpan, token, state);
                }
            }
        }

        internal static SymbolSpan CreateSymbolSpan(TextSpan span)
        {
            var symbolSpan = new SymbolSpan()
            {
                Start = span.Start,
                Length = span.Length,
            };

            return symbolSpan;
        }

        private static SymbolSpan CreateSymbolSpan(ISpan span)
        {
            return CreateSymbolSpan(new TextSpan(span.Start, span.Length));
        }

        private void AddAdditionalReferenceSymbols(ISymbol symbol, ReferenceSpan symbolSpan, SyntaxToken token, SpanAnalysisState state)
        {
            if (symbol.Kind == SymbolKind.Method)
            {
                if (symbol is IMethodSymbol methodSymbol)
                {
                    if (state.OperationKind == OperationKind.ObjectCreation)
                    {
                        references.Add(symbolSpan.CreateReference(GetReferenceSymbol(methodSymbol.ContainingType, ReferenceKind.Instantiation)));
                    }
                }
            }
        }

        private void ProcessDefinitionAndAddAdditionalReferenceSymbols(ISymbol symbol, SymbolSpan symbolSpan, DefinitionSymbol definition, SyntaxToken token)
        {
            // Handle potentially virtual or interface member implementations
            if (symbol.Kind == SymbolKind.Method || symbol.Kind == SymbolKind.Property || symbol.Kind == SymbolKind.Event)
            {
                AddReferencesToOverriddenMembers(symbolSpan, symbol, relatedDefinition: definition.Id);

                AddReferencesToImplementedMembers(symbolSpan, symbol, definition.Id);
            }

            if (symbol.Kind == SymbolKind.Method)
            {
                if (symbol is IMethodSymbol methodSymbol)
                {
                    // Case: Constructor and Static Constructor
                    if (methodSymbol.MethodKind == MethodKind.Constructor || methodSymbol.MethodKind == MethodKind.StaticConstructor)
                    {
                        // Exclude constructors from default search
                        // Add a constructor reference with the containing type
                        definition.ExcludeFromDefaultSearch = true;
                        references.Add(symbolSpan.CreateReference(GetReferenceSymbol(methodSymbol.ContainingType, ReferenceKind.Constructor)));
                    }

                    if (methodSymbol.SymbolEquals(CompilationServices.EntryPoint.Value))
                    {
                        definition.Keywords.Add(nameof(WellKnownKeywords.main));
                    }
                }
            }
            else if (symbol.Kind == SymbolKind.Field)// && State.TryGetState(symbol, out var state))
            {
                // Handle enum fields
                if (symbol is IFieldSymbol fieldSymbol
                    //&& fieldSymbol.HasConstantValue
                    && symbol.ContainingType.TypeKind == TypeKind.Enum
                    && fieldSymbol.ConstantValue is IConvertible cv
                    && cv.TryGetIntegralValue(out var value))
                {
                    //definition.ConstantValue = value;
                }
            }
        }

        private static StringEnum<SymbolKinds> GetSymbolKind(ISymbol symbol)
        {
            if (symbol.Kind == SymbolKind.Property
                && symbol is IPropertySymbol property
                && property.IsIndexer)
            {
                return SymbolKinds.Indexer;
            }

            if (symbol.Kind == SymbolKind.Method)
            {
                IMethodSymbol methodSymbol = symbol as IMethodSymbol;
                if (methodSymbol != null)
                {
                    // Case: Constructor and Static Constructor
                    if (methodSymbol.MethodKind == MethodKind.Constructor || methodSymbol.MethodKind == MethodKind.StaticConstructor)
                    {
                        // Exclude constructors from default search
                        // Add a constructor reference with the containing type
                        return SymbolKinds.Constructor;
                    }
                    else if (methodSymbol.MethodKind == MethodKind.UserDefinedOperator)
                    {
                        return SymbolKinds.Operator;
                    }
                }

            }
            else if (symbol.Kind == SymbolKind.NamedType)
            {
                INamedTypeSymbol typeSymbol = symbol as INamedTypeSymbol;
                if (typeSymbol != null)
                {
                    switch (typeSymbol.TypeKind)
                    {
                        case TypeKind.Class:
                            return SymbolKinds.Class;
                        case TypeKind.Delegate:
                            return SymbolKinds.Delegate;
                        case TypeKind.Enum:
                            return SymbolKinds.Enum;
                        case TypeKind.Interface:
                            return SymbolKinds.Interface;
                        case TypeKind.Struct:
                            return SymbolKinds.Struct;
                        default:
                            break;
                    }
                }
            }

            return symbol.Kind.GetString();
        }

        private void AddReferencesToImplementedMembers(
            SymbolSpan symbolSpan,
            ISymbol declaredSymbol,
            SymbolId relatedDefinition = default(SymbolId))
        {
            var declaringType = declaredSymbol.ContainingType;
            ILookup<ISymbol, ISymbol> implementationLookup = GetImplementedMemberLookup(declaringType).memberByImplementedLookup;

            foreach (var implementedMember in implementationLookup[declaredSymbol])
            {
                references.Add(symbolSpan.CreateReference(GetReferenceSymbol(implementedMember, ReferenceKind.InterfaceMemberImplementation), relatedDefinition));
            }
        }

        private InterfaceMemberMapping GetImplementedMemberLookup(INamedTypeSymbol type)
        {
            return State.GetState(type).InterfaceMemberMapping ??= get();

            InterfaceMemberMapping get()
            {
                InterfaceMemberMapping result = default;
                result.interfaceMemberToImplementationMap = type.AllInterfaces
                    .SelectMany(implementedInterface =>
                        implementedInterface.GetMembers()
                            .Select(member => (implementation: type.FindImplementationForInterfaceMember(member), implemented: member))
                            .Where(kvp => kvp.implementation != null))
                    .ToDictionarySafe(kvp => kvp.implemented, kvp => kvp.implementation);

                result.memberByImplementedLookup = result.interfaceMemberToImplementationMap.ToLookup(kvp => kvp.Value, kvp => kvp.Key, SymbolEqualityComparer.Default);

                var directInterfaceImplementations = new HashSet<INamedTypeSymbol>(type.Interfaces, SymbolEqualityComparer.Default);
                foreach (var entry in result.interfaceMemberToImplementationMap)
                {
                    var interfaceMember = entry.Key;
                    var implementation = entry.Value;

                    if (Features.AddDefinitionForInheritedInterfaceImplementations)
                    {
                        if (!implementation.ContainingType.SymbolEquals(type) && directInterfaceImplementations.Contains(interfaceMember.ContainingType))
                        {
                            var reparentedSymbol = BaseSymbolWrapper.WrapWithOverrideContainer(implementation, type);

                            // Call to trigger addition of the symbol to the set of symbols referenced by the project
                            // TODO: Specify that the definition should not show up in the referenced definitions
                            GetReferenceSymbol(reparentedSymbol, ReferenceKind.Definition, addReferenceDefinitions: false);
                        }
                    }
                }

                return result;
            }
        }

        private void AddReferencesToOverriddenMembers(
            SymbolSpan symbolSpan,
            ISymbol declaredSymbol,
            bool excludeFromSearch = false,
            SymbolId relatedDefinition = default(SymbolId))
        {
            if (!declaredSymbol.IsOverride)
            {
                return;
            }

            var overriddenSymbol = GetOverriddenSymbol(declaredSymbol);
            if (overriddenSymbol != null)
            {
                references.Add(symbolSpan.CreateReference(GetReferenceSymbol(overriddenSymbol, ReferenceKind.Override), relatedDefinition)
                    .ApplyIf(excludeFromSearch, static r => r.Reference.ExcludeFromSearch = true));
            }

            // TODO: Should we add transitive overrides
        }

        private ISymbol GetOverriddenSymbol(ISymbol declaredSymbol)
        {
            IMethodSymbol method = declaredSymbol as IMethodSymbol;
            if (method != null)
            {
                return method.OverriddenMethod;
            }

            IPropertySymbol property = declaredSymbol as IPropertySymbol;
            if (property != null)
            {
                return property.OverriddenProperty;
            }

            IEventSymbol eventSymbol = declaredSymbol as IEventSymbol;
            if (eventSymbol != null)
            {
                return eventSymbol.OverriddenEvent;
            }

            return null;
        }

        private ISymbol GetExplicitlyImplementedMember(ISymbol symbol)
        {
            IMethodSymbol methodSymbol = symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                return methodSymbol.ExplicitInterfaceImplementations.FirstOrDefault();
            }

            IPropertySymbol propertySymbol = symbol as IPropertySymbol;
            if (propertySymbol != null)
            {
                return propertySymbol.ExplicitInterfaceImplementations.FirstOrDefault();
            }

            IEventSymbol eventSymbol = symbol as IEventSymbol;
            if (eventSymbol != null)
            {
                return eventSymbol.ExplicitInterfaceImplementations.FirstOrDefault();
            }

            return null;
        }

        private DefinitionSymbol GetDefinitionSymbol(ISymbol symbol, string id = null)
        {
            // Use unspecialized generic
            symbol = symbol.OriginalDefinition;
            id = id ?? GetDocumentationCommentId(symbol);

            ISymbol displaySymbol = symbol;

            bool isMember = symbol.Kind == SymbolKind.Field ||
                symbol.Kind == SymbolKind.Method ||
                symbol.Kind == SymbolKind.Event ||
                symbol.Kind == SymbolKind.Property;

            bool isEntryPoint = false;

            ITypeSymbol extensionContainerType = null;
            if (isMember)
            {
                if (symbol.Kind == SymbolKind.Method && symbol is IMethodSymbol methodSymbol)
                {
                    isEntryPoint = methodSymbol.SymbolEquals(CompilationServices.EntryPoint.Value);

                    if (methodSymbol.MethodKind == MethodKind.UserDefinedOperator)
                    {
                        displaySymbol = new OperatorMethodSymbolDisplayOverride(methodSymbol);
                    }

                    if (methodSymbol.IsExtensionMethod
                        && methodSymbol.Parameters[0].Type is { } thisParameterType)
                    {
                        if (thisParameterType.TypeKind != TypeKind.TypeParameter)
                        {
                            extensionContainerType = thisParameterType;
                        }
                        else if (thisParameterType is ITypeParameterSymbol typeParam)
                        {
                            extensionContainerType = typeParam.ConstraintTypes.SingleOrDefaultNoThrow();
                        }
                    }
                }
            }

            string containerQualifierName = string.Empty;
            if (symbol.ContainingSymbol != null)
            {
                containerQualifierName = symbol.ContainingSymbol.GetQualifiedName();
            }

            var displayParts = symbol.GetDisplayParts();

            var boundSymbol = new DefinitionSymbol()
            {
                ProjectId = symbol.GetProjectId(),
                Id = CreateSymbolId(id),
                ContainerTypeSymbolId = CreateSymbolId(symbol.ContainingType),
                DisplayName = displayParts.ToDisplayString(),
                Classifications = displayParts.GetClassifications(),
                ShortName = displaySymbol.ToSymbolDisplayString(DisplayFormats.ShortNameDisplayFormat),
                ContainerQualifiedName = containerQualifierName,
                Kind = GetSymbolKind(symbol),
                Glyph = symbol.GetDisplayGlyph(),
                SymbolDepth = symbol.GetSymbolDepth(),
                Comment = symbol.GetDocumentationCommentXml(),
                DeclarationName = displaySymbol.ToDeclarationName(),
                TypeName = GetTypeName(symbol),
            };

            if (extensionContainerType != null)
            {
                extensionContainerType = extensionContainerType.OriginalDefinition ?? extensionContainerType;
                boundSymbol.ExtensionInfo = new()
                {
                    ProjectId = extensionContainerType.GetProjectId(),
                    ContainerQualifiedName = extensionContainerType.GetQualifiedName()
                };
            }

            return boundSymbol;
        }


        private static SymbolId CreateSymbolId(ISymbol symbol)
        {
            if (symbol == null)
            {
                return default;
            }

            return CreateSymbolId(GetDocumentationCommentId(symbol));
        }

        /// <summary>
        /// Transforms a documentation id into a symbol id
        /// </summary>
        private static SymbolId CreateSymbolId(string id)
        {
            return SymbolId.CreateFromId(id);
        }

        private ReferenceSpan GetReferenceSpan(SymbolSpan span, ISymbol symbol, string id, SyntaxToken token, SpanAnalysisState state)
        {
            (ReferenceKind referenceKind, SymbolId relatedDefinitionId) = DetermineReferenceKind(symbol, token, span, state);

            var referenceSymbol = GetReferenceSymbol(symbol, referenceKind, id);
            //if (!relatedDefinitionId.IsValid && referenceKind == ReferenceKind.TypeForwardedTo)
            //{
            //    // For type forwards, the related definition has the same id as the reference symbol
            //    relatedDefinitionId = referenceSymbol.Id;
            //}

            return span.CreateReference(referenceSymbol, relatedDefinitionId);
        }

        private (ReferenceKind kind, SymbolId relatedDefinitionId) DetermineReferenceKind(ISymbol referencedSymbol, SyntaxToken token, SymbolSpan span, SpanAnalysisState spanState)
        {
            (ReferenceKind kind, SymbolId relatedDefinitionId) result = (ReferenceKind.Reference, default);
            // Case: nameof() - Do we really care about distinguishing this case.

            if ((spanState != null || State.TryGetStateAt(token, out spanState)) && spanState.ReferenceKind != null)
            {
                result.kind = spanState.ReferenceKind.Value;
                return result;
            }

            if (referencedSymbol.Kind == SymbolKind.NamedType)
            {
                var node = GetBindableParent(token);

                if (node.HasAncestorOrSelf<CSS.TypeArgumentListSyntax, VBS.TypeArgumentListSyntax>())
                {
                    return result;
                }

                Placeholder.Todo("Implement support for as cast reference kind");
                //if (node.TryGetAncestorOrSelfCS<CSS.BinaryExpressionSyntax>(out var binaryExpression)
                //    && binaryExpression.OperatorToken.IsEquivalentKind(CS.SyntaxKind.AsKeyword))
                //{
                //    var operation = SemanticModel.GetOperation(binaryExpression);
                //}

                if (node.TryGetAncestorOrSelf<CSS.BaseListSyntax, VBS.InheritsStatementSyntax>(out var baseList)
                    || node.TryGetAncestorOrSelfVB<VBS.ImplementsStatementSyntax>(out baseList))
                {
                    var typeDeclaration = baseList.Parent;
                    if (typeDeclaration != null)
                    {
                        var derivedType = SemanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
                        if (derivedType != null)
                        {
                            INamedTypeSymbol baseSymbol = referencedSymbol as INamedTypeSymbol;
                            if (baseSymbol != null)
                            {
                                void setRelatedTypeKind(ReferenceKind targetKind)
                                {
                                    result.relatedDefinitionId = CreateSymbolId(GetDocumentationCommentId(derivedType));
                                    result.kind = targetKind;

                                    if (targetKind == ReferenceKind.InterfaceImplementation)
                                    {
                                        AddSyntheticInterfaceMemberImplementations(
                                            interfaceSymbol: baseSymbol,
                                            implementerSymbol: derivedType);
                                    }
                                }

                                if (baseSymbol.TypeKind == TypeKind.Class && baseSymbol.Equals(derivedType.BaseType))
                                {
                                    setRelatedTypeKind(ReferenceKind.DerivedType);
                                }
                                else if (baseSymbol.TypeKind == TypeKind.Interface)
                                {
                                    if (derivedType.Interfaces.Contains(baseSymbol))
                                    {
                                        setRelatedTypeKind(derivedType.TypeKind == TypeKind.Interface
                                            ? ReferenceKind.InterfaceInheritance
                                            : ReferenceKind.InterfaceImplementation);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (referencedSymbol.Kind == SymbolKind.Field ||
                referencedSymbol.Kind == SymbolKind.Property)
            {
                var node = GetBindableParent(token);
                if (IsWrittenTo(node))
                {
                    result.kind = ReferenceKind.Write;
                }
            }

            return result;
        }

        private void AddSyntheticInterfaceMemberImplementations(INamedTypeSymbol interfaceSymbol, INamedTypeSymbol implementerSymbol)
        {

            GetImplementedMemberLookup(implementerSymbol);
        }

        private ReferenceSymbol GetReferenceSymbol(
            ISymbol symbol,
            ReferenceKind referenceKind, 
            string id = null,
            bool addReferenceDefinitions = true)
        {
            // Use unspecialized generic
            // TODO: Consider adding reference symbol for specialized generic as well so one can find all references
            // to List<string>.Add rather than just List<T>.Add
            symbol = symbol.OriginalDefinition;

            var boundSymbol = new ReferenceSymbol()
            {
                ProjectId = symbol.ContainingAssembly.Name,
                Id = CreateSymbolId(id ?? GetDocumentationCommentId(symbol)),
                Kind = GetSymbolKind(symbol),
                ReferenceKind = referenceKind,
            };

            AddReferenceDefinitions(boundSymbol, symbol, addReferenceDefinitions);

            return boundSymbol;
        }

        private void AddReferenceDefinitions(IReferenceSymbol reference, ISymbol symbol, bool addReferenceDefinitions)
        {
            if (!string.IsNullOrEmpty(symbol.Name) && addReferenceDefinitions && Features.AddReferenceDefinitions)
            {
                DefinitionSymbol definition;
                if (!context.ReferenceDefinitionMap.TryGetValue(reference, out definition))
                {
                    var createdDefinition = GetDefinitionSymbol(symbol);
                    definition = context.ReferenceDefinitionMap.GetOrAdd(createdDefinition, createdDefinition);
                    if (createdDefinition == definition)
                    {
                        if (symbol.Kind != SymbolKind.Namespace &&
                            symbol.ContainingNamespace != null &&
                            !symbol.ContainingNamespace.IsGlobalNamespace)
                        {
                            var namespaceSymbol = GetReferenceSymbol(symbol.ContainingNamespace, ReferenceKind.Reference);
                            var extData = context.GetReferenceNamespaceData(namespaceSymbol);
                            definition.ExtData = extData;
                        }
                    }
                }

                if (reference.ReferenceKind != ReferenceKind.Definition)
                {
                    definition.IncrementReferenceCount();
                }
            }
        }

        private bool IsWrittenTo(SyntaxNode node)
        {
            bool result = semanticServices.IsWrittenTo(SemanticModel, node, CancellationToken.None);
            return result;
        }

        private ISymbol GetSymbol(SyntaxNode node, out bool isThis, SyntaxToken token)
        {
            var symbolInfo = SemanticModel.GetSymbolInfo(node);
            isThis = false;
            ISymbol symbol = symbolInfo.Symbol;
            if (symbol == null)
            {
                return null;
            }

            if (IsThisParameter(symbol))
            {
                isThis = token.IsEquivalentKind(CS.SyntaxKind.ThisKeyword);
                return symbol;
                //var typeInfo = SemanticModel.GetTypeInfo(node);
                //if (typeInfo.Type != null)
                //{
                //    return typeInfo.Type;
                //}
            }
            else if (IsFunctionValue(symbol))
            {
                var method = symbol.ContainingSymbol as IMethodSymbol;
                if (method != null)
                {
                    if (method.AssociatedSymbol != null)
                    {
                        return method.AssociatedSymbol;
                    }
                    else
                    {
                        return method;
                    }
                }
            }
            else if (symbol.Kind == SymbolKind.Method)
            {
                var method = symbol as IMethodSymbol;
                if (method != null)
                {
                    if (method.ReducedFrom != null)
                    {
                        return method.ReducedFrom;
                    }
                }
            }

            symbol = ResolveAccessorParameter(symbol);

            return symbol;
        }

        private static string GetTypeName(ISymbol symbol)
        {
            if (symbol.Kind == SymbolKind.Method)
            {
                // Case: Constructor
                IMethodSymbol methodSymbol = symbol as IMethodSymbol;
                return methodSymbol.ReturnType?.ToSymbolDisplayString(DisplayFormats.TypeNameDisplayFormat);

            }
            else if (symbol.Kind == SymbolKind.Property)
            {
                IPropertySymbol propertySymbol = symbol as IPropertySymbol;
                return propertySymbol.Type?.ToSymbolDisplayString(DisplayFormats.TypeNameDisplayFormat);
            }
            else if (symbol.Kind == SymbolKind.Field)
            {
                IFieldSymbol fieldSymbol = symbol as IFieldSymbol;
                return fieldSymbol.Type?.ToSymbolDisplayString(DisplayFormats.TypeNameDisplayFormat);
            }

            return null;
        }

        private static string GetDocumentationCommentId(ISymbol symbol)
        {
            string result = null;
            if (!symbol.IsDefinition)
            {
                symbol = symbol.OriginalDefinition;
            }

            result = symbol.GetDocumentationCommentId();
            if (result == null)
            {
                result = symbol.ToSymbolDisplayString();
            }
            else
            {
                result = result.Replace("#ctor", "ctor");
            }

            return result;
        }

        private ISymbol ResolveAccessorParameter(ISymbol symbol)
        {
            if (symbol == null || !symbol.IsImplicitlyDeclared)
            {
                return symbol;
            }

            var parameterSymbol = symbol as IParameterSymbol;
            if (parameterSymbol == null)
            {
                return symbol;
            }

            var accessorMethod = parameterSymbol.ContainingSymbol as IMethodSymbol;
            if (accessorMethod == null)
            {
                return symbol;
            }

            var property = accessorMethod.AssociatedSymbol as IPropertySymbol;
            if (property == null)
            {
                return symbol;
            }

            int ordinal = parameterSymbol.Ordinal;
            if (property.Parameters.Length <= ordinal)
            {
                return symbol;
            }

            return property.Parameters[ordinal];
        }

        private static bool IsFunctionValue(ISymbol symbol)
        {
            return symbol is ILocalSymbol && ((ILocalSymbol)symbol).IsFunctionValue;
        }

        private static bool IsThisParameter(ISymbol symbol)
        {
            return symbol != null && symbol.Kind == SymbolKind.Parameter && ((IParameterSymbol)symbol).IsThis;
        }

        private IEnumerable<ClassifiedSpan> MergeSpans(IEnumerable<ClassifiedSpan> classificationSpans)
        {
            ClassifiedSpan mergedSpan = default(ClassifiedSpan);
            bool skippedNonWhitespace = false;
            foreach (var span in classificationSpans)
            {
                if (!TryMergeSpan(ref mergedSpan, span, ref skippedNonWhitespace))
                {
                    // Reset skippedNonWhitespace value
                    skippedNonWhitespace = false;
                    yield return mergedSpan;
                    mergedSpan = span;
                }
            }

            if (!string.IsNullOrEmpty(mergedSpan.ClassificationType))
            {
                yield return mergedSpan;
            }
        }

        bool TryMergeSpan(ref ClassifiedSpan current, ClassifiedSpan next, ref bool skippedNonWhitespace)
        {
            if (next.ClassificationType == ClassificationTypeNames.WhiteSpace)
            {
                return true;
            }

            if (SkipSpan(next))
            {
                skippedNonWhitespace = true;
                return true;
            }

            try
            {
                if (string.IsNullOrEmpty(current.ClassificationType))
                {
                    current = next;
                    return true;
                }

                if (current.TextSpan.Equals(next.TextSpan) && !IsSemanticSpan(current) && IsSemanticSpan(next))
                {
                    // If there are completely overlapping spans. Take the span which is semantic over the span which is non-semantic.
                    current = next;
                    return true;
                }

                if (current.TextSpan.Contains(next.TextSpan))
                {
                    return true;
                }

                if (!AllowMerge(next))
                {
                    return false;
                }

                var normalizedClassification = NormalizeClassification(current);
                if (normalizedClassification != NormalizeClassification(next))
                {
                    return false;
                }

                if (current.TextSpan.End < next.TextSpan.Start && (skippedNonWhitespace || !IsWhitespace(current.TextSpan.End, next.TextSpan.Start)))
                {
                    return false;
                }

                current = new ClassifiedSpan(normalizedClassification, new TextSpan(current.TextSpan.Start, next.TextSpan.End - current.TextSpan.Start));
                return true;
            }
            finally
            {
                skippedNonWhitespace = false;
            }
        }

        bool IsWhitespace(int start, int endExclusive)
        {
            for (int i = start; i < endExclusive; i++)
            {
                if (!char.IsWhiteSpace(DocumentText[i]))
                {
                    return false;
                }
            }

            return true;
        }

        static bool AllowMerge(ClassifiedSpan span)
        {
            var classificationType = NormalizeClassification(span);

            switch (classificationType)
            {
                case ClassificationTypeNames.Comment:
                case ClassificationTypeNames.StringLiteral:
                case ClassificationTypeNames.XmlDocCommentComment:
                case ClassificationTypeNames.ExcludedCode:
                    return true;
                default:
                    return false;
            }
        }

        static bool SkipSpan(ClassifiedSpan span)
        {
            if (span.ClassificationType?.Contains("regex") == true)
            {
                return true;
            }

            switch (span.ClassificationType)
            {
                case ClassificationTypeNames.WhiteSpace:
                case ClassificationTypeNames.Punctuation:
                case ClassificationTypeNames.StringEscapeCharacter:
                case ClassificationTypeNames.StaticSymbol:
                    return true;
                default:
                    return false;
            }
        }

        static T ExchangeDefault<T>(ref T value)
        {
            var captured = value;
            value = default(T);
            return captured;
        }

        static string NormalizeClassification(ClassifiedSpan span)
        {
            if (span.ClassificationType == null)
            {
                return null;
            }

            switch (span.ClassificationType)
            {
                case ClassificationTypeNames.XmlDocCommentName:
                case ClassificationTypeNames.XmlDocCommentAttributeName:
                case ClassificationTypeNames.XmlDocCommentAttributeQuotes:
                case ClassificationTypeNames.XmlDocCommentCDataSection:
                case ClassificationTypeNames.XmlDocCommentComment:
                case ClassificationTypeNames.XmlDocCommentDelimiter:
                case ClassificationTypeNames.XmlDocCommentText:
                case ClassificationTypeNames.XmlDocCommentProcessingInstruction:
                    return ClassificationTypeNames.XmlDocCommentComment;
                case ClassificationTypeNames.FieldName:
                case ClassificationTypeNames.EnumMemberName:
                case ClassificationTypeNames.ConstantName:
                case ClassificationTypeNames.LocalName:
                case ClassificationTypeNames.ParameterName:
                case ClassificationTypeNames.MethodName:
                case ClassificationTypeNames.ExtensionMethodName:
                case ClassificationTypeNames.PropertyName:
                case ClassificationTypeNames.EventName:
                    return ClassificationTypeNames.Identifier;
                case ClassificationTypeNames.RecordClassName:
                case ClassificationTypeNames.RecordStructName:
                case ClassificationTypeNames.StructName:
                    return ClassificationTypeNames.ClassName;
                default:
                    return span.ClassificationType;
            }
        }

        static bool IsSemanticSpan(ClassifiedSpan span)
        {
            switch (NormalizeClassification(span))
            {
                case ClassificationTypeNames.Keyword:
                case ClassificationTypeNames.Identifier:
                case ClassificationTypeNames.Operator:
                case ClassificationTypeNames.ClassName:
                case ClassificationTypeNames.InterfaceName:
                case ClassificationTypeNames.StructName:
                case ClassificationTypeNames.EnumName:
                case ClassificationTypeNames.DelegateName:
                case ClassificationTypeNames.TypeParameterName:
                    return true;
                default:
                    return false;
            }
        }

        private SyntaxNode GetBindableParent(SyntaxToken token, SpanAnalysisState state = null)
        {
            if ((state != null || State.TryGetStateAt(token, out state)) && state.DeclarationNode.HasValue)
            {
                return state.DeclarationNode.Value;
            }

            var result = semanticServices.GetBindableParent(token);
            if (state != null)
            {
                state.DeclarationNode = result;
            }

            return result;
        }
    }
}
