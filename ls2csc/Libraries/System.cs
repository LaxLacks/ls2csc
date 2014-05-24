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

        namespace Generic
        {

            [Serializable]
            public struct KeyValuePair<TKey, TValue>
            {
                public KeyValuePair(TKey key, TValue value);

                public TKey Key { get; }
                public TValue Value { get; }
                public override string ToString();
            }

            public class Dictionary<TKey, TValue> : IEnumerable
            {
                public int Count { get; }
                public TValue this[TKey key] { get; set; }
                public void Add(TKey key, TValue value);
                public void Clear();
                public bool ContainsKey(TKey key);
                public bool ContainsValue(TValue value);
                public bool Remove(TKey key);
                public bool TryGetValue(TKey key, out TValue value);

                public Dictionary<TKey, TValue>.Enumerator GetEnumerator();

                public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IDisposable, IDictionaryEnumerator, IEnumerator
                {

                    public KeyValuePair<TKey, TValue> Current { get; }

                    public void Dispose();
                    public bool MoveNext();
                }

            }

        }
    }

    namespace Diagnostics
    {
        public sealed class Debugger
        {
            public static readonly string DefaultCategory;
            public static bool IsAttached { get; }
            public static void Break();
            public static bool IsLogging();
            public static bool Launch();
            public static void Log(int level, string category, string message);
            public static void NotifyOfCrossThreadDependency();
        }
    }

    public class Attribute
    {
    }
        
    // defines an intrinsic method or property, which is defined somewhere in ls2csc, as opposed to being defined at runtime
    public class LS2Intrinsic : Attribute
    {
        // the name will be used to determine which intrinsic is to be used
        public LS2Intrinsic(string name)
        {
            Name = name;
        }

        public string Name { get; set; }
    }

    public abstract class ValueType
    {
    }

    public abstract class Array : Collections.IEnumerable
    {
        // this will translate into the LEN instruction, rather than a property access
        [LS2Intrinsic("Array.Length")]
        public int Length { get; }

        public Collections.IEnumerator GetEnumerator();
    }

    public abstract class Enum : ValueType
    {
        protected Enum();

        public static Array GetValues(Type enumType);

        public static object Parse(Type enumType, string value);
        public static object Parse(Type enumType, string value, bool ignoreCase);        
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

    public struct Char
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

        [LS2Intrinsic("Object.GetMetaTable")]
        public virtual LavishScript2.Table GetMetaTable();

        [LS2Intrinsic("Object.GetType")]
        public virtual LavishScript2.Type GetType();
    }

    public struct Void
    {
    }

    /*
#if !BOOTSTRAP_MODE
    public class Exception
    {
        public Exception()
        {

        }

        public Exception(string message)
        {
            Message = message;
        }

        public Exception(string message, Exception innerException)
        {
            Message = message;
            InnerException = innerException;
        }

        public virtual string Message { get; set; }
        public Exception InnerException { get; private set; }
        public virtual string Source { get; set; }
    }
#endif
    /**/

    public class String
    {
        public static readonly string Empty = "";
        public static bool IsNullOrEmpty(string value);

        // Summary:
        //     Gets the number of characters in the current System.String object.
        //
        // Returns:
        //     The number of characters in the current string.

        // this will translate into the LEN instruction, rather than a property access
        [LS2Intrinsic("Length")]
        public int Length { get; }

        public string[] Split(params char[] separator);
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

    public class Type : LavishScript2.Type
    {

    }
}