using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using Roslyn.Services.CSharp;

namespace ls2csc
{
    class PrefixUnaryToBinaryRewriter : SyntaxRewriter
    {
        public override SyntaxNode VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            switch (node.Kind)
            {
                case SyntaxKind.PreIncrementExpression: 
                    return Syntax.BinaryExpression(SyntaxKind.AddAssignExpression, node.Operand, Syntax.LiteralExpression(SyntaxKind.NumericLiteralExpression, Syntax.Literal(1)));
                case SyntaxKind.PreDecrementExpression:
                    return Syntax.BinaryExpression(SyntaxKind.SubtractAssignExpression, node.Operand, Syntax.LiteralExpression(SyntaxKind.NumericLiteralExpression, Syntax.Literal(1)));
                case SyntaxKind.NegateExpression:
                    if (node.Operand.Kind == SyntaxKind.NumericLiteralExpression)
                    {
                        dynamic newvalue = -((dynamic)((LiteralExpressionSyntax)node.Operand).Token.Value);
                        return Syntax.LiteralExpression(SyntaxKind.NumericLiteralExpression, Syntax.Literal(newvalue));
                    }
                    return node;
                case SyntaxKind.LogicalNotExpression:
                    return Syntax.BinaryExpression(SyntaxKind.NotEqualsExpression, node.Operand, Syntax.LiteralExpression(SyntaxKind.TrueLiteralExpression));
            }
            throw new NotImplementedException("Unary prefix " + node.Kind.ToString());
        }
    }
}
