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
    class FieldInitializerRewriter : SyntaxRewriter
    {
        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            node = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);
            MethodDeclarationSyntax prector = Syntax.MethodDeclaration(Syntax.PredefinedType(Syntax.Token(SyntaxKind.VoidKeyword)), ".prector");
            List<StatementSyntax> Initializers = new List<StatementSyntax>();
            foreach (MemberDeclarationSyntax member in node.Members)
            {
                if (member.Kind == SyntaxKind.FieldDeclaration)
                {
                    FieldDeclarationSyntax fds = (FieldDeclarationSyntax)member;
                    foreach (VariableDeclaratorSyntax vds in fds.Declaration.Variables)
                    {
                        if (vds.Initializer != null)
                        {
                            Initializers.Add(Syntax.ExpressionStatement(Syntax.BinaryExpression(SyntaxKind.AssignExpression, Syntax.IdentifierName(vds.Identifier), vds.Initializer.Value)));
                        }
                    }
                }
            }


            if (Initializers.Count == 0)
                return node;
            

            int constructors = node.Members.Count((m) => (m is ConstructorDeclarationSyntax));

            prector = prector.AddBodyStatements(Initializers.ToArray());
            node = node.AddMembers(prector);

            if (constructors == 0)
            {
                ConstructorDeclarationSyntax ctor = Syntax.ConstructorDeclaration(node.Identifier);
                ctor = ctor.AddBodyStatements(Syntax.ExpressionStatement(Syntax.InvocationExpression(Syntax.IdentifierName(".prector"))));
                ctor = ctor.AddModifiers(Syntax.Token(SyntaxKind.PublicKeyword));
                return node.AddMembers(ctor);
            }

            SyntaxList<MemberDeclarationSyntax> newMembers = new SyntaxList<MemberDeclarationSyntax>();

            foreach (MemberDeclarationSyntax member in node.Members)
            {
                if (member.Kind == SyntaxKind.ConstructorDeclaration)
                {
                    newMembers = newMembers.Add((MemberDeclarationSyntax)ConstructorPrefixerDeclaration((ConstructorDeclarationSyntax)member));
                }
                else
                {
                    newMembers = newMembers.Add(member);
                }
            }

            return node.WithMembers(newMembers);
        }

        public SyntaxNode ConstructorPrefixerDeclaration(ConstructorDeclarationSyntax node)
        {
            node = node.WithBody(node.Body.WithStatements(node.Body.Statements.Insert(0, Syntax.ExpressionStatement(Syntax.InvocationExpression(Syntax.IdentifierName(".prector"))))));
            return node;
        }
    }
}