using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;

namespace Unity.Entities.SourceGen.SystemGenerator.SystemAPI;

public static class SystemApiContextErrors
{
    const string k_SystemAPIGenericAccess = "SystemAPI usage with Generic Access";
    const string k_SystemAPIComponentAccess = "SystemAPI usage with Component Access";

    public static void SGSA0001(ISourceGeneratorDiagnosable systemDescription, CandidateSyntax candidate)
    {
        systemDescription.LogError(nameof(SGSA0001), k_SystemAPIGenericAccess,
            "SystemAPI usage with generic parameter not supported", candidate.Node.GetLocation());
    }

    public static void SGSA0002(ISourceGeneratorDiagnosable systemDescription, CandidateSyntax candidate)
    {
        var dataLookupName = candidate.Type.ToString();

        systemDescription.LogError(nameof(SGSA0002), k_SystemAPIComponentAccess,
            $"{dataLookupName} usage with variable read permission not supported outside OnCreate of your system. To fix, instead call it inside OnCreate of your calling system",
            candidate.Node.GetLocation());
    }
}
