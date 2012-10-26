using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LS2IL;
using Roslyn.Compilers.CSharp;

namespace ls2csc
{

    /// <summary>
    /// This SyntaxWalker implements the first phase of scanning. We collect Class declarations and tell the Chunk about their members. 
    /// The results will go into the metadata emitted into the LS2IL, which in turn gets used by the VM/runtime to initialize types.
    /// </summary>
    class DeclarationCollector : SyntaxWalker
    {
        public DeclarationCollector(Chunk chunk, SemanticModel model)
        {
            Chunk = chunk;
            Model = model;
        }
        public Chunk Chunk { get; private set; }
        public SemanticModel Model { get; private set; }

        public LS2IL.TypeExtraInfo.ClassMetadataGenerator CurrentClass { get; private set; }

        public override void Visit(SyntaxNode node)
        {
            base.Visit(node);
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {            
            LS2IL.TypeExtraInfo.ClassMetadataGenerator wasClass = CurrentClass;

            NamedTypeSymbol s = Model.GetDeclaredSymbol(node);
            
            TypeExtraInfo tei = Chunk.AddTypeExtraInfo(s, Model);
            CurrentClass = tei.MetadataGenerator;

            foreach (MemberDeclarationSyntax mds in node.Members)
            {
                CurrentClass.AddMember(mds);
            }

            base.VisitClassDeclaration(node);
            CurrentClass = wasClass;
        }

        #region unused declarations
        public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            base.VisitAccessorDeclaration(node);
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            base.VisitFieldDeclaration(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            base.VisitConstructorDeclaration(node);
        }

        public override void VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            base.VisitDestructorDeclaration(node);
        }

        public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            base.VisitDelegateDeclaration(node);
        }

        public override void VisitEventDeclaration(EventDeclarationSyntax node)
        {
            base.VisitEventDeclaration(node);
        }
        public override void VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
        {
            base.VisitEventFieldDeclaration(node);
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            LS2IL.TypeExtraInfo.ClassMetadataGenerator wasClass = CurrentClass;

            NamedTypeSymbol s = Model.GetDeclaredSymbol(node);

            TypeExtraInfo tei = Chunk.AddTypeExtraInfo(s, Model);
            CurrentClass = tei.MetadataGenerator;

            foreach (MemberDeclarationSyntax mds in node.Members)
            {
                CurrentClass.AddMember(mds);
            }

            base.VisitEnumDeclaration(node);
            CurrentClass = wasClass;
        }

        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            base.VisitEnumMemberDeclaration(node);
        }

        public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            base.VisitIndexerDeclaration(node);
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            LS2IL.TypeExtraInfo.ClassMetadataGenerator wasClass = CurrentClass;

            NamedTypeSymbol s = Model.GetDeclaredSymbol(node);

            TypeExtraInfo tei = Chunk.AddTypeExtraInfo(s, Model);
            CurrentClass = tei.MetadataGenerator;

            foreach (MemberDeclarationSyntax mds in node.Members)
            {
                CurrentClass.AddMember(mds);
            }

            base.VisitInterfaceDeclaration(node);
            CurrentClass = wasClass;
        }

        public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            base.VisitNamespaceDeclaration(node);
        }

        public override void VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            base.VisitOperatorDeclaration(node);
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            base.VisitPropertyDeclaration(node);
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            base.VisitStructDeclaration(node);
        }

        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            base.VisitVariableDeclaration(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            base.VisitMethodDeclaration(node);
        }
        #endregion
    }
}
