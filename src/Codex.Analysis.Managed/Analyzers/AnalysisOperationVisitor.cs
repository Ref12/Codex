using System;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;

namespace Codex.Analysis.Managed
{
    internal partial class AnalysisOperationVisitor : ScopeOperationWalker<AnalysisState>
    {
        private DocumentAnalyzer Owner;

        public int ScopeDepth;
        public int SymbolDepth;

        public AnalysisOperationVisitor(DocumentAnalyzer owner)
        {
            Owner = owner;
        }

        public override object? VisitBinaryOperator(IBinaryOperation operation, AnalysisState argument)
        {
            if (operation.OperatorMethod !=  null)
            {
                if (operation.Syntax.WrapAs<CSS.BinaryExpressionSyntax>().TrySelect(be => be.OperatorToken, out var opToken))
                {
                    argument.Analyzer.AddSymbolSpan(operation.OperatorMethod, opToken);
                }
            }

            return base.VisitBinaryOperator(operation, argument);
        }

        public override object? VisitAwait(IAwaitOperation operation, AnalysisState argument)
        {
            return base.VisitAwait(operation, argument);
        }

        public override object? VisitIncrementOrDecrement(IIncrementOrDecrementOperation operation, AnalysisState argument)
        {
            VisitUnaryOperatorCore(operation, operation.OperatorMethod, argument);

            return base.VisitIncrementOrDecrement(operation, argument);
        }

        public override object? VisitUnaryOperator(IUnaryOperation operation, AnalysisState argument)
        {
            VisitUnaryOperatorCore(operation, operation.OperatorMethod, argument);

            return base.VisitUnaryOperator(operation, argument);
        }

        public void VisitUnaryOperatorCore(IOperation operation, IMethodSymbol operatorMethod, AnalysisState argument)
        {
            if (operatorMethod != null)
            {
                if (operation.Syntax.WrapAs<CSS.PrefixUnaryExpressionSyntax>().TrySelect(be => be.OperatorToken, out var opToken)
                    || operation.Syntax.WrapAs<CSS.PostfixUnaryExpressionSyntax>().TrySelect(be => be.OperatorToken, out opToken))
                {
                    argument.Analyzer.AddSymbolSpan(operatorMethod, opToken);
                }
            }
        }

        public override object VisitFieldInitializer(IFieldInitializerOperation operation, AnalysisState argument)
        {
            // Enum field initializer doesn't have parent.
            if (operation.Parent == null 
                && operation.Value.Kind == OperationKind.Literal
                //&& operation.Syntax.Wrap<CSS.EqualsValueClauseSyntax>().TrySelect(out var parent, e => e.Parent)
                && operation.Syntax.Parent.WrapAs<CSS.EnumMemberDeclarationSyntax>().TrySelect(e => e, out var enumMember)
                && operation.Value is ILiteralOperation literal
                && literal.ConstantValue.Value is IConvertible constantValue
                && constantValue.TryGetIntegralValue(out var constantInt))
            {
                foreach (var field in operation.InitializedFields)
                {
                    Owner.State.GetState(field).ConstantValue = constantInt;
                }
            }

            return base.VisitFieldInitializer(operation, argument);
        }

        public override object? VisitLocalFunction(ILocalFunctionOperation operation, AnalysisState argument)
        {
            VisitLocalSymbol(argument, operation.Symbol);

            BeforeVisitScope(operation, argument);
            foreach (var parameter in operation.Symbol.Parameters)
            {
                VisitLocalSymbol(argument, parameter);
            }

            return AfterVisitScope(operation, argument, base.VisitLocalFunction(operation, argument));
        }

        public override void VisitLocalSymbol(IOperation operation, ILocalSymbol symbol, AnalysisState argument)
        {
            VisitLocalSymbol(argument, symbol);
        }

        public override void BeforeVisitScope(IOperation operation, AnalysisState argument)
        {
            ScopeDepth++;
        }

        public override object AfterVisitScope(IOperation operation, AnalysisState argument, object? result)
        {
            ScopeDepth--;
            return result;
        }

        private void VisitLocalSymbol(AnalysisState argument, ISymbol local)
        {
            var symbolState = argument.GetState(local);
            symbolState.SymbolDepth ??= SymbolDepth + ScopeDepth;
        }

        public override object? VisitWith(IWithOperation operation, AnalysisState argument)
        {
            if (operation.Type != null &&
                operation.Syntax.TrySelectCS<CSS.WithExpressionSyntax, SyntaxToken>(out var withKeyword, w => w.WithKeyword))
            {
                var state = argument.GetState(withKeyword);
                state.ReferenceKind = ReferenceKind.CopyWith;

                argument.Analyzer.AddSymbolSpan(operation.Type, withKeyword);
            }
            return base.VisitWith(operation, argument);
        }

        public override object? VisitWithStatement(IWithStatementOperation operation, AnalysisState argument)
        {
            return base.VisitWithStatement(operation, argument);
        }

