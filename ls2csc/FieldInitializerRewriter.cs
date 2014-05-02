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
    class FieldInitializerRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            node = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);
            MethodDeclarationSyntax prector = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), ".prector");
            List<StatementSyntax> Initializers = new List<StatementSyntax>();
            foreach (MemberDeclarationSyntax member in node.Members)
            {
                if (member.CSharpKind() == SyntaxKind.FieldDeclaration)
                {
                    FieldDeclarationSyntax fds = (FieldDeclarationSyntax)member;
                    foreach (VariableDeclaratorSyntax vds in fds.Declaration.Variables)
                    {
                        if (vds.Initializer != null)
                        {
                            Initializers.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.BinaryExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName(vds.Identifier), vds.Initializer.Value)));
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
                ConstructorDeclarationSyntax ctor = SyntaxFactory.ConstructorDeclaration(node.Identifier);
                ctor = ctor.AddBodyStatements(SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(".prector"))));
                ctor = ctor.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));
                return node.AddMembers(ctor);
            }

            SyntaxList<MemberDeclarationSyntax> newMembers = new SyntaxList<MemberDeclarationSyntax>();

            foreach (MemberDeclarationSyntax member in node.Members)
            {
                if (member.CSharpKind() == SyntaxKind.ConstructorDeclaration)
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
            node = node.WithBody(node.Body.WithStatements(node.Body.Statements.Insert(0, SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(".prector"))))));
            return node;
        }
    }
}