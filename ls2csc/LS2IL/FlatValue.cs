using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;

namespace LS2IL
{

    enum FlatValueType : int
    {
        VT_Null = 0,

        VT_Single,
        VT_Double,
        VT_Decimal,

        VT_Boolean,

        VT_Int8,
        VT_Int16,
        VT_Int32,
        VT_Int64,
        VT_UInt8,
        VT_UInt16,
        VT_UInt32,
        VT_UInt64,

        VT_Char,

        VT_String,
        VT_Binary,
        VT_Array,
        VT_Table,

        VT_Reference,

        VT_Function,
        VT_Exception,

        // object support
        VT_Object,
        VT_Type,
        VT_Method,
        VT_StaticMethod,
        VT_Property,
        VT_StaticProperty,

        // not real value types:
        VT_Namespace,
        VT_Enum,
        VT_Label,

        VT_Unknown,
    }

    static class FlatObjectType
    {
        private static Compilation _Compilation;
        public static Compilation Compilation 
        {
            get
            {
                return _Compilation;
            }
            set
            {
                _Compilation = value;
                SetTypes();
            }
        
        }
        private static void SetTypes()
        {
            //System_Int32 = _Compilation.GetSpecialType(SpecialType.System_Int32);
            System_Object = _Compilation.GetSpecialType(SpecialType.System_Object);
            System_String = _Compilation.GetSpecialType(SpecialType.System_String);
            System_Void = _Compilation.GetSpecialType(SpecialType.System_Void);
            
            System_Type = null;
        }

        public static TypeSymbol System_Array { get; set; }
        public static TypeSymbol System_Exception { get; set; }
        //public static TypeSymbol System_Int32 {get;set;}
        public static TypeSymbol System_Object { get; set; }
        public static TypeSymbol System_String { get; set; }
        public static TypeSymbol System_Void { get; set; }
        //public static TypeSymbol System_Null { get; set; }


        public static TypeSymbol System_Type { get; set; }
        public static TypeSymbol System_Method { get; set; }
        public static TypeSymbol System_StaticMethod { get; set; }
        public static TypeSymbol System_Property { get; set; }
        public static TypeSymbol System_StaticProperty { get; set; }
    }

    class FlatValue
    {
        protected FlatValue()
        {
            ValueType = FlatValueType.VT_Unknown;
        }

        public FlatValue(FlatValueType value_type, string value_text, object _object)
        {
            _CurrentType = value_type;
            ValueText = value_text;
            Object = _object;
        }

        FlatValueType _CurrentType;
        public FlatValueType ValueType
        {
            get { return _CurrentType; }
            set
            {
                _CurrentType = value;
                _CurrentValue = string.Empty;
                _CurrentObject = null;
            }
        }

        string _CurrentValue;
        public string ValueText
        {
            get { return _CurrentValue; }
            set { _CurrentValue = value; }
        }

        object _CurrentObject;
        public object Object
        {
            get { return _CurrentObject; }
            set { _CurrentObject = value; }
        }

        public override string ToString()
        {
            switch (ValueType)
            {
                case FlatValueType.VT_Array:
                    if (Object != null)
                        return Object.ToString();
                    break;
                case FlatValueType.VT_Table:
                    if (Object != null)
                        return Object.ToString();
                    break;
                case FlatValueType.VT_Int8:
                    return "i8 " + ValueText;
                case FlatValueType.VT_Int16:
                    return "i16 " + ValueText;
                case FlatValueType.VT_Int32:
                    return "i32 " + ValueText;
                case FlatValueType.VT_Int64:
                    return "i64 " + ValueText;
                case FlatValueType.VT_UInt8:
                    return "u8 " + ValueText;
                case FlatValueType.VT_UInt16:
                    return "u16 " + ValueText;
                case FlatValueType.VT_UInt32:
                    return "u32 " + ValueText;
                case FlatValueType.VT_UInt64:
                    return "u64 " + ValueText;
                case FlatValueType.VT_Single:
                    return "f32 " + ValueText;
                case FlatValueType.VT_Double:
                    return "f64 " + ValueText;
                case FlatValueType.VT_Decimal:
                    throw new NotImplementedException("decimal is not technically implemented...");
                    return "f64 " + ValueText;
            }
            /*
            switch (ValueType)
            {
                case FlatValueType.VT_Type:
                    return "\"" + ((FlatObjectType)Object).FullyQualifiedName + "\"";
                case FlatValueType.VT_String:
                    return "\"" + ValueText + "\"";
            }
            /**/
            return ValueText;
        }

