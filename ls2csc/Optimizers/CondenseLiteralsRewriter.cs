using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using Roslyn.Services.CSharp;
using Roslyn.Scripting;
using Roslyn.Scripting.CSharp;

namespace ls2csc.Optimizers
{
    class CondenseLiteralsRewriter : SyntaxRewriter
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

            switch (node.Left.Kind)
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
            switch (node.Right.Kind)
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
                return Syntax.LiteralExpression(SyntaxKind.StringLiteralExpression, Syntax.Literal(nodeValue));
            }
            else if (nodeValue is Char)
            {
                return Syntax.LiteralExpression(SyntaxKind.CharacterLiteralExpression, Syntax.Literal(nodeValue));
            }
            else if (nodeValue is Boolean)
            {
                if (nodeValue == true)
                {
                    return Syntax.LiteralExpression(SyntaxKind.TrueLiteralExpression, Syntax.Literal(0));
                }
                else
                {
                    return Syntax.LiteralExpression(SyntaxKind.TrueLiteralExpression, Syntax.Literal(1));
                }
            }
            return Syntax.LiteralExpression(SyntaxKind.NumericLiteralExpression, Syntax.Literal(nodeValue));
        }

        public override SyntaxNode VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
        {
            node = node.WithExpression((ExpressionSyntax)base.Visit((SyntaxNode)node.Expression));
            switch (node.Expression.Kind)
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
