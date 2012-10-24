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
    class ForeachRewriter : SyntaxRewriter
    {
        public override SyntaxNode VisitForEachStatement(ForEachStatementSyntax node)
        {
            /*
{
    var s__iter = scripts.GetEnumerator();
    using(s__iter as IDisposable) 
    {
        if(s__iter.MoveNext()) 
        {
            do
            {
                Script s = (Script)s__iter.Current;
                // do stuff
            }
            while(s__iter.MoveNext());
        }
    }              
}
             */
            string itername = node.Identifier.ToString() + "__iter";

            return base.Visit
            (
                Syntax.Block
                (
                    Syntax.LocalDeclarationStatement
                    (
                        Syntax.VariableDeclaration
                        (
                            Syntax.IdentifierName
                            (
                                "var"
                            ),
                            Syntax.SeparatedList<VariableDeclaratorSyntax>
                            (
                                Syntax.VariableDeclarator
                                (
                                    Syntax.Identifier(itername),
                                    null,
                                    Syntax.EqualsValueClause
                                    (
                                        Syntax.Token(SyntaxKind.EqualsToken),
                                        Syntax.InvocationExpression
                                        (
                                            Syntax.MemberAccessExpression
                                            (
                                                SyntaxKind.MemberAccessExpression,
                                                node.Expression,
                                                Syntax.IdentifierName("GetEnumerator")
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    Syntax.UsingStatement
                    (
                        null,
                        Syntax.CastExpression
                        (
                            Syntax.ParseTypeName("System.IDisposable"),
                            Syntax.IdentifierName(itername)
                        ),
                        Syntax.Block
                        (
                            Syntax.IfStatement
                            (
                                Syntax.InvocationExpression
                                (
                                    Syntax.MemberAccessExpression
                                    (
                                        SyntaxKind.MemberAccessExpression,
                                        Syntax.IdentifierName(itername),
                                        Syntax.IdentifierName("MoveNext")
                                    )
                                ),
                                Syntax.Block
                                (
                                    Syntax.DoStatement
                                    (
                                        Syntax.Block
                                        (
                                            Syntax.LocalDeclarationStatement
                                            (
                                                Syntax.VariableDeclaration
                                                (
                                                    node.Type, 
                                                    Syntax.SeparatedList<VariableDeclaratorSyntax>
                                                    (
                                                        Syntax.VariableDeclarator
                                                        (
                                                            node.Identifier,
                                                            null,
                                                            Syntax.EqualsValueClause
                                                            (
                                                                Syntax.CastExpression
                                                                (
                                                                    node.Type,
                                                                    Syntax.MemberAccessExpression
                                                                    (
                                                                        SyntaxKind.MemberAccessExpression,
                                                                        Syntax.IdentifierName(itername),
                                                                        Syntax.IdentifierName("Current")
                                                                    )
                                                                )
                                                            )
                                                        )
                                                    )
                                                )
                                            ),
                                            node.Statement
                                        ),
                                        Syntax.InvocationExpression(
                                            Syntax.MemberAccessExpression
                                            (
                                                SyntaxKind.MemberAccessExpression,
                                                Syntax.IdentifierName(itername),
                                                Syntax.IdentifierName("MoveNext")
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            ).NormalizeWhitespace();

        }
    }

}