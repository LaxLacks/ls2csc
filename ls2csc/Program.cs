// ls2csc - C# Compiler for LavishScript 2.0 Virtual Machine
//
// See something you don't like? Let's improve it.
//
//this #define is a convenience thing for me for debugging. sorry if you hate it.
//#define USEPREDEF
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
            System.Console.WriteLine("- Building for LS2IL version 0.6.20121017.2");

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

#if USEPREDEF
                string predef = @"
/* expected
done.
*/
namespace noob536.Test{
	public class Crash {
		
		// comment this out = no crash
		protected Crash _crash;
		
		public static void Main(){

			// causes innerspace to crash when you make a instance of a class containing itself
			Crash c = new Crash();
			System.Console.WriteLine(""done."");
		}

		// have to have this... get this otherwise.
		// [System.Exception] Not found: Type 'noob536.Test.Crash' Method '.ctor{}'
		//public Crash(){}
	}
}";
#endif

#if USEPREDEF
                System.Console.WriteLine("Attempting to compile from pre-defined text:");
                System.Console.WriteLine(predef);
                tree = SyntaxTree.ParseText(predef);
#endif
            }

#if !USEPREDEF
            try
#endif
            {
                // get libraries!

                SyntaxNode newRoot = new PrefixUnaryToBinaryRewriter().Visit(tree.GetRoot());
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
#if !USEPREDEF
            catch (Exception e)
            {
                System.Console.Error.WriteLine("ls2csc: Unhandled Exception " + e.ToString());
            }
#endif

        }
    }

    class PrefixUnaryToBinaryRewriter : SyntaxRewriter
    {
        public override SyntaxNode VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            switch (node.Kind)
            {
                case SyntaxKind.PreIncrementExpression:
                    return Syntax.BinaryExpression(SyntaxKind.AddAssignExpression, node.Operand, Syntax.LiteralExpression(SyntaxKind.NumericLiteralExpression, Syntax.Literal(1)));
                case SyntaxKind.PreDecrementExpression:
                    return Syntax.BinaryExpression(SyntaxKind.SubtractAssignExpression, node.Operand, Syntax.LiteralExpression(SyntaxKind.NumericLiteralExpression, Syntax.Literal(1)));
                case SyntaxKind.NegateExpression:
                    if (node.Operand.Kind == SyntaxKind.NumericLiteralExpression)
                    {
                        dynamic newvalue = -((dynamic)((LiteralExpressionSyntax)node.Operand).Token.Value);
                        return Syntax.LiteralExpression(SyntaxKind.NumericLiteralExpression,Syntax.Literal(newvalue));
                    }
                    return node;
            }
            throw new NotImplementedException("Unary prefix " + node.Kind.ToString());
        }

    }
}
