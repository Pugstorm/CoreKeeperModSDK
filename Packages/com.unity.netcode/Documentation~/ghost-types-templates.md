# Ghost Templates

Ghost component types (i.e. all components with a GhostField attribute, or other netcode interfaces) are all handled a certain way during Baking, and by the NetCode code generator, to produce the right code when building players. 
It's possible to define the desired behavior in code, and on a per-ghost prefab basis, and on a per-component basis.

## Supported Types
Inside the package, we have default templates for how to generate serializers (called the "default serializers") for a limited set of types:

* bool
* Entity
* FixedString32Bytes
* FixedString64Bytes
* FixedString128Bytes
* FixedString512Bytes
* FixedString4096Bytes
* float
* float2
* float3
* float4
* byte
* sbyte
* short
* ushort
* int
* uint
* long
* ulong
* enums (only for int/uint underlying type)  
* quaternion
* double

For certain types (i.e float, double, quaternion ,float2/3/4) multiple templates exists, which handle different ways to serialise the type: 

* Quantization [Quantized or un-quantized]: Quantized means a float value is sent as an int, with a certain multiplication factor, which sets its precision (e.g. 12.456789 can be sent as 12345 with a quantization factor of 1000). Unquantized means the float will be sent with full precision.
* Smoothing method [`Clamp`, `Interpolate`, or `InterpolateAndExtrapolate`]: Denotes how a new value is applied on the client, when a snapshot is received. See docs for `SmoothingAction` for more details).

Since each of these can change how the source value is serialized, deserialized, and applied on the target, we have multiple serialization templates. 
Additionally; each template uses different, named regions to handle these cases. The code generator will pick the appropriate regions to generate, and thus bake your user-defined serialization settings for fields on your types, directly into the "Serializer" for your type.
You can explore these generated types in the projects `Temp/NetCodeGenerated` folder (note that they are deleted when Unity is closed).

### Changing how a ComponentType is serialized via "Ghost Component *Variants*"

[Ghost Component Variants](ghost-snapshots.md#ghost-component-variants) give you the ability to clobber the "Default Serializer" generated for a given type, replacing it with your own serializer. 
Variants can also be applied on a per-ghost, per-component basis, via the `GhostAuthoringInspectionComponent`. See docs through the above link for futher details.

### Changing how a ComponentType is serialized via "Ghost Component *SubTypes*"

You may have multiple Templates defined and available for a given Type (e.g. a 2D and a 3D Template, for a float3). 
SubTypes allow you to choose which one to use, on a per-GhostField basis. See example below.

## Defining Additional Templates

It's possible to register additional types (i.e. types that netcode doesn't already support in its above defaults) so that they can be replicated correctly as `GhostField`s.

### Writing the Template
You must define the Template file correctly. It can be added to any package or folder in the project, but the requirements are:
- The file must have a `NetCodeSourceGenerator.additionalfile` extension (i.e: MyCustomType.NetCodeSourceGenerator.additionalfile).
- The first line must contains a`#templateid: XXX` line. This assign to the template a globally unique user defined id.
You will get errors if a) you define a `UserDefinedTemplate` that has no found Template file b) vice-versa, or c) you make errors when defining the `UserDefinedTemplate`.
Code-Generation errors of the Template _may_ cause compiler errors.

This new template `MyCustomTypeTemplate.NetCodeSourceGenerator.additionalfile` needs to be set up similarly to the default types templates. 
Here is an example copied from the default `float` template (where the float is quantized and stored in an int field):

