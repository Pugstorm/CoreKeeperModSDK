# Transforms comparison

Many of the transform operations available in the [`UnityEngine.Transform`](xref:UnityEngine.Transform) class are available in the Entities package, with some key syntax differences. 

## Unity engine transform property equivalents

<table>
<tr>
<th>UnityEngine property</th> 
<th>ECS equivalent</th>
</tr>
<tr>
<td>

[`childCount`](xref:UnityEngine.Transform.childCount)

</td>
<td>

Use [`SystemAPI.GetBuffer`](xref:Unity.Entities.SystemAPI.GetBuffer*):<br/><br/>
<pre>
<code class="lang-c#">
int childCount(ref SystemState state, Entity e)
{
  return SystemAPI.GetBuffer<Child>(e).Length;
}
</code>
</pre>
</td>
</tr>
<tr>
<td>

[`forward`](xref:UnityEngine.Transform.forward)

</td>
<td>

Use the Mathematics package [`normalize`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.normalize.html) with [`LocalToWorld.Forward`](xref:Unity.Transforms.LocalToWorld.Forward). You can omit `normalize` if you know that the transform hierarchy doesn't have scale:<br/><br/>
<pre>
<code class="lang-c#">
float3 forward(ref SystemState state, Entity e)
{
  return math.normalize(SystemAPI.GetComponent<LocalToWorld>(e).Forward);
}
</code>
</pre>
</td>
</tr>
<tr>
<td>

[`localPosition`](xref:UnityEngine.Transform.localPosition)

</td>
<td>

Use [`LocalTransform.Position`](xref:Unity.Transforms.LocalTransform.Position):<br/><br/>
<pre>
<code class="lang-c#">
float3 localPosition(ref SystemState state, Entity e)
{
  return SystemAPI.GetComponent<LocalTransform>(e).Position;
}
</code>
</pre>
</td>
</tr>
<tr>
<td>

[`localRotation`](xref:UnityEngine.Transform.localRotation)

</td>
<td>

Use [`LocalTransform.Rotation`](xref:Unity.Transforms.LocalTransform.Rotation):<br/><br/>

<pre>
<code class="lang-c#">
quaternion localRotation(ref SystemState state, Entity e)
{
  return SystemAPI.GetComponent<LocalTransform>(e).Rotation;
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`localScale`](xref:UnityEngine.Transform.localScale)

</td>
<td>

Use [`LocalTransform.Scale`](xref:Unity.Transforms.LocalTransform.Scale) and [`PostTransformMatrix`](xref:Unity.Transforms.PostTransformMatrix):<br/><br/>

<pre>
<code class="lang-c#">
float3 localScale(ref SystemState state, Entity e)
{
  float scale = SystemAPI.GetComponent<LocalTransform>(e).Scale;
  if ( SystemAPI.HasComponent<PostTransformMatrix>(e))
  {
    // If PostTransformMatrix contains skew, returned value will be inexact,
    // and diverge from GameObjects.
    float4x4 ptm = SystemAPI.GetComponent<PostTransformMatrix>(e).Value;
    float lx = math.length(ptm.c0.xyz);
    float ly = math.length(ptm.c1.xyz);
    float lz = math.length(ptm.c2.xyz);
    return new float3(lx, ly, lz) * scale;
  }
  else
  {
    return new float3(scale, scale, scale);
  }
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`localToWorldMatrix`](xref:UnityEngine.Transform.localToWorldMatrix)

</td>
<td>

Use [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value):<br/><br/>

<pre>
<code class="lang-c#">
float4x4 localToWorldMatrix(ref SystemState state, Entity e)
{
  return SystemAPI.GetComponent<LocalToWorld>(e).Value;
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`lossyScale`](xref:UnityEngine.Transform.lossyScale)

</td>
<td>

Use [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value) and the Mathematics package [`length`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.length.html):<br/><br/>

<pre>
<code class="lang-c#">
float3 lossyScale(ref SystemState state, Entity e)
{
  // If LocalToWorld contains skew, returned value will be inexact,
  // and diverge from GameObjects.
  float4x4 l2w = SystemAPI.GetComponent<LocalToWorld>(e).Value;
  float lx = math.length(l2w.c0.xyz);
  float ly = math.length(l2w.c1.xyz);
  float lz = math.length(l2w.c2.xyz);
  return new float3(lx, ly, lz);
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`parent`](xref:UnityEngine.Transform.parent)

