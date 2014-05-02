// http://roslyn.codeplex.com/wikipage?title=FAQ&referringTitle=Home#What%20happened%20to%20the%20REPL%20and%20hosting%20scripting%20APIs
#if SCRIPTING_API_REINTRODUCED
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CSharp;

namespace ls2csc.Optimizers
{
    class CondenseLiteralsRewriter : CSharpSyntaxRewriter
    {
        CommonScriptEngine engine;
        Session session;
        public CondenseLiteralsRewriter()
        {
            engine = new ScriptEngine();
            session = engine.CreateSession();
        }

        public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            node = node.WithLeft((ExpressionSyntax)base.Visit((SyntaxNode)node.Left));
            node = node.WithRight((ExpressionSyntax)base.Visit((SyntaxNode)node.Right));

            switch (node.Left.CSharpKind())
            {
                case SyntaxKind.CharacterLiteralExpression:
                case SyntaxKind.FalseLiteralExpression:
                case SyntaxKind.NumericLiteralExpression:
                case SyntaxKind.StringLiteralExpression:
                case SyntaxKind.TrueLiteralExpression:
                    break;
                default:
                    return node;
            }
            switch (node.Right.CSharpKind())
            {
                case SyntaxKind.CharacterLiteralExpression:
                case SyntaxKind.FalseLiteralExpression:
                case SyntaxKind.NumericLiteralExpression:
                case SyntaxKind.StringLiteralExpression:
                case SyntaxKind.TrueLiteralExpression:
                    break;
                default:
                    return node;
            }

            dynamic nodeValue = (dynamic)session.Execute(node.ToFullString());
            if (nodeValue is String)
            {
                return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(nodeValue));
            }
            else if (nodeValue is Char)
            {
                return SyntaxFactory.LiteralExpression(SyntaxKind.CharacterLiteralExpression, SyntaxFactory.Literal(nodeValue));
            }
            else if (nodeValue is Boolean)
            {
                if (nodeValue == true)
                {
                    return SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression, SyntaxFactory.Token(SyntaxKind.TrueKeyword));
                }
                else
                {
                    return SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression, SyntaxFactory.Token(SyntaxKind.FalseKeyword));
                }
            }
            return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(nodeValue));
        }

        public override SyntaxNode VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
        {
            node = node.WithExpression((ExpressionSyntax)base.Visit((SyntaxNode)node.Expression));
            switch (node.Expression.CSharpKind())
            {
                case SyntaxKind.CharacterLiteralExpression:
                case SyntaxKind.FalseLiteralExpression:
                case SyntaxKind.NumericLiteralExpression:
                case SyntaxKind.StringLiteralExpression:
                case SyntaxKind.TrueLiteralExpression:
                    return node.Expression;
            }

            return node;
        }
    }

}
#endif
