using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ls2csc
{
    /// <summary>
    /// Enums can be defined with implicit values
    /// </summary>
    class EnumValueRewriter : CSharpSyntaxRewriter
    {
        int lastValue = -1;

        public override SyntaxNode VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            lastValue = -1;
            return base.VisitEnumDeclaration(node);
        }
        public override SyntaxNode VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            if (node.EqualsValue == null)
            {
                lastValue++;
                return node.WithEqualsValue(SyntaxFactory.EqualsValueClause(SyntaxFactory.Token(SyntaxKind.EqualsToken),
                    SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(lastValue))
                    ));
            }
            else
            {
                ExpressionSyntax expr = node.EqualsValue.Value;
                if (expr.CSharpKind() == SyntaxKind.NumericLiteralExpression)
                {
                    lastValue = int.Parse(expr.ToString());
                    return node;
                }

                throw new NotImplementedException("enum with non-literal explicit value");
            }

            return base.VisitEnumMemberDeclaration(node);
        }
    }
}
