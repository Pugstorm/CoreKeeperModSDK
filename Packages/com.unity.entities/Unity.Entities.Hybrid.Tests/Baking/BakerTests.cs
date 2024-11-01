using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Tests;
using Unity.Scenes.Editor.Tests;
using UnityEngine;

namespace Unity.Entities.Hybrid.Tests.Baking
{
    static class UnsafeHashSetExt
    {
        public static T First<T>(this UnsafeHashSet<T> @this)
            where T : unmanaged, IEquatable<T>
        {
            using var enumerator = @this.GetEnumerator();
            enumerator.MoveNext();
            return enumerator.Current;
        }
    }

    public class DefaultAuthoringComponent : MonoBehaviour { public int Field; }
    public class Authoring_WithGameObjectField : MonoBehaviour { public GameObject GameObjectField; }
    public class Authoring_AddComponentByComponentType_PrimaryEntity : MonoBehaviour { public int Field; }
    public class Authoring_AddComponentByComponentType_SecondaryValidEntity : MonoBehaviour { public int Field; }
    public class Authoring_AddComponentByMultipleComponentTypes_SecondaryValidEntity : MonoBehaviour { public int Field; }
    public class Authoring_AddComponentGeneric_PrimaryValidEntity : MonoBehaviour { public int Field; }
    public class Authoring_AddChunkComponentGeneric_SecondaryValidEntity : MonoBehaviour { public int Field; }
    public class Authoring_AddSharedComponentGeneric_SecondaryValidEntity : MonoBehaviour { public int Field; }
    public class Authoring_AddUnmanagedSharedComponentGeneric_SecondaryValidEntity : MonoBehaviour { public int Field; }
    public class Authoring_AddBufferGeneric_SecondaryValidEntity : MonoBehaviour { public int Field; }
    public class Authoring_BaseClass : MonoBehaviour { public int Field; }
    public class Authoring_DerivedFromBaseClass : Authoring_BaseClass { public int Field2; }
    public class Authoring_DerivedFromDerivedClass : Authoring_DerivedFromBaseClass { public int Field3; }
    public abstract class Authoring_Abstract : MonoBehaviour
    {
        public int Field;
        public abstract void DoSomething();
    }
    public class Authoring_DerivedFromAbstract : Authoring_Abstract
    {
        public int Field2;
        public override void DoSomething() { UnityEngine.Debug.Log("Derived"); }
    }

    public class Authoring_DerivedFromBaseClass_DefinedBeforeBase : Authoring_BaseClass_DefinedAfterDerived {}

    public class Authoring_BaseClass_DefinedAfterDerived : MonoBehaviour
    {
        public List<string> BakerTypeOrder = new List<string>();
    }

    struct ComponentTest1 : IComponentData
    {
        public int Field;
    }

    struct SharedComponentTest1 : ISharedComponentData
    {
        public int Field;
    }

    struct GetComponentTest1 : IComponentData
    {
        public int Field;
        public int GUID;
    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    class ManagedComponent : IComponentData
    {
        public int Field;
    }
#endif

    struct ComponentTest2 : IComponentData
    {
        public int Field;
    }

    struct ComponentTest3 : IComponentData
    {
        public int Field;
    }

    struct ComponentTest4 : IComponentData
    {
        public int Field;
    }

    struct UnmanagedSharedComponent : ISharedComponentData
    {
        public int Field;
    }

    struct IntElement : IBufferElementData
    {
        public static implicit operator int(IntElement e)
        {
            return e.Value;
        }

        public static implicit operator IntElement(int e)
        {
            return new IntElement {Value = e};
        }

        public int Value;
    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    class AddManagedComponentBakerTest : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent authoring)
        {
            AddComponentObject(GetEntity(authoring, TransformUsageFlags.None), new ManagedComponent() { Field = 2});
            AddComponentObject(CreateAdditionalEntity(TransformUsageFlags.None), new ManagedComponent(){Field = 4});
        }
    }
#endif

    class AddComponent_WithUnregisteredEntity_Baker : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            var manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            AddComponent(manager.CreateEntity(), new ComponentTest1());
        }
    }
#if !UNITY_DISABLE_MANAGED_COMPONENTS
    class AddComponentObject_WithUnregisteredEntity_Baker : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            var manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            AddComponentObject(manager.CreateEntity(), new ManagedComponent());
        }
    }
