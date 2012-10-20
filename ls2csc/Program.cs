// ls2csc - C# Compiler for LavishScript 2.0 Virtual Machine
//
// See something you don't like? Let's improve it.
//
//this #define is a convenience thing for me for debugging. sorry if you hate it.
//#define USEPREDEF
// uncomment one is to catch exceptions with visual studio instead of dumping it to Error. there's probably a built in debug flag...
#define OUTPUTEXCEPTIONS
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Roslyn.Services;
using Roslyn.Services.CSharp;

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
            System.Console.WriteLine("C# Compiler for LavishScript 2.0 Virtual Machine");
            System.Console.WriteLine("- Building for LS2IL version 0.7.20121020.1");

            // TODO: manage args. multiple input files, use as reference (e.g. Libraries/LavishScriptAPI.cs), etc. 
            //       Roslyn supports #r directive for references. May be able to support that too.

            // to hold the syntax tree from the input file.
            SyntaxTree tree;

            if (args != null && args.Length > 0)
            {
                System.Console.WriteLine("Attempting to compile from file '" + args[0] + "'");
                tree = SyntaxTree.ParseFile(args[0]);
            }
            else
            {
#if !USEPREDEF
                System.Console.WriteLine("ls2csc: Filename required");
                return;
#endif

                // this mess is for testing.
#if USEPREDEF
                string predef = @"
/* expected
done.
*/
namespace ls2csc.Test
{
	public class Program
    {				
        public static void Main()
        {
            ushort i = 65535;
            i = (ushort)(i+1);
            i++;

			System.Console.WriteLine(i.ToString());
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

                tree = SyntaxTree.Create((CompilationUnitSyntax)newRoot);

                List<SyntaxTree> syntaxTrees = new List<SyntaxTree>();

                syntaxTrees.Add(SyntaxTree.ParseText(Resources.Instance.DeserializeStream("ls2csc.Libraries.LavishScriptAPI.cs")));
                syntaxTrees.Add(SyntaxTree.ParseText(Resources.Instance.DeserializeStream("ls2csc.Libraries.System.cs")));

                var root = (CompilationUnitSyntax)tree.GetRoot();
                syntaxTrees.Add(tree);
                var compilation = Compilation.Create("MyCompilation",syntaxTrees: syntaxTrees);
                LS2IL.FlatObjectType.Compilation = compilation;
                var model = compilation.GetSemanticModel(tree);

                var diags = model.GetDiagnostics();
                if (diags != null)
                {
                    int nErrors = 0;
                    foreach (Diagnostic d in diags)
                    {
                        nErrors++;
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

                chunk.Emit();

                System.Console.WriteLine("");
            }
#if OUTPUTEXCEPTIONS
            catch (Exception e)
            {
                System.Console.Error.WriteLine("ls2csc: Unhandled Exception " + e.ToString());
            }
#endif

        }
    }
}
