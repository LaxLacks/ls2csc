using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ls2csc
{
    class ForeachRewriter : CSharpSyntaxRewriter
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
                SyntaxFactory.Block
                (
                    SyntaxFactory.LocalDeclarationStatement
                    (
                        SyntaxFactory.VariableDeclaration
                        (
                            SyntaxFactory.IdentifierName
                            (
                                "var"
                            ),
                            SyntaxFactory.SeparatedList<VariableDeclaratorSyntax>
                            (
                                new VariableDeclaratorSyntax[] {
                                SyntaxFactory.VariableDeclarator
                                (
                                    SyntaxFactory.Identifier(itername),
                                    null,
                                    SyntaxFactory.EqualsValueClause
                                    (
                                        SyntaxFactory.Token(SyntaxKind.EqualsToken),
                                        SyntaxFactory.InvocationExpression
                                        (
                                            SyntaxFactory.MemberAccessExpression
                                            (
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                node.Expression,
                                                SyntaxFactory.IdentifierName("GetEnumerator")
                                            )
                                        )
                                    )
                                )
                            }
                            )
                        )
                    ),
                    SyntaxFactory.UsingStatement
                    (
                        null,
                        SyntaxFactory.CastExpression
                        (
                            SyntaxFactory.ParseTypeName("System.IDisposable"),
                            SyntaxFactory.IdentifierName(itername)
                        ),
                        SyntaxFactory.Block
                        (
                            SyntaxFactory.IfStatement
                            (
                                SyntaxFactory.InvocationExpression
                                (
                                    SyntaxFactory.MemberAccessExpression
                                    (
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName(itername),
                                        SyntaxFactory.IdentifierName("MoveNext")
                                    )
                                ),
                                SyntaxFactory.Block
                                (
                                    SyntaxFactory.DoStatement
                                    (
                                        SyntaxFactory.Block
                                        (
                                            SyntaxFactory.LocalDeclarationStatement
                                            (
                                                SyntaxFactory.VariableDeclaration
                                                (
                                                    node.Type, 
                                                    SyntaxFactory.SeparatedList<VariableDeclaratorSyntax>
                                                    (
                                                        new VariableDeclaratorSyntax[] { 
                                                        SyntaxFactory.VariableDeclarator
                                                        (
                                                            node.Identifier,
                                                            null,
                                                            SyntaxFactory.EqualsValueClause
                                                            (
                                                                SyntaxFactory.CastExpression
                                                                (
                                                                    node.Type,
                                                                    SyntaxFactory.MemberAccessExpression
                                                                    (
                                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                                        SyntaxFactory.IdentifierName(itername),
                                                                        SyntaxFactory.IdentifierName("Current")
                                                                    )
                                                                )
                                                            )
                                                        )
                                                        }
                                                    )
                                                )
                                            ),
                                            node.Statement
                                        ),
                                        SyntaxFactory.InvocationExpression(
                                            SyntaxFactory.MemberAccessExpression
                                            (
                                                SyntaxKind.SimpleMemberAccessExpression,
                                                SyntaxFactory.IdentifierName(itername),
                                                SyntaxFactory.IdentifierName("MoveNext")
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