using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LS2IL
{
    class DefaultRegisterPacker : IRegisterPacker
    {
        public DefaultRegisterPacker(ControlFlowGraph cfg)
        {
            CFG = cfg;
        }

        void ResetRegisters()
        {
            NumOutputRegisters = 0;

            AvailableRegisters = new Stack<int>();
            UnavailableRegisters = new Dictionary<int, int>();
            
            RegisterTransforms = new Dictionary<int, int>();
            for (int i = 255; i >= 0; i--)
            {
                AvailableRegisters.Push(i);
            }
        }

        public ControlFlowGraph CFG { get; private set; }


        Stack<int> AvailableRegisters = new Stack<int>();
        /// <summary>
        /// The set of RegisterLiveness that are unavailable. We get to put some of them back into the Available stack when appropriate.
        /// </summary>
        Dictionary<int, int> UnavailableRegisters = new Dictionary<int, int>();

        Dictionary<int, int> RegisterTransforms = new Dictionary<int, int>();

        public int NumOutputRegisters { get; private set; }

        class NaiveRegisterPacker : IDisposable
        {
            public NaiveRegisterPacker(BasicBlock bb, DefaultRegisterPacker derp)
            {
                BasicBlock = bb;
                Derp = derp;
            }

            public BasicBlock BasicBlock { get; private set; }
            public DefaultRegisterPacker Derp { get; private set; }
            Dictionary<int, RegisterLiveness> InputRegisters = new Dictionary<int, RegisterLiveness>();
            Dictionary<int, RegisterLiveness> OutputRegisters = new Dictionary<int, RegisterLiveness>();

            public void HeadRegisters(List<int> registers, List<FlatStatement> instructions)
            {
                foreach (int r in registers)
                {
                    TouchRegister(InputRegisters, r, -1);
                    //int nOutputRegister = Derp.GetOutputRegister(r,this);
                    this.GetOutputRegister(r, -1);
                }
            }

            public void TailRegisters(List<int> registers, List<FlatStatement> instructions)
            {
                foreach (int r in registers)
                {
                    TouchRegister(InputRegisters, r, instructions.Count+1);
                }
            }

            /// <summary>
            /// Scans a list of instructions to generate the InputRegisters table with liveness info
            /// </summary>
            /// <param name="instructions"></param>
            public void ScanRanges(List<FlatStatement> instructions)
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
                                TouchRegister(InputRegisters, from_reg, nInstruction);

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
                    rl = new RegisterLiveness(nRegister, at_instruction);
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
            public void Condense(List<FlatStatement> instructions)
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
                List<int> futureReads = null;
                Dictionary<int, int> newList = new Dictionary<int,int>();
                foreach (KeyValuePair<int,int> kvp in Derp.UnavailableRegisters)
                {
                    int nOutputRegister = kvp.Key;
                    RegisterLiveness unav;
                    if (OutputRegisters.TryGetValue(nOutputRegister, out unav))
                    {
                        if (nInstruction > unav.ToInstruction)
                            Derp.AvailableRegisters.Push(nOutputRegister);
                        else
                            newList.Add(kvp.Key, kvp.Value);
                    }
                    else
                    {
                        int nInputRegister = kvp.Value;

                        RegisterLiveness irl;
                        if (InputRegisters.TryGetValue(nInputRegister, out irl))
                        {
                            if (nInstruction > irl.ToInstruction)
                                Derp.AvailableRegisters.Push(nOutputRegister);
                            else
                                newList.Add(kvp.Key, kvp.Value);
                        }
                        else
                        {
                            if (futureReads==null)
                                futureReads = this.BasicBlock.GetRegisterFutureReads();


                            if (futureReads.Contains(nInputRegister))
                            {
                                // this will probably never happen because of the Head/Tail methods
                                // stay unavailable
                                newList.Add(kvp.Key, kvp.Value);
                            }
                            else
                            {
                                Derp.AvailableRegisters.Push(nOutputRegister);
                            }
                        }
                    }
                }
                Derp.UnavailableRegisters = newList;
            }

            public bool AllowRegisterTransform(int nInputRegister, int nOutputRegister)
            {
                RegisterLiveness irl = InputRegisters[nInputRegister];
                if (irl.Transformed != null)
                    return irl.Transformed.NumRegister == nOutputRegister;

                RegisterLiveness rl;
                if (OutputRegisters.TryGetValue(nOutputRegister, out rl))
                {
                    return !rl.Intersects(irl);
                }

                return true;
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
                RegisterLiveness irl = InputRegisters[nInputRegister];
                if (irl.Transformed != null)
                    return irl.Transformed.NumRegister;

                int nOutputRegister = Derp.GetOutputRegister(nInputRegister,this);
                RegisterLiveness rl;
                if (OutputRegisters.TryGetValue(nOutputRegister, out rl))
                {
                    rl.Touch(nInstruction);
                    irl.Transformed = rl;

                    rl.TransformedFrom.Add(irl);
                }
                else
                {
                    rl = new RegisterLiveness(nOutputRegister, irl);
                    rl.Touch(nInstruction);
                    OutputRegisters.Add(nOutputRegister, rl);
                }
                return nOutputRegister;
            }
            public void Dispose()
            {
                InputRegisters.Clear();
                InputRegisters = null;
                OutputRegisters.Clear();
                OutputRegisters = null;
//                UnavailableRegisters.Clear();
//                UnavailableRegisters = null;
//                AvailableRegisters.Clear();
//                AvailableRegisters = null;
            }
        }

        /// <summary>
        /// Selects an output register number, given an input register number
        /// An improved implementation might be more picky about the selection, this one grabs the next available off a stack.
        /// </summary>
        /// <param name="nInputRegister"></param>
        /// <param name="nInstruction"></param>
        /// <returns></returns>
        int GetOutputRegister(int nInputRegister, NaiveRegisterPacker narp)
        {
            // see if it's already transformed
            int nOutputRegister = -1;
            if (RegisterTransforms.TryGetValue(nInputRegister, out nOutputRegister))
            {
                return nOutputRegister;
            }

            Stack<int> Rejects = new Stack<int>();

            do
            {


                // it's not.
                if (AvailableRegisters.Count == 0)
                {
                    throw new NotImplementedException("Out of registers!");
                }

                nOutputRegister = AvailableRegisters.Pop();
                if (narp.AllowRegisterTransform(nInputRegister, nOutputRegister))
                {
                    break;
                }

                Rejects.Push(nOutputRegister);
            }
            while (true);

            while (Rejects.Count > 0)
            {
                AvailableRegisters.Push(Rejects.Peek());
                Rejects.Pop();
            }

            UnavailableRegisters.Add(nOutputRegister, nInputRegister);
            RegisterTransforms.Add(nInputRegister, nOutputRegister);

            if (nOutputRegister >= NumOutputRegisters)
                NumOutputRegisters = nOutputRegister + 1;

            return nOutputRegister;
        }

        void PackRegisters(BasicBlock bb)
        {

            NaiveRegisterPacker nrp = new NaiveRegisterPacker(bb,this);

            List<int> registerPastWrites = bb.GetRegisterPastWrites();
            List<int> registerFutureReads = bb.GetRegisterFutureReads();

            
            {
                List<int> listReserved = new List<int>();


                // listReserved is the intersection of RegisterPastWrites with RegisterReads OR RegisterFutureReads
                foreach (int n in registerPastWrites)
                {
                    if (bb.RegisterReads.Contains(n) || registerFutureReads.Contains(n))
                        listReserved.Add(n);
                }                                
                if (listReserved.Count>0)
                    nrp.HeadRegisters(listReserved, bb.Instructions);
            }
            
            nrp.ScanRanges(bb.Instructions);

            {
                List<int> listReserved = new List<int>();

                // listReserved is the intersection of RegisterFutureReads with RegisterWrites OR RegisterPastWrites
                foreach (int n in registerFutureReads)
                {
                    if (bb.RegisterWrites.Contains(n) || registerPastWrites.Contains(n))
                        listReserved.Add(n);
                }

                if (listReserved.Count > 0)
                    nrp.TailRegisters(listReserved, bb.Instructions);
            }

            nrp.Condense(bb.Instructions);
        }

        public int PackRegisters()
        {
            ResetRegisters();

            foreach (BasicBlock bb in CFG.BasicBlocks)
            {
                PackRegisters(bb);
            }
            return NumOutputRegisters;
        }
    }
}
