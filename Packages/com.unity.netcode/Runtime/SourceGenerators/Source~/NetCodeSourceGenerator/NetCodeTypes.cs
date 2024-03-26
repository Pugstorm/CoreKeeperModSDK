using System;

namespace Unity.NetCode.Generators
{
    // The following enums are copies of the one present in NetCode.GhostModifiers
    // Any changes to those enums in the the package must be reflected also here.
    // The use of enums is not mandatory, but make it easy to match the values 1:1 and generate the correct names

    enum SmoothingAction
    {
        Clamp = 0,
        Interpolate = 1,
        InterpolateAndExtrapolate = 3,
    }

    [Flags]
    enum GhostPrefabType
    {
        None = 0,
        InterpolatedClient = 1,
        PredictedClient = 2,
        Client = 3,
        Server = 4,
        AllPredicted = 6,
        All = 7
    }

    [Flags]
    enum GhostSendType
    {
        DontSend = 0,
        OnlyInterpolatedClients = 1,
        OnlyPredictedClients = 2,
        AllClients = 3
    }

    [Flags]
    enum SendToOwnerType
    {
        None = 0,
        SendToOwner = 1,
        SendToNonOwner = 2,
        All = 3,
    }

    //Internal representation of the GhostFieldAttribute used to setup the Attribute field of the TypeInformation class.
    class GhostField
    {
        public int Quantization { get; set; } = -1;
        public SmoothingAction Smoothing { get; set; }
        public int SubType { get; set; }
        public float MaxSmoothingDistance { get; set; }
        public bool ?Composite { get; set; }
        public bool SendData { get; set; } = true;
    }

    //Internal copy of the TypeRegistryEntry in NetCode package. Is used to declare the default type registry and
    //by in user land, to specify the custom type list inside the UserDefinedTemplate.RegisterTemplates.
    //Please reflect here any changes to NetCode/Authoring/TypeRegistryEntry.cs
    class TypeRegistryEntry
    {
        public string Type;
        public string Template;
        public string TemplateOverride;
        public int SubType;
        public SmoothingAction Smoothing;
        public bool Quantized;
        public bool SupportCommand;
        public bool Composite;

        public override string ToString()
        {
            return $"{nameof(TypeRegistryEntry)}:[{nameof(Type)}: {Type}, {nameof(Template)}: {Template}, {nameof(TemplateOverride)}: {TemplateOverride}, {nameof(SubType)}: {SubType}, {nameof(Smoothing)}: {Smoothing}, {nameof(Quantized)}: {Quantized}, {nameof(SupportCommand)}: {SupportCommand}, {nameof(Composite)}: {Composite}]";
        }
    }
}

