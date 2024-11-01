#if !UNITY_DISABLE_MANAGED_COMPONENTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Tests
{
    [TestFixture]
    [IgnoreTest_IL2CPP("DOTSE-1903 - Users Properties which is broken in non-generic sharing IL2CPP builds")]
    public class ManagedObjectRemapTests
    {
        struct StructWithEntityReference
        {
            public Entity Entity;
        }

        public class ClassWithSelfReference : IComponentData
        {
            public ClassWithSelfReference Self;
        }
        
        class ClassWithEntityReference : IComponentData
        {
            public Entity Value;
        }
        
        class ClassWithStructWithEntityReference : IComponentData
        {
            public StructWithEntityReference Value;
        }
        
        class ClassWithArrayOfEntityReference : IComponentData
        {
            public Entity[] Value;
        }
        
        class ClassWithListOfEntityReference : IComponentData
        {
            public List<Entity> Value;
        }
        
        sealed class ClassWithArrayOfStructsWithEntityReference : IComponentData
        {
            public StructWithEntityReference[] Value;
        }

        sealed class ClassWithListOfStructsWithEntityReference : IComponentData
        {
            public List<StructWithEntityReference> Value;
        }

        sealed class ClassWithHashSetOfStructsWithEntityReference : IComponentData
        {
            public HashSet<StructWithEntityReference> Value;
        }

        sealed class ClassWithDictionaryOfStructsWithEntityReference : IComponentData
        {
            public Dictionary<string, StructWithEntityReference> Value;
        }
        
        NativeArray<EntityRemapUtility.EntityRemapInfo> m_Remapping;

        [SetUp]
        public void Setup()
        {
            m_Remapping = new NativeArray<EntityRemapUtility.EntityRemapInfo>(100, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            m_Remapping.Dispose();
        }

        [Test]
        public unsafe void ManagedObjectRemap_ClassWithSelfReference()
        {
            var data = new ClassWithSelfReference();
            data.Self = data;

            var managedObjectRemap = new ManagedObjectRemap();
            Assert.DoesNotThrow(() =>
            {
                var local = (object) data;
                managedObjectRemap.RemapEntityReferences(ref local, null);
            });
        }

        [Test]
        public unsafe void ManagedObjectRemap_ClassWithEntityReference()
        {
            var a = new Entity { Index = 1, Version = 2 };
            var b = new Entity { Index = 3, Version = 5 };
            
            EntityRemapUtility.AddEntityRemapping(ref m_Remapping, a, b);
            
            var data = new ClassWithEntityReference
            {
                Value = a
            };

            var managedObjectRemap = new ManagedObjectRemap();
            
            var obj = (object) data;
            managedObjectRemap.RemapEntityReferences(ref obj, (EntityRemapUtility.EntityRemapInfo*) m_Remapping.GetUnsafePtr());
            
            Assert.That(data.Value, Is.EqualTo(b));
        }

        [Test]
        public unsafe void ManagedObjectRemap_ClassWithStructWithEntityReference()
        {
            var a = new Entity { Index = 1, Version = 2 };
            var b = new Entity { Index = 3, Version = 5 };
            
            EntityRemapUtility.AddEntityRemapping(ref m_Remapping, a, b);
            
            var data = new ClassWithStructWithEntityReference
            {
                Value = new StructWithEntityReference { Entity = a }
            };

            var managedObjectRemap = new ManagedObjectRemap();
            
            var obj = (object) data;
            managedObjectRemap.RemapEntityReferences(ref obj, (EntityRemapUtility.EntityRemapInfo*) m_Remapping.GetUnsafePtr());
            
            Assert.That(data.Value.Entity, Is.EqualTo(b));
        }
        
        [Test]
        public unsafe void ManagedObjectRemap_ClassWithArrayOfEntityReference()
        {
            var a = new Entity { Index = 1, Version = 2 };
            var b = new Entity { Index = 3, Version = 5 };
            
            EntityRemapUtility.AddEntityRemapping(ref m_Remapping, a, b);
            
            var data = new ClassWithArrayOfEntityReference
            {
                Value = new [] { a }
            };

            var managedObjectRemap = new ManagedObjectRemap();
            
            var obj = (object) data;
            managedObjectRemap.RemapEntityReferences(ref obj, (EntityRemapUtility.EntityRemapInfo*) m_Remapping.GetUnsafePtr());
            
            Assert.That(data.Value[0], Is.EqualTo(b));
        }
        
        [Test]
        public unsafe void ManagedObjectRemap_ClassWithListOfEntityReference()
        {
            var a = new Entity { Index = 1, Version = 2 };
            var b = new Entity { Index = 3, Version = 5 };
            
            EntityRemapUtility.AddEntityRemapping(ref m_Remapping, a, b);
            
            var data = new ClassWithListOfEntityReference
            {
                Value = new List<Entity> { a }
            };

            var managedObjectRemap = new ManagedObjectRemap();
            
            var obj = (object) data;
            managedObjectRemap.RemapEntityReferences(ref obj, (EntityRemapUtility.EntityRemapInfo*) m_Remapping.GetUnsafePtr());
            
            Assert.That(data.Value[0], Is.EqualTo(b));
        }
        
        [Test]
        public unsafe void ManagedObjectRemap_ClassWithArrayOfStructsWithEntityReference()
        {
            var a = new Entity { Index = 1, Version = 2 };
            var b = new Entity { Index = 3, Version = 5 };
            
            EntityRemapUtility.AddEntityRemapping(ref m_Remapping, a, b);

            var data = new ClassWithArrayOfStructsWithEntityReference()
            {
                Value = new []
                {
                    new StructWithEntityReference
                    {
                        Entity = a
                    }
                }
            };

            var managedObjectRemap = new ManagedObjectRemap();
            
            var obj = (object) data;
            managedObjectRemap.RemapEntityReferences(ref obj, (EntityRemapUtility.EntityRemapInfo*) m_Remapping.GetUnsafePtr());

            Assert.That(data.Value[0].Entity, Is.EqualTo(b));
        }
        
        [Test]
        public unsafe void ManagedObjectRemap_ClassWithListOfStructsWithEntityReference()
        {
            var a = new Entity { Index = 1, Version = 2 };
            var b = new Entity { Index = 3, Version = 5 };
            
            EntityRemapUtility.AddEntityRemapping(ref m_Remapping, a, b);

            var data = new ClassWithListOfStructsWithEntityReference()
            {
                Value = new List<StructWithEntityReference>
                {
                    new StructWithEntityReference
                    {
                        Entity = a
                    }
                }
            };

            var managedObjectRemap = new ManagedObjectRemap();
            var obj = (object) data;
            managedObjectRemap.RemapEntityReferences(ref obj, (EntityRemapUtility.EntityRemapInfo*) m_Remapping.GetUnsafePtr());
            
            Assert.That(data.Value[0].Entity, Is.EqualTo(b));
        }
        
        [Test, Ignore("Not supported yet")]
        public unsafe void ManagedObjectRemap_ClassWithHashSetOfStructsWithEntityReference()
        {
            var a = new Entity { Index = 1, Version = 2 };
            var b = new Entity { Index = 3, Version = 5 };
            
            EntityRemapUtility.AddEntityRemapping(ref m_Remapping, a, b);

            var data = new ClassWithHashSetOfStructsWithEntityReference
            {
                Value = new HashSet<StructWithEntityReference>
                {
                    new StructWithEntityReference
                    {
                        Entity = a
                    }
                }
            };

            var managedObjectRemap = new ManagedObjectRemap();
            var obj = (object) data;
            managedObjectRemap.RemapEntityReferences(ref obj, (EntityRemapUtility.EntityRemapInfo*) m_Remapping.GetUnsafePtr());
            
            Assert.That(data.Value.First().Entity, Is.EqualTo(b));
        }
        
        [Test, Ignore("Not supported yet")]
        public unsafe void ManagedObjectRemap_ClassWithDictionaryOfStructsWithEntityReference()
        {
            var a = new Entity { Index = 1, Version = 2 };
            var b = new Entity { Index = 3, Version = 5 };
            
            EntityRemapUtility.AddEntityRemapping(ref m_Remapping, a, b);

            var data = new ClassWithDictionaryOfStructsWithEntityReference
            {
                Value = new Dictionary<string, StructWithEntityReference>
                {
                    {
                        "a",
                        new StructWithEntityReference
                        {
                            Entity = a
                        }
                    }
                }
            };

            var managedObjectRemap = new ManagedObjectRemap();
            var obj = (object) data;
            managedObjectRemap.RemapEntityReferences(ref obj, (EntityRemapUtility.EntityRemapInfo*) m_Remapping.GetUnsafePtr());
            
            Assert.That(data.Value.First().Value.Entity, Is.EqualTo(b));
        }
    }
}
#endif
