﻿// This is scanned FOR DECLARATIONS and used as a library, whereby the code ls2csc compiles may use this functionality and hope it is "referenced" ;)
// The implementations of these functions are provided during runtime; code implementation here is not necessary and will be ignored.
// This stuff will ideally match the C# standards pretty closely... we would like code to cross-compile between ls2csc, .NET and Roslyn, Mono, etc.
namespace System
{
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

    public abstract class Array
    {
        // this will translate into the LEN instruction, rather than a property access
        [LS2Intrinsic("Array.Length")]
        public int Length { get; }
    }

    public struct Int32
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
}