        public override object? VisitDeclarationPattern(IDeclarationPatternOperation operation, AnalysisState argument)
        {
            SdkFeatures.TestLogger.Value?.LogDiagnostic($"conversion syntax {operation.Kind} => {operation.Syntax.Kind()}");
            if (operation.Syntax.WrapAs<CSS.DeclarationPatternSyntax>().TrySelect(d => d.Type, out var type)
                && type.TryGetIdentifier(out var identifier))
            {
                AddSymbolSpan(operation.NarrowedType, identifier, ReferenceKind.ExplicitCast);
            }

            return base.VisitDeclarationPattern(operation, argument);
        }

        public override object VisitConversion(IConversionOperation operation, AnalysisState argument)
        {
            if (!operation.IsImplicit)
            {
                SdkFeatures.TestLogger.Value?.LogDiagnostic($"conversion syntax {operation.Kind} => {operation.Syntax.Kind()}");
                if (operation.Type is { } typeSymbol
                    && (operation.Syntax.TrySelect<CSS.CastExpressionSyntax, VBS.CastExpressionSyntax, SyntaxNode>(
                        out var type, node => node.Type, node => node.Type)
                        || operation.Syntax.TrySelect<CSS.BinaryExpressionSyntax, SyntaxNode>(out type, node => node.Right)
                        //|| operation.Syntax.WrapAs<CSS.IsPatternExpressionSyntax>().TrySelect(out type, node => node.type)
                        )
                    && type.TryGetIdentifier(out var identifier))
                {
                    AddSymbolSpan(typeSymbol, identifier, ReferenceKind.ExplicitCast);

                    AddSymbolSpan(operation.OperatorMethod, identifier, ReferenceKind.Reference);
                }
            }

            return base.VisitConversion(operation, argument);
        }

        private void AddSymbolSpan(ISymbol symbol, SyntaxToken token, ReferenceKind? kind = null, OperationKind? opKind = null)
        {
            if (symbol == null) return;
            Owner.AddSymbolSpan(symbol, token, configureState: state =>
            {
                state.Skip = false;
                if (kind != null) state.ReferenceKind = kind;
                if (opKind != null) state.OperationKind = opKind;
            });

        }

        public override object VisitObjectCreation(IObjectCreationOperation operation, AnalysisState argument)
        {
            if (operation.Constructor is { } constructor)
            {
                if (operation.Syntax.TrySelect(out var token, (CSS.ImplicitObjectCreationExpressionSyntax node) => node.NewKeyword)
                    || (operation.Syntax.TrySelect(out SyntaxNode type, (CSS.ObjectCreationExpressionSyntax node) => node.Type, (VBS.ObjectCreationExpressionSyntax node) => node.Type)
                        && type.TryGetIdentifier(out token)))
                {
                    AddSymbolSpan(constructor, token, opKind: OperationKind.ObjectCreation);
                }
            }

            return base.VisitObjectCreation(operation, argument);
        }

        public override object VisitPropertyReference(IPropertyReferenceOperation operation, AnalysisState argument)
        {
            if (operation.Property.ContainingType.IsAnonymousType)
            {
                return default;
            }

            if (operation.Syntax.TrySelect(out var token, (CSS.ElementAccessExpressionSyntax node) => node.ArgumentList.OpenBracketToken)
                || operation.Syntax.TrySelect(out token, (CSS.ImplicitElementAccessSyntax node) => node.ArgumentList.OpenBracketToken)
                || operation.Syntax.TrySelect(out token, (CSS.MemberAccessExpressionSyntax node) => node.Name.Identifier)
                || operation.Syntax.TrySelect(out token, (CSS.IdentifierNameSyntax node) => node.Identifier))
            {
                var state = argument.GetState(token);
                bool isWrite = operation.Parent is IAssignmentOperation;
                state.ReferenceKind = isWrite ? ReferenceKind.Write : ReferenceKind.Read;

                argument.Analyzer.AddSymbolSpan(operation.Property, token);
            }
            else
            {

            }

            return base.VisitPropertyReference(operation, argument);
        }

        public override object VisitAttribute(IAttributeOperation operation, AnalysisState argument)
        {
            if (operation.Operation is IObjectCreationOperation creation
                && creation.Constructor.SymbolEquals(Owner.CompilationServices.TypeForwarderConstructor.Value)
                && creation.Arguments[0].Value is ITypeOfOperation typeOf
                && typeOf.Syntax.TrySelect(out SyntaxNode node, (CSS.TypeOfExpressionSyntax node) => node.Type, (VBS.TypeOfExpressionSyntax node) => node.Type)
                && node.TryGetIdentifier(out var identifier))
            {
                var state = argument.GetState(identifier.SpanStart);
                state.ReferenceKind = ReferenceKind.TypeForwardedTo;
            }

            return base.VisitAttribute(operation, argument);
        }
    }
}
