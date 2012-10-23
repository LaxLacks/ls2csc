using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using Roslyn.Services.CSharp;
using Roslyn.Services.Formatting;
namespace ls2csc
{
    class AutoImplementedPropertyRewriter : SyntaxRewriter
    {
        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            node = (ClassDeclarationSyntax)base.VisitClassDeclaration(node);
            SyntaxList<MemberDeclarationSyntax> newMembers = new SyntaxList<MemberDeclarationSyntax>();
            foreach (MemberDeclarationSyntax member in node.Members)
            {
                if (member.Kind == SyntaxKind.PropertyDeclaration)
                {
                    PropertyDeclarationSyntax prop = (PropertyDeclarationSyntax)member;
                    SyntaxList<AccessorDeclarationSyntax> newAccessors = new SyntaxList<AccessorDeclarationSyntax>();
                    bool implementfield = false;
                    foreach (AccessorDeclarationSyntax accessor in prop.AccessorList.Accessors)
                    {
                        if (accessor.Body == null)
                        {
                            switch (accessor.Kind)
                            {
                                case SyntaxKind.GetAccessorDeclaration:
                                    implementfield = true;
                                    newAccessors = newAccessors.Add(accessor.WithBody(Syntax.Block(Syntax.ReturnStatement(Syntax.IdentifierName("_" + prop.Identifier.ValueText)))));
                                    break;
                                case SyntaxKind.SetAccessorDeclaration:
                                    implementfield = true;
                                    newAccessors = newAccessors.Add(accessor.WithBody(Syntax.Block(Syntax.ExpressionStatement(Syntax.BinaryExpression(SyntaxKind.AssignExpression, Syntax.IdentifierName("_" + prop.Identifier.ValueText), Syntax.IdentifierName("value"))))));
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
                        variables = variables.Add(Syntax.VariableDeclarator("_" + prop.Identifier.ValueText));
                        newMembers = newMembers.Add(Syntax.FieldDeclaration(Syntax.VariableDeclaration(prop.Type, variables)));
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