</td>
<td>

Use [`Parent.Value`](xref:Unity.Transforms.Parent.Value):<br/><br/>

<pre>
<code class="lang-c#">
Entity parent(ref SystemState state, Entity e)
{
  return SystemAPI.GetComponent<Parent>(e).Value;
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`position`](xref:UnityEngine.Transform.position)

</td>
<td>

Use [`LocalToWorld.Position`](xref:Unity.Transforms.LocalToWorld.Position):<br/><br/>

<pre>
<code class="lang-c#">
float3 position(ref SystemState state, Entity e)
{
  return SystemAPI.GetComponent<LocalToWorld>(e).Position;
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`right`](xref:UnityEngine.Transform.right)

</td>
<td>

Use the Mathematics package [`normalize`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.normalize.html) with [`LocalToWorld.Right`](xref:Unity.Transforms.LocalToWorld.Right). You can omit `normalize` if you know that the transform hierarchy doesn't have scale:<br/><br/>

<pre>
<code class="lang-c#">
float3 right(ref SystemState state, Entity e)
{
  return math.normalize(SystemAPI.GetComponent<LocalToWorld>(e).Right);
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`root`](xref:UnityEngine.Transform.root)

</td>
<td>

Use [`Parent.Value`](xref:Unity.Transforms.Parent.Value):<br/><br/>

<pre>
<code class="lang-c#">
Entity root(ref SystemState state, Entity e)
{
  while (SystemAPI.HasComponent<Parent>(e))
  {
    e = SystemAPI.GetComponent<Parent>(e).Value;
  }
  return e;
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`rotation`](xref:UnityEngine.Transform.rotation)

</td>
<td>

Use [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value):<br/><br/>

<pre>
<code class="lang-c#">
quaternion rotation(ref SystemState state, Entity e)
{
  return SystemAPI.GetComponent<LocalToWorld>(e).Value.Rotation();
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`up`](xref:UnityEngine.Transform.up)

</td>
<td>

Use the Mathematics package [`normalize`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.normalize.html) with [`LocalToWorld.Up`](xref:Unity.Transforms.LocalToWorld.Up). You can omit `normalize` if you know that the transform hierarchy doesn't have scale:<br/><br/>

<pre>
<code class="lang-c#">
float3 up(ref SystemState state, Entity e)
{
  return math.normalize(SystemAPI.GetComponent<LocalToWorld>(e).Up);
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`worldToLocalMatrix`](xref:UnityEngine.Transform.worldToLocalMatrix)

</td>
<td>

