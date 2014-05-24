using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;

namespace LS2IL
{
    class FieldExtraInfo
    {
        public string Name { get; set; }
        public ITypeSymbol Type { get; set; }
        public ExpressionSyntax Initializer { get; set; }
    }

    class TypeExtraInfo
    {
        public TypeExtraInfo(Chunk chunk, SemanticModel model, INamedTypeSymbol cls, bool isLibrary)
        {
            Chunk = chunk;
            Model = model;
            Type = cls;
            Fields = new List<FieldExtraInfo>();
            StaticFields = new List<FieldExtraInfo>();
            FieldNames = new Dictionary<string, int>();
            StaticFieldNames = new Dictionary<string, int>();
            MetadataGenerator = new ClassMetadataGenerator(chunk, model, cls,isLibrary);

            if (Type.BaseType != null)
            {
                BaseType = Chunk.AddTypeExtraInfo(Type.BaseType, Model, isLibrary);
            }
        }

        public class ClassMetadataGenerator : FlatTableBuilder
        {
            public ClassMetadataGenerator(Chunk chunk, SemanticModel model, INamedTypeSymbol cls, bool isLibrary)
            {
                Chunk = chunk;
                Class = cls;
                Model = model;
                Members = new List<FlatValue>();
                IsLibrary = isLibrary;
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

            public bool IsLibrary { get; private set; }
            public Chunk Chunk { get; private set; }
            public SemanticModel Model { get; private set; }
            public INamedTypeSymbol Class { get; private set; }
            public List<FlatValue> Members { get; private set; }

            public FlatArrayBuilder GenerateInputDeclarations(IMethodSymbol ms)
            {
                FlatArrayBuilder fab = new FlatArrayBuilder();
                if (!ms.ReturnsVoid)
                {
                    FlatArrayBuilder decl = new FlatArrayBuilder();
                    // name
                    decl.Add(FlatValue.String(""));
                    // type
                    decl.Add(FlatValue.String(ms.ReturnType.GetFullyQualifiedName()));
                    // csharp declaration
                    decl.Add(FlatValue.String(ms.ReturnType.GetFullyQualifiedName()));

                    fab.Add(decl.GetFlatValue());
                }
                foreach(IParameterSymbol param in ms.Parameters)
                {
                    FlatArrayBuilder decl = new FlatArrayBuilder();
                    // name
                    decl.Add(FlatValue.String(param.Name));
                    // type
                    decl.Add(FlatValue.String(param.Type.GetFullyQualifiedName()));
                    // csharp declaration
                    decl.Add(FlatValue.String(param.Type.GetFullyQualifiedName()+" "+param.Name));

                    fab.Add(decl.GetFlatValue());
                }

                return fab;
            }

            public void Add(IMethodSymbol ms)
            {
                if (IsLibrary)
                    return;
                FlatArrayBuilder fab = new FlatArrayBuilder();
                fab.Add(FlatValue.Int32(ms.IsStatic ? (int)ClassMemberType.StaticMethod : (int)ClassMemberType.Method));
                fab.Add(FlatValue.String(ms.GetFullyQualifiedName()));

                Function f;
                if (!Chunk.Functions.TryGetValue(ms, out f))
                {
                    throw new NotImplementedException("Method not found " + ms.ToString());
                }

                fab.Add(FlatValue.Int32(f.NumFunction));

                // input declarations
                fab.Add(GenerateInputDeclarations(ms).GetFlatValue());

                Members.Add(fab.GetFlatValue());
            }

            public void Add(ConstructorDeclarationSyntax node)
            {
                if (IsLibrary)
                    return;
                Add(Model.GetDeclaredSymbol(node));
                /*
                FlatArrayBuilder fab = new FlatArrayBuilder();
                IMethodSymbol ms = Chunk.Model.GetDeclaredSymbol(node);
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
                TypeExtraInfo tei = Chunk.AddTypeExtraInfo(this.Class, Model, IsLibrary);
                foreach (VariableDeclaratorSyntax vds in decl.Variables)
                {
                    TypeInfo ti = Model.GetTypeInfo(decl.Type);
                    tei.AddField(modifiers, ti.ConvertedType, vds);
                }
            }

            public void Add(IndexerDeclarationSyntax node)
            {
                if (IsLibrary)
                    return;

                ISymbol s = Model.GetDeclaredSymbol(node);
                Function fGet = null;
                Function fSet = null;

                string sGet = string.Empty;
                string sSet = string.Empty;
                IMethodSymbol msGet = null;
                IMethodSymbol msSet = null;
                foreach (AccessorDeclarationSyntax ads in node.AccessorList.Accessors)
                {
                    switch (ads.Keyword.CSharpKind())
                    {
                        case SyntaxKind.GetKeyword:
                            {
                                msGet = Model.GetDeclaredSymbol(ads);
                                if (!Chunk.Functions.TryGetValue(msGet, out fGet))
                                {
                                    throw new NotImplementedException("Method not found " + msGet.ToString());
                                }
                                sGet = /*"get_" +*/ msGet.GetFullyQualifiedName();
                            }
                            break;
                        case SyntaxKind.SetKeyword:
                            {
                                msSet = Model.GetDeclaredSymbol(ads);
                                if (!Chunk.Functions.TryGetValue(msSet, out fSet))
                                {
                                    throw new NotImplementedException("Method not found " + msSet.ToString());
                                }
                                sSet = /*"set_" +*/ msSet.GetFullyQualifiedName();
                            }
                            break;
                        default:
                            throw new NotImplementedException("unhandled property accessor: " + ads.Keyword.CSharpKind().ToString());
                            break;
                    }
                }