```c#
#templateid: MyCustomNamespace.MyCustomTypeTemplate
#region __GHOST_IMPORTS__
#endregion
namespace Generated
{
    public struct GhostSnapshotData
    {
        struct Snapshot
        {
            #region __GHOST_FIELD__
            public int __GHOST_FIELD_NAME__;
            #endregion
        }

        public void PredictDelta(uint tick, ref GhostSnapshotData baseline1, ref GhostSnapshotData baseline2)
        {
            var predictor = new GhostDeltaPredictor(tick, this.tick, baseline1.tick, baseline2.tick);
            #region __GHOST_PREDICT__
            snapshot.__GHOST_FIELD_NAME__ = predictor.PredictInt(snapshot.__GHOST_FIELD_NAME__, baseline1.__GHOST_FIELD_NAME__, baseline2.__GHOST_FIELD_NAME__);
            #endregion
        }

        public void Serialize(int networkId, ref GhostSnapshotData baseline, ref DataStreamWriter writer, StreamCompressionModel compressionModel)
        {
            #region __GHOST_WRITE__
            if ((changeMask & (1 << __GHOST_MASK_INDEX__)) != 0)
                writer.WritePackedIntDelta(snapshot.__GHOST_FIELD_NAME__, baseline.__GHOST_FIELD_NAME__, compressionModel);
            #endregion
        }

        public void Deserialize(uint tick, ref GhostSnapshotData baseline, ref DataStreamReader reader,
            StreamCompressionModel compressionModel)
        {
            #region __GHOST_READ__
            if ((changeMask & (1 << __GHOST_MASK_INDEX__)) != 0)
                snapshot.__GHOST_FIELD_NAME__ = reader.ReadPackedIntDelta(baseline.__GHOST_FIELD_NAME__, compressionModel);
            else
                snapshot.__GHOST_FIELD_NAME__ = baseline.__GHOST_FIELD_NAME__;
            #endregion
        }

        public unsafe void CopyToSnapshot(ref Snapshot snapshot, ref IComponentData component)
        {
            if (true)
            {
                #region __GHOST_COPY_TO_SNAPSHOT__
                snapshot.__GHOST_FIELD_NAME__ = (int) math.round(component.__GHOST_FIELD_REFERENCE__ * __GHOST_QUANTIZE_SCALE__);
                #endregion
            }
        }
        public unsafe void CopyFromSnapshot(ref Snapshot snapshot, ref IComponentData component)
        {
            if (true)
            {
                #region __GHOST_COPY_FROM_SNAPSHOT__
                component.__GHOST_FIELD_REFERENCE__ = snapshotBefore.__GHOST_FIELD_NAME__ * __GHOST_DEQUANTIZE_SCALE__;
                #endregion

                #region __GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE_SETUP__
                var __GHOST_FIELD_NAME___Before = snapshotBefore.__GHOST_FIELD_NAME__ * __GHOST_DEQUANTIZE_SCALE__;
                var __GHOST_FIELD_NAME___After = snapshotAfter.__GHOST_FIELD_NAME__ * __GHOST_DEQUANTIZE_SCALE__;
                #endregion
                #region __GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE_DISTSQ__
                var __GHOST_FIELD_NAME___DistSq = math.distancesq(__GHOST_FIELD_NAME___Before, __GHOST_FIELD_NAME___After);
                #endregion
                #region __GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE__
                component.__GHOST_FIELD_REFERENCE__ = math.lerp(__GHOST_FIELD_NAME___Before, __GHOST_FIELD_NAME___After, snapshotInterpolationFactor);
                #endregion
            }
        }
        public unsafe void RestoreFromBackup(ref IComponentData component, in IComponentData backup)
        {
            #region __GHOST_RESTORE_FROM_BACKUP__
            component.__GHOST_FIELD_REFERENCE__ = backup.__GHOST_FIELD_REFERENCE__;
            #endregion
        }
        public void CalculateChangeMask(ref Snapshot snapshot, ref Snapshot baseline, uint changeMask)
        {
            #region __GHOST_CALCULATE_CHANGE_MASK_ZERO__
            changeMask = (snapshot.__GHOST_FIELD_NAME__ != baseline.__GHOST_FIELD_NAME__) ? 1u : 0;
            #endregion
            #region __GHOST_CALCULATE_CHANGE_MASK__
            changeMask |= (snapshot.__GHOST_FIELD_NAME__ != baseline.__GHOST_FIELD_NAME__) ? (1u<<__GHOST_MASK_INDEX__) : 0;
            #endregion
        }
        #if UNITY_EDITOR || NETCODE_DEBUG
        private static void ReportPredictionErrors(ref IComponentData component, in IComponentData backup, ref UnsafeList<float> errors, ref int errorIndex)
        {
            #region __GHOST_REPORT_PREDICTION_ERROR__
            errors[errorIndex] = math.max(errors[errorIndex], math.abs(component.__GHOST_FIELD_REFERENCE__ - backup.__GHOST_FIELD_REFERENCE__));
            ++errorIndex;
            #endregion
        }
        private static int GetPredictionErrorNames(ref FixedString512Bytes names, ref int nameCount)
        {
            #region __GHOST_GET_PREDICTION_ERROR_NAME__
            if (nameCount != 0)
                names.Append(new FixedString32Bytes(","));
            names.Append(new FixedString64Bytes("__GHOST_FIELD_REFERENCE__"));
            ++nameCount;
            #endregion
        }
        #endif
    }
}
```