Use the Mathematics package [`inverse`](https://docs.unity3d.com/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.inverse.html) with [`LocalToWorld.Value`](xref:Unity.Transforms.LocalToWorld.Value). You can omit `normalize` if you know that the transform hierarchy doesn't have scale:<br/><br/>

<pre>
<code class="lang-c#">
float4x4 worldToLocalMatrix(ref SystemState state, Entity e)
{
  return math.inverse(SystemAPI.GetComponent<LocalToWorld>(e).Value);
}
</code>
</pre>

</td>
</tr>
</table>


### Properties with no equivalent

The following properties have no equivalent in the Entities package:

* [`eulerAngles`](xref:UnityEngine.Transform.eulerAngles)
* [`localEulerAngles`](xref:UnityEngine.Transform.localEulerAngles)
* [`hasChanged`](xref:UnityEngine.Transform.hasChanged)
* [`hierarchyCapacity`](xref:UnityEngine.Transform.hierarchyCapacity). Not needed. There is no limit to the number of children an entity can have.
* [`hierarchyCount`](xref:UnityEngine.Transform.hierarchyCount)

## Unity engine transform method equivalents

<table>
<tr>
<th>UnityEngine method</th> 
<th>ECS equivalent</th>
</tr>
<tr>
<td>

[`DetachChildren`](xref:UnityEngine.Transform.DetachChildren)

</td>
<td>

Use the following:<br/><br/>

<pre>
<code class="lang-c#">
void DetachChildren(ref SystemState state, Entity e)
{
  DynamicBuffer<Child> buffer = SystemAPI.GetBuffer<Child>(e);
  state.EntityManager.RemoveComponent(buffer.AsNativeArray().Reinterpret<Entity>(),
                                      ComponentType.ReadWrite<Parent>());
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`GetChild`](xref:UnityEngine.Transform.GetChild(System.Int32))

</td>
<td>

Use the following:<br/><br/>

<pre>
<code class="lang-c#">
Child child(ref SystemState state, Entity e, int index)
{
  return SystemAPI.GetBuffer<Child>(e)[index];
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`GetLocalPositionAndRotation`](https://docs.unity3d.com/ScriptReference/Transform.GetLocalPositionAndRotation.html)

</td>
<td>

Use the following:<br/><br/>

<pre>
<code class="lang-c#">
void GetLocalPositionAndRotation(ref SystemState state, Entity e, out float3 localPosition, out quaternion localRotation)
{
  LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
  localPosition = transform.Position;
  localRotation = transform.Rotation;
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`GetPositionAndRotation`](https://docs.unity3d.com/ScriptReference/Transform.GetPositionAndRotation.html)

</td>
<td>

Use the following:<br/><br/>

<pre>
<code class="lang-c#">
void GetPositionAndRotation(ref SystemState state, Entity e, out float3 position, out quaternion rotation)
{
  LocalToWorld l2w = SystemAPI.GetComponent<LocalToWorld>(e);
  position = l2w.Value.Translation();
  rotation = l2w.Value.Rotation();
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`InverseTransformDirection`](xref:UnityEngine.Transform.InverseTransformDirection(UnityEngine.Vector3))

</td>
<td>

Use the following:<br/><br/>

<pre>
<code class="lang-c#">
float3 InverseTransformDirection(ref SystemState state, Entity e, float3 direction)
{
  LocalToWorld l2w = SystemAPI.GetComponent<LocalToWorld>(e);
  return math.inverse(l2w.Value).TransformDirection(direction);
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`InverseTransformPoint`](xref:UnityEngine.Transform.InverseTransformPoint(UnityEngine.Vector3))

</td>
<td>

Use the following:<br/><br/>

<pre>
<code class="lang-c#">
float3 InverseTransformPoint(ref SystemState state, Entity e, float3 position)
{
  LocalToWorld l2w = SystemAPI.GetComponent<LocalToWorld>(e);
  return math.inverse(l2w.Value).TransformPoint(worldPoint);
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`InverseTransformVector`](xref:UnityEngine.Transform.InverseTransformVector(UnityEngine.Vector3))

</td>
<td>

Use the following:<br/><br/>

<pre>
<code class="lang-c#">
float3 InverseTransformVector(ref SystemState state, Entity e, float3 vector)
{
  return math.inverse(SystemAPI.GetComponent<LocalToWorld>(e).Value)
         .TransformDirection(vector);
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`IsChildOf`](xref:UnityEngine.Transform.IsChildOf(UnityEngine.Transform))

</td>
<td>

Use the following:<br/><br/>

<pre>
<code class="lang-c#">
bool IsChildOf(ref SystemState state, Entity e, Entity parent)
{
  return SystemAPI.HasComponent<Parent>(e)
         && SystemAPI.GetComponent<Parent>(e).Value == parent;
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`LookAt`](xref:UnityEngine.Transform.LookAt(UnityEngine.Transform))

</td>
<td>

Use the following:<br/><br/>

<pre>
<code class="lang-c#">
void LookAt(ref SystemState state, Entity e, float3 target, float3 worldUp)
{
  if (SystemAPI.HasComponent<Parent>(e))
  {
    Entity parent = SystemAPI.GetComponent<Parent>(e).Value;
    float4x4 parentL2W = SystemAPI.GetComponent<LocalToWorld>(parent).Value;
    target = math.inverse(parentL2W).TransformPoint(target);
  }
  LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
  quaternion rotation = quaternion.LookRotationSafe(target, worldUp);
  SystemAPI.SetComponent(e, transform.WithRotation(rotation));
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`Rotate`](xref:UnityEngine.Transform.Rotate(UnityEngine.Vector3))

</td>
<td>

In the Entities transform system, rotation is always expressed as a quaternion, and an angle is always in radians. There is functionality in the Mathematics package library to convert Euler angles into quaternions, and to convert degrees into radians.

With `Space.Self`, or if the entity has no parent:<br/><br/>

<pre>
<code class="lang-c#">
public void Rotate(ref SystemState state, Entity e, quaternion rotation)
{
  LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
  rotation = math.mul(rotation, transform.Rotation);
  SystemAPI.SetComponent(e, transform.WithRotation(rotation));
}
</code>
</pre>

With `Space.World`, and the entity may have a parent:<br/><br/>

<pre>
<code class="lang-c#">
void Rotate(ref SystemState state, Entity e, quaternion rotation)
{
  if (SystemAPI.HasComponent<Parent>(e))
  {
    Entity parent = SystemAPI.GetComponent<Parent>(e).Value;
    float4x4 parentL2W = SystemAPI.GetComponent<LocalToWorld>(parent).Value;
    rotation = math.inverse(parentL2W).TransformRotation(rotation);
  }
  LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(e);
  rotation = math.mul(rotation, transform.Rotation);
  SystemAPI.SetComponent(e, transform.WithRotation(rotation));
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`RotateAround`](xref:UnityEngine.Transform.RotateAround(UnityEngine.Vector3,System.Single))

</td>
<td>

Use the following:<br/><br/>

<pre>
<code class="lang-c#">
public void RotateAround(ref SystemState state, Entity e, float3 point, float3 axis, float angle)
{
  // Note: axis should be of unit length
  if (SystemAPI.HasComponent<Parent>(e))
  {
    Entity parent = SystemAPI.GetComponent<Parent>(e).Value;
    float4x4 parentL2W = SystemAPI.GetComponent<LocalToWorld>(parent).Value;
    float4x4 invParentL2W = math.inverse(parentL2W);
    point = invParentL2W.TransformPoint(point);
    axis = invParentL2W.TransformDirection(axis);
  }
  var transform = SystemAPI.GetComponent<LocalTransform>(e);
  var q = quaternion.AxisAngle(axis, angle);
  transform.Position = point + math.mul(q, transform.Position - point);
  transform.Rotation = math.mul(q, transform.Rotation);
  SystemAPI.SetComponent(e, transform);
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`SetLocalPositionAndRotation`](xref:UnityEngine.Transform.SetLocalPositionAndRotation(UnityEngine.Vector3,UnityEngine.Quaternion))

</td>
<td>

Use the following:<br/><br/>

<pre>
<code class="lang-c#">
void SetLocalPositionAndRotation(ref SystemState state, Entity e, float3 localPosition, quaternion localRotation)
{
  SystemAPI.SetComponent(e, LocalTransform.FromPositionRotation(localPosition, localRotation));
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`SetParent`](xref:UnityEngine.Transform.SetParent(UnityEngine.Transform))

</td>
<td>

Without `worldPositionStays`:<br/><br/>

<pre>
<code class="lang-c#">
void SetParent(ref SystemState state, Entity e, Entity parent)
{
  SystemAPI.SetComponent(e, new Parent { Value = parent});
}
</code>
</pre>

With `worldPositionStays`:<br/><br/>

<pre>
<code class="lang-c#">
void SetParent(ref SystemState state, Entity e, Entity parent)
{
  float4x4 childL2W = SystemAPI.GetComponent<LocalToWorld>(e).Value;
  float4x4 parentL2W = SystemAPI.GetComponent<LocalToWorld>(parent).Value;
  float4x4 temp = math.mul(math.inverse(parentL2W), childL2W);

  SystemAPI.SetComponent(e, new Parent { Value = parent});
  SystemAPI.SetComponent(e, LocalTransform.FromMatrix(temp));
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`SetPositionAndRotation`](xref:UnityEngine.Transform.SetPositionAndRotation(UnityEngine.Vector3,UnityEngine.Quaternion))

</td>
<td>

If the entity has no parent:<br/><br/>

<pre>
<code class="lang-c#">
void SetPositionAndRotationA(ref SystemState state, Entity e, float3 position, quaternion rotation)
{
  SystemAPI.SetComponent(e, LocalTransform.FromPositionRotation(position, rotation));
}
</code>
</pre>

If the entity has a parent:<br/><br/>

<pre>
<code class="lang-c#">
void SetPositionAndRotationB(ref SystemState state, Entity e, float3 position, quaternion rotation)
{
  if (SystemAPI.HasComponent<Parent>(e))
  {
    Entity parent = SystemAPI.GetComponent<Parent>(e).Value;
    float4x4 parentL2W = SystemAPI.GetComponent<LocalToWorld>(parent).Value;
    float4x4 invParentL2W = math.inverse(parentL2W);
    position = invParentL2W.TransformPoint(position);
    rotation = invParentL2W.TransformRotation(rotation);
  }
  SystemAPI.SetComponent(e, LocalTransform.FromPositionRotation(position, rotation));
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`TransformDirection`](xref:UnityEngine.Transform.TransformDirection(UnityEngine.Vector3))

</td>
<td>

Use the following:<br/><br/>

<pre>
<code class="lang-c#">
float3 TransformDirection(ref SystemState state, Entity e, float3 direction)
{
  float3 temp = SystemAPI.GetComponent<LocalToWorld>(e).Value.TransformDirection(direction);
  return temp * (math.length(direction) / math.length(temp));
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`TransformPoint`](xref:UnityEngine.Transform.TransformPoint(UnityEngine.Vector3))

</td>
<td>

Use the following:<br/><br/>

<pre>
<code class="lang-c#">
float3 TransformPoint(ref SystemState state, Entity e, float3 position)
{
  return SystemAPI.GetComponent<LocalToWorld>(e).Value.TransformPoint(position);
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`Transformvector`](xref:UnityEngine.Transform.TransformVector(UnityEngine.Vector3))

</td>
<td>

Use the following:<br/><br/>

<pre>
<code class="lang-c#">
float3 TransformVector(ref SystemState state, Entity e, float3 vector)
{
  return SystemAPI.GetComponent<LocalToWorld>(e).Value.TransformDirection(vector);
}
</code>
</pre>

</td>
</tr>
<tr>
<td>

[`Translate`](xref:UnityEngine.Transform.Translate(UnityEngine.Vector3))

</td>
<td>

With `Space.Self`, or if the entity has no parent:<br/><br/>

<pre>
<code class="lang-c#">
void Translate(ref SystemState state, Entity e, float3 translation)
{
  SystemAPI.GetComponentRW<LocalTransform>(e, false).ValueRW.Position += translation;
}
</code>
</pre>

With `Space.World`, and the entity may have a parent:<br/><br/>

<pre>
<code class="lang-c#">
void Translate(ref SystemState state, Entity e, float3 translation)
{
  if (SystemAPI.HasComponent<Parent>(e))
  {
    Entity parent = SystemAPI.GetComponent<Parent>(e).Value;
    float4x4 parentL2W = SystemAPI.GetComponent<LocalToWorld>(parent).Value;
    translation = math.inverse(parentL2W).TransformDirection(translation);
  }
  SystemAPI.GetComponentRW<LocalTransform>(e, false).ValueRW.Position += translation;
}
</code>
</pre>

</td>
</tr>
</table>

### Methods with no equivalent

The following methods have no equivalent in the Entities package:

* [`Find`](xref:UnityEngine.Transform.Find(System.String))
* [`GetSiblingIndex`](xref:UnityEngine.Transform.GetSiblingIndex). Children are in arbitrary order.
* [`SetAsFirstSibling`](xref:UnityEngine.Transform.SetAsFirstSibling). Children are in arbitrary order.
* [`SetAsLastSibling`](xref:UnityEngine.Transform.SetAsLastSibling). Children are in arbitrary order.
* [`SetSiblingIndex`](xref:UnityEngine.Transform.SetSiblingIndex(System.Int32)). Children are in arbitrary order.

## Additional resources

* [Using transforms](transforms-using.md)