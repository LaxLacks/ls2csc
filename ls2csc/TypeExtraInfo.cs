﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;

namespace LS2IL
{
    class FieldExtraInfo
    {
        public string Name { get; set; }
        public TypeSymbol Type { get; set; }
        public ExpressionSyntax Initializer { get; set; }
    }

    class TypeExtraInfo
    {
        public TypeExtraInfo(Chunk chunk, NamedTypeSymbol cls)
        {
            Chunk = chunk;
            Type = cls;
            Fields = new List<FieldExtraInfo>();
            StaticFields = new List<FieldExtraInfo>();
            FieldNames = new Dictionary<string, int>();
            StaticFieldNames = new Dictionary<string, int>();
            MetadataGenerator = new ClassMetadataGenerator(chunk, cls);

            if (Type.ContainingType != null)
            {
                Parent = Chunk.AddTypeExtraInfo(Type.ContainingType);
            }
        }

        public class ClassMetadataGenerator : FlatTableBuilder
        {
            public ClassMetadataGenerator(Chunk chunk, NamedTypeSymbol cls)
            {
                Chunk = chunk;
                Class = cls;
                Members = new List<FlatValue>();
            }

            enum ClassMemberType : int
            {
                Field = 1,
                StaticField = 2,
                Method = 3,
                StaticMethod = 4,
                Property = 5,
                StaticProperty = 6,
            }

            public Chunk Chunk { get; private set; }
            public NamedTypeSymbol Class { get; private set; }
            public List<FlatValue> Members { get; private set; }

            public void Add(MethodSymbol ms)
            {
                FlatArrayBuilder fab = new FlatArrayBuilder();
                fab.Add(FlatValue.Int32(ms.IsStatic ? (int)ClassMemberType.StaticMethod : (int)ClassMemberType.Method));
                fab.Add(FlatValue.String(ms.GetFullyQualifiedName()));

                Function f;
                if (!Chunk.Functions.TryGetValue(ms, out f))
                {
                    throw new NotImplementedException("Method not found " + ms.ToString());
                }

                fab.Add(FlatValue.Int32(f.NumFunction));

                Members.Add(fab.GetFlatValue());
            }

            public void Add(ConstructorDeclarationSyntax node)
            {
                Add(Chunk.Model.GetDeclaredSymbol(node));
                /*
                FlatArrayBuilder fab = new FlatArrayBuilder();
                MethodSymbol ms = Chunk.Model.GetDeclaredSymbol(node);
                fab.Add(FlatValue.Int32(ms.IsStatic ? (int)ClassMemberType.StaticMethod : (int)ClassMemberType.Method));
                fab.Add(FlatValue.String(ms.GetFullyQualifiedName()));

                Function f;
                if (!Chunk.Functions.TryGetValue(ms, out f))
                {
                    throw new NotImplementedException("Method not found "+ms.ToString());
                }

                fab.Add(FlatValue.Int32(f.NumFunction));

                Members.Add(fab.GetFlatValue());
                 * */
            }

            public void Add(SyntaxTokenList modifiers, VariableDeclarationSyntax decl)
            {
                TypeExtraInfo tei = Chunk.AddTypeExtraInfo(this.Class);


                foreach (VariableDeclaratorSyntax vds in decl.Variables)
                {
                    TypeInfo ti = Chunk.Model.GetTypeInfo(decl.Type);
                    tei.AddField(modifiers, ti.ConvertedType, vds);
                }
            }

