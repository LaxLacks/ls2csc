using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

                return ftb.GetFlatValue();
            }
        }

        public Chunk Chunk { get; private set; }
        public NamedTypeSymbol Type { get; private set; }
        public TypeExtraInfo Parent {get;private set;}
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
            if (vds.Initializer!=null)
                fei.Initializer = vds.Initializer.Value;

            int nField = Fields.Count;
            FieldNames.Add(fei.Name, nField);
            Fields.Add(fei);
        }
    }

    class Chunk
    {
        public Chunk(CompilationUnitSyntax root, Compilation compilation, SemanticModel model)
        {
            Root = root;
            Compilation = compilation;
            Model = model;

            Functions = new Dictionary<MethodSymbol, LS2IL.Function>();
            FunctionsByNumber = new List<LS2IL.Function>();
            EmittedChunkValues = new List<string>();
            ChunkValues = new List<FlatValue>();
            TypeExtraInfo = new Dictionary<NamedTypeSymbol, TypeExtraInfo>();
            MetaValues = new Dictionary<string, FlatValue>();

            //MetaValues.Add("Author", FlatValue.String("Lax"));
            MetaValues.Add("Build Date", FlatValue.String(DateTime.UtcNow.ToString()+" UTC"));
            MetaValues.Add("Compiler", FlatValue.String("ls2csc"));
            MetaValues.Add("Language", FlatValue.String("C#"));



        }

        public SemanticModel Model { get; private set; }
        public CompilationUnitSyntax Root { get; private set; }
        public Compilation Compilation { get; private set; }

        public List<FlatValue> ChunkValues { get; private set; }
        public List<string> EmittedChunkValues { get; private set; }
        public Dictionary<string, LS2IL.FlatValue> MetaValues { get; private set; }
        public Dictionary<NamedTypeSymbol, TypeExtraInfo> TypeExtraInfo { get; private set; }

        //public Function EntryPoint { get; set; }

        public Dictionary<MethodSymbol, LS2IL.Function> Functions { get; private set; }
        public List<Function> FunctionsByNumber { get; private set; }

        public TypeExtraInfo GetTypeExtraInfo(NamedTypeSymbol sym)
        {
            TypeExtraInfo tei;
            if (!TypeExtraInfo.TryGetValue(sym, out tei))
                return null;
            return tei;
        }
        public TypeExtraInfo AddTypeExtraInfo(NamedTypeSymbol sym)
        {
            TypeExtraInfo tei;
            if (TypeExtraInfo.TryGetValue(sym, out tei))
                return tei;

            tei = new TypeExtraInfo(this,sym);
            TypeExtraInfo.Add(sym, tei);
            return tei;
        }

        public void AddFunction(MethodDeclarationSyntax node)
        {
            int nFunction = FunctionsByNumber.Count;
            Function f = new Function(this,nFunction, Model, Model.GetDeclaredSymbol(node));
            Functions.Add(f.MethodSymbol, f);
            FunctionsByNumber.Add(f);
        }
        public void AddFunction(ConstructorDeclarationSyntax node)
        {
            int nFunction = FunctionsByNumber.Count;
            Function f = new Function(this, nFunction, Model, Model.GetDeclaredSymbol(node));
            Functions.Add(f.MethodSymbol, f);
            FunctionsByNumber.Add(f);
        }
        public void AddFunction(DestructorDeclarationSyntax node)
        {
            int nFunction = FunctionsByNumber.Count;
            Function f = new Function(this, nFunction, Model, Model.GetDeclaredSymbol(node));
            Functions.Add(f.MethodSymbol, f);
            FunctionsByNumber.Add(f);
        }
        public void AddFunction(AccessorDeclarationSyntax node)
        {
            int nFunction = FunctionsByNumber.Count;
            Function f = new Function(this, nFunction, Model, Model.GetDeclaredSymbol(node));
            Functions.Add(f.MethodSymbol, f);
            FunctionsByNumber.Add(f);        
        }
        public void AddFunction(MethodSymbol ms)
        {
            Function f;
            if (Functions.TryGetValue(ms, out f))
            {
                return;
            }
            int nFunction = FunctionsByNumber.Count;
            f = new Function(this, nFunction, Model, ms);
            Functions.Add(f.MethodSymbol, f);
            FunctionsByNumber.Add(f);

            TypeExtraInfo tei = AddTypeExtraInfo(ms.ContainingType);
            tei.MetadataGenerator.Add(ms);

        }

        public void GenerateTypesMetadata()
        {
            FlatArrayBuilder fab = new FlatArrayBuilder();
            foreach (KeyValuePair<NamedTypeSymbol, TypeExtraInfo> kvp in TypeExtraInfo)
            {
                LS2IL.TypeExtraInfo.ClassMetadataGenerator cmg = kvp.Value.MetadataGenerator;

                fab.Add(cmg.GetFlatValue());
            }

            MetaValues.Add("Declared Types", fab.GetFlatValue());
        }

        public void EmitMetaTable()
        {
            System.Console.WriteLine("; - begin meta table -");
            foreach (KeyValuePair<string, FlatValue> kvp in MetaValues)
            {
                System.Console.WriteLine(".meta \"" + kvp.Key + "\" " + kvp.Value.ToString());
            }
            System.Console.WriteLine("; - end meta table -");
        }

        public void Emit()
        {
            System.Console.WriteLine("; ---- begin chunk ----");

            
            MethodSymbol entryPoint = Compilation.GetEntryPoint(CancellationToken.None);


            if (entryPoint != null)
            {
                Function fEntryPoint;
                if (!Functions.TryGetValue(entryPoint, out fEntryPoint))
                {
                    throw new NotImplementedException("Entry point function not built");
                }

                System.Console.WriteLine(".entry " + fEntryPoint.NumFunction);
            }

            for (int i = 0; i < FunctionsByNumber.Count; i++)
            {
                LS2IL.Function f = FunctionsByNumber[i];
                f.FlattenToInstructions(true, true);
            }

            GenerateTypesMetadata();

            EmitMetaTable();

            System.Console.WriteLine("; ---- begin chunk values ----");
            foreach (string s in EmittedChunkValues)
            {
                System.Console.WriteLine(s);
            }
            System.Console.WriteLine("; ---- end chunk values ----");
            System.Console.WriteLine("");

            System.Console.WriteLine("; ---- begin functions ----");

            foreach (LS2IL.Function f in FunctionsByNumber)
            {


                f.Emit();
            }
            System.Console.WriteLine("; ---- end functions ----");

            System.Console.WriteLine("; ---- end chunk ----");

        }

    }
}
