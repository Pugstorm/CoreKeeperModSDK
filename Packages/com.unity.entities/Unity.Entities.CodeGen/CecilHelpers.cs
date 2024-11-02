using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Unity.Entities.CodeGen
{
    static class CecilHelpers
    {
        public static SequencePoint FindBestSequencePointFor(MethodDefinition method, Instruction instruction)
        {
            var sequencePoints = method.DebugInformation?.GetSequencePointMapping().Values.OrderBy(s => s.Offset).ToList();
            if (sequencePoints == null || !sequencePoints.Any())
                return null;

            for (int i = 0; i != sequencePoints.Count-1; i++)
            {
                if (sequencePoints[i].Offset < instruction.Offset &&
                    sequencePoints[i + 1].Offset > instruction.Offset)
                    return sequencePoints[i];
            }

            return sequencePoints.FirstOrDefault();
        }
    }
}
