using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuickGraph;
using Microsoft.ExtendedReflection.Metadata;

namespace DUCover.Graph
{
    /// <summary>
    /// Represents an instruction vertex
    /// </summary>
    public class InstructionVertex
        : Vertex
    {
        private Instruction instruction = null;

        public InstructionVertex(Instruction instruction)
        {
            this.instruction = instruction;
        }

        public Instruction Instruction
        {
            get
            {
                if (this.instruction == null)
                    throw new InvalidOperationException();
                return this.instruction;
            }
            set
            {
                this.instruction = value;
            }
        }

        public override bool Equals(object obj)
        {
            InstructionVertex iv = obj as InstructionVertex;
            if (iv == null)
                return false;

            return this.instruction.Offset == iv.instruction.Offset;
        }

        public override int GetHashCode()
        {
            return this.instruction.Offset;
        }

        public override string ToString()
        {
            return Convert.ToString(this.instruction.Offset, 16);
        }
    }
}
