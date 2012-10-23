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

            string new_block = @"
{
    var " + itername + @" = " + node.Expression.ToString() + @".GetEnumerator();
    using(" + itername + @" as IDisposable) 
    {
        if(" + itername + @".MoveNext()) 
        {
            do
            {
                " + node.Type.ToString() + " " + node.Identifier.ToString() + @" = (" + node.Type.ToString() + ")" + itername + @".Current;
                " + node.Statement.ToString() + @"
            }
            while(" + itername + @".MoveNext());
        }
    }              
}";

            return Syntax.ParseStatement(new_block);
            // umm.. easier to have it parsed. ;)
            /*
            return Syntax.Block
                (
                    Syntax.LocalDeclarationStatement
                    (
                        Syntax.VariableDeclaration
                        (
                            Syntax.IdentifierName
                            (
                                Syntax.Token(SyntaxKind.TypeVarKeyword)
                            ),
                            Syntax.SeparatedList<VariableDeclaratorSyntax>
                            (
                                Syntax.VariableDeclarator
                                (
                                    Syntax.Identifier("__iter"), 
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

                );
             */
        }
    }

}