A good way to assign this "#templateid" is to use something like `CustomNamespace.CustomTemplateFileName`. All the default Netcode package templates uses an internal
id (not present in the template) with the following format: `NetCode.GhostSnapshotValueXXX.cs`.

>[!NOTE] The default types uses a slightly different approach at the moment, being embedded in the generator dlls.
The template contains a set of c-sharp like regions, `#region __GHOST_XXX__`, that are processed by code gen, and uses them to extract the code inside the region to create the serializer.
The template uses the `__GHOST_XXX__` as reserved keyword, and are substituted at generation time with the corresponding variable names and/or values.

For more information about the template format you can check the documentation present in the `SourceGenerator/Documentation` folder, or reference to other template files (see `Editor/Templates/DefaultTypes`).

### Registering your new Template with NetCode

Templates are added to the project by implementing a partial class, `UserDefinedTemplates`, and then injecting it into the `Unity.Netcode` package by using
an [AssemblyDefinitionReference](https://docs.unity3d.com/2020.1/Documentation/Manual/class-AssemblyDefinitionReferenceImporter.html).
The partial implementation must define the method `RegisterTemplates`, and add a new `TypeRegistry` entry (or entries).

The class must also exist inside the `Unity.NetCode.Generators` namespace.


```c#
namespace Unity.NetCode.Generators
{
    public static partial class UserDefinedTemplates
    {
        static partial void RegisterTemplates(System.Collections.Generic.List<TypeRegistryEntry> templates, string defaultRootPath)
        {
            templates.AddRange(new[]{
                new TypeRegistryEntry
                {
                    Type = "MyCustomNamespace.MyCustomType",
                    Quantized = true,
                    Smoothing = SmoothingAction.InterpolateAndExtrapolate,
                    SupportCommand = false,
                    Composite = false,
                    Template = "MyCustomNamespace.MyCustomTypeTemplate",
                    TemplateOverride = "",
                },
            });
        }
    }
}
```
>![NOTE]: This above example only registers `MyCustomType` when the GhostField is defined as follows `[GhostField(Quantization=100, Smoothing=SmoothingAction.InterpolateAndExtrapolate, Composite=false)]`.
> You must register all exact combinations you wish to support (and register them exactly as used).


### Additional Template Definition Rules
- When `Quantized` is set to true, the `__GHOST_QUANTIZE_SCALE__` variable must be present in the template. Also, the quantization scale **must** be specified when using the type in a `GhostField`.

- `Smoothing` is also important, as it changes how serialization is done in the `CopyFromSnapshot` function. In particular:
    - When smoothing is set to `Clamp`, only the `__GHOST_COPY_FROM_SNAPSHOT__` is required.
    - When smoothing is set to `Interpolate` or `InterpolateAndExtrapolate`, the regions `__GHOST_COPY_FROM_SNAPSHOT__`, `__GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE__`,
  `GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE_SETUP`, `__GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE_DISTSQ__` and `GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE_CLAMP_MAX` must be present and filled in.

- The `SupportCommand` denotes if the type can be used inside `Commands` and/or `Rpc`.

- The `Template` value is mandatory, and must point to the `#templateid` defined in the target Template file.

- The `TemplateOverride` is optional (can be null or empty). `TemplateOverride` is used when you want to re-use an existing template, but only override a specific section of it. 
This works well when using `Composite` types, as you'll point `Template` to the basic type (like the float template), and then point to the `TemplateOverride` only for the sections which need to be customized.
For example; `float2` only defines `CopyFromSnapshot`, `ReportPredictionErrors` and `GetPredictionErrorNames`, the rest uses the basic float template as a composite of the 2 values `float2` contains.
The assigned value must be the `#templateid` of the "base" template, as declared inside the other template file.

- The `Composite` flag should be `true` when declaring templates for 'container-like' types (i.e. types that contain multiple fields of the same type (like float3, float4 etc)).
  When this is set, the `Template` and `TemplateOverride` are applied to the field types, and not to containing type.

- If you need your template to define additional fields in the snapshot (for example: to map correctly on the server), you must define `__GHOST_CALCULATE_CHANGE_MASK_NO_COMMAND__` and `__GHOST_CALCULATE_CHANGE_MASK_ZERO_NO_COMMAND__` in the changemask calculation method, as commands point to the type directly (but components have snapshots that can store additional data).
These changemasks can then be correctly found for any/all additional field(s). See the `GhostSnapshotValueEntity` Template for an example.

All sections must be filled in.

>![NOTE]: When making changes to the templates you need to use the _Multiplayer->Force Code Generation_ menu to force a new code compilation (which will then use the updated templates).


## Defining SubType Templates

As mentioned, Subtypes are a way to define multiple templates for a given type. You use them by specifying them in the `GhostField` attribute.

```c#
using Unity.NetCode;

public struct MyComponent : Unity.Entities.IComponentData
{
    [GhostField(SubType=GhostFieldSubType.MySubType)] // <- This field uses the SubType `MySubType`.
    public float value;
    [GhostField] // <- This filed uses the default serializer Template for unquantized floats.
    public float value;
}

```

SubTypes are added to projects by implementing a partial class, `GhostFieldSubTypes`, and injecting it into the `Unity.Netcode` package by using
an [AssemblyDefinitionReference](https://docs.unity3d.com/2020.1/Documentation/Manual/class-AssemblyDefinitionReferenceImporter.html). The implementation should just
need to add new constant string literals to that class (at your own discretion) and they will be available to all your packages which already reference the `Unity.Netcode` assembly.

```c#
namespace Unity.NetCode
{
    static public partial class GhostFieldSubType
    {
        public const int MySubType = 1;
    }
}
```

Templates for the SubTypes are handled identically to other `UserDefinedTemplates`, but need to set the `SubType` field index. 
Therefore, see the above tutorial to define a Template, and note the only difference is: `SubType = GhostFieldSubType.MySubType,`.

```c#
namespace Unity.NetCode.Generators
{
    public static partial class UserDefinedTemplates
    {
        static partial void RegisterTemplates(System.Collections.Generic.List<TypeRegistryEntry> templates, string defaultRootPath)
        {
            templates.AddRange(new[]{
                new TypeRegistryEntry
                {
                    Type = "System.Single",
                    SubType = GhostFieldSubType.MySubType,
                    ...
                },
            });
        }
    }
}
```

As when using any template registration like this, you need to be careful to specify the correct parameters when defining the `GhostField` to exactly match it.
The important properties are `SubType` (of course), in addition to `Quantized` and `Smoothing`, as these can affect how the serializer code is generated from the template.

---
**IMPORTANT**:
The `Composite` parameter should always be false with subtypes, as it is assumed the Template given is the one in use for the whole type.
