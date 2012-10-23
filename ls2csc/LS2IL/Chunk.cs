using System;
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

        /// <summary>
        /// Retrieves an existing TypeExtraInfo given a NamedTypeSymbol
        /// </summary>
        /// <param name="sym"></param>
        /// <returns></returns>
        public TypeExtraInfo GetTypeExtraInfo(NamedTypeSymbol sym)
        {
            TypeExtraInfo tei;
            if (!TypeExtraInfo.TryGetValue(sym, out tei))
                return null;
            return tei;
        }

        /// <summary>
        /// Retrieves a TypeExtraInfo given a NamedTypeSymbol, instantiating a new one if necessary
        /// </summary>
        /// <param name="sym"></param>
        /// <returns></returns>
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
            MethodSymbol ms = Model.GetDeclaredSymbol(node);
            AddFunction(ms);
        }
        public void AddFunction(ConstructorDeclarationSyntax node)
        {
            MethodSymbol ms = Model.GetDeclaredSymbol(node);
            AddFunction(ms);
        }
        public void AddFunction(DestructorDeclarationSyntax node)
        {
            MethodSymbol ms = Model.GetDeclaredSymbol(node);
            AddFunction(ms);
        }
        public void AddFunction(AccessorDeclarationSyntax node)
        {
            MethodSymbol ms = Model.GetDeclaredSymbol(node);
            AddFunction(ms);
        }

        /// <summary>
        /// Adds a function to the chunk, also generating Chunk metadata if MethodSymbol.IsImplicitlyDeclared
        /// </summary>
        /// <param name="ms"></param>
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

            if (ms.IsImplicitlyDeclared)
            {
                TypeExtraInfo tei = AddTypeExtraInfo(ms.ContainingType);
                tei.MetadataGenerator.Add(ms);
            }
        }

        /// <summary>
        /// Generates metadata for Declared Types, from TypeExtraInfo
        /// </summary>
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

        /// <summary>
        /// Emits the Chunk's metatable to output in LS2IL
        /// </summary>
        /// <param name="output"></param>
        public void EmitMetaTable(TextWriter output)
        {
            output.WriteLine("; - begin meta table -");
            foreach (KeyValuePair<string, FlatValue> kvp in MetaValues)
            {
                output.WriteLine(".meta \"" + kvp.Key + "\" " + kvp.Value.ToString());
            }
            output.WriteLine("; - end meta table -");
        }

        /// <summary>
        /// Emits the Chunk to output in LS2IL
        /// </summary>
        /// <param name="output"></param>
        public void Emit(TextWriter output)
        {
            output.WriteLine("; ---- begin chunk ----");

            
            MethodSymbol entryPoint = Compilation.GetEntryPoint(CancellationToken.None);


            if (entryPoint != null)
            {
                Function fEntryPoint;
                if (!Functions.TryGetValue(entryPoint, out fEntryPoint))
                {
                    throw new NotImplementedException("Entry point function not built");
                }

                output.WriteLine(".entry " + fEntryPoint.NumFunction);
            }

            for (int i = 0; i < FunctionsByNumber.Count; i++)
            {
                LS2IL.Function f = FunctionsByNumber[i];
                f.FlattenToInstructions(true, true);
            }

            GenerateTypesMetadata();

            EmitMetaTable(output);

            output.WriteLine("; ---- begin chunk values ----");
            foreach (string s in EmittedChunkValues)
            {
                output.WriteLine(s);
            }
            output.WriteLine("; ---- end chunk values ----");
            output.WriteLine("");

            output.WriteLine("; ---- begin functions ----");

            foreach (LS2IL.Function f in FunctionsByNumber)
            {


                f.Emit(output);
            }
            output.WriteLine("; ---- end functions ----");

            output.WriteLine("; ---- end chunk ----");

        }

    }
}