                if (msGet != null)
                {
                    FlatArrayBuilder fab = new FlatArrayBuilder();
                    fab.Add(FlatValue.Int32(s.IsStatic ? (int)ClassMemberType.StaticMethod : (int)ClassMemberType.Method));


                    fab.Add(FlatValue.String(sGet));
                    Function f;
                    if (!Chunk.Functions.TryGetValue(msGet, out f))
                    {
                        throw new NotImplementedException("Method not found " + msGet.ToString());
                    }

                    fab.Add(FlatValue.Int32(f.NumFunction));

                    fab.Add(GenerateInputDeclarations(msGet).GetFlatValue());

                    Members.Add(fab.GetFlatValue());
                }
                if (msSet != null)
                {
                    FlatArrayBuilder fab = new FlatArrayBuilder();
                    fab.Add(FlatValue.Int32(s.IsStatic ? (int)ClassMemberType.StaticMethod : (int)ClassMemberType.Method));


                    fab.Add(FlatValue.String(sSet));
                    Function f;
                    if (!Chunk.Functions.TryGetValue(msSet, out f))
                    {
                        throw new NotImplementedException("Method not found " + msGet.ToString());
                    }

                    fab.Add(FlatValue.Int32(f.NumFunction));

                    fab.Add(GenerateInputDeclarations(msSet).GetFlatValue());

                    Members.Add(fab.GetFlatValue());
                }
            }

