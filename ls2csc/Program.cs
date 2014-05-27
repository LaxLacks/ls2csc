// ls2csc - C# Compiler for LavishScript 2.0 Virtual Machine
//
// See something you don't like? Let's improve it.
//
#if DEBUG
//this #define is a convenience thing for me for debugging. sorry if you hate it.
//#define USEPREDEF
// this one is to catch exceptions with visual studio instead of dumping it to Error. there's probably a built in debug flag...
//#define OUTPUTEXCEPTIONS
#else
//this #define is a convenience thing for me for debugging. sorry if you hate it.
//#define USEPREDEF
// comment this one to catch exceptions with visual studio instead of dumping it to Error
#define OUTPUTEXCEPTIONS
#endif
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using LS2IL;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NDesk.Options;

namespace ls2csc
{
    /// <summary>
    /// Helper class that doesn't belong in this file, but retrieves streams by name from the assembly, e.g. ls2csc.Libraries.LavishScriptAPI.cs, which is Build Action=Embedded Resource
    /// </summary>
    public class Resources
    {
        static Resources _Instance;
        static public Resources Instance
        {
            get
            {
                if (_Instance == null)
                    _Instance = new Resources();
                return _Instance;
            }
        }

        private Resources()
        {
            Assembly = Assembly.GetExecutingAssembly();

            ResourceNames = new List<string>(Assembly.GetManifestResourceNames());
        }

        public Assembly Assembly { get; private set; }
        public List<string> ResourceNames { get; private set; }

        public Stream GetStream(string exact_name)
        {
            return Assembly.GetManifestResourceStream(exact_name);
        }

        public Stream FindStream(string contains_name)
        {
            if (ResourceNames == null)
                return null;

            string lwr = contains_name.ToLowerInvariant();

            foreach (string s in ResourceNames)
            {
                if (s.ToLowerInvariant().Contains(lwr))
                    return GetStream(s);
            }
            return null;
        }

        public string DeserializeStream(string exact_name)
        {
            return DeserializeStream(GetStream(exact_name));
        }

