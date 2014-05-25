using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace ls2csc
{
    class AutoImplementedPropertyRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            node = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);
            SyntaxList<MemberDeclarationSyntax> newMembers = new SyntaxList<MemberDeclarationSyntax>();
            foreach (MemberDeclarationSyntax member in node.Members)
            {
                if (member.CSharpKind() == SyntaxKind.PropertyDeclaration)
                {
                    PropertyDeclarationSyntax prop = (PropertyDeclarationSyntax)member;
                    SyntaxList<AccessorDeclarationSyntax> newAccessors = new SyntaxList<AccessorDeclarationSyntax>();
                    bool implementfield = false;
                    foreach (AccessorDeclarationSyntax accessor in prop.AccessorList.Accessors)
                    {
                        if (accessor.Body == null)
                        {
                            switch (accessor.CSharpKind())
                            {
                                case SyntaxKind.GetAccessorDeclaration:
                                    implementfield = true;
                                    newAccessors = newAccessors.Add(accessor.WithBody(SyntaxFactory.Block(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("_" + prop.Identifier.ValueText)))));
                                    break;
                                case SyntaxKind.SetAccessorDeclaration:
                                    implementfield = true;
                                    newAccessors = newAccessors.Add(accessor.WithBody(SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(SyntaxFactory.BinaryExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName("_" + prop.Identifier.ValueText), SyntaxFactory.IdentifierName("value"))))));
                                    break;
                                default:
                                    newAccessors = newAccessors.Add(accessor);
                                    break;
                            }
                        }
                        else
                        {
                            newAccessors = newAccessors.Add(accessor);
                        }
                    }
                    if (implementfield)
                    {
                        SeparatedSyntaxList<VariableDeclaratorSyntax> variables = new SeparatedSyntaxList<VariableDeclaratorSyntax>();
                        variables = variables.Add(SyntaxFactory.VariableDeclarator("_" + prop.Identifier.ValueText));
                        newMembers = newMembers.Add(SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(prop.Type, variables)).WithModifiers(prop.Modifiers));
                    }

                    newMembers = newMembers.Add(prop.WithAccessorList(prop.AccessorList.WithAccessors(newAccessors)));
                }
                else
                {
                    newMembers = newMembers.Add(member);
                }
            }
            return node.WithMembers(newMembers);
        }
    }
}