            public void Add(EnumMemberDeclarationSyntax node)
            {
                /*        // Summary:
        //     Gets the attribute declaration list.
        public SyntaxList<AttributeListSyntax> AttributeLists { get; }
        public EqualsValueClauseSyntax EqualsValue { get; }
        //
        // Summary:
        //     Gets the identifier.
        public SyntaxToken Identifier { get; }
                 */
                // add field info

                TypeExtraInfo tei = Chunk.AddTypeExtraInfo(this.Class,Model, IsLibrary);
                tei.AddEnumMember(node.Identifier.ToString(), node.EqualsValue != null ? node.EqualsValue.Value : null);


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

                fab.Add(FlatValue.Int32((int)ClassMemberType.StaticField));
                //TypeInfo ti = Chunk.Model.GetTypeInfo(node..Type);

                fab.Add(FlatValue.String(Class.GetFullyQualifiedName()));

                {
                    FlatArrayBuilder varList = new FlatArrayBuilder();
                    varList.Add(FlatValue.String(node.Identifier.ToString()));
                    fab.Add(varList.GetFlatValue());
                }
                {
                    FlatArrayBuilder valueList = new FlatArrayBuilder();
                    if (node.EqualsValue != null)
                    {
                        if (node.EqualsValue.Value.CSharpKind() == SyntaxKind.NumericLiteralExpression)
                        {
                            valueList.Add(FlatValue.Int32(int.Parse(node.EqualsValue.Value.ToString())));
                        }
                        else
                        {
                            throw new NotImplementedException("Enum member without numeric literal expression");
                        }
                    }
                    else
                    {
                        throw new NotImplementedException("Enum member without numeric literal expression");
                    }

                    fab.Add(valueList.GetFlatValue());
                }

                Members.Add(fab.GetFlatValue());
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
                TypeInfo ti = Model.GetTypeInfo(node.Declaration.Type);

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
                if (IsLibrary)
                    return;

                ISymbol s = Model.GetDeclaredSymbol(node);
                Function fGet = null;
                Function fSet = null;
                foreach (AccessorDeclarationSyntax ads in node.AccessorList.Accessors)
                {
                    switch (ads.Keyword.CSharpKind())
                    {
                        case SyntaxKind.GetKeyword:
                            {
                                IMethodSymbol ms = Model.GetDeclaredSymbol(ads);
                                if (!Chunk.Functions.TryGetValue(ms, out fGet))
                                {
                                    throw new NotImplementedException("Method not found " + ms.ToString());
                                }
                            }
                            break;
                        case SyntaxKind.SetKeyword:
                            {
                                IMethodSymbol ms = Model.GetDeclaredSymbol(ads);
                                if (!Chunk.Functions.TryGetValue(ms, out fSet))
                                {
                                    throw new NotImplementedException("Method not found " + ms.ToString());
                                }
                            }
                            break;
                        default:
                            throw new NotImplementedException("unhandled property accessor: " + ads.Keyword.CSharpKind().ToString());
                            break;
                    }
                }


                FlatArrayBuilder fab = new FlatArrayBuilder();
                fab.Add(FlatValue.Int32(s.IsStatic ? (int)ClassMemberType.StaticProperty : (int)ClassMemberType.Property));

                //fab.Add(FlatValue.Int32((int)ClassMemberType.Property));
                fab.Add(FlatValue.String(node.Identifier.ToString()));

                TypeInfo ti = Model.GetTypeInfo(node.Type);

                fab.Add(FlatValue.String(ti.ConvertedType.GetFullyQualifiedName()));

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

            public void Add(ClassDeclarationSyntax node)
            {
                //throw new NotImplementedException();
                // nothing technically to do here atm?
            }

            public void Add(StructDeclarationSyntax node)
            {
                //throw new NotImplementedException();
                // nothing technically to do here atm?
            }

            public void Add(MethodDeclarationSyntax node)
            {
                if (IsLibrary)
                    return;
                FlatArrayBuilder fab = new FlatArrayBuilder();

                IMethodSymbol s = Model.GetDeclaredSymbol(node);
                fab.Add(FlatValue.Int32(s.IsStatic ? (int)ClassMemberType.StaticMethod : (int)ClassMemberType.Method));

                fab.Add(FlatValue.String(s.GetFullyQualifiedName()));

                IMethodSymbol ms = Model.GetDeclaredSymbol(node);

                Function f;
                if (!Chunk.Functions.TryGetValue(ms, out f))
                {
                    throw new NotImplementedException("Method not found " + ms.ToString());
                }

                fab.Add(FlatValue.Int32(f.NumFunction));

                fab.Add(GenerateInputDeclarations(ms).GetFlatValue());

                Members.Add(fab.GetFlatValue());
            }

            public void AddMember(MemberDeclarationSyntax node)
            {
                switch (node.CSharpKind())
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
                    case SyntaxKind.EnumMemberDeclaration:
                        {
                            Add((EnumMemberDeclarationSyntax)node);
                            return;
                        }
                        break;
                    case SyntaxKind.IndexerDeclaration:
                        {
                            Add((IndexerDeclarationSyntax)node);
                            return;
                        }
                        break;
                    case SyntaxKind.ClassDeclaration:
                        {
                            Add((ClassDeclarationSyntax)node);
                            return;
                        }
                        break;
                    case SyntaxKind.StructDeclaration:
                        {
                            Add((StructDeclarationSyntax)node);
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

                ImmutableArray<INamedTypeSymbol> interfaces = this.Class.Interfaces;
                if (interfaces != null && interfaces.Count() > 0)
                {
                    FlatArrayBuilder fab = new FlatArrayBuilder();
                    foreach (INamedTypeSymbol iface in interfaces)
                    {
                        fab.Add(FlatValue.String(iface.GetFullyQualifiedName()));
                    }
                    ftb.Add("Interfaces", fab.GetFlatValue());
                }

                return ftb.GetFlatValue();
            }
        }

        public Chunk Chunk { get; private set; }
        public SemanticModel Model { get; private set; }
        public INamedTypeSymbol Type { get; private set; }
        public TypeExtraInfo BaseType { get; private set; }
        public ClassMetadataGenerator MetadataGenerator { get; private set; }

        public List<FieldExtraInfo> Fields { get; private set; }
        public List<FieldExtraInfo> StaticFields { get; private set; }
        public Dictionary<string, int> FieldNames { get; private set; }
        public Dictionary<string, int> StaticFieldNames { get; private set; }

        public int GetFullyQualifiedField(int from_number)
        {
            TypeExtraInfo baseType = BaseType;
            while (baseType != null)
            {
                from_number += baseType.Fields.Count;
                baseType = baseType.BaseType;
            }
            return from_number;
        }
        public int GetFullyQualifiedStaticField(int from_number)
        {
            TypeExtraInfo parent = BaseType;
            while (parent != null)
            {
                from_number += parent.StaticFields.Count;
                parent = parent.BaseType;
            }
            return from_number;
        }

        public bool ResolveLocalField(string name, out int nField)
        {
            int nValue;
            if (FieldNames.TryGetValue(name, out nValue))
            {
                nField = nValue;// GetFullyQualifiedField(nValue);
                return true;
            }

            nField = -1;
            return false;
        }

        public bool ResolveRuntimeField(string name, out int nField)
        {
            int nValue;
            if (FieldNames.TryGetValue(name, out nValue))
            {
                nField = GetFullyQualifiedField(nValue);
                return true;
            }

            nField = -1;
            return false;
        }

        public bool ResolveLocalStaticField(string name, out int nField)
        {
            int nValue;
            if (StaticFieldNames.TryGetValue(name, out nValue))
            {
                nField = nValue;// GetFullyQualifiedStaticField(nValue);
                return true;
            }

            nField = -1;
            return false;
        }
        public bool ResolveRuntimeStaticField(string name, out int nField)
        {
            int nValue;
            if (StaticFieldNames.TryGetValue(name, out nValue))
            {
                nField = GetFullyQualifiedStaticField(nValue);
                return true;
            }

            nField = -1;
            return false;
        }

        public void AddField(SyntaxTokenList modifiers, ITypeSymbol type, VariableDeclaratorSyntax vds)
        {
            if (vds.ArgumentList != null)
            {
                throw new NotImplementedException("array field");
            }

            //bool bStatic = false;
            if (modifiers.ToString().Contains("static"))
            {
                FieldExtraInfo fei = new FieldExtraInfo() { Name = vds.Identifier.ToString(), Type = type };
                if (vds.Initializer != null)
                    fei.Initializer = vds.Initializer.Value;

                int nField = StaticFields.Count;
                StaticFieldNames.Add(fei.Name, nField);
                StaticFields.Add(fei);
                return;
            }
            else
            {
                FieldExtraInfo fei = new FieldExtraInfo() { Name = vds.Identifier.ToString(), Type = type };
                if (vds.Initializer != null)
                    fei.Initializer = vds.Initializer.Value;

                int nField = Fields.Count;
                FieldNames.Add(fei.Name, nField);
                Fields.Add(fei);
            }
        }

        public void AddEnumMember(string name, ExpressionSyntax initializer)
        {

            FieldExtraInfo fei = new FieldExtraInfo() { Name = name, Type = this.Type };
            fei.Initializer = initializer;

            int nField = StaticFields.Count;
            StaticFieldNames.Add(fei.Name, nField);
            StaticFields.Add(fei);
        }

    }
}