            public void Add(FieldDeclarationSyntax node)
            {
                // add field info
                Add(node.Modifiers, node.Declaration);


                /*
        // Summary:
        //     Gets the attribute declaration list.
        public override SyntaxList<AttributeListSyntax> AttributeLists { get; }
        public override VariableDeclarationSyntax Declaration { get; }
        //
        // Summary:
        //     Gets the modifier list.
        public override SyntaxTokenList Modifiers { get; }
        public override SyntaxToken SemicolonToken { get; }                 
                 */

                // add to metadata for runtime type building
                FlatArrayBuilder fab = new FlatArrayBuilder();

                fab.Add(FlatValue.Int32(node.Modifiers.ToString().Contains("static") ? (int)ClassMemberType.StaticField : (int)ClassMemberType.Field));
                TypeInfo ti = Chunk.Model.GetTypeInfo(node.Declaration.Type);

                fab.Add(FlatValue.String(ti.ConvertedType.GetFullyQualifiedName()));

                {
                    FlatArrayBuilder varList = new FlatArrayBuilder();
                    foreach (VariableDeclaratorSyntax vds in node.Declaration.Variables)
                    {
                        if (vds.ArgumentList != null)
                        {
                            throw new NotImplementedException("array field");
                        }
                        varList.Add(FlatValue.String(vds.Identifier.ToString()));
                    }


                    fab.Add(varList.GetFlatValue());
                }

                Members.Add(fab.GetFlatValue());
            }

            public void Add(PropertyDeclarationSyntax node)
            {
                Symbol s = Chunk.Model.GetDeclaredSymbol(node);
                Function fGet = null;
                Function fSet = null;
                foreach (AccessorDeclarationSyntax ads in node.AccessorList.Accessors)
                {
                    switch (ads.Keyword.Kind)
                    {
                        case SyntaxKind.GetKeyword:
                            {
                                MethodSymbol ms = Chunk.Model.GetDeclaredSymbol(ads);
                                if (!Chunk.Functions.TryGetValue(ms, out fGet))
                                {
                                    throw new NotImplementedException("Method not found " + ms.ToString());
                                }
                            }
                            break;
                        case SyntaxKind.SetKeyword:
                            {
                                MethodSymbol ms = Chunk.Model.GetDeclaredSymbol(ads);
                                if (!Chunk.Functions.TryGetValue(ms, out fSet))
                                {
                                    throw new NotImplementedException("Method not found " + ms.ToString());
                                }
                            }
                            break;
                        default:
                            throw new NotImplementedException("unhandled property accessor: " + ads.Keyword.Kind.ToString());
                            break;
                    }
                }


                FlatArrayBuilder fab = new FlatArrayBuilder();
                fab.Add(FlatValue.Int32(s.IsStatic ? (int)ClassMemberType.StaticProperty : (int)ClassMemberType.Property));

                //fab.Add(FlatValue.Int32((int)ClassMemberType.Property));
                fab.Add(FlatValue.String(node.Identifier.ToString()));

                if (fGet == null)
                    fab.Add(FlatValue.Null());
                else
                    fab.Add(FlatValue.Int32(fGet.NumFunction));

                if (fSet == null)
                    fab.Add(FlatValue.Null());
                else
                    fab.Add(FlatValue.Int32(fSet.NumFunction));

                Members.Add(fab.GetFlatValue());
            }

            public void Add(MethodDeclarationSyntax node)
            {
                FlatArrayBuilder fab = new FlatArrayBuilder();

                MethodSymbol s = Chunk.Model.GetDeclaredSymbol(node);
                fab.Add(FlatValue.Int32(s.IsStatic ? (int)ClassMemberType.StaticMethod : (int)ClassMemberType.Method));

                fab.Add(FlatValue.String(s.GetFullyQualifiedName()));

                MethodSymbol ms = Chunk.Model.GetDeclaredSymbol(node);

                Function f;
                if (!Chunk.Functions.TryGetValue(ms, out f))
                {
                    throw new NotImplementedException("Method not found " + ms.ToString());
                }

                fab.Add(FlatValue.Int32(f.NumFunction));

                Members.Add(fab.GetFlatValue());
            }

