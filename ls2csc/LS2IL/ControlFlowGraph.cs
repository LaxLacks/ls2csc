using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LS2IL
{
    /// <summary>
    /// An LS2IL graph of basic blocks, labels, data flow, etc
    /// </summary>
    class ControlFlowGraph : IDisposable
    {
        public ControlFlowGraph(Function f, List<FlatStatement> instructions)
        {
            Function = f;
            InputInstructions = instructions;
            Labels = new Dictionary<string, BasicBlock>(); //new Dictionary<string, int>();
            BasicBlocks = new List<BasicBlock>();
        }

        /// <summary>
        /// Labels resolved to basic blocks, for locating successors and predecessors
        /// </summary>
        public Dictionary<string, BasicBlock> Labels { get; private set; }

        /// <summary>
        /// Function being graphed
        /// </summary>
        public Function Function { get; private set; }

        /// <summary>
        /// Set of instructions as given to the graph. This may get transformed to output instructions.
        /// </summary>
        public List<FlatStatement> InputInstructions { get; private set; }

        /// <summary>
        /// The basic blocks, hey?
        /// </summary>
        public List<BasicBlock> BasicBlocks { get; private set; }

        /// <summary>
        /// Current basic block during generation
        /// </summary>
        BasicBlock CurrentBasicBlock;
        /// <summary>
        /// Current exception-handling state during generation
        /// </summary>
        EHInfo EHState;

        /// <summary>
        /// Given an input instruction number, retrieves the appropriate BasicBlock containing it
        /// </summary>
        /// <param name="at_instruction"></param>
        /// <returns></returns>
        public BasicBlock GetBlockFromInstruction(int at_instruction)
        {
            foreach (BasicBlock bb in BasicBlocks)
            {
                if (bb.Includes(at_instruction))
                    return bb;
            }
            return null;
        }
        public void Dispose()
        {
            if (BasicBlocks != null)
            {
                foreach (BasicBlock bb in BasicBlocks)
                {
                    bb.Dispose();
                }
                BasicBlocks.Clear();
                BasicBlocks = null;
            }
            if (Labels != null)
            {
                Labels.Clear();
                Labels = null;
            }
        }

        /// <summary>
        /// Adds a normal instruction to the graph (new BasicBlock only if we're not already in one)
        /// </summary>
        /// <param name="nInstruction"></param>
        /// <param name="ehinfo"></param>
        void NormalInstruction(int nInstruction, EHInfo ehinfo)
        {
            if (CurrentBasicBlock == null)
            {
                HeadInstruction(nInstruction, ehinfo);
            }
            else
                CurrentBasicBlock.Touch(nInstruction);
        }

        /// <summary>
        /// Adds a BasicBlock HEAD instruction to the graph (always a new BasicBlock)
        /// </summary>
        /// <param name="nInstruction"></param>
        /// <param name="ehinfo"></param>
        void HeadInstruction(int nInstruction, EHInfo ehinfo)
        {
            CurrentBasicBlock = new BasicBlock(this, BasicBlocks.Count, nInstruction, ehinfo);
            BasicBlocks.Add(CurrentBasicBlock);
        }

        /// <summary>
        /// Adds a BasicBlock TAIL instruction to the graph (terminates the current BasicBlock after adding this instruction)
        /// </summary>
        /// <param name="nInstruction"></param>
        /// <param name="ehinfo"></param>
        void TailInstruction(int nInstruction, EHInfo ehinfo)
        {
            NormalInstruction(nInstruction, ehinfo);
            CurrentBasicBlock = null;
        }

        /// <summary>
        /// When hitting a new TRY instruction, this provides stacking behavior with the current exception handling
        /// </summary>
        /// <param name="nInstruction"></param>
        /// <param name="tryInstruction"></param>
        void PushFinallyState(int nInstruction, FlatStatement tryInstruction)
        {
            EHInfo CurrentState = EHState;
            if (tryInstruction.Operands.Count == 1)
            {
                EHState = new EHInfo(CurrentState, nInstruction,tryInstruction.Operands[0], tryInstruction.Comment);
            }
            else
                EHState = new EHInfo(CurrentState, nInstruction, tryInstruction.Operands[0], tryInstruction.Operands[1]);
        }

        /// <summary>
        /// This closes out an exception handler on the stack
        /// </summary>
        void PopFinallyState()
        {
            EHState = EHState.PreviousState;
        }

        /// <summary>
        /// Perform initial splitting into Basic Blocks and Exception Handler state
        /// </summary>
        void BuildBasicBlocks()
        {
            for (int i = 0; i < InputInstructions.Count; i++)
            {
                FlatStatement fs = InputInstructions[i];

                switch (fs.Instruction)
                {
                    case Instruction.meta_LABEL:
                        // remember label
                        {
                            string labelName = fs.Operands[0].ImmediateValue.ValueText;

                            if (labelName.StartsWith("__try") && labelName.Contains("begin"))
                            {
                                EHState.EHPart = EHPart.Catch;
                            }
                            else if (labelName.Contains("finally"))
                            {
                                EHState.EHPart = EHPart.Finally;
                            }
                            else if (EHState != null && !string.IsNullOrEmpty(EHState.ehEndLabel) && labelName == EHState.ehEndLabel)
                            {
                                PopFinallyState();
                            }

                            // begin basic block
                            HeadInstruction(i, EHState);
                            Labels.Add(labelName, CurrentBasicBlock);
                        }
                        break;
                    case Instruction.JMP:
                        TailInstruction(i, EHState);
                        break;
                    case Instruction.JE:
                    case Instruction.JG:
                    case Instruction.JGE:
                    case Instruction.JL:
                    case Instruction.JLE:
                    case Instruction.JNE:
                    case Instruction.JNZ:
                    case Instruction.JZ:
                        TailInstruction(i, EHState);
                        break;
                    case Instruction.THROW:
                        TailInstruction(i, EHState);
                        break;
                    case Instruction.SWITCH:
                        TailInstruction(i, EHState);
                        break;
                    case Instruction.RETURN:                        
                        TailInstruction(i, EHState);
                        break;
                    case Instruction.LEAVE:
                        // end basic block; exiting to do finally->endfinally. LEAVE should not exist yet at this point unless we are re-graphing...
                        throw new NotSupportedException("LEAVE");
                        break;
                    case Instruction.ENDFINALLY:
                        // end basic block; jumping back to LEAVE+1
                        PopFinallyState();
                        TailInstruction(i, EHState);
                        break;
                    case Instruction.TRY:
                        // begin basic block, our basic blocks hold exception handler state information      
                        PushFinallyState(i, fs);
                        HeadInstruction(i, EHState);
                        break;
                    default:
                        NormalInstruction(i, EHState);
                        break;
                }
            }
        }

        /// <summary>
        /// Builds the graph of BasicBlocks and all successors and predecessors
        /// </summary>
        public void Build(bool removeUnusedBlocks)
        {           
            // first, build the basic blocks.
            BuildBasicBlocks();

            // now inject LEAVEs (creates additional blocks)
            InjectLEAVE();

            // Third, identify and mark all successor blocks
            GraphSuccessors();

            // Pare the graph of any BasicBlock with no Predecessors (and is also not the first BasicBlock)
            if (removeUnusedBlocks)
            {
                PareGraph();
            }
        }

        /// <summary>
        /// Ensures the List keys and the InputBlockNum field match, done when when we inject a block
        /// </summary>
        /// <param name="from_block"></param>
        void UpdateBasicBlockNumbers(int from_block)
        {
            for (int i = from_block; i < BasicBlocks.Count; i++)
            {
                BasicBlock bb = BasicBlocks[i];
                bb.InputBlockNum = i;
            }
        }

        /// <summary>
        /// Scan the Exception Handler structures and inject LEAVE instructions where appropriate (generating a new BasicBlock)
        /// </summary>
        void InjectLEAVE()
        {
            for (int i = 0; i < BasicBlocks.Count; i++)
            {
                BasicBlock bb = BasicBlocks[i];
                if (bb.EHInfo == null || bb.EHInfo.EHPart == EHPart.None)
                    continue;


                FlatStatement fs = bb.GetExitInstruction();
                switch (fs.Instruction)
                {
                    case Instruction.THROW:
                        {
                            if (bb.EHInfo != null && bb.EHInfo.EHPart == EHPart.Catch)
                            {
                                //throw new NotImplementedException("inject a new BasicBlock");

                                BasicBlocks.Insert(i+1, new BasicBlock(this, i+1, bb.ToInstruction, bb.EHInfo.PreviousState));
                                UpdateBasicBlockNumbers(i+2);

                                bb.Instructions[bb.Instructions.Count - 1] = FlatStatement.LEAVE();
                            }
                        }
                        break;
                    case Instruction.RETURN:
                        {
                            if (bb.EHInfo != null)
                            {
                                BasicBlocks.Insert(i+1, new BasicBlock(this, i+1, bb.ToInstruction, bb.EHInfo.PreviousState));
                                UpdateBasicBlockNumbers(i+2);
                                bb.Instructions[bb.Instructions.Count - 1] = FlatStatement.LEAVE();
                                break;
                            }
                        }
                        break;
                    case Instruction.SWITCH:
                        {
                            //   throw new NotImplementedException("Control Flow Graph: Switch");
                            // well, for now i hope switches dont leave the exception handler block... (it's illegal in C# syntax, but if we're re-graphing LS2IL...)
                        }
                        break;
                    case Instruction.JMP:
                        {
                            BasicBlock targetBlock = this.Labels[fs.Operands[0].ImmediateValue.ValueText];

                            if (targetBlock.EHInfo == null || targetBlock.EHInfo.EHPart == EHPart.None)
                            {
                                // target has no exception handler, so it must be outside of ours. leave and move on.
                                if (bb.EHInfo != null)
                                {
                                    BasicBlocks.Insert(i+1, new BasicBlock(this, i+1, bb.ToInstruction, bb.EHInfo.PreviousState));
                                    UpdateBasicBlockNumbers(i+2);
                                    bb.Instructions[bb.Instructions.Count - 1] = FlatStatement.LEAVE();
                                    break;
                                }
                            }

                            // locate target's exception handler in our EH stack
                            int targetNum = targetBlock.EHInfo.NumTryInstruction;

                            EHInfo ehInfo = bb.EHInfo;
                            if (ehInfo.NumTryInstruction == targetNum)
                            {
                                // same exception handler, do not need to inject LEAVE
                                break; 
                            }
                            
                            ehInfo = ehInfo.PreviousState;

                            bool bFound = false;
                            while (ehInfo != null)
                            {
                                if (ehInfo.NumTryInstruction == targetNum)
                                {
                                    // we have a path to the right exception handler via LEAVE, let's go...
                                    BasicBlocks.Insert(i+1, new BasicBlock(this, i+1, bb.ToInstruction, bb.EHInfo.PreviousState));
                                    UpdateBasicBlockNumbers(i+2);
                                    bb.Instructions[bb.Instructions.Count - 1] = FlatStatement.LEAVE();
                                    bFound = true;
                                    break;
                                }
                           
                                ehInfo = ehInfo.PreviousState;
                            }

                            if (bFound)
                                break;

                            throw new NotImplementedException("Control Flow Graph: inject LEAVE for JMP");
                        }
                        break;
                    case Instruction.JE:
                    case Instruction.JG:
                    case Instruction.JGE:
                    case Instruction.JL:
                    case Instruction.JLE:
                    case Instruction.JNE:
                    case Instruction.JNZ:
                    case Instruction.JZ:
                        {
                            BasicBlock targetBlock = this.Labels[fs.Operands[0].ImmediateValue.ValueText];
                            if (targetBlock.EHInfo.NumTryInstruction == bb.EHInfo.NumTryInstruction)
                                continue;


                            throw new NotImplementedException("Control Flow Graph: inject LEAVE for Jcc");
                        }
                        break;
                }
            }
        }

        void GraphSuccessors()
        {
            for (int i = 0; i < BasicBlocks.Count; i++)
            {
                BasicBlock bb = BasicBlocks[i];
                bb.GraphSuccessors();
            }
        }

        /// <summary>
        /// Removes any node that has no Predecessors (thereby paring the graph), except for the first node (which may not have one because it's the entry point)
        /// </summary>
        void PareGraph()
        {
            // remove any node besides the first one, with no Predecessor
            bool bChanged;
            do
            {
                bChanged = false;
                for (int i = 1; i < BasicBlocks.Count; i++)
                {
                    BasicBlock bb = BasicBlocks[i];

                    while (bb.Predecessors == null || bb.Predecessors.Count == 0)
                    {
                        bChanged = true;
                        bb.StripUnusedBlock();

                        bb.Dispose();
                        BasicBlocks.RemoveAt(i);

                        if (BasicBlocks.Count <= i)
                            break;
                        bb = BasicBlocks[i];
                    }
                }
                UpdateBasicBlockNumbers(1);
            }
            while (bChanged);
        }

        /// <summary>
        /// Scans and records register read/writes
        /// </summary>
        public void ScanRegisters()
        {
            for (int i = 0; i < BasicBlocks.Count; i++)
            {
                BasicBlock bb = BasicBlocks[i];
                bb.ResetRegisterScan();
            }

            for (int i = 0; i < BasicBlocks.Count; i++)
            {
                BasicBlock bb = BasicBlocks[i];
                bb.ScanRegisters();
            }
        }

        /// <summary>
        /// Retrieve a list of registers READ by a set of BasicBlocks
        /// </summary>
        /// <param name="listBlocks"></param>
        /// <returns></returns>
        public List<int> GetRegisterReads(List<BasicBlock> listBlocks)
        {
            List<int> list = new List<int>();
            foreach (BasicBlock bb in listBlocks)
            {
                if (bb == null)
                    continue;
                foreach (int nInputRegister in bb.RegisterReads)
                {
                    if (!list.Contains(nInputRegister))
                        list.Add(nInputRegister);
                }
            }
            return list;
        }

        /// <summary>
        /// Retrieve a list of registers WRITTEN by a set of BasicBlocks
        /// </summary>
        /// <param name="listBlocks"></param>
        /// <returns></returns>
        public List<int> GetRegisterWrites(List<BasicBlock> listBlocks)
        {
            List<int> list = new List<int>();
            foreach (BasicBlock bb in listBlocks)
            {
                if (bb == null)
                    continue;

                foreach (int nInputRegister in bb.RegisterWrites)
                {
                    if (!list.Contains(nInputRegister))
                        list.Add(nInputRegister);
                }
            }
            return list;
        }

        /// <summary>
        /// Get a list of all BasicBlocks that READ a given input register
        /// </summary>
        /// <param name="nInputRegister"></param>
        /// <returns></returns>
        public List<BasicBlock> GetRegisterReads(int nInputRegister)
        {
            List<BasicBlock> list = new List<BasicBlock>();
            foreach (BasicBlock bb in BasicBlocks)
            {
                if (bb == null)
                    continue;
                if (bb.RegisterReads.Contains(nInputRegister))
                    list.Add(bb);
            }
            return list;
        }

        /// <summary>
        /// Get a list of all BasicBlocks that WRITE a given input register
        /// </summary>
        /// <param name="nInputRegister"></param>
        /// <returns></returns>
        public List<BasicBlock> GetRegisterWrites(int nInputRegister)
        {
            List<BasicBlock> list = new List<BasicBlock>();
            foreach (BasicBlock bb in BasicBlocks)
            {
                if (bb == null)
                    continue;
                if (bb.RegisterWrites.Contains(nInputRegister))
                    list.Add(bb);
            }
            return list;
        }

        /// <summary>
        /// Flatten the graph back into FlatStatements
        /// </summary>
        /// <returns></returns>
        public List<FlatStatement> Flatten()
        {
            List<FlatStatement> list = new List<FlatStatement>();

            // OPTIMIZATION TODO: blocks can potentially be re-ordered to reduce jumps, etc.
            foreach (BasicBlock bb in BasicBlocks)
            {
                foreach (FlatStatement fs in bb.Instructions)
                {
                    list.Add(fs);
                }
            }

            return list;
        }

    }

    public enum EHPart
    {
        None,
        Try,
        Catch,
        Finally,
    }


    class EHInfo
    {
        public EHInfo()
        {
            EHPart = LS2IL.EHPart.None;
            NumTryInstruction = -1;
        }

        public EHInfo(EHInfo copyfrom)
        {
            if (copyfrom == null)
            {
                EHPart = LS2IL.EHPart.None;
                NumTryInstruction = -1;
                return;
            }

            EHPart = copyfrom.EHPart;
            CatchesLabel = copyfrom.CatchesLabel;
            ehEndLabel = copyfrom.ehEndLabel;
            FinallyLabel = copyfrom.FinallyLabel;
            PreviousState = copyfrom.PreviousState;
            NumTryInstruction = copyfrom.NumTryInstruction;
        }

        public EHInfo(EHInfo oldState,int numTryInstruction, FlatOperand catchesLabel, string ehendLabel)
        {
            CatchesLabel = catchesLabel.ImmediateValue.ValueText;
            ehEndLabel = ehendLabel;
            PreviousState = oldState;
            EHPart = EHPart.Try;
            NumTryInstruction = numTryInstruction;
        }

        public EHInfo(EHInfo oldState,int numTryInstruction, FlatOperand catchesLabel, FlatOperand finallyLabel)
        {
            CatchesLabel = catchesLabel.ImmediateValue.ValueText;
            FinallyLabel = finallyLabel.ImmediateValue.ValueText;
            PreviousState = oldState;
            NumTryInstruction = numTryInstruction;
            EHPart = EHPart.Try;
        }

        /// <summary>
        /// The active exception handler part (try/catch/finally)
        /// </summary>
        public EHPart EHPart { get; set; }

        /// <summary>
        /// The input instruction number that defined this exception handler block
        /// </summary>
        public int NumTryInstruction { get; set; }

        /// <summary>
        /// The label, if any, where we jump if an exception is thrown
        /// </summary>
        public string CatchesLabel { get; set; }
        /// <summary>
        /// The label, if any, that marks the end of the exception handler block
        /// </summary>
        public string ehEndLabel { get; set; }
        /// <summary>
        /// The label, if any, where we jump for a LEAVE instruction
        /// </summary>
        public string FinallyLabel { get; set; }

        /// <summary>
        /// The previous exception handler state, for when we exit this one
        /// </summary>
        public EHInfo PreviousState { get; set; }
    }

    class BasicBlock : IDisposable
    {
        public BasicBlock(ControlFlowGraph cfg, int nBlock, int at_instruction, EHInfo copy_ehState_from)
        {
            ControlFlowGraph = cfg;
            InputBlockNum = nBlock;
            FromInstruction = at_instruction;
            ToInstruction = at_instruction;
            Instructions = new List<FlatStatement>();
            if (at_instruction>=0)
                Instructions.Add(ControlFlowGraph.InputInstructions[at_instruction]);

            if (copy_ehState_from != null)
            {
                EHInfo = new LS2IL.EHInfo(copy_ehState_from);
            }

            ResetRegisterScan();
        }

        public void ResetRegisterScan()
        {
            RegisterReads = new List<int>();
            RegisterWrites = new List<int>();
        }

        public int InputBlockNum {get;set;}

        public ControlFlowGraph ControlFlowGraph { get; private set; }

        public int FromInstruction { get; private set; }
        public int ToInstruction { get; private set; }

        public List<FlatStatement> Instructions { get; private set; }

        public List<int> RegisterReads { get; private set; }
        public List<int> RegisterWrites { get; private set; }
         
        public List<BasicBlock> Predecessors { get; private set; }
        public List<BasicBlock> Successors { get; private set; }

        /// <summary>
        /// Retrieve the set of all BasicBlocks that could have executed before arriving at this BasicBlock
        /// </summary>
        /// <param name="list"></param>
        public void GetPossibleHistory(List<BasicBlock> list)
        {
            if (this.Predecessors == null || this.Predecessors.Count == 0)
            {
                if (!list.Contains(null))
                {
                    list.Add(null);
                }
            }
            else
            {
                foreach (BasicBlock bb in this.Predecessors)
                {
                    if (bb != this && !list.Contains(bb))
                    {
                        list.Add(bb);

                        if (bb != null)
                            bb.GetPossibleHistory(list);
                    }
                }
            }
        }

        /// <summary>
        /// Retrieve the set of all BasicBlocks that could execute after leaving this BasicBlock
        /// </summary>
        /// <param name="list"></param>
        public void GetPossibleFuture(List<BasicBlock> list)
        {
            if (this.Successors == null || this.Successors.Count == 0)
            {
                if (!list.Contains(null))
                {
                    list.Add(null);
                }
            }
            else
            {
                foreach (BasicBlock bb in this.Successors)
                {
                    if (bb != this && !list.Contains(bb))
                    {
                        list.Add(bb);
                        if (bb!=null)
                            bb.GetPossibleFuture(list);
                    }
                }
            }
        }

        /// <summary>
        /// Get the set of all registers that are READ in the possible future from this BasicBlock
        /// </summary>
        /// <returns></returns>
        public List<int> GetRegisterFutureReads()
        {
            List<BasicBlock> listBlocks = new List<BasicBlock>();

            GetPossibleFuture(listBlocks);
            if (listBlocks.Contains(this))
                listBlocks.Remove(this);
            return ControlFlowGraph.GetRegisterReads(listBlocks);
        }

        /// <summary>
        /// Get the set of all registers that are WRITTEN in the possible history from this BasicBlock
        /// </summary>
        /// <returns></returns>
        public List<int> GetRegisterPastWrites()
        {
            List<BasicBlock> listBlocks = new List<BasicBlock>();
            GetPossibleHistory(listBlocks);
            if (listBlocks.Contains(this))
                listBlocks.Remove(this);
            return ControlFlowGraph.GetRegisterWrites(listBlocks);
        }

        public EHInfo EHInfo { get; private set; }

        public string CatchesLabel
        {
            get
            {
                if (EHInfo == null)
                    return null;
                return EHInfo.CatchesLabel;
            }
        }

        public string FinallyLabel
        {
            get
            {
                if (EHInfo == null)
                    return null;
                return EHInfo.FinallyLabel;
            }
        }

        /// <summary>
        /// Touches an instruction, adding it to this block
        /// </summary>
        /// <param name="at_instruction"></param>
        public void Touch(int at_instruction)
        {
            if (at_instruction > ToInstruction)
            {
                Instructions.Add(ControlFlowGraph.InputInstructions[at_instruction]);
                ToInstruction = at_instruction;
            }
        }

        /// <summary>
        /// Determine if this block includes a given input instruction by number
        /// </summary>
        /// <param name="at_instruction"></param>
        /// <returns></returns>
        public bool Includes(int at_instruction)
        {
            return at_instruction >= FromInstruction && at_instruction <= ToInstruction; 
        }

        public void Dispose()
        {
            ControlFlowGraph = null;
        }

        public FlatStatement GetInputInstruction(int nInstruction)
        {
            return ControlFlowGraph.InputInstructions[nInstruction];
        }

        /// <summary>
        /// Gets the last instruction in this BasicBlock, if there is one...
        /// </summary>
        /// <returns></returns>
        public FlatStatement GetExitInstruction()
        {
            if (this.Instructions.Count > 0)
            {
                return this.Instructions[Instructions.Count - 1];
            }
            return null;
        }

        /// <summary>
        /// Strips this block from the graph, because it will not be used
        /// </summary>
        public void StripUnusedBlock()
        {
            if (Successors == null)
                return;

            foreach (BasicBlock bb in Successors)
            {
                if (bb!=null)
                    bb.RemovePredecessor(this);
            }
        }

        void RemovePredecessor(BasicBlock bb)
        {
            if (Predecessors == null)
            {
                return;
            }
            if (Predecessors.Contains(bb))
                Predecessors.Remove(bb);
        }

        void AddPredecessor(BasicBlock bb)
        {
            if (Predecessors == null)
            {
                Predecessors = new List<BasicBlock>();
                Predecessors.Add(bb);
                return;
            }

            if (bb == this)
                return;

            if (Predecessors.Contains(bb))
                return;

            Predecessors.Add(bb);
        }

        void AddSuccessorByBlock(int nBlock)
        {
            if (nBlock >= ControlFlowGraph.BasicBlocks.Count)
                return;

            AddSuccessor(ControlFlowGraph.BasicBlocks[nBlock]);
        }

        void AddSuccessor(BasicBlock bb)
        {
            if (Successors == null)
                Successors = new List<BasicBlock>();

            if (bb == this)
                return;

            if (Successors.Contains(bb))
                return;

            Successors.Add(bb);
            if (bb!=null)
                bb.AddPredecessor(this);
        }

        void AddSuccessorByInstruction(int nInstruction)
        {
            BasicBlock bb = ControlFlowGraph.GetBlockFromInstruction(nInstruction);
            AddSuccessorByBlock(bb.InputBlockNum);
        }

        void AddSuccessorByLabel(string label_name)
        {
            BasicBlock bb = ControlFlowGraph.Labels[label_name];
            AddSuccessor(bb);
        }

        void AddSuccessorByRelativeOpnd(FlatOperand opnd)
        {
            switch (opnd.OperandType)
            {
                case FlatOperandType.OPND_LABEL:
                    {
                        AddSuccessorByLabel(opnd.ImmediateValue.ValueText);
                        return;
                    }
                    break;
            }
            throw new NotImplementedException();
        }

        void AddSuccessorByThrow()
        {
            if (string.IsNullOrEmpty(this.CatchesLabel))
            {
                AddSuccessor(null);
                return;
            }
            AddSuccessorByLabel(this.CatchesLabel);
        }

        void AddSuccessorByEndFinally()
        {
            if (EHInfo != null && !string.IsNullOrEmpty(EHInfo.FinallyLabel))
            {
                BasicBlock bb = ControlFlowGraph.Labels[EHInfo.FinallyLabel];

                // each of the Predecessors ends with a LEAVE instruction we could have come from
                foreach (BasicBlock pred in bb.Predecessors)
                {
                    // if we are returning to this particular LEAVE instruction, our successor is the block AFTER it.
                    AddSuccessorByBlock(pred.InputBlockNum+1);
                }
            }
            else
                throw new LS2ILLabelException("ENDFINALLY missing FinallyLabel?");
        }

        void AddSuccessorByLeave()
        {
            AddSuccessorByBlock(InputBlockNum + 1);

            if (EHInfo != null && !string.IsNullOrEmpty(EHInfo.FinallyLabel))
            {
                AddSuccessorByLabel(EHInfo.FinallyLabel);
            }
        }

        void AddSuccessorBySwitch(FlatOperand opnd)
        {
            switch (opnd.ImmediateValue.ValueType)
            {
                case FlatValueType.VT_Array:
                    {
                        FlatArrayBuilder fab = (FlatArrayBuilder)opnd.ImmediateValue.Object;
                        foreach (FlatValue value in fab.Values)
                        {
                            AddSuccessorByLabel(value.ValueText);
                        }
                        return;
                    }
                    break;
                case FlatValueType.VT_Table:
                    {
                        FlatTableBuilder ftb = (FlatTableBuilder)opnd.ImmediateValue.Object;
                        foreach (KeyValuePair<string, FlatValue> kvp in ftb.Values)
                        {
                            AddSuccessorByLabel(kvp.Value.ValueText);
                        }
                        return;
                    }
                    break;
            }

            throw new NotImplementedException("unhandled switch type?");
        }

        void AddRegisterRead(int nRegister)
        {
            if (RegisterReads.Contains(nRegister))
                return;
            RegisterReads.Add(nRegister);
        }

        void AddRegisterWrite(int nRegister)
        {
            if (RegisterWrites.Contains(nRegister))
                return;
            RegisterWrites.Add(nRegister);
        }


        public void ScanRegisters()
        {
            foreach (FlatStatement fs in this.Instructions)
            {
                if (fs.Operands == null)
                    continue;

                if (fs.Instruction.HasLValue())
                {
                    // yes l-value

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

                        AddRegisterWrite(from_reg);
                        continue;
                    }

                    if (fs.Instruction == Instruction.REREFERENCE)
                    {
                        // operand 0 MAY BE an integer immediate value (l-value = register number)
                        if (fs.Operands[0].OperandType == FlatOperandType.OPND_IMMEDIATE)
                        {
                            // operand 0 is an integer immediate value (l-value = register number)
                            AddRegisterWrite((int)fs.Operands[0].ImmediateValue.Object);
                        }
                    }
                    else
                    {
                        // operand 0 is an integer immediate value (l-value = register number)
                        AddRegisterWrite((int)fs.Operands[0].ImmediateValue.Object);
                    }

                    // remaining r-values
                    for (int i = 1; i < fs.Operands.Count; i++)
                    {
                        if (fs.Operands[i].OperandType == FlatOperandType.OPND_REGISTER_VALUEREF)
                        {
                            AddRegisterRead((int)fs.Operands[i].OperandIndex);
                        }
                    }

                }
                else
                {
                    // all r-values
                    for (int i = 0; i < fs.Operands.Count; i++)
                    {
                        if (fs.Operands[i].OperandType == FlatOperandType.OPND_REGISTER_VALUEREF)
                        {
                            AddRegisterRead((int)fs.Operands[i].OperandIndex);
                        }
                    }
                }




            }
        }

        /// <summary>
        /// Applies the successors to this BasicBlock, given its Exit instruction and Exception Handler state
        /// </summary>
        public void GraphSuccessors()
        {
            if (EHInfo!=null && EHInfo.EHPart == EHPart.Try)
            {
                AddSuccessorByLabel(EHInfo.CatchesLabel);
            }

            FlatStatement fs = GetExitInstruction();

            if (!fs.Instruction.IsBasicBlockExit())
            {
                // fall-thru...
                AddSuccessorByBlock(InputBlockNum + 1);
                return;
            }

            switch (fs.Instruction)
            {
                case Instruction.JE:
                case Instruction.JG:
                case Instruction.JGE:
                case Instruction.JL:
                case Instruction.JLE:
                case Instruction.JNE:
                case Instruction.JNZ:
                case Instruction.JZ:
                    {
                        AddSuccessorByBlock(InputBlockNum + 1);
                        AddSuccessorByRelativeOpnd(fs.Operands[0]);
                    }
                    break;
                case Instruction.JMP:
                    {
                        AddSuccessorByRelativeOpnd(fs.Operands[0]);
                    }
                    break;
                case Instruction.LEAVE:
                    {
                        AddSuccessorByLeave(); // perform finally->endfinally and then fall through
                    }
                    break;
                case Instruction.ENDFINALLY:
                    {
                        AddSuccessorByEndFinally();
                    }
                    break;
                case Instruction.THROW:
                    {
                        AddSuccessorByThrow();                        
                    }
                    break;
                case Instruction.SWITCH:
                    {
                        AddSuccessorBySwitch(fs.Operands[0]);
                    }
                    break;
                case Instruction.RETURN:
                    {
                        // NO successor.
                        AddSuccessor(null);
                    }
                    break;
            }
        }
    }
}
