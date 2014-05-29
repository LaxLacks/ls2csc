using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LS2IL
{

    enum FlatOperandType
    {
        OPND_IMMEDIATE,

        // referenced values
        OPND_CHUNK_VALUEREF,
        OPND_FUNCTION_VALUEREF,
        OPND_FIELD_VALUEREF,
        OPND_INPUT_VALUEREF,
        OPND_REGISTER_VALUEREF,

        // referenced objects
        OPND_CHUNK,
        OPND_FUNCTION,
        OPND_INPUTS,
        OPND_REGISTERS,
        OPND_EXCEPTION,
        OPND_THIS,

        // invalid operand types
        OPND_LABEL,
    }

    sealed class FlatOperand
    {
        private FlatOperand(FlatOperandType type, FlatValue imm)
        {
            OperandType = type;
            ImmediateValue = imm;
        }
        private FlatOperand(FlatOperandType type, int index, FlatValue imm)
        {
            OperandType = type;
            ImmediateValue = imm;
            OperandIndex = index;
        }

        public FlatOperandType OperandType { get; private set; }
        public int OperandIndex { get; private set; }

        //public string ImmediateValue { get; private set; }

        public FlatValue ImmediateValue { get; private set; }

        //public string IdentifierName { get; private set; }

        public FlatOperand WithImmediateValue(FlatValue newValue)
        {
            return new FlatOperand(OperandType, OperandIndex, newValue);
        }

        public FlatOperand GetLValue(Function function, List<FlatStatement> list)
        {
            switch (OperandType)
            {
                case FlatOperandType.OPND_REGISTER_VALUEREF:
                    return FlatOperand.LiteralInteger(OperandIndex);
                case FlatOperandType.OPND_IMMEDIATE:
                case FlatOperandType.OPND_CHUNK:
                case FlatOperandType.OPND_CHUNK_VALUEREF:
                case FlatOperandType.OPND_EXCEPTION:
                case FlatOperandType.OPND_FUNCTION:
                case FlatOperandType.OPND_FUNCTION_VALUEREF:
                case FlatOperandType.OPND_FIELD_VALUEREF:
                case FlatOperandType.OPND_INPUT_VALUEREF:
                case FlatOperandType.OPND_INPUTS:
                case FlatOperandType.OPND_REGISTERS:
                case FlatOperandType.OPND_THIS:
                    {

                        FlatOperand fop;
                        fop = function.AllocateRegister("");
                        FlatOperand lvalue = fop.GetLValue(function, list);
                        list.Add(FlatStatement.REFERENCE(lvalue, this));
                        return lvalue;
                    }
                case FlatOperandType.OPND_LABEL:
                    {
                        throw new NotSupportedException("LValue from OPND_LABEL (doesn't exist!)");
                    }
            }


            throw new NotImplementedException("LValue from " + OperandType.ToString());
        }
       
        public FlatOperand AsRValue(FlatValue immediate_value)
        {
            if (OperandType != FlatOperandType.OPND_IMMEDIATE)
                throw new NotSupportedException("Can only turn OPND_IMMEDIATE into RValue");

            if (ImmediateValue.ValueType == FlatValueType.VT_Int32)
            {
                return FlatOperand.RegisterRef((int)ImmediateValue.Object,immediate_value);
            }

            throw new NotImplementedException("ImmediateValue type "+ImmediateValue.ValueType.ToString());
        }        

        public override string ToString()
        {
            switch (OperandType)
            {
                case FlatOperandType.OPND_IMMEDIATE:
                    return ImmediateValue.ToString();
                case FlatOperandType.OPND_LABEL:
                    return ImmediateValue.ToString();
                case FlatOperandType.OPND_FIELD_VALUEREF:
                    return "field[" + OperandIndex.ToString() + "]";
                case FlatOperandType.OPND_REGISTER_VALUEREF:
                    return "register["+OperandIndex.ToString()+"]";
                case FlatOperandType.OPND_INPUT_VALUEREF:
                    return "input[" + OperandIndex.ToString() + "]";
                case FlatOperandType.OPND_FUNCTION_VALUEREF:
                    return "functionvalue[" + OperandIndex.ToString() + "]";
                case FlatOperandType.OPND_EXCEPTION:
                    return "exception";
                case FlatOperandType.OPND_THIS:
                    return "this";
                case FlatOperandType.OPND_INPUTS:
                    return "inputs";
            }

            throw new NotImplementedException("Operand type " + OperandType.ToString());
        }

        public static FlatOperand Inputs()
        {
            return new FlatOperand(FlatOperandType.OPND_INPUTS, new FlatValue(FlatValueType.VT_Array,"{ }",null));
        }

        public static FlatOperand FunctionValueRef(int nValue, FlatValue flatvalue)
        {
            return new FlatOperand(FlatOperandType.OPND_FUNCTION_VALUEREF, nValue, flatvalue);
        }

        public static FlatOperand FieldRef(int nRegister, FlatValue flatvalue)
        {
            return new FlatOperand(FlatOperandType.OPND_FIELD_VALUEREF, nRegister, flatvalue);
        }

        public static FlatOperand RegisterRef(int nRegister, FlatValue flatvalue)
        {
            return new FlatOperand(FlatOperandType.OPND_REGISTER_VALUEREF, nRegister, flatvalue);
        }

        public static FlatOperand InputRef(int nValue, FlatValue flatvalue)
        {
            return new FlatOperand(FlatOperandType.OPND_INPUT_VALUEREF, nValue,flatvalue );
        }

        public static FlatOperand LiteralInteger(int value)
        {
            return new FlatOperand(FlatOperandType.OPND_IMMEDIATE, FlatValue.Int32(value));
        }
        public static FlatOperand LiteralString(string value)
        {
            return new FlatOperand(FlatOperandType.OPND_IMMEDIATE, FlatValue.String(value));
        }

        public static FlatOperand LiteralNull()
        {
            return new FlatOperand(FlatOperandType.OPND_IMMEDIATE, FlatValue.Null());
        }
        public static FlatOperand LabelRef(string name)
        {
            return new FlatOperand(FlatOperandType.OPND_LABEL, FlatValue.Label(name));
        }
        public static FlatOperand ExceptionRef()
        {
            return new FlatOperand(FlatOperandType.OPND_EXCEPTION, FlatValue.Exception(null));
        }
        public static FlatOperand ThisRef(FlatValue flatvalue)
        {
            return new FlatOperand(FlatOperandType.OPND_THIS, flatvalue);
        }

#if false


        /*
        public static FlatOperand LiteralLabel(string name)
        {
            return new FlatOperand(FlatOperandType.OPND_IMMEDIATE, FlatValue.Label(name));
        }*/

        public static FlatOperand LiteralString(string value)
        {
            return new FlatOperand(FlatOperandType.OPND_IMMEDIATE, FlatValue.String(value));
        }

        public static FlatOperand LiteralNull()
        {
            return new FlatOperand(FlatOperandType.OPND_IMMEDIATE, FlatValue.Null());
        }

        public static FlatOperand LiteralValue(string value)
        {
            return new FlatOperand(FlatOperandType.OPND_IMMEDIATE, FlatValue.Unknown(value));
        }
#endif

        public static FlatOperand Immediate(FlatValue value)
        {
            return new FlatOperand(FlatOperandType.OPND_IMMEDIATE, value);
        }
    }

    abstract class Statement
    {
    }

    sealed class FlatStatement 
    {
        private FlatStatement(Instruction instr)
        {
            Instruction = instr;
        }

        private FlatStatement(Instruction instr, FlatOperand op1)
        {
            Instruction = instr;
            Operands = new List<FlatOperand>();
            Operands.Add(op1);
        }

        private FlatStatement(Instruction instr, FlatOperand op1, FlatOperand op2)
        {
            Instruction = instr;
            Operands = new List<FlatOperand>();
            Operands.Add(op1);
            Operands.Add(op2);
        }

        private FlatStatement(Instruction instr, FlatOperand op1, FlatOperand op2, FlatOperand op3)
        {
            Instruction = instr;
            Operands = new List<FlatOperand>();
            Operands.Add(op1);
            Operands.Add(op2);
            Operands.Add(op3);
        }

        public Instruction Instruction { get; private set; }
        public List<FlatOperand> Operands { get; private set; }
        public string Comment { get; set; }

#if false
        public override void Expand(Function function, List<FlatStatement> list)
        {
            list.Add(this);
        }
#endif

        public static FlatStatement NOP()
        {
            return new FlatStatement(Instruction.NOP);
        }
        public static FlatStatement NULLIFY(FlatOperand lvalue)
        {
            return new FlatStatement(Instruction.NULLIFY, lvalue, lvalue);
        }
        public static FlatStatement AS(FlatOperand lvalue, FlatOperand subject, FlatOperand as_type)
        {
            return new FlatStatement(Instruction.AS, lvalue, subject, as_type);
        }
        public static FlatStatement IS(FlatOperand lvalue, FlatOperand subject, FlatOperand is_type)
        {
            return new FlatStatement(Instruction.IS, lvalue, subject, is_type);
        }
        public static FlatStatement ADD(FlatOperand lvalue, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.ADD, lvalue, left, right);
        }
        public static FlatStatement SUB(FlatOperand lvalue, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.SUB, lvalue, left, right);
        }
        public static FlatStatement DIV(FlatOperand lvalue, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.DIV, lvalue, left, right);
        }
        public static FlatStatement MUL(FlatOperand lvalue, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.MUL, lvalue, left, right);
        }
        public static FlatStatement MOD(FlatOperand lvalue, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.MOD, lvalue, left, right);
        }
        public static FlatStatement POW(FlatOperand lvalue, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.POW, lvalue, left, right);
        }
        public static FlatStatement AND(FlatOperand lvalue, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.AND, lvalue, left, right);
        }
        public static FlatStatement OR(FlatOperand lvalue, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.OR, lvalue, left, right);
        }
        public static FlatStatement XOR(FlatOperand lvalue, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.XOR, lvalue, left, right);
        }
        public static FlatStatement SHL(FlatOperand lvalue, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.SHL, lvalue, left, right);
        }
        public static FlatStatement SHR(FlatOperand lvalue, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.SHR, lvalue, left, right);
        }
        public static FlatStatement NEGATE(FlatOperand lvalue, FlatOperand left)
        {
            return new FlatStatement(Instruction.NEGATE, lvalue, left);
        }
        public static FlatStatement JZ(FlatOperand label, FlatOperand left)
        {
            return new FlatStatement(Instruction.JZ, label, left);
        }
        public static FlatStatement JNZ(FlatOperand label, FlatOperand left)
        {
            return new FlatStatement(Instruction.JNZ, label, left);
        }
        public static FlatStatement JMP(FlatOperand label)
        {
            return new FlatStatement(Instruction.JMP, label);
        }
        public static FlatStatement JL(FlatOperand label, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.JL, label, left, right);
        }
        public static FlatStatement JLE(FlatOperand label, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.JLE, label, left, right);
        }
        public static FlatStatement JG(FlatOperand label, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.JG, label, left, right);
        }
        public static FlatStatement JGE(FlatOperand label, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.JGE, label, left, right);
        }
        public static FlatStatement JE(FlatOperand label, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.JE, label, left, right);
        }
        public static FlatStatement JNE(FlatOperand label, FlatOperand left, FlatOperand right)
        {
            return new FlatStatement(Instruction.JNE, label, left, right);
        }
        public static FlatStatement SWITCH(FlatOperand array_or_table, FlatOperand key)
        {
            return new FlatStatement(Instruction.SWITCH, array_or_table, key);
        }

        public static FlatStatement LABEL(FlatOperand label)
        {
            return new FlatStatement(Instruction.meta_LABEL, label);
        }

        /*
        public static FlatStatement FINALLY(FlatOperand label)
        {
            return new FlatStatement(Instruction.FINALLY, label);
        }
        /**/

        public static FlatStatement LEAVE()
        {
            return new FlatStatement(Instruction.LEAVE);
        }

        public static FlatStatement ENDFINALLY()
        {
            return new FlatStatement(Instruction.ENDFINALLY);
        }

        public static FlatStatement THROW(FlatOperand value)
        {
            return new FlatStatement(Instruction.THROW, value);
        }

        public static FlatStatement REFERENCE(FlatOperand lvalue, FlatOperand right)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.REFERENCE, lvalue, right);
        }

        public static FlatStatement DUPLICATE(FlatOperand lvalue, FlatOperand right)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.DUPLICATE, lvalue, right);
        }

        public static FlatStatement LEN(FlatOperand lvalue, FlatOperand right)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.LEN, lvalue, right);
        }

        public static FlatStatement TABLEGET(FlatOperand lvalue, FlatOperand table, FlatOperand key)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }

            return new FlatStatement(Instruction.TABLEGET, lvalue, table, key);
        }
        public static FlatStatement TABLESET(FlatOperand table, FlatOperand key, FlatOperand rvalue)
        {
            return new FlatStatement(Instruction.TABLESET, table, key, rvalue);
        }

        public static FlatStatement ARRAYGET(FlatOperand lvalue, FlatOperand array, FlatOperand index)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }

            return new FlatStatement(Instruction.ARRAYGET, lvalue, array, index);
        }
        public static FlatStatement ARRAYSET(FlatOperand array, FlatOperand index, FlatOperand rvalue)
        {
            return new FlatStatement(Instruction.ARRAYSET, array, index, rvalue);
        }

        public static FlatStatement GETPROPERTY(FlatOperand lvalue, FlatOperand property, FlatOperand subject)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }

            return new FlatStatement(Instruction.GETPROPERTY, lvalue, property,subject);
        }

        public static FlatStatement GETSTATICPROPERTY(FlatOperand lvalue,FlatOperand property)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }

            return new FlatStatement(Instruction.GETSTATICPROPERTY, lvalue, property);
        }

        public static FlatStatement SETPROPERTY(FlatOperand property, FlatOperand subject, FlatOperand rvalue)
        {
            return new FlatStatement(Instruction.SETPROPERTY, property, subject, rvalue);
        }

        public static FlatStatement SETSTATICPROPERTY(FlatOperand property, FlatOperand rvalue)
        {
            return new FlatStatement(Instruction.SETSTATICPROPERTY, property, rvalue);
        }
        public static FlatStatement GETFIELD(FlatOperand lvalue, FlatOperand field, FlatOperand subject)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }

            return new FlatStatement(Instruction.GETFIELD, lvalue, field, subject);
        }
        public static FlatStatement SETFIELD(FlatOperand field, FlatOperand subject, FlatOperand rvalue)
        {
            return new FlatStatement(Instruction.SETFIELD, field, subject, rvalue);
        }
        public static FlatStatement GETSTATICFIELD(FlatOperand lvalue, FlatOperand field_number)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }

            return new FlatStatement(Instruction.GETSTATICFIELD, lvalue, field_number);
        }
        public static FlatStatement SETSTATICFIELD(FlatOperand field, FlatOperand rvalue)
        {
            return new FlatStatement(Instruction.SETSTATICFIELD, field, rvalue);
        }
        public static FlatStatement LEQUAL(FlatOperand lvalue, FlatOperand left, FlatOperand right)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.LEQUAL, lvalue, left, right);
        }

        public static FlatStatement LLESS(FlatOperand lvalue, FlatOperand left, FlatOperand right)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.LLESS, lvalue, left, right);
        }

        public static FlatStatement FASTCALLSTATICMETHOD(FlatOperand method)
        {
            return new FlatStatement(Instruction.FASTCALLSTATICMETHOD, method);
        }

        public static FlatStatement FASTCALLSTATICMETHOD(FlatOperand method, FlatOperand input0)
        {
            return new FlatStatement(Instruction.FASTCALLSTATICMETHOD, method, input0);
        }

        public static FlatStatement FASTCALLSTATICMETHOD(FlatOperand method, FlatOperand input0, FlatOperand input1)
        {
            return new FlatStatement(Instruction.FASTCALLSTATICMETHOD, method, input0, input1);
        }

        public static FlatStatement CALLSTATICMETHOD(FlatOperand method, FlatOperand inputs_array)
        {
            return new FlatStatement(Instruction.CALLSTATICMETHOD, method, inputs_array);
        }

        public static FlatStatement FASTCALLMETHOD(FlatOperand method, FlatOperand subject)
        {
            return new FlatStatement(Instruction.FASTCALLMETHOD, method, subject);
        }

        public static FlatStatement CALLMETHOD(FlatOperand method, FlatOperand subject, FlatOperand inputs)
        {
            return new FlatStatement(Instruction.CALLMETHOD, method, subject, inputs);
        }

        public static FlatStatement FASTCALLMETHOD(FlatOperand method, FlatOperand subject, FlatOperand input0)
        {
            return new FlatStatement(Instruction.FASTCALLMETHOD, method, subject, input0);
        }

        public static FlatStatement RESOLVETYPE(FlatOperand lvalue, FlatOperand typename)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.RESOLVETYPE, lvalue, typename);
        }

        public static FlatStatement RESOLVEPROPERTY(FlatOperand lvalue, FlatOperand type, FlatOperand propertyname)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.RESOLVEPROPERTY, lvalue, type, propertyname);
        }

        public static FlatStatement RESOLVEFIELD(FlatOperand lvalue, FlatOperand type, FlatOperand fieldnum)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.RESOLVEFIELD, lvalue, type, fieldnum);
        }
        public static FlatStatement RESOLVESTATICFIELD(FlatOperand lvalue, FlatOperand type, FlatOperand fieldname)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.RESOLVESTATICFIELD, lvalue, type, fieldname);
        }
        public static FlatStatement RESOLVESTATICMETHOD(FlatOperand lvalue, FlatOperand type, FlatOperand methodname)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.RESOLVESTATICMETHOD, lvalue, type, methodname);
        }
        public static FlatStatement RESOLVESTATICPROPERTY(FlatOperand lvalue, FlatOperand type, FlatOperand propertyname)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.RESOLVESTATICPROPERTY, lvalue, type, propertyname);
        }
        public static FlatStatement RESOLVEMETHOD(FlatOperand lvalue, FlatOperand type, FlatOperand methodname)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.RESOLVEMETHOD, lvalue, type, methodname);
        }
        public static FlatStatement NEWARRAY(FlatOperand lvalue, FlatOperand size, FlatOperand fop_type)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.NEWARRAY, lvalue, size, fop_type);
        }

        public static FlatStatement NEWDELEGATE(FlatOperand lvalue, FlatOperand type, FlatOperand array_with_method_and_optional_type)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.NEWDELEGATE, lvalue, type, array_with_method_and_optional_type);
        }

        public static FlatStatement NEWOBJECT(FlatOperand lvalue, FlatOperand constructor, FlatOperand arguments)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.NEWOBJECT, lvalue, constructor, arguments);
        }
        public static FlatStatement REREFERENCE(FlatOperand lvalue, FlatOperand right)
        {
            /*
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            /**/
            return new FlatStatement(Instruction.REREFERENCE, lvalue, right);
        }
        public static FlatStatement DEREFERENCE(FlatOperand lvalue, FlatOperand right)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.DEREFERENCE, lvalue, right);
        }

        public static FlatStatement STRINGVAL(FlatOperand lvalue, FlatOperand right)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.STRINGVAL, lvalue, right);
        }

        public static FlatStatement GETMETATABLE(FlatOperand lvalue, FlatOperand right)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.GETMETATABLE, lvalue, right);
        }

        public static FlatStatement TYPEOF(FlatOperand lvalue, FlatOperand right)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.TYPEOF, lvalue, right);
        }

        public static FlatStatement RETURN()
        {
            return new FlatStatement(Instruction.RETURN);
        }

        public static FlatStatement TRY(FlatOperand ehbeginLabel, string ehendLabel)
        {
            return new FlatStatement(Instruction.TRY, ehbeginLabel) { Comment = ehendLabel };
        }

        public static FlatStatement TRY(FlatOperand ehbeginLabel, FlatOperand finallyLabel)
        {
            return new FlatStatement(Instruction.TRY, ehbeginLabel, finallyLabel);
        }

        /*
        public static FlatStatement GETEXCEPTIONHANDLER(FlatOperand lvalue)
        {
            if (lvalue.OperandType != FlatOperandType.OPND_IMMEDIATE)
            {
                throw new NotSupportedException("expected register number (LValue) in left");
            }
            return new FlatStatement(Instruction.GETEXCEPTIONHANDLER, lvalue);
        }

        public static FlatStatement SETEXCEPTIONHANDLER(FlatOperand label)
        {
            return new FlatStatement(Instruction.SETEXCEPTIONHANDLER, label);
        }
        /**/

        public override string ToString()
        {
            return Emit();
        }

        public string Emit()
        {
            string val = string.Empty;

            if (Instruction == LS2IL.Instruction.meta_LABEL)
            {
                // a commented NOP will hold our place
                return /*"NOP ; "+*/ Operands[0].ToString() + ":";
            }

            val += Instruction.ToString();

            if (Operands != null)
            {
                int nOperands = Operands.Count;

                if (nOperands >= 1)
                {
                    val += " "+Operands[0].ToString();
                }
                if (nOperands >= 2)
                {
                    val += ", " + Operands[1].ToString();
                }
                if (nOperands >= 3)
                {
                    val += ", " + Operands[2].ToString();
                }
            }

            if (!string.IsNullOrEmpty(Comment))
            {
                val += " ; " + Comment;
            }

            return val;
        }
    }

    enum Instruction
    {
        NOP,

        NULLIFY,
        DUPLICATE,
        REFERENCE,

        // reference stuff
        REREFERENCE,
        DEREFERENCE,

        TYPEOF,		// type
        BOOLVAL,		// 
        INTVAL,		// 
        FLOATVAL,
        STRINGVAL,
        BINARYVAL,

        IS,
        AS,

        GETMETATABLE,

        // unused stuff
        //		ASSIGN,	 // assigns a new value to the LS2CodeBoxValue...

        // number stuff
        ADD,
        SUB,
        MUL,
        DIV,
        MOD,
        POW,
        NEGATE, // change - to + or + to -

        // logical stuff
        LLESS,
        LLESSEQUAL,
        LGREATER,
        LGREATEREQUAL,
        LEQUAL,
        LNOTEQUAL,

        LAND,
        LOR,
        LNOT,

        // bitwise stuff
        SHL,
        SHR,
        AND,
        OR,
        NOT,
        XOR,

        // string stuff
        LEN,
        //CONCAT, // implicit with ADD

        // table stuff <string key>
        NEWTABLE,
        TABLESET,
        TABLEGET,

        // array stuff <integer key>
        NEWARRAY,
        ARRAYSET,
        ARRAYGET,
        ARRAYRESIZE,
        ARRAYCONCAT,

        // delegate stuff
        NEWDELEGATE,

        // object stuff
        NEWOBJECT,
        //RESOLVEOBJECT,
        RESOLVETYPE,

        RESOLVEMETHOD,
        RESOLVESTATICMETHOD,
        RESOLVEFIELD,
        RESOLVESTATICFIELD,
        RESOLVEPROPERTY,
        RESOLVESTATICPROPERTY,
        CALLMETHOD,
        CALLSTATICMETHOD,
        FASTCALLMETHOD,
        FASTCALLSTATICMETHOD,

        GETPROPERTY,
        SETPROPERTY,
        GETSTATICPROPERTY,
        SETSTATICPROPERTY,

        GETFIELD,
        GETSTATICFIELD,
        SETFIELD,
        SETSTATICFIELD,

        //		SETMETADATA,
        //		GETMETADATA,

        // control flow
        JMP,
        JE,
        JNE,
        JGE,
        JG,
        JL,
        JLE,
        JZ,
        JNZ,
        SWITCH,

        RETURN,
        CALL,
        FASTCALL,

        // processor flow
        YIELD,
        ATOMIZE,
        DEATOMIZE,

        // exceptions
        THROW,
        UNTHROW,
        //SETEXCEPTIONHANDLER,
        //GETEXCEPTIONHANDLER,
        TRY, // <catches>,<finally>  enter exception-protected region
        //FINALLY, // FINALLY <range>, <instruction #>
        LEAVE, // may jump to specified FINALLY. 0 to specfify not to pop exception handler, 1 to specify pop exception handler.
        ENDFINALLY, // jumps to a remembered instruction #

        // fake instructions
        meta_LABEL,
    }

}