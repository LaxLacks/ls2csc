// This is scanned FOR DECLARATIONS and used as a library, whereby the code ls2csc compiles may use this functionality and hope it is "referenced" ;)
// The implementations of these functions are provided during runtime; code implementation here is not necessary and will be ignored.
namespace LavishScriptAPI
{
    public class LavishScriptObject : System.IDisposable
    {
        public void Invalidate();
        public void Dispose();
        public bool IsValid { get; }

        //public static bool IsNullOrInvalid(LavishScriptObject obj)

        public LavishScriptObject GetMember(string Member);
        public LavishScriptObject GetMember(string Member, params string[] indices);
        public LavishScriptObject GetIndex(params string[] indices);
        public bool ExecuteMethod(string Method);
        public bool ExecuteMethod(string Method, params string[] indices);
        public LavishScriptObject GetLSType();
        public string LSType { get; }

        //public override string ToString(); // this is implied...
    }

    public class LavishScript
    {
        static public void ExecuteCommand(string Command);
        static public string DataParse(string DataSequence);

        static public class Objects
        {
            static public LavishScriptObject NewObject(string Type);
            static public LavishScriptObject NewObject(string Type, params string[] indices);
            static public LavishScriptObject GetObject(string Name);
            static public LavishScriptObject GetObject(string Name, params string[] indices);
        }
    }
}