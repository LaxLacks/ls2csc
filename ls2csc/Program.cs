// ls2csc - C# Compiler for LavishScript 2.0 Virtual Machine
//
// See something you don't like? Let's improve it.
//
#if DEBUG
//this #define is a convenience thing for me for debugging. sorry if you hate it.
#define USEPREDEF
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
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using Roslyn.Services.CSharp;
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
            List<String> inputfiles = new List<String>();
            List<SyntaxTree> inputtrees = new List<SyntaxTree>();
            String outputfile = "";
            bool help = false;
            int silence = 0;

            OptionSet Options = new OptionSet(){
                {"i|input=", "Input {file}", v => { inputfiles.Add(v); }},
                {"o|output=", "Output {file}", v => { outputfile = v; }},
                {"h|?|help", "Show this help and exit", v => { help = (v != null); }},
                {"s|silent", "Silence, 1 per level", v => { silence++; }}
            };

            Options.Parse(args);

            if (silence <= 0)
            {
                System.Console.Error.WriteLine("C# Compiler for LavishScript 2.0 Virtual Machine");
                System.Console.Error.WriteLine("- Building for LS2IL version " + LS2IL.Chunk.LS2ILVersion);
            }

            if (help)
            {
                System.Console.Error.WriteLine("Usage: ls2csc -i input.cs -o output.il");
                Options.WriteOptionDescriptions(System.Console.Error);
                return;
            }

            TextWriter output;

            if (outputfile != "")
            {
                output = new StreamWriter(outputfile);
            }
            else
            {
                output = System.Console.Out;
            }

            if (inputfiles.Count != 0)
            {
                foreach (string inputfile in inputfiles)
                {
                    if (silence <= 0)
                    {
                        System.Console.Error.WriteLine("Attempting to compile from file '" + inputfile + "'");
                    }
                    inputtrees.Add(SyntaxTree.ParseFile(inputfile));
                }
            }
            else
            {
#if !USEPREDEF
                System.Console.Error.WriteLine("ls2csc: Filename required");
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

#if OUTPUTEXCEPTIONS
            try
#endif
            {
                // get libraries!

                List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();

                syntaxTrees.Add(SyntaxTree.ParseText(Resources.Instance.DeserializeStream("ls2csc.Libraries.LavishScriptAPI.cs")));
                syntaxTrees.Add(SyntaxTree.ParseText(Resources.Instance.DeserializeStream("ls2csc.Libraries.LavishScript2.cs")));
                syntaxTrees.Add(SyntaxTree.ParseText(Resources.Instance.DeserializeStream("ls2csc.Libraries.System.cs")));

                // detect errors!
                Compilation compilation = Compilation.Create("MyCompilation", syntaxTrees: syntaxTrees);

                
                compilation = compilation.AddSyntaxTrees(inputtrees);
                int nErrors = 0;
                foreach (SyntaxTree tree in inputtrees)
                {
                    SemanticModel model = compilation.GetSemanticModel(tree);
                    IEnumerable<Diagnostic> diags;
                    if ((diags = model.GetDiagnostics())!=null)
                    {
                        foreach (Diagnostic diag in diags)
                        {
                            if (diag.Info.Severity == DiagnosticSeverity.Error)
                            {
                                nErrors++;
                            }
                            int neededSilence = 1;
                            switch (diag.Info.Severity)
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

                // Perform optimizations and language feature rewrites

                List<SyntaxTree> finaltrees = new List<SyntaxTree>();

                foreach (SyntaxTree tree in inputtrees)
                {
                    SyntaxNode newRoot = tree.GetRoot();

	                newRoot = new EnumValueRewriter().Visit(newRoot);
                    newRoot = new Optimizers.CondenseLiteralsRewriter().Visit(newRoot);
                    newRoot = new PrefixUnaryToBinaryRewriter().Visit(newRoot);
                    newRoot = new FieldInitializerRewriter().Visit(newRoot);
                    newRoot = new ForeachRewriter().Visit(newRoot);
                    newRoot = new AutoImplementedPropertyRewriter().Visit(newRoot);

                    finaltrees.Add(SyntaxTree.Create((CompilationUnitSyntax)newRoot));
                }

                compilation = Compilation.Create("MyCompilation", syntaxTrees: syntaxTrees);
                compilation = compilation.AddSyntaxTrees(finaltrees);

                LS2IL.FlatObjectType.Compilation = compilation;

                LS2IL.Chunk chunk = new LS2IL.Chunk(compilation);

                foreach (SyntaxTree tree in finaltrees)
                {

                    SemanticModel model = compilation.GetSemanticModel(tree);

                    SyntaxNode root = tree.GetRoot();

                    MethodCollector mc = new MethodCollector(chunk, model);
                    mc.Visit(root);
                    
                    DeclarationCollector dc = new DeclarationCollector(chunk, model);
                    dc.Visit(root);

                }

                chunk.Emit(output);

                output.WriteLine("");
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