            public void AddMember(MemberDeclarationSyntax node)
            {
                switch (node.Kind)
                {
                    case SyntaxKind.ConstructorDeclaration:
                        {
                            Add((ConstructorDeclarationSyntax)node);
                            return;
                        }
                        break;
                    case SyntaxKind.FieldDeclaration:
                        {
                            Add((FieldDeclarationSyntax)node);
                            return;
                        }
                        break;
                    case SyntaxKind.PropertyDeclaration:
                        {
                            Add((PropertyDeclarationSyntax)node);
                            return;
                        }
                        break;
                    case SyntaxKind.MethodDeclaration:
                        {
                            Add((MethodDeclarationSyntax)node);
                            return;
                        }
                        break;
                }
                throw new NotImplementedException();
            }

            public new FlatValue GetFlatValue()
            {
                FlatTableBuilder ftb = new FlatTableBuilder();
                ftb.Add("Name", FlatValue.String(Class.GetFullyQualifiedName()));
                {
                    FlatArrayBuilder fab = new FlatArrayBuilder();
                    foreach (FlatValue fv in Members)
                    {
                        fab.Add(fv);
                    }
                    ftb.Add("Members", fab.GetFlatValue());
                }

                // base type
                if (this.Class.BaseType != null && this.Class.BaseType.SpecialType!= SpecialType.System_Object)
                {
                    ftb.Add("Base", FlatValue.String(this.Class.BaseType.GetFullyQualifiedName()));
                }

                ReadOnlyArray<NamedTypeSymbol> interfaces = this.Class.Interfaces;
                if (interfaces != null && interfaces.Count > 0)
                {
                    FlatArrayBuilder fab = new FlatArrayBuilder();
                    foreach (NamedTypeSymbol iface in interfaces)
                    {
                        fab.Add(FlatValue.String(iface.GetFullyQualifiedName()));
                    }
                    ftb.Add("Interfaces", fab.GetFlatValue());
                }

                return ftb.GetFlatValue();
            }
        }

        public Chunk Chunk { get; private set; }
        public NamedTypeSymbol Type { get; private set; }
        public TypeExtraInfo Parent { get; private set; }
        public ClassMetadataGenerator MetadataGenerator { get; private set; }

        public List<FieldExtraInfo> Fields { get; private set; }
        public List<FieldExtraInfo> StaticFields { get; private set; }
        public Dictionary<string, int> FieldNames { get; private set; }
        public Dictionary<string, int> StaticFieldNames { get; private set; }

        public int GetFullyQualifiedField(int from_number)
        {
            TypeExtraInfo parent = Parent;
            while (parent != null)
            {
                from_number += parent.Fields.Count;
                parent = parent.Parent;
            }
            return from_number;
        }
        public int GetFullyQualifiedStaticField(int from_number)
        {
            TypeExtraInfo parent = Parent;
            while (parent != null)
            {
                from_number += parent.StaticFields.Count;
                parent = parent.Parent;
            }
            return from_number;
        }

        public bool ResolveRuntimeField(string name, out int nField)
        {
            int nValue;
            if (!FieldNames.TryGetValue(name, out nValue))
            {
                nField = GetFullyQualifiedField(nValue);
                return true;
            }

            nField = -1;
            return false;
        }
        public bool ResolveRuntimeStaticField(string name, out int nField)
        {
            int nValue;
            if (!StaticFieldNames.TryGetValue(name, out nValue))
            {
                nField = GetFullyQualifiedStaticField(nValue);
                return true;
            }

            nField = -1;
            return false;
        }

        public void AddField(SyntaxTokenList modifiers, TypeSymbol type, VariableDeclaratorSyntax vds)
        {
            if (vds.ArgumentList != null)
            {
                throw new NotImplementedException("array field");
            }

            //bool bStatic = false;
            if (modifiers.ToString().Contains("static"))
            {
                throw new NotImplementedException("static field declaration");
            }

            FieldExtraInfo fei = new FieldExtraInfo() { Name = vds.Identifier.ToString(), Type = type };
            if (vds.Initializer != null)
                fei.Initializer = vds.Initializer.Value;

            int nField = Fields.Count;
            FieldNames.Add(fei.Name, nField);
            Fields.Add(fei);
        }
    }
}