#endif

    class BakerDependsOnInheritedType : Baker<AuthoringComponentTestInheritance>
    {
        public override void Bake(AuthoringComponentTestInheritance component)
        {
            GetComponentsInParent<AuthoringComponentBaseTest>();
        }
    }

    // test for
    //     void AddComponent(ComponentType componentType)
    class AddComponentByComponentType_PrimaryEntity : Baker<Authoring_AddComponentByComponentType_PrimaryEntity>
    {
        public override void Bake(Authoring_AddComponentByComponentType_PrimaryEntity component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, ComponentType.ReadWrite<ComponentTest1>());
        }
    }

    // test for
    //     void AddComponent(Entity entity, ComponentType componentType)
    //
    // with valid secondary entity
    class AddComponentByComponentType_SecondaryValidEntity : Baker<Authoring_AddComponentByComponentType_SecondaryValidEntity>
    {
        public override void Bake(Authoring_AddComponentByComponentType_SecondaryValidEntity component)
        {
            AddComponent(CreateAdditionalEntity(TransformUsageFlags.None), ComponentType.ReadWrite<ComponentTest1>());
        }
    }

    // test for
    //     void AddComponent(Entity entity, ComponentType componentType)
    //
    // with invalid secondary entity
    class AddComponentByComponentType_SecondaryInvalidEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            AddComponent(Entity.Null, ComponentType.ReadWrite<ComponentTest1>());
        }
    }

    // test for
    //     void AddComponent<T>()
    public sealed class AddComponentGeneric_PrimaryValidEntity : Baker<Authoring_AddComponentGeneric_PrimaryValidEntity>
    {
        public override void Bake(Authoring_AddComponentGeneric_PrimaryValidEntity component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<ComponentTest1>(entity, new ComponentTest1() {Field = 3});
        }
    }

    // test for
    //     void AddComponent<T>(Entity entity)
    //
    // with valid secondary entity
    class AddComponentGeneric_SecondaryValidEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            AddComponent<ComponentTest1>(CreateAdditionalEntity(TransformUsageFlags.None), new ComponentTest1() {Field = 3});
        }
    }

    // test for
    //     void AddComponent<T>(Entity entity)
    //
    // with invalid secondary entity
    class AddComponentGeneric_SecondaryInvalidEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            AddComponent<ComponentTest1>(Entity.Null, new ComponentTest1() {Field = 3});
        }
    }

    // test for
    //     void AddComponent(ComponentTypes types)
    class AddComponentByMultipleComponentTypes_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            ComponentTypeSet componentTypeSet = new ComponentTypeSet(ComponentType.ReadWrite<ComponentTest1>(), ComponentType.ReadWrite<ComponentTest2>());
            AddComponent(entity, componentTypeSet);
        }
    }

    // test for
    //     void AddComponent(Entity entity, ComponentTypes types)
    //
    // with valid secondary entity
    class AddComponentByMultipleComponentTypes_SecondaryValidEntity : Baker<Authoring_AddComponentByMultipleComponentTypes_SecondaryValidEntity>
    {
        public override void Bake(Authoring_AddComponentByMultipleComponentTypes_SecondaryValidEntity component)
        {
            ComponentTypeSet componentTypeSet = new ComponentTypeSet(ComponentType.ReadWrite<ComponentTest1>(), ComponentType.ReadWrite<ComponentTest2>());
            AddComponent(CreateAdditionalEntity(TransformUsageFlags.None), componentTypeSet);
        }
    }

    // test for
    //     void AddComponent(Entity entity, ComponentTypes types)
    //
    // with invalid secondary entity
    class AddComponentByMultipleComponentTypes_SecondaryInvalidEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            ComponentTypeSet componentTypeSet = new ComponentTypeSet(ComponentType.ReadWrite<ComponentTest1>(), ComponentType.ReadWrite<ComponentTest2>());
            AddComponent(Entity.Null, componentTypeSet);
        }
    }

    // test for
    //     void AddChunkComponent<T>() where T : struct, IComponentData
    class AddChunkComponentGeneric_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, ComponentType.ChunkComponent<ComponentTest1>());
        }
    }

    // test for
    //     void AddChunkComponent<T>(Entity entity)
    //
    // with valid secondary entity
    class AddChunkComponentGeneric_SecondaryValidEntity : Baker<Authoring_AddChunkComponentGeneric_SecondaryValidEntity>
    {
        public override void Bake(Authoring_AddChunkComponentGeneric_SecondaryValidEntity component)
        {
            AddComponent(CreateAdditionalEntity(TransformUsageFlags.None), ComponentType.ChunkComponent<ComponentTest1>());
        }
    }

    // test for
    //     void AddChunkComponent<T>(Entity entity)
    //
    // with invalid secondary entity
    class AddChunkComponentGeneric_SecondaryInvalidEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            AddComponent(Entity.Null, ComponentType.ChunkComponent<ComponentTest1>());
        }
    }

    // test for
    //     void AddSharedComponent<T>(T componentData)
    class AddSharedComponentGeneric_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddSharedComponentManaged<UnmanagedSharedComponent>(entity, new UnmanagedSharedComponent { Field = 1 });
        }
    }

    // test for
    //     void AddSharedComponentManaged<T>(Entity entity)
    //
    // with valid secondary entity
    class AddSharedComponentGeneric_SecondaryValidEntity : Baker<Authoring_AddSharedComponentGeneric_SecondaryValidEntity>
    {
        public override void Bake(Authoring_AddSharedComponentGeneric_SecondaryValidEntity component)
        {
            AddSharedComponentManaged<UnmanagedSharedComponent>(CreateAdditionalEntity(TransformUsageFlags.None), new UnmanagedSharedComponent { Field = 3 });
        }
    }

    // test for
    //     void AddSharedComponentManaged<T>(Entity entity) where T : struct, IComponentData
    //
    // with invalid secondary entity
    class AddSharedComponentGeneric_SecondaryInvalidEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            AddSharedComponentManaged<UnmanagedSharedComponent>(Entity.Null, new UnmanagedSharedComponent { Field = 3 });
        }
    }

    // test for
    //     void AddSharedComponent<T>(Entity entity, T componentData)
    class AddUnmanagedSharedComponentGeneric_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddSharedComponent<UnmanagedSharedComponent>(entity, new UnmanagedSharedComponent { Field = 1 });
        }
    }

    // test for
    //     void AddSharedComponent<T>(Entity entity, T componentData)
    //
    // with valid secondary entity
    class AddUnmanagedSharedComponentGeneric_SecondaryValidEntity : Baker<Authoring_AddUnmanagedSharedComponentGeneric_SecondaryValidEntity>
    {
        public override void Bake(Authoring_AddUnmanagedSharedComponentGeneric_SecondaryValidEntity component)
        {
            AddSharedComponent<UnmanagedSharedComponent>(CreateAdditionalEntity(TransformUsageFlags.None), new UnmanagedSharedComponent { Field = 3 });
        }
    }

    // test for
    //     void AddSharedComponent<T>(Entity entity, T componentData)
    //
    // with invalid secondary entity
    class AddUnmanagedSharedComponentGeneric_SecondaryInvalidEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            AddSharedComponent<UnmanagedSharedComponent>(Entity.Null, new UnmanagedSharedComponent { Field = 3 });
        }
    }

    // test for
    //     void AppendToBuffer<T>()
    class AppendToBufferGeneric_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddBuffer<IntElement>(entity);
            AppendToBuffer(entity, new IntElement{Value = 1});
            AppendToBuffer(entity, new IntElement{Value = 2});
            AppendToBuffer(entity, new IntElement{Value = 3});
        }
    }

    // test for
    //     void AppendToBuffer<T>(Entity entity)
    //
    // with valid secondary entity
    class AppendToBufferGeneric_SecondaryValidEntity : Baker<Authoring_AddBufferGeneric_SecondaryValidEntity>
    {
        public override void Bake(Authoring_AddBufferGeneric_SecondaryValidEntity component)
        {
            var entity = CreateAdditionalEntity(TransformUsageFlags.None);
            AddBuffer<IntElement>(entity);
            AppendToBuffer(entity, new IntElement{Value = 1});
            AppendToBuffer(entity, new IntElement{Value = 2});
            AppendToBuffer(entity, new IntElement{Value = 3});
        }
    }

    // test for
    //     void AppendToBuffer<T>(Entity entity)
    //
    // with invalid secondary entity
    class AppendToBufferGeneric_SecondaryInvalidEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            AppendToBuffer(Entity.Null, new IntElement{Value = 1});
            AppendToBuffer(Entity.Null, new IntElement{Value = 2});
            AppendToBuffer(Entity.Null, new IntElement{Value = 3});
        }
    }

    // test for
    //     void SetBuffer<T>()
    class SetBufferGeneric_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddBuffer<IntElement>(entity);
            DynamicBuffer<IntElement> buffer = SetBuffer<IntElement>(entity);
            buffer.CopyFrom(new IntElement[] { 1, 2, 3 });
        }
    }

    // test for
    //     void SetBuffer<T>(Entity entity)
    //
    // with valid secondary entity
    class SetBufferGeneric_SecondaryValidEntity : Baker<Authoring_AddBufferGeneric_SecondaryValidEntity>
    {
        public override void Bake(Authoring_AddBufferGeneric_SecondaryValidEntity component)
        {
            var entity = CreateAdditionalEntity(TransformUsageFlags.None);
            AddBuffer<IntElement>(entity);
            DynamicBuffer<IntElement> buffer = SetBuffer<IntElement>(entity);
            buffer.CopyFrom(new IntElement[] { 1, 2, 3 });
        }
    }

    // test for
    //     void SetBuffer<T>(Entity entity)
    //
    // with invalid secondary entity
    class SetBufferGeneric_SecondaryInvalidEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            DynamicBuffer<IntElement> buffer = SetBuffer<IntElement>(Entity.Null);
            buffer.CopyFrom(new IntElement[] { 1, 2, 3 });
        }
    }

    // test for
    //     void AddBuffer<T>()
    class AddBufferGeneric_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            buffer.CopyFrom(new IntElement[] { 1, 2, 3 });
        }
    }

    // test for
    //     void AddBuffer<T>(Entity entity)
    //
    // with valid secondary entity
    class AddBufferGeneric_SecondaryValidEntity : Baker<Authoring_AddBufferGeneric_SecondaryValidEntity>
    {
        public override void Bake(Authoring_AddBufferGeneric_SecondaryValidEntity component)
        {
            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(CreateAdditionalEntity(TransformUsageFlags.None));
            buffer.CopyFrom(new IntElement[] { 1, 2, 3 });
        }
    }

    // test for
    //     void AddBuffer<T>(Entity entity)
    //
    // with invalid secondary entity
    class AddBufferGeneric_SecondaryInvalidEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(Entity.Null);
            buffer.CopyFrom(new IntElement[] { 1, 2, 3 });
        }
    }

    // test for
    //     void AddComponent<T>()
    public sealed class AddDuplicateComponent_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<ComponentTest1>(entity, new ComponentTest1() {Field = 3});
            AddComponent<ComponentTest1>(entity, new ComponentTest1() {Field = 4});
        }
    }

    // test for
    //     void GetComponent<T>()
    public sealed class GetComponent_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponent<Collider>();
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponent<T>(Component)
    public sealed class GetComponent_Component : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponent<Collider>(component);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponent<T>(GameObject)
    public sealed class GetComponent_GameObject : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponent<Collider>(component.gameObject);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponent<T>(Component)
    public sealed class GetComponent_ComponentNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            Component nullComponent = null;
            var found = GetComponent<Collider>(nullComponent);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponent<T>(GameObject)
    public sealed class GetComponent_GameObjectNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            GameObject nullGo = null;
            var found = GetComponent<Collider>(nullGo);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponents<T>()
    public sealed class GetComponents_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponents<Collider>();
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponents<T>(Component)
    public sealed class GetComponents_Component : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponents<Collider>(component);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponents<T>(GameObject)
    public sealed class GetComponents_GameObject : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponents<Collider>(component.gameObject);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponents<T>(List)
    public sealed class GetComponents_PrimaryEntity_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<Collider> found = new List<Collider>();
            GetComponents<Collider>(found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponents<T>(Component, List)
    public sealed class GetComponents_Component_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<Collider> found = new List<Collider>();
            GetComponents<Collider>(component, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponents<T>(GameObject, List)
    public sealed class GetComponents_GameObject_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<Collider> found = new List<Collider>();
            GetComponents<Collider>(component.gameObject, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponents<T>(GameObject, List)
    public sealed class GetComponents_GameObjectNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<Collider> found = new List<Collider>();
            GameObject nullGo = null;
            GetComponents<Collider>(nullGo, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponents<T>(Component, List)
    public sealed class GetComponents_ComponentNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<Collider> found = new List<Collider>();
            Component nullComponent = null;
            GetComponents<Collider>(nullComponent, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponents<T>(GameObject, List)
    public sealed class GetComponents_GameObjectNull_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<Collider> found = new List<Collider>();
            GameObject nullGo = null;
            GetComponents<Collider>(nullGo, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponents<T>(Component, List)
    public sealed class GetComponents_ComponentNull_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<Collider> found = new List<Collider>();
            Component nullComponent = null;
            GetComponents<Collider>(nullComponent, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentInParent<T>()
    public sealed class GetComponentInParent_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponentInParent<Collider>();
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponentInParent<T>(Component)
    public sealed class GetComponentInParent_Component : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponentInParent<Collider>(component);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponentInParent<T>(GameObject)
    public sealed class GetComponentInParent_GameObject : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponentInParent<Collider>(component.gameObject);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponentInParent<T>(Component)
    public sealed class GetComponentInParent_ComponentNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            Component nullComponent = null;
            var found = GetComponentInParent<Collider>(nullComponent);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponentInParent<T>(GameObject)
    public sealed class GetComponentInParent_GameObjectNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            GameObject nullGameObject = null;
            var found = GetComponentInParent<Collider>(nullGameObject);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponentsInParent<T>()
    public sealed class GetComponentsInParent_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponentsInParent<Collider>();
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInParent<T>(Component)
    public sealed class GetComponentsInParent_Component : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponentsInParent<Collider>(component);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInParent<T>(GameObject)
    public sealed class GetComponentsInParent_GameObject : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponentsInParent<Collider>(component.gameObject);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInParent<T>(Component)
    public sealed class GetComponentsInParent_ComponentNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            Component nullComponent = null;
            var found = GetComponentsInParent<Collider>(nullComponent);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInParent<T>(GameObject)
    public sealed class GetComponentsInParent_GameObjectNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            GameObject nullGameObject = null;
            var found = GetComponentsInParent<Collider>(nullGameObject);
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInParent<T>(List)
    public sealed class GetComponentsInParent_PrimaryEntity_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<Collider> found = new List<Collider>();
            GetComponentsInParent<Collider>(found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInParent<T>(Component, List)
    public sealed class GetComponentsInParent_Component_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<Collider> found = new List<Collider>();
            GetComponentsInParent<Collider>(component, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInParent<T>(GameObject, List)
    public sealed class GetComponentsInParent_GameObject_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<Collider> found = new List<Collider>();
            GetComponentsInParent<Collider>(component.gameObject, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInParent<T>(Component, List)
    public sealed class GetComponentsInParent_ComponentNull_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            Component nullComponent = null;
            List<Collider> found = new List<Collider>();
            GetComponentsInParent<Collider>(nullComponent, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInParent<T>(GameObject, List)
    public sealed class GetComponentsInParent_GameObjectNull_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            GameObject nullGameObject = null;
            List<Collider> found = new List<Collider>();
            GetComponentsInParent<Collider>(nullGameObject, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentInChildren<T>(Component)
    public sealed class GetComponentInChildren_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponentInChildren<Collider>();
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponentInChildren<T>(Component)
    public sealed class GetComponentInChildren_Component : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponentInChildren<Collider>(component);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponentInChildren<T>(GameObject)
    public sealed class GetComponentInChildren_GameObject : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            var found = GetComponentInChildren<Collider>(component.gameObject);
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponentInChildren<T>(Component)
    public sealed class GetComponentInChildren_ComponentNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            Component nullComponent = null;
            var found = GetComponentInChildren<Collider>(nullComponent);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponentInChildren<T>(GameObject)
    public sealed class GetComponentInChildren_GameObjectNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            GameObject nullGo = null;
            var found = GetComponentInChildren<Collider>(nullGo);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetComponentsInChildren<T>()
    public sealed class GetComponentsInChildren_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponentsInChildren<Collider>();
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInChildren<T>(GameObject)
    public sealed class GetComponentsInChildren_GameObject : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponentsInChildren<Collider>(component.gameObject);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInChildren<T>(Component)
    public sealed class GetComponentsInChildren_Component : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetComponentsInChildren<Collider>(component);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInChildren<T>(GameObject)
    public sealed class GetComponentsInChildren_GameObjectNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            GameObject nullGo = null;
            var found = GetComponentsInChildren<Collider>(nullGo);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInChildren<T>(Component)
    public sealed class GetComponentsInChildren_ComponentNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            Component nullComponent = null;
            var found = GetComponentsInChildren<Collider>(nullComponent);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInChildren<T>(List)
    public sealed class GetComponentsInChildren_PrimaryEntity_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<Collider> found = new List<Collider>();
            GetComponentsInChildren<Collider>(found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInChildren<T>(GameObject, List)
    public sealed class GetComponentsInChildren_GameObject_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<Collider> found = new List<Collider>();
            GetComponentsInChildren<Collider>(component.gameObject, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInChildren<T>(Component, List)
    public sealed class GetComponentsInChildren_Component_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<Collider> found = new List<Collider>();
            GetComponentsInChildren<Collider>(component, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInChildren<T>(GameObject, List)
    public sealed class GetComponentsInChildren_GameObjectNull_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            GameObject nullGo = null;
            List<Collider> found = new List<Collider>();
            GetComponentsInChildren<Collider>(nullGo, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetComponentsInChildren<T>(Component, List)
    public sealed class GetComponentsInChildren_ComponentNull_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            Component nullComponent = null;
            List<Collider> found = new List<Collider>();
            GetComponentsInChildren<Collider>(nullComponent, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetParent()
    public sealed class GetParent_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetParent();
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetParent(Component)
    public sealed class GetParent_Component : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetParent(component);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetParent(GameObject)
    public sealed class GetParent_GameObject : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetParent(component.gameObject);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetParent(Component)
    public sealed class GetParent_ComponentNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            Component nullComponent = null;
            var found = GetParent(nullComponent);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetParent(GameObject)
    public sealed class GetParent_GameObjectNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            GameObject nullGameObject = null;
            var found = GetParent(nullGameObject);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = (found != null ? 1 : 0),
                GUID = found != null ? found.GetInstanceID() : 0
            });
        }
    }

        // test for
    //     void GetParents()
    public sealed class GetParents_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetParents();
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetParents(Component)
    public sealed class GetParents_Component : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetParents(component);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetParents(GameObject)
    public sealed class GetParents_GameObject : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetParents(component.gameObject);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetParents(Component)
    public sealed class GetParents_ComponentNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            Component nullComponent = null;
            var found = GetParents(nullComponent);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetParents(GameObject)
    public sealed class GetParents_GameObjectNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            GameObject nullGameObject = null;
            var found = GetParents(nullGameObject);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetParent<T>(List)
    public sealed class GetParents_PrimaryEntity_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<GameObject> found = new List<GameObject>();
            GetParents(found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetParents(Component, List)
    public sealed class GetParents_Component_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<GameObject> found = new List<GameObject>();
            GetParents(component, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetParents(GameObject, List)
    public sealed class GetParents_GameObject_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<GameObject> found = new List<GameObject>();
            GetParents(component.gameObject, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetParents(Component, List)
    public sealed class GetParents_ComponentNull_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            Component nullComponent = null;
            List<GameObject> found = new List<GameObject>();
            GetParents(nullComponent, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetParents(GameObject, List)
    public sealed class GetParents_GameObjectNull_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            GameObject nullGameObject = null;
            List<GameObject> found = new List<GameObject>();
            GetParents(nullGameObject, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetChild()
    public sealed class GetChild_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public static int QueryIndex;
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            int count = GetChildCount();
            AddComponent(entity, new GetComponentTest1()
            {
                Field = count,
                GUID = count > 0 ? GetChild(QueryIndex).GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetChild(GameObject)
    public sealed class GetChild_GameObject : Baker<DefaultAuthoringComponent>
    {
        public static int QueryIndex;
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            int count = GetChildCount(component.gameObject);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = count,
                GUID = count > 0 ? GetChild(component.gameObject, QueryIndex).GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetChild(Component)
    public sealed class GetChild_Component : Baker<DefaultAuthoringComponent>
    {
        public static int QueryIndex;
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            int count = GetChildCount(component);
            AddComponent(entity, new GetComponentTest1()
            {
                Field = count,
                GUID = count > 0 ? GetChild(component, QueryIndex).GetInstanceID() : 0
            });
        }
    }

    // test for
    //     void GetChild(GameObject)
    public sealed class GetChild_GameObjectNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            GameObject nullGo = null;
            AddComponent(entity, new GetComponentTest1()
            {
                Field = GetChildCount(),
                GUID = GetChild(nullGo, 0).GetInstanceID()
            });
        }
    }

    // test for
    //     void GetChild(Component)
    public sealed class GetChild_ComponentNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            Component nullComponent = null;
            AddComponent(entity, new GetComponentTest1()
            {
                Field = GetChildCount(),
                GUID = GetChild(nullComponent, 0).GetInstanceID()
            });
        }
    }

    // test for
    //     void GetChildren()
    public sealed class GetChildren_PrimaryEntity : Baker<DefaultAuthoringComponent>
    {
        public static bool Recursive;
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetChildren(Recursive);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetChildren(GameObject)
    public sealed class GetChildren_GameObject : Baker<DefaultAuthoringComponent>
    {
        public static bool Recursive;
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetChildren(component.gameObject, Recursive);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetChildren(Component)
    public sealed class GetChildren_Component : Baker<DefaultAuthoringComponent>
    {
        public static bool Recursive;
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var found = GetChildren(component, Recursive);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetChildren(GameObject)
    public sealed class GetChildren_GameObjectNull : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            GameObject nullGo = null;
            var found = GetChildren(nullGo);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetChildren(Component)
    public sealed class GetChildren_ComponentNull : Baker<DefaultAuthoringComponent>
    {
        public static bool Recursive;
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            Component nullComponent = null;
            var found = GetChildren(nullComponent);
            AddComponent(entity, new ComponentTest1() {Field = found.Length});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetChildren(List)
    public sealed class GetChildren_PrimaryEntity_PassingList : Baker<DefaultAuthoringComponent>
    {
        public static bool Recursive;
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<GameObject> found = new List<GameObject>();
            GetChildren(found, Recursive);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetChildren(GameObject, List)
    public sealed class GetChildren_GameObject_PassingList : Baker<DefaultAuthoringComponent>
    {
        public static bool Recursive;
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<GameObject> found = new List<GameObject>();
            GetChildren(component.gameObject, found, Recursive);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetChildren(Component, List)
    public sealed class GetChildren_Component_PassingList : Baker<DefaultAuthoringComponent>
    {
        public static bool Recursive;
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            List<GameObject> found = new List<GameObject>();
            GetChildren(component, found, Recursive);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetChildren<T>(GameObject, List)
    public sealed class GetChildren_GameObjectNull_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            GameObject nullGo = null;
            List<GameObject> found = new List<GameObject>();
            GetChildren(nullGo, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }

    // test for
    //     void GetChildren<T>(Component, List)
    public sealed class GetChildren_ComponentNull_PassingList : Baker<DefaultAuthoringComponent>
    {
        public override void Bake(DefaultAuthoringComponent component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            Component nullComponent = null;
            List<GameObject> found = new List<GameObject>();
            GetChildren(nullComponent, found);
            AddComponent(entity, new ComponentTest1() {Field = found.Count});

            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            foreach (var obj in found)
            {
                buffer.Add(obj.GetInstanceID());
            }
        }
    }


    // test for
    //  Base Class (Base Class)
    class BaseClassBaker : Baker<Authoring_BaseClass>
    {
        public override void Bake(Authoring_BaseClass component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<ComponentTest1>(entity);
        }
    }

    // test for
    //  Base Class (Derived Class)
    class DerivedClassBaker : Baker<Authoring_DerivedFromBaseClass>
    {
        public override void Bake(Authoring_DerivedFromBaseClass component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<ComponentTest2>(entity);
        }
    }

    // test for
    //  Base Class (Derived Derived Class)
    class DerivedDerivedClassBaker : Baker<Authoring_DerivedFromDerivedClass>
    {
        public override void Bake(Authoring_DerivedFromDerivedClass component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<ComponentTest3>(entity);
        }
    }

    // test for
    //  Base Class (Base Class)
    [BakeDerivedTypes]
    class BaseClassBaker_WithAttribute : Baker<Authoring_BaseClass>
    {
        public override void Bake(Authoring_BaseClass component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<ComponentTest1>(entity);
        }
    }

    // test for
    //  Base Class (Derived Class)
    [BakeDerivedTypes]
    class DerivedClassBaker_WithAttribute : Baker<Authoring_DerivedFromBaseClass>
    {
        public override void Bake(Authoring_DerivedFromBaseClass component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<ComponentTest2>(entity);
        }
    }

    // test for
    //  Abstract Class (Abstract Class)
    class AbstractClassBaker : Baker<Authoring_Abstract>
    {
        public override void Bake(Authoring_Abstract component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<ComponentTest1>(entity);
        }
    }

    // test for
    //  Abstract Class (Derived Class)
    class DerivedFromAbstractClassBaker : Baker<Authoring_DerivedFromAbstract>
    {
        public override void Bake(Authoring_DerivedFromAbstract component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<ComponentTest2>(entity);
        }
    }

    // test for
    //  Abstract Class (Abstract Class)
    [BakeDerivedTypes]
    class AbstractClassBaker_WithAttribute : Baker<Authoring_Abstract>
    {
        public override void Bake(Authoring_Abstract component)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<ComponentTest1>(entity);
        }
    }

    class DerivedBakerDefinedBeforeBaseBaker : Baker<Authoring_DerivedFromBaseClass_DefinedBeforeBase>
    {
        public override void Bake(Authoring_DerivedFromBaseClass_DefinedBeforeBase authoring)
        {
            authoring.BakerTypeOrder.Add(nameof(DerivedBakerDefinedBeforeBaseBaker));
        }
    }

    [BakeDerivedTypes]
    class BaseBakerDefinedAfterDerivedBaker : Baker<Authoring_BaseClass_DefinedAfterDerived>
    {
        public override void Bake(Authoring_BaseClass_DefinedAfterDerived authoring)
        {
            authoring.BakerTypeOrder.Add(nameof(BaseBakerDefinedAfterDerivedBaker));
        }
    }

    public class BakerTests : BakingSystemFixtureBase
    {
        private BakingSystem m_BakingSystem;
        private GameObject m_Prefab;
        private GameObject m_Prefab1;
        private GameObject m_PrefabWithAbtractMB;
        private bool m_PreviousBakingState;
        private TestLiveConversionSettings m_Settings;

        readonly System.Text.RegularExpressions.Regex kNullEntityException = new System.Text.RegularExpressions.Regex("InvalidOperationException: Entity Entity.Null doesn't belong to the current authoring component.");
        readonly System.Text.RegularExpressions.Regex kNumberedEntityException = new System.Text.RegularExpressions.Regex(@"InvalidOperationException: Entity Entity\(\d+:\d+\) doesn't belong to the current authoring component.");
        readonly System.Text.RegularExpressions.Regex kCreateAdditionalEntitiesInvalidArrayException = new System.Text.RegularExpressions.Regex("ArgumentException: outputEntities is not valid or empty");

        [SetUp]
        public override void Setup()
        {
            m_Settings.Setup(true);
            base.Setup();

            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();

            var blobAssetStore = new BlobAssetStore(128);
            var bakingSettings = MakeDefaultSettings();
            bakingSettings.BlobAssetStore = blobAssetStore;

            m_BakingSystem.BakingSettings = bakingSettings;

            m_Manager = World.EntityManager;
            m_Prefab = InstantiatePrefab("Prefab");
            m_Prefab1 = InstantiatePrefab("Prefab");
            m_PrefabWithAbtractMB = InstantiatePrefab("Prefab_AbstractMB");
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            if (m_BakingSystem != null)
            {
                var assetStore = m_BakingSystem.BlobAssetStore;
                if (assetStore.IsCreated)
                    assetStore.Dispose();
            }
            m_BakingSystem = null;
            m_Settings.TearDown();
        }

        T GetBakedSingleton<T>() where T : unmanaged, IComponentData
        {
            Assert.AreEqual(1, m_BakingSystem.EntityManager.UniversalQuery.CalculateEntityCount());
            var query = new EntityQueryBuilder(m_BakingSystem.WorldUpdateAllocator).WithAll<T>().Build(m_BakingSystem);
            return query.GetSingleton<T>();
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void Baker_AddComponentObjectGeneric()
        {
            var component = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddManagedComponentBakerTest)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(component).ToNativeArray(Allocator.Temp);
                Assert.IsTrue(m_Manager.HasComponent<ManagedComponent>(entities[0]));
                Assert.IsTrue(m_Manager.HasComponent<ManagedComponent>(entities[1]));

                var comp1 = m_Manager.GetComponentData<ManagedComponent>(entities[0]);
                var comp2 = m_Manager.GetComponentData<ManagedComponent>(entities[1]);

                Assert.IsTrue(comp1.Field == 2);
                Assert.IsTrue(comp2.Field == 4);
            }
        }
#endif

        [DisableAutoCreation]
        public class BakerWithPrefabReference : Baker<Authoring_WithGameObjectField>
        {
            public override void Bake(Authoring_WithGameObjectField authoring)
            {
                GetEntity(authoring.GameObjectField, TransformUsageFlags.None);
            }
        }

        [Test]
        public void Reference_WithPrefab_PrefabGetsBaked()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(BakerTests.BakerWithPrefabReference));
            var com = m_Prefab.AddComponent<Authoring_WithGameObjectField>();
            com.GameObjectField = LoadPrefab("Prefab");

            BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

            var entity = m_BakingSystem.GetEntity(com.GameObjectField);
            Assert.IsTrue(m_Manager.HasComponent<Prefab>(entity));

        }


        [DisableAutoCreation]
        public class BakerWithRegisterPrefabsForBaking : Baker<Authoring_WithGameObjectField>
        {
            public override void Bake(Authoring_WithGameObjectField authoring)
            {
                RegisterPrefabForBaking(authoring.GameObjectField);
            }
        }

        [Test]
        public void RegisterPrefabsForBaking_PrefabGetsBaked()
        {
            using var overrideBake = new BakerDataUtility.OverrideBakers(true, typeof(BakerTests.BakerWithRegisterPrefabsForBaking));
            var com = m_Prefab.AddComponent<Authoring_WithGameObjectField>();
            com.GameObjectField = LoadPrefab("Prefab");

            BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

            var entity = m_BakingSystem.GetEntity(com.GameObjectField);
            Assert.IsTrue(m_Manager.HasComponent<Prefab>(entity));

        }

        [Test]
        public void AddComponent_WithUnregistredEntity_Throws_Test()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddComponent_WithUnregisteredEntity_Baker)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, kNumberedEntityException);

                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void AddComponentObject_WithUnregistredEntity_Throws_Test()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddComponentObject_WithUnregisteredEntity_Baker)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, kNumberedEntityException);

                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }
#endif

        [Test]
        public void AddComponent_WithValidEntity_Doesnt_Throw()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddComponent_WithUnregisteredEntity_Baker)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, kNumberedEntityException);

                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void Baker_Destroy_Entities_On_ConvertGameObjects_CodePath_Test()
        {
            var componentEntity = m_Prefab.AddComponent<Authoring_AddComponentByComponentType_PrimaryEntity>();
            var componentOnAdditionalEntities = m_Prefab.AddComponent<Authoring_AddComponentByComponentType_SecondaryValidEntity>();

            BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

            var entities1 = m_BakingSystem.GetEntitiesForBakers(componentEntity).ToNativeArray(Allocator.Temp);
            Assert.IsTrue(entities1.Length == 1);

            var entities2 = m_BakingSystem.GetEntitiesForBakers(componentOnAdditionalEntities).ToNativeArray(Allocator.Temp);
            Assert.IsTrue(entities2.Length == 2);

            EntitiesAssert.Contains(m_Manager, EntityMatch.Partial<ComponentTest1>(entities1[0]));
            EntitiesAssert.Contains(m_Manager, EntityMatch.Partial<ComponentTest1>(entities2[1]));

            //@TODO: Discuss with team if this is really the behaviour we want. Seems like common case is to directly generate entitites into the live game world with the ConvertGameObjects codepath
            //Test if fully rebaking the same gameobject, internally on the ConvertGameObjects path, will destroy the primary and additional entities before recreating new ones.
            BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

            entities1 = m_BakingSystem.GetEntitiesForBakers(componentEntity).ToNativeArray(Allocator.Temp);
            Assert.IsTrue(entities1.Length == 1);

            entities2 = m_BakingSystem.GetEntitiesForBakers(componentOnAdditionalEntities).ToNativeArray(Allocator.Temp);
            Assert.IsTrue(entities2.Length == 2);

            EntitiesAssert.Contains(m_Manager, EntityMatch.Partial<ComponentTest1>(entities1[0]));
            EntitiesAssert.Contains(m_Manager, EntityMatch.Partial<ComponentTest1>(entities2[1]));
        }

        [Test]
        public void AddComponentByComponentType()
        {
            var component0 = m_Prefab.AddComponent<Authoring_AddComponentByComponentType_PrimaryEntity>();
            var component1 = m_Prefab.AddComponent<Authoring_AddComponentByComponentType_SecondaryValidEntity>();

            BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

            // jiv - m_BakingSystem.GetEntities(component0) will fail.
            var entities = m_BakingSystem.GetEntitiesForBakers(component1).ToNativeArray(Allocator.Temp);
            Assert.IsTrue(entities.Length == 2);

            Assert.IsTrue(m_Manager.HasComponent<ComponentTest1>(entities[0]));
            Assert.IsTrue(m_Manager.HasComponent<ComponentTest1>(entities[1]));
        }

        [Test]
        public void AddComponentByComponentType_Throws()
        {
            var component0 = m_Prefab.AddComponent<Authoring_AddComponentByComponentType_PrimaryEntity>();
            var component1 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddComponentByComponentType_SecondaryInvalidEntity), typeof(AddComponentByComponentType_PrimaryEntity)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, kNullEntityException);

                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void AddComponentGeneric()
        {
            var component0 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddComponentGeneric_SecondaryValidEntity)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(component0).ToNativeArray(Allocator.Temp);
                Assert.IsTrue(entities.Length == 2);

                Assert.IsTrue(m_Manager.HasComponent<ComponentTest1>(entities[1]));

                ComponentTest1 comp1 = m_Manager.GetComponentData<ComponentTest1>(entities[1]);
                Assert.IsTrue(comp1.Field == 3);
            }
        }

        [Test]
        public void AddComponentGeneric_Throws()
        {
            var component1 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddComponentGeneric_SecondaryInvalidEntity)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, kNullEntityException);

                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void AddComponentByMultipleComponentTypes()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();
            var component1 = m_Prefab.AddComponent<Authoring_AddComponentByMultipleComponentTypes_SecondaryValidEntity>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddComponentByMultipleComponentTypes_PrimaryEntity),
                typeof(AddComponentByMultipleComponentTypes_SecondaryValidEntity)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(component1).ToNativeArray(Allocator.Temp);
                Assert.IsTrue(entities.Length == 2);

                Assert.IsTrue(m_Manager.HasComponent<ComponentTest1>(entities[0]));
                Assert.IsTrue(m_Manager.HasComponent<ComponentTest2>(entities[0]));

                Assert.IsTrue(m_Manager.HasComponent<ComponentTest1>(entities[1]));
                Assert.IsTrue(m_Manager.HasComponent<ComponentTest2>(entities[1]));
            }
        }

        [Test]
        public void AddComponentByMultipleComponentTypes_Throws()
        {
            var component1 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true,
                typeof(AddComponentByMultipleComponentTypes_SecondaryInvalidEntity)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, kNullEntityException);

                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void AddChunkComponentGeneric()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();
            var component1 = m_Prefab.AddComponent<Authoring_AddChunkComponentGeneric_SecondaryValidEntity>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddChunkComponentGeneric_PrimaryEntity),
                typeof(AddChunkComponentGeneric_SecondaryValidEntity)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(component1).ToNativeArray(Allocator.Temp);
                Assert.IsTrue(entities.Length == 2);

                Assert.IsTrue(m_Manager.HasChunkComponent<ComponentTest1>(entities[0]));
                Assert.IsTrue(m_Manager.HasChunkComponent<ComponentTest1>(entities[1]));
            }
        }

        [Test]
        public void AddChunkComponentGeneric_Throws()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddChunkComponentGeneric_SecondaryInvalidEntity)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, kNullEntityException);

                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void AddSharedComponentGeneric()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();
            var component1 = m_Prefab.AddComponent<Authoring_AddSharedComponentGeneric_SecondaryValidEntity>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddSharedComponentGeneric_PrimaryEntity),
                typeof(AddSharedComponentGeneric_SecondaryValidEntity)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(component1).ToNativeArray(Allocator.Temp);
                Assert.IsTrue(entities.Length == 2);

                Assert.IsTrue(m_Manager.HasComponent<UnmanagedSharedComponent>(entities[0]));
                Assert.IsTrue(m_Manager.HasComponent<UnmanagedSharedComponent>(entities[1]));

                UnmanagedSharedComponent unmanagedSharedComponentPrimary0 = m_Manager.GetSharedComponentManaged<UnmanagedSharedComponent>(entities[0]);
                Assert.IsTrue(unmanagedSharedComponentPrimary0.Field == 1);

                UnmanagedSharedComponent unmanagedSharedComponentPrimary1 = m_Manager.GetSharedComponentManaged<UnmanagedSharedComponent>(entities[1]);
                Assert.IsTrue(unmanagedSharedComponentPrimary1.Field == 3);
            }
        }

        [Test]
        public void AddSharedComponentGeneric_Throws()
        {
            var component1 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddSharedComponentGeneric_SecondaryInvalidEntity)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, kNullEntityException);

                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void AddUnmanagedSharedComponentGeneric()
        {
            var component0 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            var component1 = m_Prefab.AddComponent<Authoring_AddUnmanagedSharedComponentGeneric_SecondaryValidEntity>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(AddUnmanagedSharedComponentGeneric_PrimaryEntity),
                typeof(AddUnmanagedSharedComponentGeneric_SecondaryValidEntity)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(component1).ToNativeArray(Allocator.Temp);
                Assert.IsTrue(entities.Length == 2);

                Assert.IsTrue(m_Manager.HasComponent<UnmanagedSharedComponent>(entities[0]));
                Assert.IsTrue(m_Manager.HasComponent<UnmanagedSharedComponent>(entities[1]));

                UnmanagedSharedComponent unmanagedSharedComponentPrimary0 = m_Manager.GetSharedComponentManaged<UnmanagedSharedComponent>(entities[0]);
                Assert.IsTrue(unmanagedSharedComponentPrimary0.Field == 1);

                UnmanagedSharedComponent unmanagedSharedComponentPrimary1 = m_Manager.GetSharedComponentManaged<UnmanagedSharedComponent>(entities[1]);
                Assert.IsTrue(unmanagedSharedComponentPrimary1.Field == 3);
            }
        }

        [Test]
        public void AddUnmanagedSharedComponentGeneric_Throws()
        {
            var component1 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true,
                typeof(AddUnmanagedSharedComponentGeneric_SecondaryInvalidEntity)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, kNullEntityException);

                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void AddBufferGeneric()
        {
            var component0 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            var component1 = m_Prefab.AddComponent<Authoring_AddBufferGeneric_SecondaryValidEntity>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddBufferGeneric_PrimaryEntity),
                typeof(AddBufferGeneric_SecondaryValidEntity)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(component1).ToNativeArray(Allocator.Temp);
                Assert.IsTrue(entities.Length == 2);

                var resultBuffer0 = m_Manager.GetBuffer<IntElement>(entities[0]);
                var resultBuffer1 = m_Manager.GetBuffer<IntElement>(entities[1]);
                Assert.IsTrue(resultBuffer0.Length == resultBuffer1.Length);
                for (int i=0; i<resultBuffer0.Length; ++i)
                {
                    Assert.AreEqual(i + 1, resultBuffer0[i].Value);
                    Assert.AreEqual(i + 1, resultBuffer1[i].Value);
                }
            }
        }

        [Test]
        public void AddBufferGeneric_Throws()
        {
            var component1 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddBufferGeneric_SecondaryInvalidEntity)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, kNullEntityException);

                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void SetBufferGeneric()
        {
            var component0 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            var component1 = m_Prefab.AddComponent<Authoring_AddBufferGeneric_SecondaryValidEntity>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(SetBufferGeneric_PrimaryEntity),
                typeof(SetBufferGeneric_SecondaryValidEntity)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(component1).ToNativeArray(Allocator.Temp);
                Assert.IsTrue(entities.Length == 2);

                var resultBuffer0 = m_Manager.GetBuffer<IntElement>(entities[0]);
                var resultBuffer1 = m_Manager.GetBuffer<IntElement>(entities[1]);
                Assert.IsTrue(resultBuffer0.Length == resultBuffer1.Length);
                for (int i=0; i<resultBuffer0.Length; ++i)
                {
                    Assert.AreEqual(i + 1, resultBuffer0[i].Value);
                    Assert.AreEqual(i + 1, resultBuffer1[i].Value);
                }
            }
        }

        [Test]
        public void SetBufferGeneric_Throws()
        {
            var component1 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(SetBufferGeneric_SecondaryInvalidEntity)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, kNullEntityException);

                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void AppendToBufferGeneric()
        {
            var component0 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            var component1 = m_Prefab.AddComponent<Authoring_AddBufferGeneric_SecondaryValidEntity>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AppendToBufferGeneric_PrimaryEntity),
                typeof(AppendToBufferGeneric_SecondaryValidEntity)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(component1).ToNativeArray(Allocator.Temp);
                Assert.IsTrue(entities.Length == 2);

                var resultBuffer0 = m_Manager.GetBuffer<IntElement>(entities[0]);
                var resultBuffer1 = m_Manager.GetBuffer<IntElement>(entities[1]);
                Assert.IsTrue(resultBuffer0.Length == resultBuffer1.Length);
                for (int i=0; i<resultBuffer0.Length; ++i)
                {
                    Assert.AreEqual(i + 1, resultBuffer0[i].Value);
                    Assert.AreEqual(i + 1, resultBuffer1[i].Value);
                }
            }
        }

        [Test]
        public void AppendToBufferGeneric_Throws()
        {
            var component1 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AppendToBufferGeneric_SecondaryInvalidEntity)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, kNullEntityException);

                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void AddDuplicateComponent_PrimaryEntity_Throws()
        {
            var component0 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(AddDuplicateComponent_PrimaryEntity)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Baking error: Attempt to add duplicate component Unity.Entities.Hybrid.Tests.Baking.ComponentTest1 for Baker Unity.Entities.Hybrid.Tests.Baking.AddDuplicateComponent_PrimaryEntity with authoring component Unity.Entities.Hybrid.Tests.Baking.DefaultAuthoringComponent.  Previous component added by Baker Unity.Entities.Hybrid.Tests.Baking.AddDuplicateComponent_PrimaryEntity"));

                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }


        [Test]
        public void AddDuplicateComponentAcrossBakers_Throws()
        {
            var component0 = m_Prefab.AddComponent<Authoring_AddComponentByComponentType_PrimaryEntity>();
            var component1 = m_Prefab.AddComponent<Authoring_AddComponentGeneric_PrimaryValidEntity>();

            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Baking error: Attempt to add duplicate component Unity.Entities.Hybrid.Tests.Baking.ComponentTest1 for Baker Unity.Entities.Hybrid.Tests.Baking.AddComponentGeneric_PrimaryValidEntity with authoring component Unity.Entities.Hybrid.Tests.Baking.Authoring_AddComponentGeneric_PrimaryValidEntity.  Previous component added by Baker Unity.Entities.Hybrid.Tests.Baking.AddComponentByComponentType_PrimaryEntity"));

            BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
        }

        [Test]
        public void AddDuplicateComponentAcrossBakers_Multiple_Throws()
        {
            var component0 = m_Prefab.AddComponent<Authoring_AddComponentGeneric_PrimaryValidEntity>();
            var component1 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Baking error: Attempt to add duplicate component Unity.Entities.Hybrid.Tests.Baking.ComponentTest1 for Baker Unity.Entities.Hybrid.Tests.Baking.AddComponentByMultipleComponentTypes_PrimaryEntity with authoring component Unity.Entities.Hybrid.Tests.Baking.DefaultAuthoringComponent.  Previous component added by Baker Unity.Entities.Hybrid.Tests.Baking.AddComponentGeneric_PrimaryValidEntity"));

            using (new BakerDataUtility.OverrideBakers(true, typeof(AddComponentGeneric_PrimaryValidEntity),
                typeof(AddComponentByMultipleComponentTypes_PrimaryEntity)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        class AddComponent_WithMultipleBakers_PerAuthoringComponent_Baker1: Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, ComponentType.ReadWrite<ComponentTest1>());
            }
        }

        class AddComponent_WithMultipleBakers_PerAuthoringComponent_Baker2 : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, ComponentType.ReadWrite<ComponentTest2>());
            }
        }

        class AddComponent_WithMultipleBakers_PerAuthoringComponent_Baker1_Throws : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, ComponentType.ReadWrite<ComponentTest2>());
            }
        }

        class AddComponent_WithMultipleBakers_PerAuthoringComponent_Baker2_Throws : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, ComponentType.ReadWrite<ComponentTest2>());
            }
        }

        [Test]
        public void AddDifferentComponents_FromMultipleBakers_PerAuthoringComponent_Is_Valid()
        {
            var component = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true,
                typeof(AddComponent_WithMultipleBakers_PerAuthoringComponent_Baker1),
                typeof(AddComponent_WithMultipleBakers_PerAuthoringComponent_Baker2)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var entity = m_BakingSystem.GetEntitiesForBakers(component);
                Assert.IsTrue(entity.Count == 1);
                EntitiesAssert.Contains(m_Manager, EntityMatch.Partial<ComponentTest1, ComponentTest2>(m_BakingSystem.GetPrimaryEntity(component)));
            }
        }

        [Test]
        public void AddSameComponent_FromMultipleBakers_PerAuthoringComponent_Throws()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true,
                typeof(AddComponent_WithMultipleBakers_PerAuthoringComponent_Baker1_Throws),
                typeof(AddComponent_WithMultipleBakers_PerAuthoringComponent_Baker2_Throws)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex(@"InvalidOperationException: Baking error: Attempt to add duplicate component Unity.Entities.Hybrid.Tests.Baking.ComponentTest2 for Baker Unity.Entities.Hybrid.Tests.Baking.BakerTests\+AddComponent_WithMultipleBakers_PerAuthoringComponent_Baker2_Throws with authoring component Unity.Entities.Hybrid.Tests.Baking.DefaultAuthoringComponent.  Previous component added by Baker Unity.Entities.Hybrid.Tests.Baking.BakerTests\+AddComponent_WithMultipleBakers_PerAuthoringComponent_Baker1_Throws"));

                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void AddSameComponent_FromMultipleBakers_PerAuthoringComponent_EndToEnd()
        {
            var componentEntity0 = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            var componentEntity1 = m_Prefab.AddComponent<Authoring_AddComponentGeneric_PrimaryValidEntity>();
            using (new BakerDataUtility.OverrideBakers(true,
                typeof(AddComponent_WithMultipleBakers_PerAuthoringComponent_Baker1_Throws),
                typeof(AddComponentGeneric_PrimaryValidEntity)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab, m_Prefab1}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(componentEntity1);
                Assert.IsTrue(entities.Count == 1);

                ComponentTest1 component1 = m_Manager.GetComponentData<ComponentTest1>(entities.First());
                Assert.IsTrue(component1.Field == 3);
            }
        }

        [DisableAutoCreation]
        internal class DefaultAuthoringAddComponentBaker : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent component)
            {
                AddComponent(GetEntity(TransformUsageFlags.None), new ComponentTest1{Field = component.Field});
            }
        }

        [DisableAutoCreation]
        internal class DefaultAuthoringAddSharedComponentBaker : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent component)
            {
                AddSharedComponent(GetEntity(TransformUsageFlags.None), new SharedComponentTest1{Field = component.Field});
            }
        }

        [DisableAutoCreation]
        class DefaultAuthoringSetComponentBaker_Value5 : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent component)
            {
                SetComponent(GetEntity(TransformUsageFlags.None), new ComponentTest1 { Field = 5});
            }
        }

        [DisableAutoCreation]
        class DefaultAuthoringUnsafeSetComponentBaker_Value5 : Baker<DefaultAuthoringComponent>
        {
            public override unsafe void Bake(DefaultAuthoringComponent authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent<ComponentTest1>(entity);

                var component = new ComponentTest1 {Field = 5};
                var typeIndex = TypeManager.GetTypeIndex<ComponentTest1>();
                var typeSize = TypeManager.GetTypeInfo(typeIndex).TypeSize;
                UnsafeSetComponent(entity, typeIndex, typeSize, &component);
            }
        }

        [DisableAutoCreation]
        class DefaultAuthoringUnsafeAddComponentBaker_Value5 : Baker<DefaultAuthoringComponent>
        {
            public override unsafe void Bake(DefaultAuthoringComponent authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                var component = new ComponentTest1 {Field = 5};
                var typeIndex = TypeManager.GetTypeIndex<ComponentTest1>();
                var typeSize = TypeManager.GetTypeInfo(typeIndex).TypeSize;
                UnsafeAddComponent(entity, typeIndex, typeSize, &component);
            }
        }

        [Test]
        public void SetComponentFromDifferentBakerThrows()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(DefaultAuthoringAddComponentBaker),
                typeof(DefaultAuthoringSetComponentBaker_Value5)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Baking error: Attempt to set component Unity.Entities.Hybrid.Tests.Baking.ComponentTest1 for Baker DefaultAuthoringSetComponentBaker_Value5 with authoring component DefaultAuthoringComponent but the component was added by a different Baker DefaultAuthoringAddComponentBaker"));
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                // SetComponent was rejected thus the value continues to be 0
                Assert.AreEqual(0, GetBakedSingleton<ComponentTest1>().Field);
            }
        }

        [DisableAutoCreation]
        class DefaultAuthoringSetSharedComponentBaker_Value5 : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent component)
            {
                SetSharedComponent(GetEntity(TransformUsageFlags.None), new SharedComponentTest1() { Field = 5});
            }
        }

        [Test]
        public void SetSharedComponentFromDifferentBakerThrows()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(DefaultAuthoringAddSharedComponentBaker),
                typeof(DefaultAuthoringSetSharedComponentBaker_Value5)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Baking error: Attempt to set component Unity.Entities.Hybrid.Tests.Baking.SharedComponentTest1 for Baker DefaultAuthoringSetSharedComponentBaker_Value5 with authoring component DefaultAuthoringComponent but the component was added by a different Baker DefaultAuthoringAddSharedComponentBaker"));
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                // SetSharedComponent was rejected thus the value continues to be 0
                var query = World.EntityManager.CreateEntityQuery(new EntityQueryDesc {All = new ComponentType[]{typeof(SharedComponentTest1)}});
                var entities = query.ToEntityArray(Allocator.Temp);
                var data = World.EntityManager.GetSharedComponentManaged<SharedComponentTest1>(entities[0]);
                Assert.AreEqual(0, data.Field);
            }
        }

        [DisableAutoCreation]
        class SetSharedComponentFromSameBakerWorksBaker : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent component)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddSharedComponent(entity, new SharedComponentTest1());
                SetSharedComponent(entity, new SharedComponentTest1() { Field = 5});
            }
        }
        [Test]
        public void SetSharedComponentFromSameBakerWorks()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(SetSharedComponentFromSameBakerWorksBaker)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var query = World.EntityManager.CreateEntityQuery(new EntityQueryDesc {All = new ComponentType[]{typeof(SharedComponentTest1)}});
                var entities = query.ToEntityArray(Allocator.Temp);
                var data = World.EntityManager.GetSharedComponentManaged<SharedComponentTest1>(entities[0]);
                Assert.AreEqual(5, data.Field);
            }
        }

        [DisableAutoCreation]
        class SetComponentFromSameBakerWorksBaker : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent component)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ComponentTest1());
                SetComponent(entity, new ComponentTest1 { Field = 5});
            }
        }
        [Test]
        public void SetComponentFromSameBakerWorks()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(SetComponentFromSameBakerWorksBaker)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                Assert.AreEqual(5, GetBakedSingleton<ComponentTest1>().Field);
            }
        }

        [DisableAutoCreation]
        class SetEnableableComponentFromSameBakerWorksBaker : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent component)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new EcsTestDataEnableable());
                SetComponentEnabled<EcsTestDataEnableable>(entity, false);
            }
        }
        [Test]
        public void SetEnableableComponentFromSameBakerWorks()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(SetEnableableComponentFromSameBakerWorksBaker)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                Assert.AreEqual(1, m_BakingSystem.EntityManager.UniversalQuery.CalculateEntityCount());
                var entities = m_BakingSystem.EntityManager.UniversalQuery.ToEntityArray(m_BakingSystem.WorldUpdateAllocator);
                Assert.AreEqual(false, m_BakingSystem.EntityManager.IsComponentEnabled<EcsTestDataEnableable>(entities[0]));
                Assert.AreEqual(true, m_BakingSystem.EntityManager.IsComponentEnabled<Simulate>(entities[0]));
            }
        }

        [DisableAutoCreation]
        class SetEnableableComponentOnPrimaryEntityWorksBaker : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent component)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new EcsTestDataEnableable());
                SetComponentEnabled<EcsTestDataEnableable>(entity, false);
            }
        }
        [Test]
        public void SetEnableableComponentOnPrimaryEntityWorks()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(SetEnableableComponentFromSameBakerWorksBaker)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                Assert.AreEqual(1, m_BakingSystem.EntityManager.UniversalQuery.CalculateEntityCount());
                var entities = m_BakingSystem.EntityManager.UniversalQuery.ToEntityArray(m_BakingSystem.WorldUpdateAllocator);
                Assert.AreEqual(false, m_BakingSystem.EntityManager.IsComponentEnabled<EcsTestDataEnableable>(entities[0]));
                Assert.AreEqual(true, m_BakingSystem.EntityManager.IsComponentEnabled<Simulate>(entities[0]));
            }
        }

        [DisableAutoCreation]
        class DefaultAuthoringAddEnableableComponentBaker : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent component)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new EcsTestDataEnableable());
            }
        }

        [Test]
        public void NotSetEnableableComponentDefaultTrue()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(DefaultAuthoringAddEnableableComponentBaker)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                Assert.AreEqual(1, m_BakingSystem.EntityManager.UniversalQuery.CalculateEntityCount());
                var entities = m_BakingSystem.EntityManager.UniversalQuery.ToEntityArray(m_BakingSystem.WorldUpdateAllocator);
                Assert.AreEqual(true, m_BakingSystem.EntityManager.IsComponentEnabled<EcsTestDataEnableable>(entities[0]));
            }
        }

        [DisableAutoCreation]
        class DefaultAuthoringSetEnableableComponentBaker : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent component)
            {
                SetComponentEnabled<EcsTestDataEnableable>(GetEntity(TransformUsageFlags.None), false);
            }
        }

        [Test]
        public void SetEnableableComponentFromDifferentBakerThrows()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(DefaultAuthoringAddEnableableComponentBaker),
                       typeof(DefaultAuthoringSetEnableableComponentBaker)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Baking error: Attempt to set component Unity.Entities.Tests.EcsTestDataEnableable for Baker DefaultAuthoringSetEnableableComponentBaker with authoring component DefaultAuthoringComponent but the component was added by a different Baker DefaultAuthoringAddEnableableComponentBaker"));
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                // SetComponentEnabled was rejected thus the value continues to be true
                Assert.AreEqual(1, m_BakingSystem.EntityManager.UniversalQuery.CalculateEntityCount());
                var entities = m_BakingSystem.EntityManager.UniversalQuery.ToEntityArray(m_BakingSystem.WorldUpdateAllocator);
                Assert.AreEqual(true, m_BakingSystem.EntityManager.IsComponentEnabled<EcsTestDataEnableable>(entities[0]));
            }
        }

        [DisableAutoCreation]
        class SetBufferFromOtherBakerThrowsBaker : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent component)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                var buffer = SetBuffer<IntElement>(entity);
                buffer.Add(new IntElement {Value = 4});
                buffer.Add(new IntElement {Value = 5});
                buffer.Add(new IntElement {Value = 6});
            }
        }

        [Test]
        public void SetBufferFromDifferentBakerThrows()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(SetBufferFromSameBakerWorksBaker),
                typeof(SetBufferFromOtherBakerThrowsBaker)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Baking error: Attempt to set component Unity.Entities.Hybrid.Tests.Baking.IntElement for Baker SetBufferFromOtherBakerThrowsBaker with authoring component DefaultAuthoringComponent but the component was added by a different Baker SetBufferFromSameBakerWorksBaker"));
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var query = World.EntityManager.CreateEntityQuery(new EntityQueryDesc {All = new ComponentType[]{typeof(IntElement)}});
                var entities = query.ToEntityArray(Allocator.Temp);
                var data = World.EntityManager.GetBuffer<IntElement>(entities[0]);
                Assert.AreEqual(3, data.Length);
                Assert.AreEqual(1, data[0].Value);
                Assert.AreEqual(2, data[1].Value);
                Assert.AreEqual(3, data[2].Value);
            }
        }

        [DisableAutoCreation]
        class SetBufferFromSameBakerWorksBaker : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent component)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddBuffer<IntElement>(entity);
                var buffer = SetBuffer<IntElement>(entity);
                buffer.Add(new IntElement {Value = 1});
                buffer.Add(new IntElement {Value = 2});
                buffer.Add(new IntElement {Value = 3});
            }
        }

        [Test]
        public void SetBufferFromSameBakerWorks()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(SetBufferFromSameBakerWorksBaker)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var query = World.EntityManager.CreateEntityQuery(new EntityQueryDesc {All = new ComponentType[]{typeof(IntElement)}});
                var entities = query.ToEntityArray(Allocator.Temp);
                var data = World.EntityManager.GetBuffer<IntElement>(entities[0]);
                Assert.AreEqual(3, data.Length);
                Assert.AreEqual(1, data[0].Value);
                Assert.AreEqual(2, data[1].Value);
                Assert.AreEqual(3, data[2].Value);
            }
        }

        [DisableAutoCreation]
        class AppendToBufferFromOtherBakerThrowsBaker : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent component)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AppendToBuffer(entity, new IntElement {Value = 4});
                AppendToBuffer(entity, new IntElement {Value = 5});
                AppendToBuffer(entity, new IntElement {Value = 6});
            }
        }

        [Test]
        public void AppendToBufferFromDifferentBakerThrows()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(AppendToBufferFromSameBakerWorksBaker),
                typeof(AppendToBufferFromOtherBakerThrowsBaker)))
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: Baking error: Attempt to set component Unity.Entities.Hybrid.Tests.Baking.IntElement for Baker AppendToBufferFromOtherBakerThrowsBaker with authoring component DefaultAuthoringComponent but the component was added by a different Baker AppendToBufferFromSameBakerWorksBaker"));
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var query = World.EntityManager.CreateEntityQuery(new EntityQueryDesc {All = new ComponentType[]{typeof(IntElement)}});
                var entities = query.ToEntityArray(Allocator.Temp);
                var data = World.EntityManager.GetBuffer<IntElement>(entities[0]);
                Assert.AreEqual(3, data.Length);
                Assert.AreEqual(1, data[0].Value);
                Assert.AreEqual(2, data[1].Value);
                Assert.AreEqual(3, data[2].Value);
            }
        }

        [DisableAutoCreation]
        class AppendToBufferFromSameBakerWorksBaker : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent component)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddBuffer<IntElement>(entity);
                AppendToBuffer(entity, new IntElement {Value = 1});
                AppendToBuffer(entity, new IntElement {Value = 2});
                AppendToBuffer(entity, new IntElement {Value = 3});
            }
        }

        [Test]
        public void AppendToBufferFromSameBakerWorks()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(AppendToBufferFromSameBakerWorksBaker)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var query = World.EntityManager.CreateEntityQuery(new EntityQueryDesc {All = new ComponentType[]{typeof(IntElement)}});
                var entities = query.ToEntityArray(Allocator.Temp);
                var data = World.EntityManager.GetBuffer<IntElement>(entities[0]);
                Assert.AreEqual(3, data.Length);
                Assert.AreEqual(1, data[0].Value);
                Assert.AreEqual(2, data[1].Value);
                Assert.AreEqual(3, data[2].Value);
            }
        }

        [Test]
        public void UnsafeSetComponentFromSameBakerWorks()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(DefaultAuthoringUnsafeSetComponentBaker_Value5)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                Assert.AreEqual(5, GetBakedSingleton<ComponentTest1>().Field);
            }
        }

        [Test]
        public void UnsafeAddComponentFromSameBakerWorks()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(DefaultAuthoringUnsafeAddComponentBaker_Value5)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                Assert.AreEqual(5, GetBakedSingleton<ComponentTest1>().Field);
            }
        }

        [Test]
        public void Authoring_GetComponent([Values(0,1)] int count, [Values(typeof(GetComponent_PrimaryEntity), typeof(GetComponent_Component),typeof(GetComponent_GameObject))] Type bakerType)
        {
            var componentEntity = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                for (int index = 0; index < count; ++index)
                    m_Prefab.AddComponent<BoxCollider>();
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(componentEntity);
                Assert.IsTrue(entities.Count == 1);

                GetComponentTest1 component1 = m_Manager.GetComponentData<GetComponentTest1>(entities.First());
                Assert.IsTrue(component1.Field == count);

                // Make sure we found the same component
                var foundComponent = m_Prefab.GetComponentInParent<Collider>();
                if (foundComponent != null)
                {
                    Assert.AreEqual(component1.GUID, foundComponent.GetInstanceID());
                }
            }
        }

        [Test]
        public void Authoring_GetComponent_NullError([Values(1)] int count, [Values(typeof(GetComponent_ComponentNull),typeof(GetComponent_GameObjectNull))]Type bakerType)
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex(@".*NullReferenceException.*"));

            var componentEntity = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                for (int index = 0; index < count; ++index)
                    m_Prefab.AddComponent<BoxCollider>();
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void Authoring_GetComponents([Values(0,1,2)] int count, [Values(typeof(GetComponents_PrimaryEntity),typeof(GetComponents_Component),typeof(GetComponents_GameObject),typeof(GetComponents_PrimaryEntity_PassingList),typeof(GetComponents_Component_PassingList),typeof(GetComponents_GameObject_PassingList))] Type bakerType)
        {
            var componentEntity = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                for (int index = 0; index < count; ++index)
                    m_Prefab.AddComponent<BoxCollider>();
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(componentEntity);
                Assert.IsTrue(entities.Count == 1);
                var primaryEntity = entities.First();

                ComponentTest1 component1 = m_Manager.GetComponentData<ComponentTest1>(primaryEntity);
                Assert.IsTrue(component1.Field == count);

                var foundComponents = m_Prefab.GetComponents<Collider>();
                if (foundComponents != null && foundComponents.Length > 0)
                {
                    var elements = m_Manager.GetBuffer<IntElement>(primaryEntity);
                    Assert.AreEqual(foundComponents.Length, elements.Length);
                    for (int index = 0; index < foundComponents.Length; ++index)
                    {
                        Assert.AreEqual(foundComponents[index].GetInstanceID(), elements[index].Value);
                    }
                }
            }
        }

        [Test]
        public void Authoring_GetComponents_NullError([Values(1)] int count, [Values(typeof(GetComponents_GameObjectNull),typeof(GetComponents_ComponentNull),typeof(GetComponents_GameObjectNull_PassingList),typeof(GetComponents_ComponentNull_PassingList))] Type bakerType)
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex(@".*NullReferenceException.*"));
            var componentEntity = m_Prefab.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                for (int index = 0; index < count; ++index)
                    m_Prefab.AddComponent<BoxCollider>();
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void Authoring_GetComponentInParent([Values] BakerTestsHierarchyHelper.ParentHierarchyMaskTests mask, [Values(typeof(GetComponentInParent_PrimaryEntity),typeof(GetComponentInParent_Component),typeof(GetComponentInParent_GameObject))]  Type bakerType)
        {
            GameObject current = BakerTestsHierarchyHelper.CreateParentHierarchyWithType<BoxCollider>(mask, m_Prefab, out int added);
            var componentEntity = current.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {current}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(componentEntity);
                Assert.IsTrue(entities.Count == 1);

                GetComponentTest1 component1 = m_Manager.GetComponentData<GetComponentTest1>(entities.First());
                Assert.IsTrue(component1.Field == (mask > 0 ? 1 : 0));

                // Make sure we found the same component
                var foundComponent = current.GetComponentInParent<Collider>();
                if (foundComponent != null)
                {
                    Assert.AreEqual(component1.GUID, foundComponent.GetInstanceID());
                }
            }
        }

        [Test]
        public void Authoring_GetComponentInParent_NullError([Values(BakerTestsHierarchyHelper.ParentHierarchyMaskTests.All)] BakerTestsHierarchyHelper.ParentHierarchyMaskTests mask, [Values(typeof(GetComponentInParent_ComponentNull),typeof(GetComponentInParent_GameObjectNull))]  Type bakerType)
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex(@".*NullReferenceException.*"));
            GameObject current = BakerTestsHierarchyHelper.CreateParentHierarchyWithType<BoxCollider>(mask, m_Prefab, out int added);
            var componentEntity = current.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {current}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void Authoring_GetComponentsInParent([Values] BakerTestsHierarchyHelper.ParentHierarchyMaskTests mask, [Values(typeof(GetComponentsInParent_PrimaryEntity),typeof(GetComponentsInParent_Component),typeof(GetComponentsInParent_GameObject),typeof(GetComponentsInParent_PrimaryEntity_PassingList),typeof(GetComponentsInParent_Component_PassingList),typeof(GetComponentsInParent_GameObject_PassingList))]  Type bakerType)
        {
            GameObject current = BakerTestsHierarchyHelper.CreateParentHierarchyWithType<BoxCollider>(mask, m_Prefab, out int added);
            var componentEntity = current.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {current}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(componentEntity);
                Assert.IsTrue(entities.Count == 1);
                var primaryEntity = entities.First();

                ComponentTest1 component1 = m_Manager.GetComponentData<ComponentTest1>(primaryEntity);
                Assert.IsTrue(component1.Field == added);

                var foundComponents = current.GetComponentsInParent<Collider>();
                if (foundComponents != null && foundComponents.Length > 0)
                {
                    var elements = m_Manager.GetBuffer<IntElement>(primaryEntity);
                    Assert.AreEqual(foundComponents.Length, elements.Length);
                    for (int index = 0; index < foundComponents.Length; ++index)
                    {
                        Assert.AreEqual(foundComponents[index].GetInstanceID(), elements[index].Value);
                    }
                }
            }
        }

        [Test]
        public void Authoring_GetComponentsInParent_NullError([Values(BakerTestsHierarchyHelper.ParentHierarchyMaskTests.All)] BakerTestsHierarchyHelper.ParentHierarchyMaskTests mask, [Values(typeof(GetComponentsInParent_ComponentNull),typeof(GetComponentsInParent_GameObjectNull),typeof(GetComponentsInParent_ComponentNull_PassingList),typeof(GetComponentsInParent_GameObjectNull_PassingList))]  Type bakerType)
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex(@".*NullReferenceException.*"));
            GameObject current = BakerTestsHierarchyHelper.CreateParentHierarchyWithType<BoxCollider>(mask, m_Prefab, out int added);
            var componentEntity = current.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {current}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(componentEntity);
            }
        }

        [Test]
        public void Authoring_GetComponentInChildren([Values] BakerTestsHierarchyHelper.HierarchyChildrenTests mask, [Values(typeof(GetComponentInChildren_PrimaryEntity),typeof(GetComponentInChildren_Component),typeof(GetComponentInChildren_GameObject))] Type bakerType)
        {
            GameObject root = BakerTestsHierarchyHelper.CreateChildrenHierarchyWithType<BoxCollider>(3, 2, (uint)mask, m_Prefab, out int added);
            var componentEntity = root.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {root}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(componentEntity);
                Assert.IsTrue(entities.Count == 1);

                GetComponentTest1 component1 = m_Manager.GetComponentData<GetComponentTest1>(entities.First());
                Assert.IsTrue(component1.Field == (mask > 0 ? 1 : 0));

                // Make sure we found the same component
                var foundComponent = root.GetComponentInChildren<Collider>();
                if (foundComponent != null)
                {
                    Assert.AreEqual(component1.GUID, foundComponent.GetInstanceID());
                }
            }
        }

        [Test]
        public void Authoring_GetComponentInChildren_NullError([Values(BakerTestsHierarchyHelper.HierarchyChildrenTests.All)] BakerTestsHierarchyHelper.HierarchyChildrenTests mask, [Values(typeof(GetComponentInChildren_ComponentNull),typeof(GetComponentInChildren_GameObjectNull))] Type bakerType)
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex(@".*NullReferenceException.*"));
            GameObject root = BakerTestsHierarchyHelper.CreateChildrenHierarchyWithType<BoxCollider>(3, 2, (uint)mask, m_Prefab, out int added);
            var componentEntity = root.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {root}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void Authoring_GetComponentsInChildren([Values] BakerTestsHierarchyHelper.HierarchyChildrenTests mask, [Values(typeof(GetComponentsInChildren_PrimaryEntity),typeof(GetComponentsInChildren_Component),typeof(GetComponentsInChildren_GameObject),typeof(GetComponentsInChildren_PrimaryEntity_PassingList),typeof(GetComponentsInChildren_Component_PassingList),typeof(GetComponentsInChildren_GameObject_PassingList))] Type bakerType )
        {
            GameObject root = BakerTestsHierarchyHelper.CreateChildrenHierarchyWithType<BoxCollider>(3, 2, (uint)mask, m_Prefab, out int added);
            var componentEntity = root.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {root}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(componentEntity);
                Assert.IsTrue(entities.Count == 1);
                var primaryEntity = entities.First();

                ComponentTest1 component1 = m_Manager.GetComponentData<ComponentTest1>(primaryEntity);
                Assert.IsTrue(component1.Field == added);

                var foundComponents = root.GetComponentsInChildren<Collider>();
                if (foundComponents != null && foundComponents.Length > 0)
                {
                    var elements = m_Manager.GetBuffer<IntElement>(primaryEntity);
                    Assert.AreEqual(foundComponents.Length, elements.Length);
                    for (int index = 0; index < foundComponents.Length; ++index)
                    {
                        Assert.AreEqual(foundComponents[index].GetInstanceID(), elements[index].Value);
                    }
                }
            }
        }

        [Test]
        public void Authoring_GetComponentsInChildren_NullError([Values(BakerTestsHierarchyHelper.HierarchyChildrenTests.All)] BakerTestsHierarchyHelper.HierarchyChildrenTests mask, [Values(typeof(GetComponentsInChildren_ComponentNull),typeof(GetComponentsInChildren_GameObjectNull),typeof(GetComponentsInChildren_ComponentNull_PassingList),typeof(GetComponentsInChildren_GameObjectNull_PassingList))] Type bakerType )
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex(@".*NullReferenceException.*"));
            GameObject root = BakerTestsHierarchyHelper.CreateChildrenHierarchyWithType<BoxCollider>(3, 2, (uint)mask, m_Prefab, out int added);
            var componentEntity = root.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {root}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void Authoring_GetParent([Values(1,2,3)] int count, [Values(typeof(GetParent_PrimaryEntity),typeof(GetParent_Component),typeof(GetParent_GameObject))]  Type bakerType)
        {
            GameObject current = BakerTestsHierarchyHelper.CreateParentHierarchy(count, m_Prefab);
            var componentEntity = current.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {current}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(componentEntity);
                Assert.IsTrue(entities.Count == 1);

                GetComponentTest1 component1 = m_Manager.GetComponentData<GetComponentTest1>(entities.First());
                Assert.IsTrue(component1.Field == (count > 1 ? 1 : 0));

                // Make sure we found the same component
                var found = current.transform.parent;
                if (found != null)
                {
                    Assert.AreEqual(component1.GUID, found.gameObject.GetInstanceID());
                }
            }
        }

        [Test]
        public void Authoring_GetParent_NullError([Values(BakerTestsHierarchyHelper.ParentHierarchyMaskTests.All)] BakerTestsHierarchyHelper.ParentHierarchyMaskTests mask, [Values(typeof(GetParent_ComponentNull),typeof(GetParent_GameObjectNull))]  Type bakerType)
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex(@".*NullReferenceException.*"));
            GameObject current = BakerTestsHierarchyHelper.CreateParentHierarchyWithType<BoxCollider>(mask, m_Prefab, out int added);
            var componentEntity = current.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {current}, m_BakingSystem.BakingSettings);
            }
        }

         [Test]
        public void Authoring_GetParents([Values(1,2,3)] int count, [Values(typeof(GetParents_PrimaryEntity),typeof(GetParents_Component),typeof(GetParents_GameObject),typeof(GetParents_PrimaryEntity_PassingList),typeof(GetParents_Component_PassingList),typeof(GetParents_GameObject_PassingList))]  Type bakerType)
        {
            GameObject current = BakerTestsHierarchyHelper.CreateParentHierarchy(count, m_Prefab);
            var componentEntity = current.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {current}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(componentEntity);
                Assert.IsTrue(entities.Count == 1);
                var primaryEntity = entities.First();

                ComponentTest1 component1 = m_Manager.GetComponentData<ComponentTest1>(primaryEntity);
                Assert.IsTrue(component1.Field == count - 1);

                var foundComponents = current.GetComponentsInParent<Transform>();
                if (foundComponents != null && foundComponents.Length > 0)
                {
                    var elements = m_Manager.GetBuffer<IntElement>(primaryEntity);
                    Assert.AreEqual(foundComponents.Length - 1, elements.Length);
                    for (int index = 1; index < foundComponents.Length; ++index)
                    {
                        Assert.AreEqual(foundComponents[index].gameObject.GetInstanceID(), elements[index - 1].Value);
                    }
                }
            }
        }

        [Test]
        public void Authoring_GetParents_NullError([Values(BakerTestsHierarchyHelper.ParentHierarchyMaskTests.All)] BakerTestsHierarchyHelper.ParentHierarchyMaskTests mask, [Values(typeof(GetParents_ComponentNull),typeof(GetParents_GameObjectNull),typeof(GetParents_ComponentNull_PassingList),typeof(GetParents_GameObjectNull_PassingList))]  Type bakerType)
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex(@".*NullReferenceException.*"));
            GameObject current = BakerTestsHierarchyHelper.CreateParentHierarchyWithType<BoxCollider>(mask, m_Prefab, out int added);
            var componentEntity = current.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {current}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(componentEntity);
            }
        }

        [Test]
        public void Authoring_GetChild([Values(0,1,2,3,4)] int query, [Values(typeof(GetChild_PrimaryEntity),typeof(GetChild_Component),typeof(GetChild_GameObject))] Type bakerType)
        {
            GetChild_PrimaryEntity.QueryIndex = query;
            GetChild_Component.QueryIndex = query;
            GetChild_GameObject.QueryIndex = query;

            if (query == 4)
            {
                // Expect error
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex(@".*Transform child out of bounds.*"));
            }

            GameObject root = new GameObject();
            List<GameObject> goList = new List<GameObject>();
            BakerTestsHierarchyHelper.CreateChildrenHierarchy(root, 1, 4, goList);

            var componentEntity = root.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {root}, m_BakingSystem.BakingSettings);

                if (query < 4)
                {
                    var entities = m_BakingSystem.GetEntitiesForBakers(componentEntity);
                    Assert.IsTrue(entities.Count == 1);
                    var primaryEntity = m_BakingSystem.GetPrimaryEntity(componentEntity);

                    GetComponentTest1 component1 = m_Manager.GetComponentData<GetComponentTest1>(primaryEntity);
                    Assert.IsTrue(component1.Field == 4);

                    // Make sure we found the same gameobject
                    var foundComponent = root.transform.GetChild(query);
                    if (foundComponent != null)
                    {
                        Assert.AreEqual(component1.GUID, foundComponent.gameObject.GetInstanceID());
                    }
                }
            }
        }

        [Test]
        public void Authoring_GetChild_NoChildren([Values(typeof(GetChild_PrimaryEntity),typeof(GetChild_Component),typeof(GetChild_GameObject))] Type bakerType)
        {
            GetChild_PrimaryEntity.QueryIndex = 0;
            GetChild_Component.QueryIndex = 0;
            GetChild_GameObject.QueryIndex = 0;

            GameObject root = new GameObject();
            List<GameObject> goList = new List<GameObject>();

            var componentEntity = root.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {root}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(componentEntity);
                Assert.IsTrue(entities.Count == 1);
                var primaryEntity = m_BakingSystem.GetPrimaryEntity(componentEntity);

                GetComponentTest1 component1 = m_Manager.GetComponentData<GetComponentTest1>(primaryEntity);
                Assert.IsTrue(component1.Field == 0);
            }
        }

        [Test]
        public void Authoring_GetChild_NullError([Values(typeof(GetChild_ComponentNull),typeof(GetChild_GameObjectNull))] Type bakerType)
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex(@".*NullReferenceException.*"));
            GameObject root = new GameObject();
            List<GameObject> goList = new List<GameObject>();
            BakerTestsHierarchyHelper.CreateChildrenHierarchy(root, 1, 4, goList);

            var componentEntity = root.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {root}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void Authoring_GetChildren([Values(0,1,2)] int depth, [Values(typeof(GetChildren_PrimaryEntity),typeof(GetChildren_Component),typeof(GetChildren_GameObject),typeof(GetChildren_PrimaryEntity_PassingList),typeof(GetChildren_Component_PassingList),typeof(GetChildren_GameObject_PassingList))] Type bakerType, [Values] bool recursive)
        {
            // Set the recursive in the bakers, to avoid duplicating all the bakers
            GetChildren_PrimaryEntity.Recursive = recursive;
            GetChildren_Component.Recursive = recursive;
            GetChildren_GameObject.Recursive = recursive;
            GetChildren_PrimaryEntity_PassingList.Recursive = recursive;
            GetChildren_Component_PassingList.Recursive = recursive;
            GetChildren_GameObject_PassingList.Recursive = recursive;

            GameObject root = new GameObject();
            List<GameObject> goList = new List<GameObject>();
            BakerTestsHierarchyHelper.CreateChildrenHierarchy(root, (uint)depth, 4, goList);

            var componentEntity = root.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {root}, m_BakingSystem.BakingSettings);

                var entities = m_BakingSystem.GetEntitiesForBakers(componentEntity);
                Assert.IsTrue(entities.Count == 1);
                var primaryEntity = m_BakingSystem.GetPrimaryEntity(componentEntity);

                var elements = m_Manager.GetBuffer<IntElement>(primaryEntity);
                if (recursive)
                {
                    var list = root.GetComponentsInChildren<Transform>();
                    Assert.AreEqual(list.Length - 1, elements.Length);
                    for (int listIndex = 1; listIndex < list.Length; ++listIndex)
                    {
                        Assert.AreEqual(list[listIndex].gameObject.GetInstanceID(), elements[listIndex - 1].Value);
                    }
                }
                else
                {
                    int index = 0;
                    foreach (Transform child in root.transform)
                    {
                        Assert.AreEqual(child.gameObject.GetInstanceID(), elements[index].Value);
                        ++index;
                    }
                    Assert.AreEqual(index, elements.Length);
                }
            }
        }

        [Test]
        public void Authoring_GetChildren_NullError([Values(2)] int depth, [Values(typeof(GetChildren_ComponentNull),typeof(GetChildren_GameObjectNull),typeof(GetChildren_ComponentNull_PassingList),typeof(GetChildren_GameObjectNull_PassingList))] Type bakerType )
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, new Regex(@".*NullReferenceException.*"));

            GameObject root = new GameObject();
            List<GameObject> goList = new List<GameObject>();
            BakerTestsHierarchyHelper.CreateChildrenHierarchy(root, (uint)depth, 4, goList);
            var componentEntity = root.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, bakerType))
            {
                BakingUtility.BakeGameObjects(World, new[] {root}, m_BakingSystem.BakingSettings);
            }
        }

        [Test]
        public void BaseAuthoringType_HasAttribute_AllBakersRun()
        {
            m_Prefab.AddComponent<Authoring_DerivedFromDerivedClass>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(BaseClassBaker_WithAttribute),typeof(DerivedClassBaker_WithAttribute),typeof(DerivedDerivedClassBaker)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var query = World.EntityManager.CreateEntityQuery(new EntityQueryDesc {All = new ComponentType[]{typeof(ComponentTest1),typeof(ComponentTest2),typeof(ComponentTest3)}});
                var entities = query.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(1, entities.Length);
            }
        }

        [Test]
        public void BaseAuthoringType_HasNoAttribute_OnlyOneBakerRun()
        {
            m_Prefab.AddComponent<Authoring_DerivedFromDerivedClass>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(BaseClassBaker),typeof(DerivedClassBaker),typeof(DerivedDerivedClassBaker)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var query = World.EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    None = new ComponentType[]{typeof(ComponentTest1),typeof(ComponentTest2)},
                    All = new ComponentType[]{typeof(ComponentTest3)}
                });
                var entities = query.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(1, entities.Length);
            }
        }

        [Test]
        public void AbstractAuthoringType_HasAttribute_AllBakersRun()
        {
            m_Prefab.AddComponent<Authoring_DerivedFromAbstract>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(AbstractClassBaker_WithAttribute),typeof(DerivedFromAbstractClassBaker)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var query = World.EntityManager.CreateEntityQuery(new EntityQueryDesc {All = new ComponentType[]{typeof(ComponentTest1),typeof(ComponentTest2)}});
                var entities = query.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(1, entities.Length);
            }
        }

        [Test]
        public void AbstractAuthoringType_HasNoAttribute_OnlyOneBakerRun()
        {
            m_Prefab.AddComponent<Authoring_DerivedFromAbstract>();

            using (new BakerDataUtility.OverrideBakers(true, typeof(AbstractClassBaker),typeof(DerivedFromAbstractClassBaker)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var query = World.EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    None = new ComponentType[]{typeof(ComponentTest1)},
                    All = new ComponentType[]{typeof(ComponentTest2)}
                });
                var entities = query.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(1, entities.Length);
            }
        }

        [Test]
        public void DerivedAuthoringType_DefinedBeforeBase_IsEvaluatedAfterBase()
        {
            var authoring = m_Prefab.AddComponent<Authoring_DerivedFromBaseClass_DefinedBeforeBase>();

            BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

            CollectionAssert.AreEqual(new string[]
            {
                nameof(BaseBakerDefinedAfterDerivedBaker),
                nameof(DerivedBakerDefinedBeforeBaseBaker)
            }, authoring.BakerTypeOrder);
        }

        [DisableAutoCreation]
        class GameObjectBaker_CreateAdditionalEntities : GameObjectBaker
        {
            public static bool CreateBatched = false;
            public static int AdditionalEntityCount = 3;
            public override void Bake(GameObject authoring)
            {
                var entities = new NativeArray<Entity>(AdditionalEntityCount, Allocator.Temp);
                if (CreateBatched)
                {
                    CreateAdditionalEntities(entities, TransformUsageFlags.None);
                }
                else
                {
                    for (int i = 0; i < entities.Length; ++i)
                        entities[i] = CreateAdditionalEntity(TransformUsageFlags.None);
                }

                foreach (var entity in entities)
                    AddComponent<AdditionalEntity>(entity);
            }
        }

        [Test]
        public void GameObjectBaker_CreateAdditionalEntities_DoesNotThrow()
        {
            using (new BakerDataUtility.OverrideBakers(true, typeof(GameObjectBaker_CreateAdditionalEntities)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);

                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<AdditionalEntity>()
                    .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)
                    .Build(World.EntityManager);
                var entities = query.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(GameObjectBaker_CreateAdditionalEntities.AdditionalEntityCount, entities.Length);
            }
        }

        [Test]
        public void GameObjectBaker_CreateAdditionalEntitiesBatched_DoesNotThrow()
        {
            using (new BakerDataUtility.OverrideBakers(true, typeof(GameObjectBaker_CreateAdditionalEntities)))
            {
                GameObjectBaker_CreateAdditionalEntities.CreateBatched = true;
                try
                {
                    BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
                }
                finally
                {
                    GameObjectBaker_CreateAdditionalEntities.CreateBatched = false;
                }

                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<AdditionalEntity>()
                    .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)
                    .Build(World.EntityManager);
                var entities = query.ToEntityArray(Allocator.Temp);
                Assert.AreEqual(GameObjectBaker_CreateAdditionalEntities.AdditionalEntityCount, entities.Length);
            }
        }

        [Test]
        public void GameObjectBaker_CreateAdditionalEntitiesBatched_ThrowIfCountIsZero()
        {
            using (new BakerDataUtility.OverrideBakers(true, typeof(GameObjectBaker_CreateAdditionalEntities)))
            {
                GameObjectBaker_CreateAdditionalEntities.CreateBatched = true;
                GameObjectBaker_CreateAdditionalEntities.AdditionalEntityCount = 0;
                try
                {
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, kCreateAdditionalEntitiesInvalidArrayException);
                    BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
                }
                finally
                {
                    GameObjectBaker_CreateAdditionalEntities.CreateBatched = false;
                    GameObjectBaker_CreateAdditionalEntities.AdditionalEntityCount = 3;
                }
            }
        }

        [DisableAutoCreation]
        class GameObjectBaker_GetGameObjectProperties : GameObjectBaker
        {
            public override void Bake(GameObject authoring)
            {
                Assert.AreEqual(authoring.name, GetName());
                Assert.AreEqual(authoring.tag, GetTag());
                Assert.AreEqual(authoring.layer, GetLayer());
                Assert.AreEqual(authoring.isStatic, IsStatic());
                Assert.AreEqual(authoring.activeSelf, IsActive());
            }
        }

        [Test]
        public void GameObjectBaker_GetGameObjectProperties_DoesNotThrow()
        {
            using (new BakerDataUtility.OverrideBakers(true, typeof(GameObjectBaker_GetGameObjectProperties)))
            {
                Assert.DoesNotThrow(() => BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings));
            }
        }

        [DisableAutoCreation]
        class GameObjectBaker_IsActiveAndEnabled : GameObjectBaker
        {
            public override void Bake(GameObject authoring)
            {
                Assert.Throws<InvalidOperationException>(() => IsActiveAndEnabled());
            }
        }

        [Test]
        public void GameObjectBaker_IsActiveAndEnabled_Throws()
        {
            using (new BakerDataUtility.OverrideBakers(true, typeof(GameObjectBaker_IsActiveAndEnabled)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
            }
        }

        struct TestComponentWithBlobAssetReference : IComponentData
        {
            public BlobAssetReference<int> Value;
        }

        [DisableAutoCreation]
        class GameObjectBaker_AddBlobAsset : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                var blobRef = BlobAssetUtility.CreateBlobAsset(authoring.Field);
                AddBlobAsset(ref blobRef, out _);
                AddComponent(entity, new TestComponentWithBlobAssetReference { Value = blobRef });
            }
        }

        [Test]
        public void GameObjectBaker_DisposeOfBlobAssetOwnedByBaker_Throws()
        {
            m_Prefab.AddComponent<DefaultAuthoringComponent>().Field = 123;
            using (new BakerDataUtility.OverrideBakers(true, typeof(GameObjectBaker_AddBlobAsset)))
            {
                BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings);
                var blobRef = GetBakedSingleton<TestComponentWithBlobAssetReference>().Value;

                Assert.Throws<InvalidOperationException>(() => blobRef.Dispose());
            }
        }

        public class GetComponent_With_AbstractBaseClass : Authoring_DerivedFromAbstract { }

        public class GetComponent_With_AbstractBaseClass_Baker : Baker<GetComponent_With_AbstractBaseClass>
        {
            public override void Bake(GetComponent_With_AbstractBaseClass authoring)
            {
                var baseclass = GetComponent<Authoring_Abstract>();
                if (baseclass != null)
                {
                    AddComponent(GetEntity(TransformUsageFlags.None), new ComponentTest4(){Field = baseclass.Field});
                }
            }
        }

        public class GetComponentInParent_With_AbstractBaseClass : MonoBehaviour { }

        public class GetComponentInParent_With_AbstractBaseClass_Baker : Baker<GetComponentInParent_With_AbstractBaseClass>
        {
            public override void Bake(GetComponentInParent_With_AbstractBaseClass authoring)
            {
                var baseclass = GetComponentInParent<Authoring_Abstract>();
                if (baseclass != null)
                {
                    AddComponent(GetEntity(TransformUsageFlags.None), new ComponentTest4(){Field = baseclass.Field});
                }
            }
        }

        [Test]
        public void GetComponent_With_Abstract_BaseClass_DoesNotThrow()
        {
            m_Prefab.AddComponent<GetComponent_With_AbstractBaseClass>().Field = 3;
            using (new BakerDataUtility.OverrideBakers(true, typeof(GetComponent_With_AbstractBaseClass_Baker)))
            {
                Assert.DoesNotThrow(() => BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings));
                Assert.AreEqual(GetBakedSingleton<ComponentTest4>().Field, 3);
            }
        }

        [Test]
        public void GetComponentInParent_With_Abstract_BaseClass_DoesNotThrow()
        {
            m_Prefab.AddComponent<Authoring_DerivedFromAbstract>().Field = 3;
            GameObject child = new GameObject();
            child.transform.SetParent(m_Prefab.transform);
            child.AddComponent<GetComponentInParent_With_AbstractBaseClass>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(GetComponentInParent_With_AbstractBaseClass_Baker)))
            {
                Assert.DoesNotThrow(() => BakingUtility.BakeGameObjects(World, new[] {m_Prefab}, m_BakingSystem.BakingSettings));
                var testQuery = m_BakingSystem.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<ComponentTest4>());
                Assert.AreEqual(1, testQuery.CalculateEntityCount());
                var entity = testQuery.ToEntityArray(Allocator.Temp)[0];
                Assert.AreEqual(3, m_BakingSystem.EntityManager.GetComponentData<ComponentTest4>(entity).Field);
            }
        }

        public sealed class GetComponent_Of_AbstractAuthoring : Baker<DefaultAuthoringComponent>
        {
            public override void Bake(DefaultAuthoringComponent authoring)
            {
                GetComponents<AbstractAuthoring>();
            }
        }

        [Test]
        public void GetComponent_Of_AbstractAuthoring_DoesNotThrow()
        {
            m_PrefabWithAbtractMB.AddComponent<DefaultAuthoringComponent>();
            using (new BakerDataUtility.OverrideBakers(true, typeof(GetComponent_Of_AbstractAuthoring)))
            {
                Assert.DoesNotThrow(() => BakingUtility.BakeGameObjects(World, new[] {m_PrefabWithAbtractMB}, m_BakingSystem.BakingSettings));
            }
        }
    }
}