        public static FlatValue Null()
        {
            return new FlatValue() { ValueType = FlatValueType.VT_Null, ValueText = "null" };
        }


        public static FlatValue FromLiteralToken(TypeSymbol type, SyntaxToken token)
        {
            if (token.Kind == SyntaxKind.StringLiteralToken)
                return new FlatValue() { ValueType = GetFlatValueType(type), Object = token.Value, ValueText = "\""+token.ValueText+"\"" };
            return new FlatValue() { ValueType = GetFlatValueType(type), Object = token.Value, ValueText = token.ValueText };
        }

        public static FlatValue FromType(TypeSymbol type)
        {
            return new FlatValue() { ValueType = GetFlatValueType(type) };
        }

        public static FlatValueType GetFlatValueType(TypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_SByte:
                    return FlatValueType.VT_Int8;
                case SpecialType.System_Int16:
                    return FlatValueType.VT_Int16;
                case SpecialType.System_Int32:
                    return FlatValueType.VT_Int32;
                case SpecialType.System_Int64:
                    return FlatValueType.VT_Int64;
                case SpecialType.System_Byte:
                    return FlatValueType.VT_UInt8;
                case SpecialType.System_UInt16:
                    return FlatValueType.VT_UInt16;
                case SpecialType.System_UInt32:
                    return FlatValueType.VT_UInt32;
                case SpecialType.System_UInt64:
                    return FlatValueType.VT_UInt64;
                case SpecialType.System_Single:
                    return FlatValueType.VT_Single;
                case SpecialType.System_Double:
                    return FlatValueType.VT_Double;
                case SpecialType.System_Decimal:
                    return FlatValueType.VT_Decimal;
                case SpecialType.System_String:
                    return FlatValueType.VT_String;
                case SpecialType.System_Boolean:
                    return FlatValueType.VT_Boolean;
                case SpecialType.System_Object:
                    return FlatValueType.VT_Object;
                case SpecialType.None:
                    return FlatValueType.VT_Object;
            }
            throw new NotImplementedException("special type" + type.SpecialType.ToString());
            
        }

        public static FlatValue Int8(sbyte value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_Int8, ValueText = value.ToString(), Object = value };
        }
        public static FlatValue Int16(short value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_Int16, ValueText = value.ToString(), Object = value };
        }
        public static FlatValue Int32(int value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_Int32, ValueText = value.ToString(), Object = value };
        }
        public static FlatValue Int64(Int64 value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_Int64, ValueText = value.ToString(), Object = value };
        }
        public static FlatValue UInt8(byte value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_UInt8, ValueText = value.ToString(), Object = value };
        }
        public static FlatValue UInt16(ushort value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_UInt16, ValueText = value.ToString(), Object = value };
        }
        public static FlatValue UInt32(uint value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_UInt32, ValueText = value.ToString(), Object = value };
        }
        public static FlatValue UInt64(UInt64 value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_UInt64, ValueText = value.ToString(), Object = value };
        }

        public static FlatValue Char(char value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_Char, ValueText = "'" + value + "'", Object = value };
        }
        public static FlatValue String(string value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_String,  ValueText = "\"" + value + "\"", Object = value };
        }
        public static FlatValue Type(TypeSymbol type)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_Type, Object = type };
        }
        public static FlatValue StaticMethod(MethodSymbol value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_StaticMethod, Object = value };
        }
        public static FlatValue Method(MethodSymbol value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_Method,Object = value };
        }
        public static FlatValue StaticProperty(PropertySymbol value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_StaticProperty, Object = value };
        }
        public static FlatValue Property(PropertySymbol value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_Property,  Object = value };
        }
        public static FlatValue StaticProperty(FieldSymbol value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_StaticProperty, Object = value };
        }
        public static FlatValue Property(FieldSymbol value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_Property,  Object = value };
        }
        public static FlatValue Exception(object value)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_Exception, Object = value };
        }

        public static FlatValue Label(string valuetext)
        {
            return new FlatValue() { ValueType = FlatValueType.VT_Label, ValueText = valuetext };
        }

        public static FlatValue ObjectRef(TypeSymbol type)
        {
            return new FlatValue() { ValueType = GetFlatValueType(type)}; 
        }

    }


}
