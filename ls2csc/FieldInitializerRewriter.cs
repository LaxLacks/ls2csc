using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LS2IL;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


namespace ls2csc
{
    class FieldInitializerRewriter : CSharpSyntaxRewriter
    {
        public FieldInitializerRewriter(SemanticModel model)
        {
           // Model = model;
        }
        //public SemanticModel Model { get; private set; }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            node = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);
            MethodDeclarationSyntax prector = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), ".prector");
            MethodDeclarationSyntax precctor = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), ".precctor");
            precctor = precctor.AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

            List<StatementSyntax> Initializers = new List<StatementSyntax>();
            List<StatementSyntax> StaticInitializers = new List<StatementSyntax>();
            foreach (MemberDeclarationSyntax member in node.Members)
            {
                if (member.CSharpKind() == SyntaxKind.FieldDeclaration)
                {
                    FieldDeclarationSyntax fds = (FieldDeclarationSyntax)member;
                    
                    foreach (VariableDeclaratorSyntax vds in fds.Declaration.Variables)
                    {
                        if (vds.Initializer != null)
                        {
                            if (fds.Modifiers.ToString().Contains("static"))
                            {
                                StaticInitializers.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.BinaryExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName(vds.Identifier), vds.Initializer.Value)));
                            }
                            else
                            {
                                Initializers.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.BinaryExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName(vds.Identifier), vds.Initializer.Value)));
                            }
                        }
                    }
                }
            }


            if (Initializers.Count == 0 && StaticInitializers.Count == 0)
                return node;
            SyntaxList<MemberDeclarationSyntax> newMembers = new SyntaxList<MemberDeclarationSyntax>();

            if (Initializers.Count > 0)
            {
                int constructors = node.Members.Count((m) => (m is ConstructorDeclarationSyntax && !((ConstructorDeclarationSyntax)m).Modifiers.ToString().Contains("static")));

                prector = prector.AddBodyStatements(Initializers.ToArray());
                node = node.AddMembers(prector);

                if (constructors == 0)
                {
                    ConstructorDeclarationSyntax ctor = SyntaxFactory.ConstructorDeclaration(node.Identifier);
                    ctor = ctor.AddBodyStatements(SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(".prector"))));
                    ctor = ctor.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

                    newMembers = newMembers.Add(ctor);
                }
               // else
                {

                    foreach (MemberDeclarationSyntax member in node.Members)
                    {
                        if (member.CSharpKind() == SyntaxKind.ConstructorDeclaration && !((ConstructorDeclarationSyntax)member).Modifiers.ToString().Contains("static"))
                        {
                            newMembers = newMembers.Add((MemberDeclarationSyntax)ConstructorPrefixerDeclaration((ConstructorDeclarationSyntax)member));
                        }
                        else
                        {
                            newMembers = newMembers.Add(member);
                        }
                    }
                }
            }
            if (StaticInitializers.Count > 0)
            {
                int constructors = node.Members.Count((m) => (m is ConstructorDeclarationSyntax && ((ConstructorDeclarationSyntax)m).Modifiers.ToString().Contains("static")));

                precctor = precctor.AddBodyStatements(StaticInitializers.ToArray());
                node = node.AddMembers(precctor);

                if (constructors == 0)
                {
                    ConstructorDeclarationSyntax ctor = SyntaxFactory.ConstructorDeclaration(node.Identifier);
                    ctor = ctor.AddModifiers(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
                    ctor = ctor.AddBodyStatements(SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(".precctor"))));
                    ctor = ctor.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

                    newMembers = newMembers.Add(ctor);
                }
                //else
                {

                    foreach (MemberDeclarationSyntax member in node.Members)
                    {
                        if (member.CSharpKind() == SyntaxKind.ConstructorDeclaration && ((ConstructorDeclarationSyntax)member).Modifiers.ToString().Contains("static"))
                        {
                            newMembers = newMembers.Add((MemberDeclarationSyntax)StaticConstructorPrefixerDeclaration((ConstructorDeclarationSyntax)member));
                        }
                        else
                        {
                            newMembers = newMembers.Add(member);
                        }
                    }
                }
            }

            return node.WithMembers(newMembers);
        }

        public SyntaxNode ConstructorPrefixerDeclaration(ConstructorDeclarationSyntax node)
        {
            node = node.WithBody(node.Body.WithStatements(node.Body.Statements.Insert(0, SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(".prector"))))));
            return node;
        }
        public SyntaxNode StaticConstructorPrefixerDeclaration(ConstructorDeclarationSyntax node)
        {
            node = node.WithBody(node.Body.WithStatements(node.Body.Statements.Insert(0, SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(".precctor"))))));
            return node;
        }
    }
}