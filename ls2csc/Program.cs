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
            System.Console.Error.WriteLine("C# Compiler for LavishScript 2.0 Virtual Machine");
            System.Console.Error.WriteLine("- Building for LS2IL version 0.7.20121023.1");

            String inputfile = "";
            String outputfile = "";
            bool help = false;

            OptionSet Options = new OptionSet(){
                {"i|input=", "Input {file}", v => { inputfile = v; }},
                {"o|output=", "Output {file}", v => { outputfile = v; }},
                {"h|?|help", "Show this help and exit", v => { help = (v != null); }},
            };

            Options.Parse(args);

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

            // TODO: manage args. multiple input files, use as reference (e.g. Libraries/LavishScriptAPI.cs), etc. 
            //       Roslyn supports #r directive for references. May be able to support that too.

            // to hold the syntax tree from the input file.
            SyntaxTree tree;

            if (inputfile != "")
            {
                System.Console.Error.WriteLine("Attempting to compile from file '" + inputfile + "'");
                tree = SyntaxTree.ParseFile(inputfile);
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
    public interface ISomeIFace
    {
        int SomeMethod();
        string SomeProperty { get; }
    }

    public class SomeClass : ISomeIFace
    {
        public virtual int SomeMethod()
        {
            return 42;
        }

        public string SomeProperty
        {
            get { return ""What is the answer to the question of life, the universe, and everything?""; }
        }
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
                System.Console.WriteLine(iface.SomeProperty+"" ""+iface.SomeMethod());
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
                System.Console.WriteLine("Attempting to compile from pre-defined text:");
                System.Console.WriteLine(predef);
                tree = SyntaxTree.ParseText(predef);
#endif
            }

#if OUTPUTEXCEPTIONS
            try
#endif
            {
                // get libraries!
                SyntaxNode newRoot = tree.GetRoot();

                newRoot = new Optimizers.CondenseLiteralsRewriter().Visit(newRoot);
                newRoot = new PrefixUnaryToBinaryRewriter().Visit(newRoot);
                newRoot = new FieldInitializerRewriter().Visit(newRoot);
                newRoot = new ForeachRewriter().Visit(newRoot);
                newRoot = new AutoImplementedPropertyRewriter().Visit(newRoot);

                tree = SyntaxTree.Create((CompilationUnitSyntax)newRoot);

                List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();

                syntaxTrees.Add(SyntaxTree.ParseText(Resources.Instance.DeserializeStream("ls2csc.Libraries.LavishScriptAPI.cs")));
                syntaxTrees.Add(SyntaxTree.ParseText(Resources.Instance.DeserializeStream("ls2csc.Libraries.LavishScript2.cs")));
                syntaxTrees.Add(SyntaxTree.ParseText(Resources.Instance.DeserializeStream("ls2csc.Libraries.System.cs")));

                var root = (CompilationUnitSyntax)tree.GetRoot();
                syntaxTrees.Add(tree);
                var compilation = Compilation.Create("MyCompilation", syntaxTrees: syntaxTrees);
                
                LS2IL.FlatObjectType.Compilation = compilation;
                var model = compilation.GetSemanticModel(tree);
                

                var diags = model.GetDiagnostics();
                if (diags != null)
                {
                    int nErrors = 0;
                    foreach (Diagnostic d in diags)
                    {
                        if (d.Info.Severity == DiagnosticSeverity.Error)
                        {
                            nErrors++;
                        }
                        System.Console.Error.WriteLine(d.ToString());
                    }
                    if (nErrors > 0)
                    {
                        System.Console.Error.WriteLine("Fix " + nErrors.ToString() + " errors. :(");
                        return;
                    }
                }

                LS2IL.Chunk chunk = new LS2IL.Chunk(root, compilation, model);
                MethodCollector mc = new MethodCollector(chunk);
                mc.Visit(root);

                DeclarationCollector dc = new DeclarationCollector(chunk);
                dc.Visit(root);

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
