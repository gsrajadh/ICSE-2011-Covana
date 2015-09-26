using System;
using System.Text;
using Microsoft.ExtendedReflection.Collections;
using Microsoft.ExtendedReflection.Metadata;

namespace Covana.Util
{
    public class BasicBlockUtil
    {
        private readonly StringBuilder Log;
        private readonly MethodBodyEx body;
        private readonly IIndexable<int> _basicBlockStartOffsets;

        public BasicBlockUtil(StringBuilder log, MethodBodyEx body, IIndexable<int> basicBlockStartOffsets)
        {
            Log = log;
            this.body = body;
            _basicBlockStartOffsets = basicBlockStartOffsets;
            PrintInstructions(body);
        }

        private void PrintInstructions(MethodBodyEx body)
        {
            Instruction instruction;
            body.TryGetInstruction(0, out instruction);
            int nextOffset = instruction.NextOffset;
            Log.AppendLine("instruction: " + instruction.Offset.ToString("x") + " code: " + instruction.OpCode +
                           " next: " + nextOffset.ToString("x"));
            while (body.TryGetInstruction(nextOffset, out instruction))
            {
                nextOffset = instruction.NextOffset;
                Log.AppendLine("instruction: " + instruction.Offset.ToString("x") + " code: " + instruction.OpCode +
                               " next: " + nextOffset.ToString("x"));
            }
        }
    }
}