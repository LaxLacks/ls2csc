using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax;
using LS2IL;

namespace ls2csc
{
    class IndexerRewriter : CSharpSyntaxRewriter
    {
        public IndexerRewriter(SemanticModel model)
        {
           Model = model;
        }
        public SemanticModel Model { get; private set; }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            //int indexers = node.Members.Count((m) => (m is IndexerDeclarationSyntax && !((IndexerDeclarationSyntax)m)));
            SyntaxList<MemberDeclarationSyntax> members = SyntaxFactory.List<MemberDeclarationSyntax>();
            int numIndexers = 0;
            foreach (MemberDeclarationSyntax member in node.Members)
            {
                if (member.CSharpKind() == SyntaxKind.IndexerDeclaration)
                {
                    IndexerDeclarationSyntax ids = (IndexerDeclarationSyntax)member;
                    numIndexers++;

                    foreach (AccessorDeclarationSyntax ads in ids.AccessorList.Accessors)
                    {
                        if (ads.Keyword.ToString() == "set")
                        {
                            members = members.Add(
                                SyntaxFactory.MethodDeclaration(ads.AttributeLists, ads.Modifiers.AddRange(ids.Modifiers), SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), ids.ExplicitInterfaceSpecifier, SyntaxFactory.Identifier(ads.Keyword.ToString() + "_Item"), null, SyntaxFactory.ParameterList(ids.ParameterList.Parameters), SyntaxFactory.List<TypeParameterConstraintClauseSyntax>(), ads.Body)
                                );
                        }
                        else
                        {
                            members = members.Add(
                                SyntaxFactory.MethodDeclaration(ads.AttributeLists, ads.Modifiers.AddRange(ids.Modifiers), ids.Type, ids.ExplicitInterfaceSpecifier, SyntaxFactory.Identifier(ads.Keyword.ToString() + "_Item"), null, SyntaxFactory.ParameterList(ids.ParameterList.Parameters), SyntaxFactory.List<TypeParameterConstraintClauseSyntax>(), ads.Body)
                                );
                        }
                    }
                }
                else
                {
                    members = members.Add(member);
                }
            }

            if (numIndexers>0)
            {
                return node.WithMembers(members);
            }

            return base.VisitClassDeclaration(node);
        }

#if false
        public override SyntaxNode VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            //return base.VisitIndexerDeclaration(node);
            /*
                    public override AccessorListSyntax AccessorList { get; }
                    public override SyntaxList<AttributeListSyntax> AttributeLists { get; }
                    public override ExplicitInterfaceSpecifierSyntax ExplicitInterfaceSpecifier { get; }
                    public override SyntaxTokenList Modifiers { get; }
                    public BracketedParameterListSyntax ParameterList { get; }
                    public SyntaxToken ThisKeyword { get; }
                    public override TypeSyntax Type { get; }
             */
            

            BlockSyntax block = SyntaxFactory.Block();
            
            /*        // Summary:
        //     Gets the attribute declaration list.
        public SyntaxList<AttributeListSyntax> AttributeLists { get; }
        //
        // Summary:
        //     Gets the optional body block which may be empty, but it is null if there
        //     are no braces.
        public BlockSyntax Body { get; }
        //
        // Summary:
        //     Gets the keyword token, or identifier if an erroneous accessor declaration.
        public SyntaxToken Keyword { get; }
        //
        // Summary:
        //     Gets the modifier list.
        public SyntaxTokenList Modifiers { get; }
        //
        // Summary:
        //     Gets the optional semicolon token.
        public SyntaxToken SemicolonToken { get; }
             */
            
            foreach(AccessorDeclarationSyntax ads in node.AccessorList.Accessors)
            {
                
                block = block.WithStatements(
                    SyntaxFactory.MethodDeclaration(ads.AttributeLists, ads.Modifiers, node.Type, node.ExplicitInterfaceSpecifier, SyntaxFactory.Identifier(ads.Keyword.ToString() + "_item"), SyntaxFactory.TypeParameterList(), SyntaxFactory.ParameterList(node.ParameterList.Parameters), SyntaxFactory.List<TypeParameterConstraintClauseSyntax>(), ads.Body)
                    );
            }


            return block;
            //return base.VisitIndexerDeclaration(node);
        }
#endif

        public override SyntaxNode VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            TypeInfo ti = Model.GetTypeInfo(node.Expression);
            if (ti.ConvertedType.TypeKind != TypeKind.Class)
                return base.VisitElementAccessExpression(node);

            if (ti.ConvertedType.GetFullyQualifiedName() == "LavishScript2.Table")
            {
                base.VisitElementAccessExpression(node);
            }

            // convert to method access
            SymbolInfo si = Model.GetSymbolInfo(node);
            if (si.Symbol.Kind != SymbolKind.Property)
            {
                throw new NotImplementedException("element access on type " + ti.ConvertedType.GetFullyQualifiedName() + " resolves to " + si.Symbol.Kind.ToString());
            }

            IPropertySymbol ps = (IPropertySymbol)si.Symbol;
            if (ps.IsStatic)
            {
                throw new NotSupportedException("static indexer?");
            }            
                                
            //SyntaxFactory.MemberAccessExpression(SyntaxKind.IdentifierName)
            return SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression
                                            (
                                                SyntaxKind.SimpleMemberAccessExpression,node.Expression,
                                                SyntaxFactory.IdentifierName("get_Item") // ps.GetMethod.Name
                                            ), SyntaxFactory.ArgumentList(node.ArgumentList.Arguments));


            //return base.VisitElementAccessExpression(node);
        }
    }
}
