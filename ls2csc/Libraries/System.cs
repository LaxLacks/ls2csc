// This is scanned FOR DECLARATIONS and used as a library, whereby the code ls2csc compiles may use this functionality and hope it is "referenced" ;)
// The implementations of these functions are provided during runtime; code implementation here is not necessary and will be ignored.
// This stuff will ideally match the C# standards pretty closely... we would like code to cross-compile between ls2csc, .NET and Roslyn, Mono, etc.
namespace System
{
    public interface IDisposable
    {
        void Dispose();
    }

    namespace Collections
    {
        public interface IEnumerable
        {
            IEnumerator GetEnumerator();
        }

        public interface IEnumerator
        {
            object Current { get; }
            bool MoveNext();
            void Reset();
        }
    }

    class Attribute
    {
    }
        
    // defines an intrinsic method or property, which is defined somewhere in ls2csc, as opposed to being defined at runtime
    class LS2Intrinsic : Attribute
    {
        // the name will be used to determine which intrinsic is to be used
        public LS2Intrinsic(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
    }

    public abstract class Array : Collections.IEnumerable
    {
        // this will translate into the LEN instruction, rather than a property access
        [LS2Intrinsic("Array.Length")]
        public int Length { get; }

        public Collections.IEnumerator GetEnumerator();
    }

    public struct SByte 
    {
    }

    public struct Int16
    {
    }

    public struct Int32
    {
    }

    public struct Int64 
    {
    }

    public struct Byte
    {
    }

    public struct UInt16
    {
    }

    public struct UInt32
    {
    }

    public struct UInt64
    {
    }

    public struct Single
    {
    }

    public struct Double
    {
    }

    public struct Decimal
    {
    }

    public struct Boolean
    {
    }

    public class Object
    {
        // this will translate into the STRINGVAL instruction, rather than a method access
        [LS2Intrinsic("Object.ToString")]
        public virtual string ToString();    
    }

    public struct Void
    {
    }

    public class Exception
    {
        public virtual string Message { get; }
    }

    public class LS2Exception : Exception
    {
        
    }

    public class String
    {
        // Summary:
        //     Gets the number of characters in the current System.String object.
        //
        // Returns:
        //     The number of characters in the current string.

        // this will translate into the LEN instruction, rather than a property access
        [LS2Intrinsic("Length")]
        public int Length { get; }
    }
    public class Console
    {
        public static void WriteLine(string s);
    }

    public static class Math
    {
        public const double E = 2.71828;
        public const double PI = 3.14159;

        public static decimal Abs(decimal value);
        public static double Abs(double value);
        public static float Abs(float value);
        public static int Abs(int value);
        public static long Abs(long value);
        public static sbyte Abs(sbyte value);
        public static short Abs(short value);
    }
}