﻿using System;
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
    class MethodCollector : CSharpSyntaxWalker
    {
        public MethodCollector(Chunk chunk, SemanticModel model)
        {
            Chunk = chunk;
            Model = model;
        }
        public Chunk Chunk { get; private set; }
        public SemanticModel Model { get; private set; }

        public override void Visit(SyntaxNode node)
        {
            base.Visit(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            base.VisitConstructorDeclaration(node);
            Chunk.AddFunction(node, Model);
        }

        public override void VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            base.VisitDestructorDeclaration(node);
            Chunk.AddFunction(node, Model);
        }

        public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            base.VisitAccessorDeclaration(node);
            Chunk.AddFunction(node, Model);
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            base.VisitMethodDeclaration(node);
            Chunk.AddFunction(node, Model);
        }

        public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            base.VisitIndexerDeclaration(node);
            Chunk.AddFunction(node, Model);
        }

    }    
}