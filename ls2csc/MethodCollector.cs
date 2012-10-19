using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LS2IL;
using Roslyn.Compilers.CSharp;

namespace ls2csc
{
    class MethodCollector : SyntaxWalker
    {
        public MethodCollector(Chunk chunk)
        {
            Chunk = chunk;
        }
        public Chunk Chunk { get; private set; }

        public override void Visit(SyntaxNode node)
        {
            base.Visit(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            base.VisitConstructorDeclaration(node);
            Chunk.AddFunction(node);
        }

        public override void VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            base.VisitDestructorDeclaration(node);
            Chunk.AddFunction(node);
        }

        public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            base.VisitAccessorDeclaration(node);
            Chunk.AddFunction(node);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            base.VisitMethodDeclaration(node);
            Chunk.AddFunction(node);
        }

    }    
}