using System;
using System.Threading;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.LanguageService;
using static Codex.Analysis.ExtensionMethods;

namespace Codex.Analysis
{
    public class SemanticServices
    {
        private readonly Func<SyntaxToken, SyntaxNode> getBindableParentDelegate;
        private readonly Func<SemanticModel, SyntaxNode, CancellationToken, bool> isWrittenToDelegate;
        private readonly string language;
        private readonly HostLanguageServices languageServices;
        public LanguageName Language;

        public SemanticServices(Workspace workspace, string language)
        {
            this.language = language;
            Language = language;

            languageServices = workspace.Services.GetLanguageServices(language);

            var syntaxFactsService = languageServices.GetService<ISyntaxFactsService>();
            var semanticFactsService = languageServices.GetService<ISemanticFactsService>();

            isWrittenToDelegate = semanticFactsService.IsWrittenTo;
            getBindableParentDelegate = syntaxFactsService.TryGetBindableParent;
        }

        public SyntaxNode GetBindableParent(SyntaxToken syntaxToken)
        {
            return getBindableParentDelegate(syntaxToken);
        }

        public bool IsWrittenTo(SemanticModel semanticModel, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            return isWrittenToDelegate(semanticModel, syntaxNode, cancellationToken);
        }

        /// <summary>
        /// Determines which tokens can possibly be semantic. Currently, implemented by exclusion,
        /// (i.e. return true for all token kinds except those known not to be semantic)
        /// </summary>
        public bool IsPossibleSemanticToken(SyntaxToken token)
        {
            if (LanguageName.CSharp == language)
            {
                switch ((CS.SyntaxKind)token.RawKind)
                {
                    case CS.SyntaxKind.NamespaceKeyword:

                    // Visibility
                    case CS.SyntaxKind.PublicKeyword:
                    case CS.SyntaxKind.ProtectedKeyword:
                    case CS.SyntaxKind.PrivateKeyword:
                    case CS.SyntaxKind.InternalKeyword:

                    // Type declaration
                    case CS.SyntaxKind.ClassKeyword:
                    case CS.SyntaxKind.EnumKeyword:
                    case CS.SyntaxKind.StructKeyword:
                    case CS.SyntaxKind.InterfaceKeyword:
                    case CS.SyntaxKind.RecordKeyword:

                    case CS.SyntaxKind.NewKeyword:
                    case CS.SyntaxKind.IfKeyword:
                    case CS.SyntaxKind.ElseKeyword:
                    case CS.SyntaxKind.ForKeyword:
                    case CS.SyntaxKind.ReadOnlyKeyword:
                    case CS.SyntaxKind.StaticKeyword:
                    case CS.SyntaxKind.EqualsToken:
                    case CS.SyntaxKind.ReturnKeyword:
                    case CS.SyntaxKind.DotToken:
                    case CS.SyntaxKind.MinusGreaterThanToken:
                        return false;
                }
            }
            else if(LanguageName.VisualBasic == language)
            {
                switch ((VB.SyntaxKind)token.RawKind)
                {
                    case VB.SyntaxKind.NamespaceKeyword:
                    case VB.SyntaxKind.OfKeyword:
                    case VB.SyntaxKind.AsKeyword:

                    // Visibility
                    case VB.SyntaxKind.PublicKeyword:
                    case VB.SyntaxKind.ProtectedKeyword:
                    case VB.SyntaxKind.PrivateKeyword:

                    // Type declaration
                    case VB.SyntaxKind.ClassKeyword:
                    case VB.SyntaxKind.EnumKeyword:
                    case VB.SyntaxKind.StructureKeyword:
                    case VB.SyntaxKind.InterfaceKeyword:

                    case VB.SyntaxKind.NewKeyword:
                    case VB.SyntaxKind.IfKeyword:
                    case VB.SyntaxKind.ForKeyword:
                    case VB.SyntaxKind.ElseKeyword:
                    case VB.SyntaxKind.ReadOnlyKeyword:
                    case VB.SyntaxKind.StaticKeyword:
                    case VB.SyntaxKind.EqualsToken:
                    case VB.SyntaxKind.ReturnKeyword:
                    case VB.SyntaxKind.DotToken:
                        return false;
                }
            }

            return true;
        }

        public bool IsOverrideKeyword(SyntaxToken token)
        {
            return token.IsEquivalentKind(CS.SyntaxKind.OverrideKeyword);
        }

        public bool IsPartialKeyword(SyntaxToken token)
        {
            return token.IsEquivalentKind(CS.SyntaxKind.PartialKeyword);
        }

        public SyntaxNode TryGetUsingExpressionFromToken(SyntaxToken token)
        {
            if (token.IsCS())
            {
                if (token.IsKind(CS.SyntaxKind.UsingKeyword))
                {
                    var node = token.Parent;
                    if (node.IsKind(CS.SyntaxKind.UsingStatement))
                    {
                        var usingStatement = (CS.Syntax.UsingStatementSyntax)node;

                        return (SyntaxNode)usingStatement.Expression ?? usingStatement.Declaration?.Type;
                    }
                    else if (node.IsKind(CS.SyntaxKind.LocalDeclarationStatement))
                    {
                        var usingStatement = (CS.Syntax.LocalDeclarationStatementSyntax)node;

                        return usingStatement.Declaration?.Type;
                    }
                }
            }
            else if(token.IsVB())
            {
                if (token.IsKind(VB.SyntaxKind.UsingKeyword))
                {
                    var node = token.Parent;
                    if (node.IsKind(VB.SyntaxKind.UsingStatement))
                    {
                        var usingStatement = (VB.Syntax.UsingStatementSyntax)node;
                        return (SyntaxNode)usingStatement.Expression ?? usingStatement.Variables.FirstOrDefault();
                    }
                }
            }

            return null;
        }

        public SyntaxNode GetEventField(SyntaxNode bindableNode)
        {
            if (language == LanguageNames.CSharp)
            {
                if (bindableNode.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.EventFieldDeclaration))
                {
                    var eventFieldSyntax = bindableNode as Microsoft.CodeAnalysis.CSharp.Syntax.EventFieldDeclarationSyntax;
                    if (eventFieldSyntax != null)
                    {
                        bindableNode = eventFieldSyntax.Declaration.Variables[0];
                    }
                }
            }

            return bindableNode;
        }
    }
}
