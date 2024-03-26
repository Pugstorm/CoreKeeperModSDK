# CODE-GEN SPECIFICATION
## Purpose of the document
Give a clear overview of the supported feature, intents and how they should work on both source-generation side and conversion.
## Glossary
Following some naming conventions/glossary used in NetCode for Entities and code-generation stack
- `Ghost` a replicated entity
- `GhostField` attribute that can be added to a struct member, indicating that it should be serialized and the field serialization properties.
- `GhostComponent`: attribute that can be added to structs and classes, used to declare stripping and other replication properties.
- `GhostComponentVariation` (usually referred as `GhostComponentVariant`): attribute used to declare a different serialization for a component/buffer type.
- `Rpc`: a struct implementing IRcpCommand interface
- `Command`: a struct implementing ICommandData interface
## PURPOSE
The code-generation system is responsible for:
- generating serialization code for component, buffers, commands and rpcs.
- collecting and register components serializers and their variations.
- generating type information to avoid use of reflection at runtime and enforce compatibility with DOTSRuntime and Tiny (see [Empty Variant](#empty-variants) for further details).
- write registration systems for rpc and components and buffers serializers.

The collected type information are used at runtime by:
- GhostAuthoringConversion: to generate important ghost metadata and strip components from the converted entity prefab.
- GhostCollectionSystem: to build the serializer for the ghost prefabs, pre-process the entity prefab, assign the correct variants and strip components at runtime.
## CODE-GEN STACK
The main purpose of code-gen is to automatize the serialization and deserialization of replicated data. The code-gen stack is comprises of three different components:
- Roslyn C# source-generator: used to parse the C# AST and extract the type information required to build the generator code.
- Template framework: used to generate the type serialization and other glue code.
- User defined type registry: used to configure and map which template to use for various types.
## SERIALIZED TYPES
Only value types (struct) with public visibility
```c#
public struct Serialized
{
}
//This is internal and no serializer will be generated
internal struct NoInternal
{
}
//This is private and no serializer will be generated
private struct NoPrivate
{
}
```
and that implements one of the following interfaces
- IComponentData
- IBufferElementData
- IRpcCommand
- ICommandData

can be serialized / generate serialization code.

[Empty Variants](#empty-variants) are also generated for [Hybrid Components](#hybrid-components)) that have a [GhostComponent](#ghost-component-attribute).

### MANDATORY REQUIREMENT:
One and only one of these interfaces can be present at the same time.
```c#
public struct Invalid1 : IComponentData, IRpcCommand
{
    public int Value1;
}
public struct Invalid2 : IComponentData, ICommandData
{
    public Unity.NetCode.NetworkTick Tick {get;set}
    public int Value1;
}
public struct Invalid3 : IComponentData, IBufferElementData
{
    public int Value1;
}
public struct Invalid4: IBufferElementData, ICommandData
{
    public Unity.NetCode.NetworkTick Tick {get;set}
    public int Value1;
}
public struct Invalid5 : IBufferElementData, IRpcCommand
{
    public int Value1;
}
```
An error, indicating what type and which interfaces is implementing, must be reported in case a struct implements multiple interfaces present in this set at the same type.
Generic interfaces like `public struct MyTest : MyInterface<AnotherType>` are not supported.
## HYBRID COMPONENTS
All classes that inherits from `UnityEngine.Component` or `UnityEngine.MonoBehavior` are inspected if the following conditions are true:
- A `GhostComponentAttribute` attribute has been added to the class declaration
- The class have public visibility.

**Hybrid components are not serialized**.

The code-generation collect and process the hybrid components by inserting them in the [EmptyVariant](#empty-variants) collection. The collected information are used at runtime to:
- strip from the prefab converted components and buffers based on the `GhostComponentAttribute` properties
- pre-processing some type information to avoid the use of reflections at runtime (in particular the VariantType and ComponentType, see [GhostComponentVariant](#variant-generation)).

### Why we extract the type information for Monobehaviour and Components
Using reflection, while discouraged, is still valid in the Editor or when targeting a classic Unity build (Hybrid). However, when targeting DOTS-Runtime and Tiny, it is not possible to use reflection to gather attributes or other type info at runtime.
Although Monobehaviours are not DOTS-Runtime compatible (they are not included in the build), some code-paths, responsible for the most to associates the variants to use, are shared by both entity and hybrid components.
**Because the first requires DOTS-Runtime compatibility**, that make necessary to have these data available for both component types.

## NAMESPACES AND INNER CLASSES
Namespaces, with multiple level of nesting, are supported
```c#
namespace N1, N2, N3
{
    struct A
    {
    }
}
```
Nested/inner struct types, with multiple level of nesting are supported.
```c#
namespace N1, N2
{
    struct A
    {
        struct B
        {
            struct C
            {
            }
        }
    }
}
```

The full typename (used internally by the generator) follow the C# roslyn naming standards, comprehends all the namespaces and declaring types and it is in following format:
`N1.N2.N3.A+B+C`

### Limitation on names and reserved keywords
- `__GHOST__` is a reserved prefix and keyword. It cannot be present in fields and/or struct names.
- `__GHOST` and `__COMMAND` are reserved prefixes. No namespace, class, struct or member name can start with this prefix.
Compilation error will be reported in all the above cases.
Class/Struct within the same with the same name but in different namespaces are valid and supported, in whiting the same or different assembly.
There are no specific restriction on name length, but shorter names should be preferred in general.
## SUPPORTED FIELDS TYPES
### PRIMITIVE TYPES
- `bool`
- `int`
- `uint`
- `short`
- `ushort`
- `sbyte`
- `byte`
- `long`
- `ulong`
- `float`
- `enums` with any integral numeric type specifier.

#### Notes about unsupported basic types:
- `char` is not supported (for various reasons).
### COMPOSITE FIELDS AND TYPE HIERARCHIES
If the serialized type (ICommand, IRpcCommand, IComponent, IBufferElement) contains a field that is a struct, the field type hierarchy is recursively traversed and fields collected for serialization.

```c#
struct ChildChildStuct
{
    public int x;
    public int y;
    public [GhostField(SendData=false)] int z;
}
struct ChildStuct
{
    public int a;
    public ChildChildStuct b;
}

public struct MySerialisedStruct : IComponentData
{
    public [GhostField] int field1;
    public [GhostField] ChildStuct field2;
}
```
The resulting flatten serialized data will looks like
```c#
struct SerializedData
{
    int field1;
    int field2_a;
    int field2_b_x;
    int field2_b_y;
}
```
If `GhostFieldAttribute` are supported by the serialized class (see [Ghost Field](#ghost-field-attribute)) the child struct traversal will only collects the fields for witch:
- the field does not have `[GhostField]`
- the field does have a `[GhostField]` attribute and the `SendData` property is set to true.
### Unity.Mathematics types
- Unity.Mathematics.float2
- Unity.Mathematics.float3
- Unity.Mathematics.float4
- Unity.Mathematics.quaternion

Unity.Engine.Vector3,Unity.Engine.Vector4,Unity.Engine.Quaternion, etc... are not supported.
### Fixed Strings
- FixedString32Bytes
- FixedString64Bytes
- FixedString128Bytes
- FixedString512Bytes
- FixedString1024Bytes
### Other special fields types
- **Replicated Entities references**.

The entity references are `weak` and can result in a `Entity.Null` by the receiver in case the ghost instance cannot be resolved.

|Sender|Receiver |
|------|---------|
| Entity.Null | Entity.Null|
| Valid  | Valid (if ghost exist) |
|        | Entity.Null (if ghost not exist) |

### Requirements for fields, properties and accessors
Only public declared fields and properties are serialized. Private and static fields are ignored.
```c#
public struct A
{
public int F1
public float P1 {get;set;}
float P2 {public get; public set;}
private int F2 // <-- ignored
internal int F3 // <-- ignored
static int SF1 // <-- ignored
}
```
Property accessors like `this[int index]` or properties that return the same declaring type are not serialized.
```c#
struct A
{
    public int this[int index]
    {
        get { return this.x; }
        set { x = value; }
    }
    public A ThisIsNotSerialized
    {
       get { return new  ThisIsNotSerialized(); }
    }
}
```
## TYPE CONFIGURATION
All types for which serialization should be generated MUST be registered in the TypeRegistry. NetCode provide a default set of types (all primitives and some mathematics) already configured (see [Supported Primitive Types](#supported-primitive-types) section);
Users can provides their own types and rules by implementing the `Unity.NetCode.Generators.UserDefinedTemplates` method.
```c#
namespace Unity.NetCode.Generators
{
    public static partial class UserDefinedTemplates
    {
        //Add there your definition
    }
```
The source-generator layer is responsible for parsing that method, extract the configuration and update the type registry.
Following some convention and limitation that users should adhere to.
### Typename naming convention
Primitive types should be referred using the C# special naming convention (to be changed)

| Type   | Name  |
| -----  | ----- |
| bool   | System_Boolean |
| sbyte  | System_SByte   |
| byte   | System_Byte    |
| short  | System_Int16   |
| ushort | System_UInt16  |
| int    | System_Int32   |
| uint   | System_UInt32  |
| long   | System_Int64   |
| ulong  | System_UInt64  |
| float  | System_Single  |

All type names should always be declared using a fully declared typename (ex: MyNamespace.MyType), prefixed by the containing namespaces and declaring class (if any).
```c#
new TypeRegistryEntry
{
    Type = "System.Int32",
    ...
},
new TypeRegistryEntry
{
    Type = "Unity.Transform.float3",
    ...
}
```
### Syntax restrictions for UserDefinedTemplates implementation
The `UserDefinedTemplates.RegisterTemplates` method code implementation is subject to some restrictions, due to the way we are parsing the method.
- Only creating and assigning TypeRegistryEntry struct are allowed.
- Only compile time constants are allowed.

It is possible to add to the template param multiple templates using multiple assignment like the following:
```c#
templates.Add(new new TypeRegistryEntry
{
    Type = "Unity.Mathematics.float3",
    SubType = GhostFieldSubType.Translation2D,
    Quantized = true,
    Smoothing = SmoothingAction.InterpolateAndExtrapolate,
    SupportCommand = false,
    Composite = false,
    Template = "Custom.Translation2d",
    TemplateOverride = "",
});
```
It is also valid to use the AddRange method. The following syntax is also a valid example.
```c#
templates.AddRange(new[]{
    new TypeRegistryEntry
    {
        Type = "Unity.Mathematics.float3",
        SubType = GhostFieldSubType.Translation2D,
        Quantized = true,
        Smoothing = SmoothingAction.InterpolateAndExtrapolate,
        SupportCommand = false,
        Composite = false,
        Template = "Custom.Translation2d",
        TemplateOverride = "",
    },
    new TypeRegistryEntry
    {
        Type = "Unity.Mathematics.quaternion",
        SubType = GhostFieldSubType.Rotation2D,
        Quantized = true,
        Smoothing = SmoothingAction.InterpolateAndExtrapolate,
        SupportCommand = false,
        Composite = false,
        Template = "Custom.Rotation2d",
        TemplateOverride = "",
    },
});
```
For `Template` and `TemplateOverride` paths, simple string interpolations are supported. For example:
```c#
Template = $"{MyPath}/Path/ToMyFile"
Template = $"{MyPath}/{Other}/ToMyFile"
```
All the parameters in string interpolation must be compile time constant.
### SubType declaration
Sub types can be specified using the GhostField attribute `SubType` property.
```c#
public struct A
{
    public [GhostField(SubType=GhostFieldSubType.MyType)] int myType;
}
```
At code-generation time the sub-type act as a filter/selector: we try to lookup from the type registry a matching
type registration with the same sub-type value (and other options, like quantization, etc).

User can assign the subtype value by:
- Setting an explicit int value
- Using their own enums or other custom compile tine literals
- By extending the partial class `GhostFieldSubType` using asmref. (**Recommend approach**)
The only requirement is that this value must be a compile time constant value. This is enforced already by C#.
## GHOST COMPONENT ATTRIBUTE
```
[GhostComponent(
    PrefabType = [All, Server, InterpolatedClient, PredictedClient, AllPredicted]
    SendTypeOptimization = [GhostSendType.Predicted,GhostSendType.Interpolated, GhostSendType.All]
    OwnerSendType = [SendToOwnerType.SendToOwner,SendToOwnerType.SendToNotOwner, SendToOwnerType.All]
)]
```
The `GhostComponentAttribute` attribute can be added to:
- structs that implement the `IComponentData` or `IBufferElementData` interface
- structs that implement the `ICommandData` interface (because it is a `IBufferElementData`)
- structs that have a `GhostComponentVariation` attribute, see [GhostComponentVariation](#variant-generation)
- classes that inherits from `UnityEngine.Monobehavior` or more generically `UnityEngine.Component`, see [Hybrid Components](#hybrid-components).

In any other cases, the `GhostComponentAttribute` is ignored and not inspected by code-generation at compile time.

```c#
[GhostComponent(...)]
public struct Component : IComponentData
{
..
}
[GhostComponent(...)]
public struct Buffer : IBufferElementData
{
}
//Allow stripping
[GhostComponent(...)]
public class MyHybridComponent : MonoBehaviour
{
}
//Allow stripping
[GhostComponent(...)]
public class MyCommand : ICommandData
{
}
```

### SPECIAL RULES FOR ICOMMANDDATA
When a `GhostComponent` attribute is added to struct that implement `ICommandData` interface:
- the `OwnerSendType` flag `SendToOwnerType.SendToOwner` cannot be set. The rule is automatically enforced by code-gen. A compilation warning is reported to advise that the flag has been removed.
- the `SendToChild` flag is ignored and always considered set to false.
## GHOST FIELD ATTRIBUTE
```c#
class GhostFieldAttribute
{
    public int Quantization = -1;
    public SmoothingAction Smoothing = Clamp
    public int SubType = 0;
    public float MaxSmoothingDistance = 0f;
    public bool Composite = false;
    public bool SendData = true;
}
```
The attribute can be added to any member of structs that implement either
- `IComponentData` interface
- `IBufferElementData` interface
- `ICommandData` interface (because it is a IBufferElementData)

### RULES
- `Composite`: can only be applied to member which type is a struct. For primitive types the flag is ignored and a compile time warning is reported.
  **The composite flag ONLY affect the change mask calculation**.
- `MaxSmoothingDistance`: used only if Smoothing is set to Interpolate or InterpolateExtrapolate.
- `Quantization`: only applied to floating point field and property members. For integer types is ignored.

For `IBufferElementData` and `ICommandData` some further rules apply:
- Smoothing action is always ignored (and `Clamp` used instead)

### GHOST FIELD PROPERTIES INHERITANCE RULES
When a `GhostFieldAttribute` is added to a member which type is composite (struct) type, the parent `GhostFieldAtttribute` is "inherited" by all the composite type members hierarchically, with the following rules:
- The `SubType` property is never inherited (is always 0 by default)
- If the field type that does have a `GhostFieldAttribute`, the applicable parent attribute properties (based on the type) are used instead.
- If a `GhostFieldAttribute` is present they take precedence and override the parent ones with the following restrictions:
  - child Quantization if greater than 0
  - child Composite if it is set to true if the type is not a primitive type
  - child MaxSmoothingDistance if greater that 0
  - child Smoothing if set to a value different than Clamp

Example:

```c#
public struct Child
{
    public int intField;
    [GhostField] public float useParentQuantization;
    [GhostField(Quantization=5000)] public int useLocalQuantization;
}

public struct Parent : IComponentData
{
    [GhostField(Quantization=1000)] public Child child;
    [GhostField(SubType=5, Quantization=700, Smooting=Interpolate)] public float3 customFloat3;
}
```
For simplicity in this example let's suppose exist a registered definition for float3 in the type registry that match the `SubType=5`, with a template that just serialize the `x` field.

The final results is equivalent to the following flatten struct:

```c#
struct SerializedData
{
    [GhostField] public int child_intField; //<-- it is a integer, quantization does not matter
    [GhostField(Quantization=1000)] public int child_useParentQuantization; //<-- this use parent quantization 1000
    [GhostField(Quantization=5000)] public int child_useLocalQuantization; //<-- this use local quantization 5000
    //this use the parent quantization and interpolation and the custom template that serialize the x field. Each field (in that case x) still have subtype 0 and match the default float implementation
    [GhostField(Quantization=700, Smooting=Interpolate)] public float customFloat3.x;
}
```
## RPC SERIALIZATION
### Syntax
```C#
public struct MyRpc : IRpcCommand
{
public int field1;
public float field2;
[DontSerializeForCommand] public int field3;
..
}
public struct EmptyRpc : IRpcCommand
{
..
}
//Inheritane is supported
public interface ExtRpcCommand : IRpcCommand
{
..
}
public struct MyExtRpc : ExtRpcCommand
{
public int field1;
public float field2;
[DontSerializeForCommand] public int field3;
..
}
```
### REQUIREMENT
- must be a struct
- must be declared as public
- must implement the IRPCommand interface
- must not contains managed types (but it is not enforced yet)
- all serialized fields must public. Private,internal and static fields are ignored
### CONDITIONS TO SKIP CODE-GENERATION
- If the rpc serializer class symbol is already present in the current assembly, the serialization code MUST not be generated.
- If a `NetCodeDisableCommandCodeGenAttribute` attribute is added to the struct definition, no serialization code will be generated.
```c#
[NetCodeDisableCommandCodeGen]
public struct NoCodeGenerateRpc : IRpcCommand
{
public int field1;
public int field2;
..
}
```
### SERIALIZED FIELDS
An RPC can have any number of fields (empty structs are valid).
- All public members (fields and properties) are serialized by default, in the order of declaration.
- Private and static fields are ignored.
- `GhostFieldAttribute` attributes are ignored.
- If a field present a `DontSerializeForCommandAttribute` attribute the field is not serialized.

| FEATURE | SUPPORTED |
| ------- | --------- |
| quantization | no |
| interpolation | no |
| extrapolation | no |
| delta compression | no |
#### SUPPORTED FIELD TYPES
- primitive types
- float2, float3, float4 and quaternion, in their un-quantized version
- FixedString supported by NetCode default templates, or used defined ones
- Entity references.
- types declared `UserDefinedTemplates` that have the `SupportCommand` property set to `true`.
### GHOST COMPONENT SUPPORT
`GhostComponent` attribute is not supported and ignored if present.
### REQUIRED TEMPLATE REGIONS
| REGION | MANDATORY |
| ------ | --------- |
|`COMMAND_READ`| yes |
|`COMMAND_WRITE`| yes |
## COMPONENT AND BUFFER SERIALIZATION
### SYNTAX
```C#
public struct Component : IComponentData
{
    [GhostField] public int FieldA;
    [GhostField] public int FieldB;
    [GhostField(SendData=false)] public int NotSerialized2;
    public int NotSerialized2;
    [GhostField(Quantization=1000)] public float FieldB;
    ...
}

public struct ValidBuffer : IBufferElementData
{
    [GhostField] public int FieldA;
    [GhostField] public int FieldB;
    [GhostField(Quantization=1000)] public float FieldB;
    ...
}

public struct InvalidBuffer : IBufferElementData
{
    public int FieldA; //<-- This will trigger compile error
    [GhostField] public int FieldB;
    [GhostField(Quantization=1000)] public float FieldB;
    ...
}
```

### REQUIREMENT
- must be a struct
- must be declared as public
- must implement `IComponentData` or `IBufferElementData` interface

Serialization code is generated for structs implementing the `IComponentData` interface when at least one field with a `GhostField` attribute is present.
A Special rules apply for buffers (see [GHOST_ALL_FIELDS_OR_NOTHING](#ghost-all-fields-or_nothing-rule)).

Because of the above requirement serialization code MUST not be generated for the following component types:
- SharedComponent
- Tag Component
- Chunk Component

### CONDITIONS TO SKIP SERIALIZATION CODE-GENERATION
- If the generated serializer variation class symbol is already present in the current assembly.
- If all component/buffer fields does not have a `GhostField` attribute.

### SERIALIZED FIELDS
- Private and static members and properties are ignored.
- Only public fields that present a `[GhostField]` attribute and `SendData` property set to `true` (default).

| FEATURE | SUPPORTED | BUFFER |
| ------- | --------- | ------ |
| quantization | yes | yes |
| interpolation | yes | no |
| extrapolation | yes | no |
| delta compression | yes | yes |
| huffman coding | yes | yes |

For buffer, Interpolation and Extrapolation are not supported. The `GhostField.Smoothing` options is ignored and forced to to `Clamp` for the declared field and its children (in case of composite).

#### SUPPORTED FIELD TYPES
- all the default supported primitive types
- float2, float3, float4 and quaternion (quantized/unquantized)
- fixed strings
- Entity reference
- Any types declared in the `UserDefinedTemplates`

### GHOST ALL FIELDS OR NOTHING RULE
For structs implementing the `IBufferElementInterface` there are only two possible `GhostField` assignment configurations:
- A `GhostField` attribute is assigned to ALL FIELDS
- NO FIELDS should have a `GhostField` attribute.

Furthermore in case all fields are marked with `GhostField` attribute the following rules also applies:
- `GhostField.SendData` MUST be set to `true`.

A compiler error is raised in case:
- `GhostField` attribute is not added to all the members and at least one is present.
- `GhostField.SendData` is set to `false`.

The reason for this rule is that we need to properly initialize new element when they are added to the collection. And because no reasonable default (apart 0) can be applied we preferred to have all values set.

### GHOST COMPONENT SUPPORT
`GhostComponent` attribute MUST be supported for both `IComponentData` and `IBufferElementData`.

### TEMPLATE REGIONS
Templates for type that can be used and serialized in buffers and components requires the following regions:

| REGION | MANDATORY | CAN BE EMPTY |
| ------ | --------- | ------------ |
|`GHOST_READ`| x |
|`GHOST_WRITE`| x |
|`GHOST_PREDICT`| x | x |
|`GHOST_COPY_TO_SNAPSHOT`| x |
|`GHOST_COPY_FROM_SNAPSHOT`| x |
|`GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE`| x |
|`GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE_SETUP`| | x |
|`GHOST_RESTORE_FROM_BACKUP`| x |
|`GHOST_CALCULATE_CHANGE_MASK_ZERO`| x |
|`GHOST_CALCULATE_CHANGE_MASK`| x |
|`GHOST_REPORT_PREDICTION_ERROR`| x |
|`GHOST_GET_PREDICTION_ERROR_NAME`| x |
## COMMANDS CODE GENERATION
Strucs implementing `ICommandData` are by default serialized into the `command stream` and sent from the clients to the server (see [COMMAND SERIALIZATION](#command-serialization)).

There are situation when you may wants this commands be received also by the other players in the sessions (for example: remote player prediction). Structs implementing `ICommandData` can
be serialised into the ghost snapshot as `input buffers` (see [COMMAND BUFFER SERIALIZATION](#command-buffer-serialization) when **all fields** are marked with a `GhostField` attribute. Differently from normal `IBufferElementData`, clients will never receive their own `input buffers`
as part of the server snapshot (the `GhostComponet.OwnerSendType` options is implicity forced to `SendToOwnerType.SendToNotOwner`).

A very important aspect to consider when `ICommandData` are sent as both `commands` (client->server) and `input buffers` (server -> clients) is the difference in the serialized data. In particular for `input buffers`,
because `GhostField` properties are used to generate the serialization code, **floating point fields can be quantized.** If the struct contains floating point fields, the data received by server and the other remote players can be different.

```c#
public struct MyCommand : ICommnaData
{
  [GhostField] public Unity.NetCode.NetworkTick Tick {get;set;}
  [GhostField] public float AllTheSame;
  [GhostField(Quantization=100)] public float TheSameOnServer;
}
```
The `AllTheSame` field value is going to be same on the client, server and other remote players. But the `TheSameOnServer` field is going to be different:
- the server will receive the `unquatized` value (sent as part of the command stream).
- the other remote players, will receive the `quantized` data.

If the data is used in the prediction loop, you can expect some slightly different prediction results because of the quantization. In most cases the difference is not noticiable (is still a prediction, so an approximation by defiition).

## COMMAND SERIALIZATION
### SYNTAX
```C#
public struct MyCommand : ICommandData
{
public int field1;
public float field2;
[DontSerializeForCommand] public int field3;
..
}
//Inheritance is supported
public interface ExtCommandData : ICommandData
{
..
}
public struct MyExtCommand : ExtCommandData
{
public int field1;
public float field2;
[DontSerializeForCommand] public int field3;
..
}
```
### REQUIREMENT
- must be a struct
- must be declared as public
- must implement the ICommandData interface
- must not contains managed types (but it is not enforced yet)
- all serialized fields must public. Private,internal and static fields are ignored,
### CONDITIONS TO SKIP CODE-GENERATION
- If the command serializer class symbol is already present in the current assembly, the serialization code MUST not be generated.
- If a `NetCodeDisableCommandCodeGenAttribute` attribute is added to the struct definition, no serialization code will be generated.
```c#
[NetCodeDisableCommandCodeGen]
public struct NotGenerated : ICommandData
{
public int field1;
public int field2;
..
}
```
### SERIALIZED FIELDS
- All public members (fields and properties) are serialized by default, in the order of declaration.
- Private and static fields are ignored.
- `GhostFieldAttribute` attributes are ignored.
- If a field present a `DontSerializeForCommandAttribute` attribute the field is not serialized.

| FEATURE | SUPPORTED |
| ------- | --------- |
| quantization | no |
| interpolation | no |
| extrapolation | no |
| delta compression | yes, see dedicated section for more details |
#### SUPPORTED FIELD TYPES
- primitive types
- float2, float3, float4 and quaternion, in their un-quantized version
- FixedString
- Entity references.
- types declared `UserDefinedTemplates` that have the `SupportCommand` property set to `true`.
### COMMANDS DELTA COMPRESSION
Command are delta compressed and packed (using the current compression model) when they are sent from client to server. The delta compression works like this:
- The first command and tick are serialized without encoding/compression.
- The following N commands and ticks (based on the window, default is 3) in the buffer are delta compressed against the first command, that it is used as baseline.
### GHOST COMPONENT SUPPORT
`GhostComponent` attribute is supported, with some properties restriction:
-`PrefabType` is supported. The underling dynamic buffer stripped from the ghost accordingly.
-`SendTypeOptimization` is supported.
-`OwnerSendType` is ignored (not applicable)
### TEMPLATE REGIONS
| REGION | MANDATORY |
| ------ | --------- |
|`COMMAND_READ`| x |
|`COMMAND_WRITE`| x |
|`COMMAND_READ_PACKED`| x |
|`COMMAND_WRITE_PACKED`| x |
## COMMAND BUFFER SERIALIZATION
### SYNTAX
```C#
public struct RemotePlayerCommand : ICommandData
{
[GhostField] public int field1;
[GhostField] public float field2;
[GhostField(Quantization=1000)] public float field2;
[DontSerializeForCommand][GhostField]public int field3;
..
}
```
Input buffer serialization is enabled by marking one or more command fields with a `GhostField` attribute. When enabled the command dynamic buffer is serialized ad part of the
server ghost snapshot and sent to other remote players.

Being `ICommandData` interface and `IBufferElementData` interface, the code-generation requirements and rules are the same used for [buffers](#component-and-buffer-generation) code-generation.
### REQUIREMENT
- must be a struct
- must be declared as public
- must implement the ICommandData interface
- ALL FIELDS MUST ba marked the `GhostField` attribute (see the [GHOST_ALL_FIELDS_OR_NOTHING](#ghost-all-fields-or-nothing-rule))

### CONDITIONS TO SKIP CODE-GENERATION
- If the command serializer class symbol is already present in the current assembly, the serialization code MUST not be generated.
### SERIALIZED FIELDS
- Private and static members and properties are ignored.
- All public fields and properties (they all have `GhostField` attribute).

| FEATURE | SUPPORTED |
| ------- | --------- |
| quantization | yes |
| interpolation | no |
| extrapolation | no |
| delta compression | yes |
| huffman encoding | yes |

Interpolation and Extrapolation are not supported. If the `GhostField.Smoothing` options is in practice ignored and forced to to `Clamp`, for the declared field and its children (in case of composite).
#### SUPPORTED FIELD TYPES
- primitive types
- float2, float3, float4 and quaternion (quantized/unquantized)
- FixedString
- Entity references.
- all types declared `UserDefinedTemplates`

### GHOST COMPONENT SUPPORT
`GhostComponent` attribute is supported, with some properties restriction:
-`PrefabType` is supported. The underling dynamic buffer stripped from the ghost accordingly.
-`SendTypeOptimization` is supported.
-`OwnerSendType` is restricted to `SendToOwnerType.SendToNotOwner`. Code-gen enforce that rule by modifying the generated flag and warn the user to change the setup.
## VARIANT GENERATION
### SYNTAX
```C#
[GhostComponentVariation(typeof(ORIGINAL_TYPE))]
struct MySerializationVariant
{
    [GhostField] public int FieldA;
    [GhostField] public int FieldB;
    [GhostField(Quantization=1000)] public float FieldB;
    ...
}
```
The declared `GhostComponentVariation` types are only used as a mean to instruct the code-generation to build a different serialization for
the `ORIGINAL_TYPE` type and should not be used for other purposes.

### REQUIREMENT
- must be a struct
- must be declared as public
- must present `GhostComponentVariation` attribute
- must declare the type for which the variant should be generated for (the `ORIGINAL_TYPE`)
- cannot have fields that are not declared in the original `ORIGINAL_TYPE` declaration.

The `ORIGINAL_TYPE` type must be either:
- a public struct implementing an `IComponentData` or `IBufferElementData` interface.
- a public [Hybrid Component](#hybrid-components)

It is **not mandatory** for the variant to declare all original `ORIGINAL_TYPE` fields in the following cases:
- the `ORIGINAL_TYPE` implements the IComponentData interface
- the `ORIGINAL_TYPE` is an [Hybrid Component](#hybrid-components)

A compiler error is raised in case:
- a `GhostComponentVariation` declares a member (property or field) that is not present in the original type declaration.
- the `ORIGINAL_TYPE` is not public
- a `DontSupportPrefabOverridesAttribute` attribute is present in the `ORIGINAL_TYPE` declaration
```c#
[DontSupportPrefabOverridesAttribute]
public struct OriginalType : IComponentData
{
}
```
### CONDITIONS TO SKIP SERIALIZATION CODE-GENERATION
- If the generated serializer variation class symbol is already present in the current assembly.

### SPECIAL RULES FOR BUFFERS
If the `ORIGINAL_TYPE` is `IBufferElementData`, all the `IBufferElementData` restrictions apply also for the variation declaration.
In particular, the [GHOST_ALL_FIELDS_OR_NOTHING](#ghost-all-fields-or-nothing-rule) rule is enforced:
- The `GhostComponentVariation` must declare all fields.
- All fields must be annotated with a `GhostField` attribute.

A compiler error is raised in case a `GhostField` attribute is not added to all the members in that case.

### GHOST COMPONENT SUPPORT
A `GhostComponentVariation` declaration allows to add a `GhostComponent` attribute to the declared struct.
```c#
//GhostComponentVariation permit to use the GhostComponent attribute
[GhostComponentVariation(typeof(ORIGINAL_TYPE))]
[GhostComponent(...)]
struct MySerializationVariant
{
    [GhostField] public int FieldA;
    [GhostField] public int FieldB;
    [GhostField(Quantization=1000)] public float FieldB;
    ...
}
```
The `GhostComponent` attribute properties are reflected in the generate code for serialisation variation in the same way it does for normal components and buffers.

### SERIALIZED FIELDS
See [COMPONENT AND BUFFERS](#component-and-buffer-generation) generation.
### TEMPLATE REGIONS
See [COMPONENT AND BUFFERS](#component-and-buffer-generation) generation.
## EMPTY VARIANTS
Are considered `EMPTY VARIANTS`:
- [component, buffer](#component-and-buffer-serialization), [command buffer](#command-buffer-serialization) that:
  - does not have any serialized field (no `[GhostField]` are present)
  - have a `GhostComponent` attribute
- [variant](#variant-generation) that:
  - either does not have fields or no `[GhostField]` are present.
- [hybrid component](#hybrid-components) that:
  - have a `GhostComponent` attribute.

`EMPTY VARIANTS` does not generated serialization code and are used to track some important piece of type information used by the NetCode runtime:
- The variant type: the class/struct type that declare the variant: avoid reflection at runtime
- The component type for which the variant is declared for: avoid reflection at runtime
- The variant hash.: associate the variant in the inspector and other usages.
- The `GhostComponent.PrefabType` property: strip component from prefab at both runtime and/or conversion

### CREATE EMPTY VARIANT USING a `GhostComponent` attribute
By adding a [GhostComponent](#ghost-component-attribute) attribute to a [serialized type](#serialized-types)) or [hybrid component](#hybrid-components) without marking any field as serialized.
```c#
[GhostComponent(...)]
struct MyEmptyComponentVariant : IComponentData
{
    public int FieldA;
    public int FieldB;
    ...
}
[GhostComponent(...)]
struct MyEmptyBufferVariant : IBufferElementData
{
    public int FieldA;
    public int FieldB;
    ...
}
[GhostComponent(...)]
struct MyEmptyCommandVariant : ICommandData
{
    public int FieldA;
    public int FieldB;
    ...
}
```
#### Important things to note:
- The [GHOST ALL OR NOTHING](#ghost-all-fields-or-nothing-rule) rule is respected for both buffers and commands.
- Adding a `GhostComponentAttribute` to a `ICommandData` struct does not affect its serialization behaviour. In particular:
  - It will still generate the serialization code for the command
  - It will not be sent to other players (so no buffer serialization)

### CREATE EMPTY VARIANT USING `GhostComponentVariation`
- By declaring a `GhostComponentVariation` that have no fields, or no `GhostField`, or all `GhostField.SendData` are set to false. The `GhostComponentAttribute` is not mandatory.
```c#
[GhostComponentVariation(typeof(MyStruct))
[GhostComponent(...)] //<-- THIS IS OPTIONAL
struct MyStructEmptyVariant : IComponentData
{
    public int FieldA;
    public int FieldB;
    ...
}
[GhostComponentVariation(typeof(MyStruct))
[GhostComponent(...)] //<-- THIS IS OPTIONAL
struct MyStructEmptyVariant : IComponentData
{
}
[GhostComponentVariation(typeof(MyBuffer))
[GhostComponent(...)] //<-- THIS IS OPTIONAL
struct MyBufferEmptyVariant
{
    public int FieldA;
    public int FieldB;
}
[GhostComponentVariation(typeof(MyBuffer))
[GhostComponent(...)] //<-- THIS IS OPTIONAL
struct MyBufferEmptyVariant
{
}
[GhostComponentVariation(typeof(MyBuffer))
[GhostComponent(...)] //<-- THIS IS OPTIONAL
struct MyBufferEmptyVariant
{
    [GhostField(SendData=false)]public int FieldA;
    [GhostField(SendData=false)]public int FieldB;
}
```
- By declaring a `GhostComponentVariation` for an [hybrid component](#hybrid-components) generate an empty variant.
```c#
[GhostComponentVariation(typeof(MyHybridComponent))
[GhostComponent(...)] //<-- THIS IS OPTIONAL
struct MyHybridEmptyVariant
{
}
```
Not that for hybrid component it is not necessary to specify any fields, since by default cannot be serialized.

