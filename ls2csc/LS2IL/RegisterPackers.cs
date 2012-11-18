using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LS2IL
{
    class RegisterLiveness : IDisposable
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
            FromInstruction = -9999;// transformed_from.FromInstruction;
            ToInstruction = -9999;// transformed_from.ToInstruction;
            transformed_from.Transformed = this;
            TransformedFrom = new List<RegisterLiveness>();
            TransformedFrom.Add(transformed_from);
        }

        /// <summary>
        /// Updates the liveness for this register, to include this instruction
        /// </summary>
        /// <param name="at_instruction"></param>
        public void Touch(int at_instruction)
        {
            if (FromInstruction == -9999)
                FromInstruction = at_instruction;
            if (at_instruction > ToInstruction)
                ToInstruction = at_instruction;
            
            if (this._Transformed != null)
            {
                _Transformed.Touch(at_instruction);
            }
        }

        public int NumRegister { get; private set; }
        public int FromInstruction { get; private set; }
        public int ToInstruction { get; private set; }

        public bool Intersects(RegisterLiveness other)
        {
            if (other.FromInstruction >= ToInstruction)
                return false;
            if (FromInstruction >= other.ToInstruction)
                return false;

            return true;
        }

        public List<RegisterLiveness> TransformedFrom { get; private set; }

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
                    if (ToInstruction == -9999)
                    {
                        ToInstruction = value.ToInstruction;
                        FromInstruction = value.FromInstruction;
                        return;
                    }

                    /*
                    if (this.Intersects(value))
                    {
                        throw new NotSupportedException("transform collided");
                    }
                    /**/
                    if (ToInstruction > _Transformed.ToInstruction)
                        _Transformed.ToInstruction = ToInstruction;
                    if (FromInstruction < _Transformed.FromInstruction)
                        _Transformed.FromInstruction = FromInstruction; // possible collisions...
                }
            }
        }

        public override string ToString()
        {
            return FromInstruction.ToString() + "-" + ToInstruction.ToString();
        }

        public void Dispose()
        {
            _Transformed = null;
            if (TransformedFrom != null)
            {
                TransformedFrom.Clear();
                TransformedFrom = null;
            }
        }
    }

    interface IRegisterPacker
    {
        /// <summary>
        /// Modifies the ControlFlowGraph as needed to use fewer registers, while not losing required data
        /// </summary>
        /// <returns>total number of registers in the updated graph</returns>
        int PackRegisters();
    }

    class RegisterPackers
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cfg">Control Flow Graph with </param>
        /// <returns>total number of registers in the updated graph</returns>
        public static int Pack(ControlFlowGraph cfg)
        {
            // make sure the register scan is current.
            cfg.ScanRegisters();
            DefaultRegisterPacker packer = new DefaultRegisterPacker(cfg);
            return packer.PackRegisters();
        }
    }
}
