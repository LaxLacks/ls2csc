using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax;

namespace ls2csc
{
    class PrefixUnaryToBinaryRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            switch (node.CSharpKind())
            {
                case SyntaxKind.PreIncrementExpression:
                    return SyntaxFactory.BinaryExpression(SyntaxKind.AddAssignmentExpression, node.Operand, SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1)));
                case SyntaxKind.PreDecrementExpression:
                    return SyntaxFactory.BinaryExpression(SyntaxKind.SubtractAssignmentExpression, node.Operand, SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(1)));
#if NEGATE_EXPRESSION
                    case SyntaxKind.NegateExpression:
                    if (node.Operand.CSharpKind() == SyntaxKind.NumericLiteralExpression)
                    {
                        dynamic newvalue = -((dynamic)((LiteralExpressionSyntax)node.Operand).Token.Value);
                        return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(newvalue));
                    }
                    return node;
#endif
                case SyntaxKind.LogicalNotExpression:
                    return SyntaxFactory.BinaryExpression(SyntaxKind.NotEqualsExpression, node.Operand, SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression));
            }
            throw new NotImplementedException("Unary prefix " + node.CSharpKind().ToString());
        }
    }
}
