using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LS2IL
{
    class RegisterLiveness
    {
        /// <summary>
        /// Creates a RegisterLiveness for an input register, starting at a given instruction
        /// </summary>
        /// <param name="nRegister"></param>
        /// <param name="at_instruction"></param>
        public RegisterLiveness(int nRegister, int at_instruction)
        {
            NumRegister = nRegister;
            FromInstruction = at_instruction;
            ToInstruction = at_instruction;
        }

        /// <summary>
        /// Creates a RegisterLiveness for an output register
        /// </summary>
        /// <param name="nRegister"></param>
        /// <param name="transformed_from"></param>
        public RegisterLiveness(int nRegister, RegisterLiveness transformed_from)
        {
            NumRegister = nRegister;
            Transformed = transformed_from;
        }

        /// <summary>
        /// Updates the liveness for this register, to include this instruction
        /// </summary>
        /// <param name="at_instruction"></param>
        public void Touch(int at_instruction)
        {
            if (at_instruction > ToInstruction)
                ToInstruction = at_instruction;
        }

        public int NumRegister { get; private set; }
        public int FromInstruction { get; private set; }
        public int ToInstruction { get; private set;  }

        RegisterLiveness _Transformed;
        /// <summary>
        /// If this is an input register, this will point to the output register
        /// </summary>
        public RegisterLiveness Transformed 
        {
            get { return _Transformed; }
            set 
            {
                _Transformed = value;
                if (value != null)
                {
                    if (_Transformed.ToInstruction < this.ToInstruction)
                        _Transformed.ToInstruction = this.ToInstruction;
                    if (_Transformed.FromInstruction > this.FromInstruction)
                        _Transformed.FromInstruction = this.FromInstruction;
                }
            }
        }

        public override string ToString()
        {
            return FromInstruction.ToString() + "-" + ToInstruction.ToString();
        }
    }

    /// <summary>
    /// Takes n input registers and fill up to 256 output registers
    /// This is a pretty simple and naive register packer, and it works well enough for our reference implementation...
    /// TODO: Add other implementations, command-line switch or something to pick between them. could even use a C# Attribute to be explicit per Function
    /// </summary>
    class RegisterPacker : IDisposable
    {

        /// <summary>
        /// Re-packs registers used by the given set of instructions. Return value is the new total number of registers.
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        public static int Pack(List<FlatStatement> instructions)
        {
            using (RegisterPacker rc = new RegisterPacker())
            {
                rc.ScanRanges(instructions);
                rc.Condense(instructions);

                return rc.OutputRegisters.Count;
            }
        }

#region private implementaion
        RegisterPacker()
        {
            for (int i = 255; i >= 0; i--)
            {
                AvailableRegisters.Push(i);
            }
        }

        Dictionary<int, RegisterLiveness> InputRegisters = new Dictionary<int, RegisterLiveness>();
        Dictionary<int, RegisterLiveness> OutputRegisters = new Dictionary<int, RegisterLiveness>();

        Stack<int> AvailableRegisters = new Stack<int>();

        /// <summary>
        /// The set of RegisterLiveness that are unavailable. We get to put some of them back into the Available stack when appropriate.
        /// </summary>
        List<RegisterLiveness> UnavailableRegisters = new List<RegisterLiveness>();

        /// <summary>
        /// Scans a list of instructions to generate the InputRegisters table with liveness info
        /// </summary>
        /// <param name="instructions"></param>
        void ScanRanges(List<FlatStatement> instructions)
        {
            int nInstruction = 0;
            foreach (FlatStatement fs in instructions)
            {
                if (fs.Operands != null)
                {

                    if (fs.Instruction.HasLValue())
                    {
                        if (fs.Instruction == Instruction.NULLIFY)
                        {
                            int from_reg = (int)fs.Operands[0].ImmediateValue.Object;
                            int to_reg_inclusive = (int)fs.Operands[1].ImmediateValue.Object;
                            // operands 0 and 1 are integer immediate values (l-value = register number)

                            if (from_reg != to_reg_inclusive)
                            {
                                // so far the compiler doesn't generate this instruction with a range anyway, but if that time comes, we will need to fix this
                                // ... possibly by spliting it into multiple NULLIFY statements
                                throw new NotImplementedException("NULLIFY range");
                            }
                            TouchRegister(InputRegisters, fs.Operands[0].OperandIndex, nInstruction);

                            /* 
                            // this is sort of all we need to do but ideally, we should start with individual NULLIFY statements and try to combine them where possible
                            for (int i = from_reg; i <= to_reg_inclusive; i++)
                            {
                                TouchRegister(InputRegisters,fs.Operands[i].OperandIndex, nInstruction);
                            }
                            */
                        }
                        else
                        {
                            if (fs.Instruction == Instruction.REREFERENCE)
                            {
                                // operand 0 MAY BE an integer immediate value (l-value = register number)
                                if (fs.Operands[0].OperandType == FlatOperandType.OPND_IMMEDIATE)
                                    TouchRegister(InputRegisters, (int)fs.Operands[0].ImmediateValue.Object, nInstruction);
                            }
                            else
                            {
                                // operand 0 is an integer immediate value (l-value = register number)
                                TouchRegister(InputRegisters, (int)fs.Operands[0].ImmediateValue.Object, nInstruction);
                            }

                            for (int i = 1; i < fs.Operands.Count; i++)
                            {
                                if (fs.Operands[i].OperandType == FlatOperandType.OPND_REGISTER_VALUEREF)
                                {
                                    TouchRegister(InputRegisters, fs.Operands[i].OperandIndex, nInstruction);
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < fs.Operands.Count; i++)
                        {
                            if (fs.Operands[i].OperandType == FlatOperandType.OPND_REGISTER_VALUEREF)
                            {
                                TouchRegister(InputRegisters, fs.Operands[i].OperandIndex, nInstruction);
                            }
                        }
                    }
                }

                nInstruction++;
            }
        }

        /// <summary>
        /// Update the liveness for a given register, to include this instruction
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="nRegister"></param>
        /// <param name="at_instruction"></param>
        /// <returns></returns>
        RegisterLiveness TouchRegister(Dictionary<int, RegisterLiveness> dict, int nRegister, int at_instruction)
        {
            RegisterLiveness rl;
            if (!dict.TryGetValue(nRegister, out rl))
            {
                rl = new RegisterLiveness(nRegister,at_instruction);
                dict[nRegister] = rl;
                return rl;
            }

            rl.Touch(at_instruction);
            return rl;
        }

        /// <summary>
        /// Performs the condense (packing) step, generating output registers and updating the instructions
        /// </summary>
        /// <param name="instructions"></param>
        void Condense(List<FlatStatement> instructions)
        {
            int nInstruction = 0;
            foreach (FlatStatement fs in instructions)
            {
                if (fs.Operands != null)
                {
                    if (fs.Instruction.HasLValue())
                    {
                        if (fs.Instruction == Instruction.NULLIFY)
                        {
                            int from_reg = (int)fs.Operands[0].ImmediateValue.Object;
                            int to_reg_inclusive = (int)fs.Operands[1].ImmediateValue.Object;
                            // operands 0 and 1 are integer immediate values (l-value = register number)
                            if (from_reg != to_reg_inclusive)
                            {
                                // so far the compiler doesn't generate this instruction with a range anyway, but if that time comes, we will need to fix this
                                // this should already be ruled out by the ScanRanges step
                                throw new NotImplementedException("NULLIFY range");
                            }

                            int nOutputRegister = GetOutputRegister(from_reg, nInstruction);
                            fs.Operands[0] = FlatOperand.LiteralInteger(nOutputRegister);
                            fs.Operands[1] = FlatOperand.LiteralInteger(nOutputRegister);
                        }
                        else
                        {
                            if (fs.Instruction == Instruction.REREFERENCE)
                            {
                                // operand 0 MAY BE an integer immediate value (l-value = register number)
                                if (fs.Operands[0].OperandType == FlatOperandType.OPND_IMMEDIATE)
                                {
                                    int nOutputRegister = GetOutputRegister((int)fs.Operands[0].ImmediateValue.Object, nInstruction);
                                    fs.Operands[0] = FlatOperand.LiteralInteger(nOutputRegister);
                                }
                                else if (fs.Operands[0].OperandType == FlatOperandType.OPND_REGISTER_VALUEREF)
                                {
                                    int nOutputRegister = GetOutputRegister(fs.Operands[0].OperandIndex, nInstruction);
                                    fs.Operands[0] = FlatOperand.RegisterRef(nOutputRegister, fs.Operands[0].ImmediateValue);
                                }
                            }
                            else
                            {
                                // operand 0 is an integer immediate value (l-value = register number)
                                int nOutputRegister = GetOutputRegister((int)fs.Operands[0].ImmediateValue.Object, nInstruction);
                                fs.Operands[0] = FlatOperand.LiteralInteger(nOutputRegister);
                            }

                            for (int i = 1; i < fs.Operands.Count; i++)
                            {
                                if (fs.Operands[i].OperandType == FlatOperandType.OPND_REGISTER_VALUEREF)
                                {
                                    int nOutputRegister = GetOutputRegister(fs.Operands[i].OperandIndex, nInstruction);
                                    fs.Operands[i] = FlatOperand.RegisterRef(nOutputRegister, fs.Operands[i].ImmediateValue);
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < fs.Operands.Count; i++)
                        {
                            if (fs.Operands[i].OperandType == FlatOperandType.OPND_REGISTER_VALUEREF)
                            {
                                int nOutputRegister = GetOutputRegister(fs.Operands[i].OperandIndex, nInstruction);
                                fs.Operands[i] = FlatOperand.RegisterRef(nOutputRegister, fs.Operands[i].ImmediateValue);
                            }
                        }
                    }
                }
                nInstruction++;
                ReleaseNewlyAvailableRegisters(nInstruction);
            }
        }

        /// <summary>
        /// Releases registers back into the available pool when they are no longer live
        /// </summary>
        /// <param name="nInstruction"></param>
        void ReleaseNewlyAvailableRegisters(int nInstruction)
        {
            List<RegisterLiveness> newList = new List<RegisterLiveness>();
            foreach (RegisterLiveness unav in UnavailableRegisters)
            {
                if (nInstruction > unav.ToInstruction)
                {
                    AvailableRegisters.Push(unav.NumRegister);
                }
                else
                    newList.Add(unav);
            }
            UnavailableRegisters = newList;
        }

        /// <summary>
        /// Selects an output register number, given an input register number
        /// An improved implementation might be more picky about the selection, this one grabs the next available off a stack.
        /// </summary>
        /// <param name="nInputRegister"></param>
        /// <param name="nInstruction"></param>
        /// <returns></returns>
        int GetOutputRegister(int nInputRegister, int nInstruction)
        {
            // see if it's already transformed
            RegisterLiveness input = InputRegisters[nInputRegister];
            RegisterLiveness transformed = input.Transformed;
            if (transformed != null)
                return transformed.NumRegister;

            // not already transformed, pick an available register
            if (AvailableRegisters.Count == 0)
            {
                throw new NotImplementedException("Out of registers!");
            }
            int nOutputRegister = AvailableRegisters.Pop();

            // are we reusing a register?
            if (!OutputRegisters.TryGetValue(nOutputRegister, out transformed))
            {
                transformed = new RegisterLiveness(nOutputRegister, input);
                OutputRegisters[nOutputRegister] = transformed;
            }

            input.Transformed = transformed;
            UnavailableRegisters.Add(transformed);
            return nOutputRegister;
        }        
        public void Dispose()
        {
            InputRegisters.Clear();
            InputRegisters = null;
            OutputRegisters.Clear();
            OutputRegisters = null;
            UnavailableRegisters.Clear();
            UnavailableRegisters = null;
            AvailableRegisters.Clear();
            AvailableRegisters = null;
        }
#endregion
    }
}
