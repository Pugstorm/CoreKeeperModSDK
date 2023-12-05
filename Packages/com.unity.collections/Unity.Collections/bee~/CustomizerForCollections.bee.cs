using System.Collections.Generic;
using JetBrains.Annotations;
using Bee.NativeProgramSupport;

[UsedImplicitly]
class CustomizerForCollections : AsmDefCSharpProgramCustomizer
{
    public override string CustomizerFor => "Unity.Collections";

    public override void CustomizeSelf(AsmDefCSharpProgram program)
    {
        var ilSupportDll = program.MainSourcePath.Parent.Combine("Unity.Collections.LowLevel.ILSupport/Unity.Collections.LowLevel.ILSupport.dll");
        // if it doesn't exist, then perhaps the codegen is in use
        if (ilSupportDll.Exists())
            program.References.Add(ilSupportDll);
    }
}

