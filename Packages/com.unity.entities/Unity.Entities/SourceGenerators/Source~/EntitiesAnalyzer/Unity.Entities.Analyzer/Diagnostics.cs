using Microsoft.CodeAnalysis;

namespace Unity.Entities.Analyzer
{
    public static class CSharpCompilerDiagnostics
    {
        public const string CS1654 = "CS1654";
    }

    public static class EntitiesDiagnostics
    {
        public const string ID_EA0001 = "EA0001";
        public static readonly DiagnosticDescriptor k_Ea0001Descriptor
            = new DiagnosticDescriptor(ID_EA0001, "You may only access BlobAssetStorage by (non-readonly) ref",
                "You may only access {0} by (non-readonly) ref, as it may only live in blob storage. Try `ref {1} {2} = ref {0}`.",
                "BlobAsset", DiagnosticSeverity.Error, isEnabledByDefault: true,
                description: "Expression contains BlobAssetStorage not passed by (non-readonly) ref.");

        public const string ID_EA0002 = "EA0002";
        public static readonly DiagnosticDescriptor k_Ea0002Descriptor
            = new DiagnosticDescriptor(ID_EA0002, "Potentially harmful construction of a BlobAsset with keywords `new` or `default`",
                "You should only construct {0} through a BlobBuilder, as it may only live in blob storage. Try using BlobBuilder .Construct/.ConstructRoot/.SetPointer.",
                "BlobAsset", DiagnosticSeverity.Warning, isEnabledByDefault: true,
                description: "Expression contains potentially harmful construction of BlobAssetStorage with `new` or `default` keywords.");

        public const string ID_EA0003 = "EA0003";
        public static readonly DiagnosticDescriptor k_Ea0003Descriptor
            = new DiagnosticDescriptor(ID_EA0003, "You cannot use the BlobBuilder to build a type containing Non-Blob References",
                "You may not build the type `{0}`, with BlobBuilder.ConstructRoot, as `{1}` {2}",
                "BlobAsset", DiagnosticSeverity.Error, isEnabledByDefault: true,
                description: "Expression contains BlobBuilder construction of type with non blob reference.");

        public const string ID_EA0004 = "EA0004";
        public static readonly DiagnosticDescriptor k_Ea0004Descriptor
            = new DiagnosticDescriptor(ID_EA0004, "SystemAPI member use is not permitted outside of a system type (SystemBase or ISystem)",
                "You may not use the SystemAPI member `{0}` outside of a system.  SystemAPI members rely on setup from the containing system.",
                "SystemAPI", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public const string ID_EA0005 = "EA0005";
        public static readonly DiagnosticDescriptor k_Ea0005Descriptor
            = new DiagnosticDescriptor(ID_EA0005, "This SystemAPI member use is not permitted inside an Entities.ForEach lambda",
                "You may not use the SystemAPI member `{0}` inside of an Entities.ForEach lambda",
                "SystemAPI", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public const string ID_EA0006 = "EA0006";
        public static readonly DiagnosticDescriptor k_Ea0006Descriptor
            = new DiagnosticDescriptor(ID_EA0006, "SystemAPI member use is not permitted in a static method",
                "You may not use the SystemAPI member `{0}` inside of a static method",
                "SystemAPI", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public const string ID_EA0007 = "EA0007";
        public static readonly DiagnosticDescriptor k_Ea0007Descriptor
            = new DiagnosticDescriptor(ID_EA0007, "Type is not marked partial",
                "Missing partial on {0}. As `{1}` uses a Roslyn Source Generator to generate a backing partial. Please add the partial keyword.",
                "Type", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public const string ID_EA0008 = "EA0008";
        public static readonly DiagnosticDescriptor k_Ea0008Descriptor
            = new DiagnosticDescriptor(ID_EA0008, "Parent Type is not marked partial",
                "Missing partial on {0}. As `{1}` uses a Roslyn Source Generator to generate a backing partial. Please add the partial keyword to `{2}`.",
                "Type", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public const string ID_EA0009 = "EA0009";
        public static readonly DiagnosticDescriptor k_Ea0009Descriptor
            = new DiagnosticDescriptor(ID_EA0009, "You may only access BlobAssetStorage by (non-readonly) ref",
                "You may only access {0} by (non-readonly) ref in `{1}`, as it may only live in blob storage. Try `ref {0}`.",
                "BlobAsset", DiagnosticSeverity.Error, isEnabledByDefault: true,
                description: "Method parameter contains BlobAssetStorage not passed by (non-readonly) ref.");

        public const string ID_EA0010 = "EA0010";
        public static readonly DiagnosticDescriptor k_Ea0010Descriptor
            = new DiagnosticDescriptor(ID_EA0010, "Containing Type is missing BurstCompile attribute",
                "Missing BurstCompile attribute on type {0} containing burst compiled method {1}. Burst needs the containing type of bursted methods to also have the BurstCompile attribute. Please add [BurstCompile] to the {0} type.",
                "Burst", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public const string ID_EA0011 = "EA0011";
        public static readonly DiagnosticDescriptor k_Ea0011Descriptor
            = new DiagnosticDescriptor(ID_EA0011, "Missing terminator",
                "Every Entities sequence needs a .ForEach() invocation, or ToQuery(), DestroyEntity(), AddComponent(), RemoveComponent(), AddComponentData(), AddChunkComponentData(), RemoveChunkComponentData(), AddSharedComponent(), or SetSharedComponent()",
                "Type", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public const string ID_EA0012 = "EA0012";
        public static readonly DiagnosticDescriptor k_Ea0012Descriptor
            = new DiagnosticDescriptor(ID_EA0012, "Chain not properly terminated",
                "Every Entities.ForEach statement needs to end with a .Schedule(), .ScheduleParallel(), or .Run() invocation",
                "Type", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public const string ID_EA0013 = "EA0013";
        public static readonly DiagnosticDescriptor k_Ea0013Descriptor
            = new DiagnosticDescriptor(ID_EA0013, "Chain not properly terminated",
                "Every Job.WithCode statement needs to end with a .Schedule() or .ScheduleParallel() invocation",
                "Type", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public const string ID_EA0014 = "EA0014";
        public static readonly DiagnosticDescriptor k_Ea0014Descriptor
            = new DiagnosticDescriptor(ID_EA0014, "'Entities' is the start of a chain, and cannot be used on its own",
                "'Entities' is the start of a chain, and cannot be used on its own",
                "Type", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public const string ID_EA0015 = "EA0015";
        public static readonly DiagnosticDescriptor k_Ea0015Descriptor
            = new DiagnosticDescriptor(ID_EA0015, "'Job' is the start of a chain, and cannot be used on its own",
                "'Job' is the start of a chain, and cannot be used on its own",
                "Type", DiagnosticSeverity.Error, isEnabledByDefault: true);

        public const string ID_EA0016 = "EA0016";
        public static readonly DiagnosticDescriptor k_Ea0016Descriptor
            = new DiagnosticDescriptor(ID_EA0016, "SystemState must be passed by ref",
                "SystemState must be passed by ref",
                "Type", DiagnosticSeverity.Warning, isEnabledByDefault: true);
    }
}