        public static string DeserializeStream(Stream s)
        {
            using (TextReader tr = new StreamReader(s))
            {
                return tr.ReadToEnd();
            }
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            List<String> referenceFiles = new List<String>();
            List<String> inputFiles = new List<String>();
            List<SyntaxTree> inputTrees = new List<SyntaxTree>();
            String outputFile = "";
            bool help = false;
            int silence = 0;
            bool bootstrap = false;

            OptionSet Options = new OptionSet(){
                {"i|input=", "Input {file}", v => { inputFiles.Add(v); }},
                {"r|reference=", "Reference {file}", v => { referenceFiles.Add(v); }},
                {"o|output=", "Output {file}", v => { outputFile = v; }},
                {"h|?|help", "Show this help and exit", v => { help = (v != null); }},
                {"s|silent", "Silence, 1 per level", v => { silence++; }},
                {"b|bootstrap", "LS2 Bootstrap compiler mode", v => { bootstrap = (v != null); }}
            };

            Options.Parse(args);

            if (silence <= 0)
            {
                System.Console.Error.WriteLine("C# Compiler for LavishScript 2.0 Virtual Machine");
                System.Console.Error.WriteLine("- Building for LS2IL version " + LS2IL.Chunk.LS2ILVersion);
            }

            if (bootstrap)
            {
                System.Console.Error.WriteLine("- LS2 Bootstrap compiler mode");
            }

            if (help)
            {
                System.Console.Error.WriteLine("Usage: ls2csc -i input.cs -o output.il");
                Options.WriteOptionDescriptions(System.Console.Error);
                return;
            }

            TextWriter output;

            if (outputFile != "")
            {
                output = new StreamWriter(outputFile);
            }
            else
            {
                output = System.Console.Out;
            }

            if (bootstrap)
            {
                if (inputFiles.Count != 0)
                {
                    System.Console.Error.WriteLine("Input files ignored in Bootstrap compiler mode.");
                }
                inputFiles = new List<string>();

                string text = string.Empty;
                text += "#define BOOTSTRAP_MODE" + System.Environment.NewLine;
                text += Resources.Instance.DeserializeStream("ls2csc.Libraries.BootStrap.cs");
                SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
                // syntaxTrees.Add(tree);
                inputTrees.Add(tree);
            }
            else
            {

                if (inputFiles.Count != 0)
                {

                    foreach (string inputfile in inputFiles)
                    {
                        if (silence <= 0)
                        {
                            System.Console.Error.WriteLine("Attempting to compile from file '" + inputfile + "'");
                        }

                        inputTrees.Add(CSharpSyntaxTree.ParseFile(inputfile));
                    }
                }
                else
                {
#if !USEPREDEF
                    System.Console.Error.WriteLine("ls2csc: Filename required");
                    System.Console.Error.WriteLine("ls2csc: To display help, use 'ls2csc -h'");
                    return;
#endif

                    // this mess is for testing.
#if USEPREDEF
                string predef = @"
/* expected
your ls2 filename: <preloaded> IsStarted=true
*/
using System;
using LavishScript2;
using LavishScriptAPI;
namespace ls2csctest
{
    public enum SomeEnum : int
    {
        SomeValue=1,
        SomeOtherValue,
    }

    public interface ISomeIFace
    {
        int SomeMethod();
        string SomeProperty { get; }
    }

    public class SomeClass : ISomeIFace
    {
        public SomeClass()
        {
            MyEnum = SomeEnum.SomeOtherValue;
        }

        public virtual int SomeMethod()
        {
            return 42;
        }

        public string SomeProperty
        {
            get { return ""What is the answer to the question of life, the universe, and everything?""; }
        }

        public SomeEnum MyEnum { get; set; }
    }

	public class Test{
		public static void Main(){
			FieldTest ft = new FieldTest();
			ft.Test();

            foreach(Script s in Script.AllScripts)
            {               
                string fname;

                try
                {
                    fname = s.Filename;
                }
                catch
                {
                    fname = ""<preloaded>""; // expected since we are currently building bytecode from ls2il in memory, instead of using a bytecode file
                }

                System.Console.WriteLine(s.Name+"": ""+fname+"" IsStarted=""+s.IsStarted.ToString());
            }

            SomeClass myClass = new SomeClass();
            if (myClass is ISomeIFace)
            {
                ISomeIFace iface = myClass as ISomeIFace;
                System.Console.WriteLine(iface.SomeProperty+"" ""+iface.SomeMethod()+"" MyEnum=""+myClass.MyEnum.ToString());
            }

/*
            using (LavishScriptObject obj = LavishScript.Objects.GetObject(""InnerSpace""), obj2 = LavishScript.Objects.GetObject(""InnerSpace""))
            {
               System.Console.WriteLine(""InnerSpace.Build==""+obj.GetMember(""Build"").ToString());
            }
*/
		}
	}
	
	public class FieldTest{
		public int num = -10;

		public void OtherTest(){
		
		}
		public void Test(){
			OtherTest();
			
			this.OtherTest();
		}
	}
}";
#endif

#if USEPREDEF
                if (silence <= 0)
                {
                    System.Console.WriteLine("Attempting to compile from pre-defined text:");
                }
                System.Console.WriteLine(predef);
                inputtrees.Add(SyntaxTree.ParseText(predef));
#endif
                }
            }
#if OUTPUTEXCEPTIONS
            try
#endif
            {
                // get libraries!

                //List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();
                List<SyntaxTree> referenceTrees = new List<SyntaxTree>();

                #region Metadata == Declarations == "Libraries"
                string[] auto_reference = { 
                                              "ls2csc.Libraries.InnerSpaceAPI.cs", 
                                              "ls2csc.Libraries.LavishScriptAPI.cs", 
                                              "ls2csc.Libraries.LavishScript2.cs", 
                                              "ls2csc.Libraries.LavishSettings.cs", 
                                              "ls2csc.Libraries.System.cs",
                                              "ls2csc.Libraries.BootStrap.cs"
                                          };
                

                foreach (string s in auto_reference)
                {
                    string text = string.Empty;
                    if (bootstrap)
                    {
                        if (s.Contains("BootStrap"))
                        {
                            continue;
                        }
                        text += "#define BOOTSTRAP_MODE" + System.Environment.NewLine;
                    }
                    text += Resources.Instance.DeserializeStream(s);
                    SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
                   // syntaxTrees.Add(tree);
                    referenceTrees.Add(tree);
                }



                foreach (string s in referenceFiles)
                {
                    SyntaxTree tree = CSharpSyntaxTree.ParseFile(s);
                    //syntaxTrees.Add(tree);
                    referenceTrees.Add(tree);
                }
                #endregion

                Compilation compilation = CSharpCompilation.Create("MyCompilation", syntaxTrees: referenceTrees);


                compilation = compilation.AddSyntaxTrees(inputTrees);

                #region Diagnose and display Errors on the original code
                int nErrors = 0;
                foreach (SyntaxTree tree in inputTrees)
                {
                    SemanticModel model = compilation.GetSemanticModel(tree);
                    IEnumerable<Diagnostic> diags;
                    if ((diags = model.GetDiagnostics()) != null)
                    {
                        foreach (Diagnostic diag in diags)
                        {
                            if (diag.Severity == DiagnosticSeverity.Error)
                            {
                                nErrors++;
                            }
                            int neededSilence = 1;
                            switch (diag.Severity)
                            {
                                case DiagnosticSeverity.Error:
                                    neededSilence = 3;
                                    break;
                                case DiagnosticSeverity.Warning:
                                    neededSilence = 2;
                                    break;
                                case DiagnosticSeverity.Info:
                                    neededSilence = 1;
                                    break;
                            }
                            if (silence <= neededSilence)
                            {
                                System.Console.Error.WriteLine(diag.ToString());
                            }
                        }

                    }
                }
                if (nErrors > 0)
                {
                    System.Console.Error.WriteLine("Fix " + nErrors.ToString() + " errors. :(");
                    return;
                }
                #endregion



                {
                    List<SyntaxTree> finalReferenceTrees = new List<SyntaxTree>();
                    foreach (SyntaxTree tree in referenceTrees)
                    {

                        SyntaxNode newRoot = tree.GetRoot();
                        SemanticModel model = compilation.GetSemanticModel(tree);
                        newRoot = new IndexerRewriter(model).Visit(newRoot);
                        newRoot = new EnumValueRewriter().Visit(newRoot);
                        newRoot = new AutoImplementedPropertyRewriter().Visit(newRoot);
                        finalReferenceTrees.Add((CSharpSyntaxTree.Create((CSharpSyntaxNode)newRoot)));
                    }
                    referenceTrees = finalReferenceTrees;
                }
                #region C# Code Transformations: Optimizations and other rewriters
                List<SyntaxTree> finaltrees = new List<SyntaxTree>();

                foreach (SyntaxTree tree in inputTrees)
                {
                    SyntaxNode newRoot = tree.GetRoot();
                    SemanticModel model = compilation.GetSemanticModel(tree);

                    newRoot = new IndexerRewriter(model).Visit(newRoot);
                    newRoot = new EnumValueRewriter().Visit(newRoot);
#if SCRIPTING_API_REINTRODUCED
                    newRoot = new Optimizers.CondenseLiteralsRewriter().Visit(newRoot);
#endif
                    newRoot = new PrefixUnaryToBinaryRewriter().Visit(newRoot);
                    newRoot = new FieldInitializerRewriter(model).Visit(newRoot);
                    newRoot = new ForeachRewriter().Visit(newRoot);
                    newRoot = new AutoImplementedPropertyRewriter().Visit(newRoot);

                    finaltrees.Add(CSharpSyntaxTree.Create((CSharpSyntaxNode)newRoot));
                }
                #endregion

                compilation = CSharpCompilation.Create("MyCompilation", syntaxTrees: referenceTrees);
                compilation = compilation.AddSyntaxTrees(finaltrees);

                #region LS2 IL Code Generation
                LS2IL.FlatObjectType.Compilation = compilation;
                LS2IL.Chunk chunk = new LS2IL.Chunk(compilation);

                foreach (SyntaxTree tree in referenceTrees)
                {
                    SemanticModel model = compilation.GetSemanticModel(tree);

                    SyntaxNode root = tree.GetRoot();
                    // Build up the metadata
                    DeclarationCollector dc = new DeclarationCollector(chunk, model, true); // isLibrary = true because these are the reference-only trees
                    dc.Visit(root);
                }

                foreach (SyntaxTree tree in finaltrees)
                {

                    SemanticModel model = compilation.GetSemanticModel(tree);

                    SyntaxNode root = tree.GetRoot();

                    // collect methods and properties to turn into LS2IL.Functions 
                    MethodCollector mc = new MethodCollector(chunk, model);
                    mc.Visit(root);

                    // Build up the metadata
                    DeclarationCollector dc = new DeclarationCollector(chunk, model, false); // isLibrary = false because these are the trees going into the Chunk
                    dc.Visit(root);
                }

                // TODO: command-line options for these flags
                LS2ILGeneratorOptions options = new LS2ILGeneratorOptions() { CondenseRegisters = true, ElevateLongValues = true, FilterUnusedInstructions = true, FlattenLabels = true };

                chunk.Emit(options, output);
                output.WriteLine("");
                #endregion

            }
#if OUTPUTEXCEPTIONS
            catch (Exception e)
            {
                System.Console.Error.WriteLine("ls2csc: Unhandled Exception " + e.ToString());
            }
            finally
            
#endif
            {
                output.Close();
            }
        }
    }
}
