using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LS2IL;

namespace ls2csc
{
    class FinallyInjectLeaves
    {
        public FinallyInjectLeaves(Function f)
        {
            Function = f;
        }

        public Function Function { get; private set; }

        public enum EHPart
        {
            Try,
            Catch,
            Finally,
        }

        public class FinallyState
        {
            public FinallyState(FinallyState oldState, int instruction, FlatOperand catchesLabel, string ehendLabel)
            {
                NumInstruction = instruction;
                CatchesLabel = catchesLabel.ImmediateValue.ValueText;
                ehEndLabel = ehendLabel;
                OldState = oldState;
                Part = EHPart.Try;
            }

            public FinallyState(FinallyState oldState, int instruction, FlatOperand catchesLabel, FlatOperand finallyLabel)
            {
                NumInstruction = instruction;
                CatchesLabel = catchesLabel.ImmediateValue.ValueText;
                FinallyLabel = finallyLabel.ImmediateValue.ValueText;
                OldState = oldState;
                Part = EHPart.Try;
            }

            public int NumInstruction { get; private set; } 
            public EHPart Part { get; set; }
            public string CatchesLabel { get; private set; }
            public string ehEndLabel { get; private set; }
            public string FinallyLabel { get; private set; }

            public FinallyState OldState { get; private set; }

        }

        public FinallyState CurrentState { get; private set; }        
            public Dictionary<string, int> Labels { get; private set; }

        void PushFinallyState(int nInstruction, FlatStatement tryInstruction)
        {
            if (tryInstruction.Operands.Count == 1)
            {
                CurrentState = new FinallyState(CurrentState, nInstruction, tryInstruction.Operands[0], tryInstruction.Comment);
            }
            else
                CurrentState = new FinallyState(CurrentState, nInstruction, tryInstruction.Operands[0], tryInstruction.Operands[1]);
        }

        void PopFinallyState()
        {
            CurrentState = CurrentState.OldState;
        }

        void FindLabels(List<FlatStatement> instructions)
        {
            if (Labels != null)
                return;

            Labels = new Dictionary<string, int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].Instruction == Instruction.meta_LABEL)
                {
                    Labels.Add(instructions[i].Operands[0].ImmediateValue.ValueText,i);
                }
            }
        }

        void IncrementLabels(List<FlatStatement> instructions, int nStart)
        {
            if (Labels == null)
                return;

            Dictionary<string, int> newLabels = new Dictionary<string, int>();

            foreach(KeyValuePair<string,int> kvp in Labels)
            {
                if (kvp.Value >= nStart)
                {
                    newLabels[kvp.Key] = kvp.Value + 1;
                }
                else
                    newLabels[kvp.Key] = kvp.Value;
            }

            Labels = newLabels;
        }


        public void InjectLeaveStatements(List<FlatStatement> instructions)
        {
            FindLabels(instructions);

            for (int i = 0; i < instructions.Count; i++)
            {
                FlatStatement fs = instructions[i];
                if (CurrentState != null)
                {
                    switch (fs.Instruction)
                    {
                        case Instruction.TRY:
                            PushFinallyState(i, fs);
                            break;
                        case Instruction.ENDFINALLY:
                            PopFinallyState();
                            break;
                        case Instruction.THROW:
                            // only inject LEAVE if this is in the exception handler (catch) portion
                            if (CurrentState.Part == EHPart.Catch)
                            {
                                IncrementLabels(instructions, i);
                                instructions.Insert(i, FlatStatement.LEAVE());
                                i++;
                            }
                            break;
                        case Instruction.meta_LABEL:
                            //fs.Operands[0].ImmediateValue;
                            if (fs.Operands[0].ImmediateValue.ValueText.Contains("ehbegin"))
                            {
                                CurrentState.Part = EHPart.Catch;
                            }
                            else if (fs.Operands[0].ImmediateValue.ValueText.Contains("finally"))
                            {
                                CurrentState.Part = EHPart.Finally;
                            }

                            if (CurrentState != null && !string.IsNullOrEmpty(CurrentState.ehEndLabel) && fs.Operands[0].ImmediateValue.ValueText == CurrentState.ehEndLabel)
                            {
                                PopFinallyState();
                            }
                            break;
                        case Instruction.RETURN:
                            IncrementLabels(instructions, i);
                            instructions.Insert(i, FlatStatement.LEAVE());
                            i++;
                            break;
                        case Instruction.JMP:
                        case Instruction.JE:
                        case Instruction.JG:
                        case Instruction.JGE:
                        case Instruction.JL:
                        case Instruction.JLE:
                        case Instruction.JNE:
                        case Instruction.JNZ:
                        case Instruction.JZ:
                            {
                                // only inject LEAVE if the jump target is outside of the try/catch blocks
                                int nTargetInstruction;
                                if (!Labels.TryGetValue(fs.Operands[0].ImmediateValue.ValueText, out nTargetInstruction))
                                {
                                    throw new NotImplementedException("Unknown jump target label " + fs.Operands[0].ImmediateValue.ValueText);
                                }

                                if (nTargetInstruction <= CurrentState.NumInstruction)
                                {
                                    // past the beginning.
                                    IncrementLabels(instructions, i);
                                    instructions.Insert(i, FlatStatement.LEAVE());
                                    i++;
                                    break;
                                }

                                // see if it's past the end
                                int nEndInstruction;
                                if (string.IsNullOrEmpty(CurrentState.FinallyLabel))
                                {
                                    if (!Labels.TryGetValue(CurrentState.ehEndLabel, out nEndInstruction))
                                    {
                                        throw new NotImplementedException("Unknown exception handler end label " + CurrentState.ehEndLabel);
                                    }
                                }
                                else
                                {
                                    if (!Labels.TryGetValue(CurrentState.FinallyLabel, out nEndInstruction))
                                    {
                                        throw new NotImplementedException("Unknown finally target label " + CurrentState.FinallyLabel);
                                    }
                                }

                                if (nTargetInstruction >= nEndInstruction)
                                {
                                    // past the end.
                                    IncrementLabels(instructions, i);
                                    instructions.Insert(i, FlatStatement.LEAVE());
                                    i++;
                                    break;
                                }

                            }
                            break;
                    }
                }
                else
                {
                    switch (fs.Instruction)
                    {
                        case Instruction.TRY:
                            PushFinallyState(i, fs);
                            break;
                    }
                }

            }
        }
    }
}
