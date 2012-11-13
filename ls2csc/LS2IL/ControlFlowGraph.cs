using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LS2IL
{
    class ControlFlowGraph : IDisposable
    {
        public ControlFlowGraph(Function f, List<FlatStatement> instructions)
        {
            Function = f;
            InputInstructions = instructions;
            Labels = new Dictionary<string, BasicBlock>(); //new Dictionary<string, int>();
            BasicBlocks = new List<BasicBlock>();
        }

        //public Dictionary<string, int> Labels { get; private set; }
        public Dictionary<string, BasicBlock> Labels { get; private set; }
        public Function Function { get; private set; }
        public List<FlatStatement> InputInstructions { get; private set; }
        public List<BasicBlock> BasicBlocks { get; private set; }
        BasicBlock CurrentBasicBlock;

        EHInfo EHState;


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

        void NormalInstruction(int nInstruction, EHInfo ehinfo)
        {
            if (CurrentBasicBlock == null)
            {
                HeadInstruction(nInstruction, ehinfo);
            }
            else
                CurrentBasicBlock.Touch(nInstruction);
        }

        void HeadInstruction(int nInstruction, EHInfo ehinfo)
        {
            CurrentBasicBlock = new BasicBlock(this, BasicBlocks.Count, nInstruction, ehinfo);
            BasicBlocks.Add(CurrentBasicBlock);
        }

        void TailInstruction(int nInstruction, EHInfo ehinfo)
        {
            NormalInstruction(nInstruction, ehinfo);
            CurrentBasicBlock = null;
        }

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

        void PopFinallyState()
        {
            EHState = EHState.PreviousState;
        }

        /*
        void FindLabels()
        {
            if (Labels != null)
                return;

            Labels = new Dictionary<string, int>();
            for (int i = 0; i < InputInstructions.Count; i++)
            {
                if (InputInstructions[i].Instruction == Instruction.meta_LABEL)
                {
                    Labels.Add(InputInstructions[i].Operands[0].ImmediateValue.ValueText, i);
                }
            }
        }
        /**/

        void BuildBasicBlocks()
        {
            for (int i = 0; i < InputInstructions.Count; i++)
            {
                FlatStatement fs = InputInstructions[i];

                switch (fs.Instruction)
                {
                    case Instruction.meta_LABEL:
                        // remember label
                        if (fs.Operands[0].ImmediateValue.ValueText.Contains("ehbegin"))
                        {
                            EHState.EHPart = EHPart.Catch;
                        }
                        else if (fs.Operands[0].ImmediateValue.ValueText.Contains("finally"))
                        {
                            EHState.EHPart = EHPart.Finally;
                        }
                        else if (EHState != null && !string.IsNullOrEmpty(EHState.ehEndLabel) && fs.Operands[0].ImmediateValue.ValueText == EHState.ehEndLabel)
                        {
                            PopFinallyState();
                        }

                        // begin basic block
                        HeadInstruction(i, EHState);
                        Labels.Add(fs.Operands[0].ImmediateValue.ValueText, CurrentBasicBlock);
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
                        /*
                        // only inject LEAVE if this is in the exception handler (catch) portion
                        if (inject_leave && EHState != null && EHState.EHPart == EHPart.Catch)
                        {
                            this.InputInstructions.Insert(i, FlatStatement.LEAVE());
                            i++;
                        }
                        /**/
                        TailInstruction(i, EHState);
                        break;
                    case Instruction.SWITCH:
                        TailInstruction(i, EHState);
                        break;
                    case Instruction.RETURN:
                        /*
                        if (inject_leave && EHState!=null && EHState.EHPart!= EHPart.None)
                        {
                            // inject LEAVE instructions

                            EHInfo ehState = EHState;
                            while (ehState != null && ehState.EHPart != EHPart.None)
                            {
                                this.InputInstructions.Insert(i, FlatStatement.LEAVE());

                                ehState = ehState.PreviousState;

                                TailInstruction(i, ehState);
                                i++;
                            }
                        }
                        /**/
                        TailInstruction(i, EHState);
                        break;
                    case Instruction.LEAVE:
                        // end basic block; exiting to do finally->endfinally
                        throw new NotImplementedException("");
                        PopFinallyState();// this is sort of wrong...
                        TailInstruction(i, EHState);
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

        public void Build()
        {           
            // first, build the basic blocks.
            BuildBasicBlocks();

            // now inject LEAVEs
            InjectLEAVE();

            // Third, identify and mark all successor blocks
            MarkSuccessors();
        }

        void UpdateBasicBlockNumbers(int from_block)
        {
            for (int i = from_block; i < BasicBlocks.Count; i++)
            {
                BasicBlock bb = BasicBlocks[i];
                bb.InputBlockNum = i;
            }
        }

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

                                BasicBlocks.Insert(i,new BasicBlock(this,i,bb.ToInstruction,bb.EHInfo.PreviousState));
                                i++;

                                bb.Instructions[bb.Instructions.Count - 1] = FlatStatement.LEAVE();
                            }
                        }
                        break;
                    case Instruction.RETURN:
                        {
                            if (bb.EHInfo != null)
                            {
                                BasicBlocks.Insert(i, new BasicBlock(this, i, bb.ToInstruction, bb.EHInfo.PreviousState));
                                bb.Instructions[bb.Instructions.Count - 1] = FlatStatement.LEAVE();
                                break;
                            }
                        }
                        break;
                    case Instruction.SWITCH:
                        {
                         //   throw new NotImplementedException("Control Flow Graph: Switch");
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
                                    BasicBlocks.Insert(i, new BasicBlock(this, i, bb.ToInstruction, bb.EHInfo.PreviousState));
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
                                    BasicBlocks.Insert(i, new BasicBlock(this, i, bb.ToInstruction, bb.EHInfo.PreviousState));
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

        void MarkSuccessors()
        {
            for (int i = 0; i < BasicBlocks.Count; i++)
            {
                BasicBlock bb = BasicBlocks[i];
                bb.Build();
            }
        }

        public List<FlatStatement> Flatten()
        {
            List<FlatStatement> list = new List<FlatStatement>();

            foreach (BasicBlock bb in BasicBlocks)
            {

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

        public EHPart EHPart { get; set; }
        public int NumTryInstruction { get; set; }
        public string CatchesLabel { get; set; }
        public string ehEndLabel { get; set; }
        public string FinallyLabel { get; set; }

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
        }

        public int InputBlockNum {get;set;}

        public ControlFlowGraph ControlFlowGraph { get; private set; }

        public int FromInstruction { get; private set; }
        public int ToInstruction { get; private set; }

        public List<FlatStatement> Instructions { get; private set; }

        /*
        public FlatStatement ExitInstruction
        {
            get
            {
                return this.ControlFlowGraph.InputInstructions[ToInstruction];
            }
        }
        */

        public List<BasicBlock> Predecessors { get; private set; }
        public List<BasicBlock> Successors { get; private set; }

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

        public void Touch(int at_instruction)
        {
            if (at_instruction > ToInstruction)
            {
                Instructions.Add(ControlFlowGraph.InputInstructions[at_instruction]);
                ToInstruction = at_instruction;
            }
        }

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

        public FlatStatement GetExitInstruction()
        {
            if (this.Instructions.Count > 0)
            {
                return this.Instructions[Instructions.Count - 1];
            }
            return null;
        }

        

        void AddPredecessor(BasicBlock bb)
        {
            if (Predecessors == null)
            {
                Predecessors = new List<BasicBlock>();
                Predecessors.Add(bb);
                return;
            }

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

        void AddSuccessorByLeave()
        {
            AddSuccessorByBlock(InputBlockNum + 1);

            if (EHInfo != null && !string.IsNullOrEmpty(EHInfo.FinallyLabel))
                AddSuccessorByLabel(EHInfo.FinallyLabel);
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

        void LocateSuccessors()
        {
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

        public void Build()
        {
            LocateSuccessors();
        }
    }
}
