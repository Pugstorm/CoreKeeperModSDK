using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Tests;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Unity.Entities.PerformanceTests
{
    using static AspectUtils;
    readonly partial struct PerfTestAspect : IAspect
    {
        readonly public RefRW<EcsTestFloatData3> Output;
        readonly public RefRO<EcsTestFloatData> Input;
    }

    [TestFixture]
    [Category("Performance")]
    [BurstCompile]
    public partial class SystemUpdatePerformanceTests : EntityPerformanceTestFixture
    {
        static IEnumerable TestCombinations_WithoutEnable()
        {
            /*
            // Parameters: int iterationCount, int loopsPerSystem, int entity Count
            yield return new TestCaseData(250, 10, 1);
            yield return new TestCaseData(500, 5, 1);
            yield return new TestCaseData(500, 5, 10);
            yield return new TestCaseData(500, 5, 100);
            yield return new TestCaseData(500, 5, 1000);
            */
            yield return new TestCaseData(500, 5, 1000);
            yield return new TestCaseData(5, 5, 100000);
        }

        static IEnumerable TestCombinations()
        {
            /*
            // Parameters: int iterationCount, int loopsPerSystem, int entity Count, EnabledBitsMode enableBitsMode
            yield return new TestCaseData(250, 10, 1, EnabledBitsMode.NoEnableableComponents);
            yield return new TestCaseData(250, 10, 1, EnabledBitsMode.NoComponentsDisabled);
            yield return new TestCaseData(500, 5, 1, EnabledBitsMode.NoEnableableComponents);
            yield return new TestCaseData(500, 5, 10, EnabledBitsMode.NoEnableableComponents);
            yield return new TestCaseData(500, 5, 10, EnabledBitsMode.NoComponentsDisabled);
            yield return new TestCaseData(500, 5, 10, EnabledBitsMode.ManyComponentsDisabled);
            yield return new TestCaseData(500, 5, 100, EnabledBitsMode.NoEnableableComponents);
            yield return new TestCaseData(500, 5, 1000, EnabledBitsMode.NoEnableableComponents);
            yield return new TestCaseData(500, 5, 1000, EnabledBitsMode.NoComponentsDisabled);
            yield return new TestCaseData(500, 5, 1000, EnabledBitsMode.FewComponentsDisabled);
            yield return new TestCaseData(500, 5, 1000, EnabledBitsMode.ManyComponentsDisabled);
            */
            yield return new TestCaseData(500, 5, 1000, EnabledBitsMode.NoEnableableComponents);

            yield return new TestCaseData(5, 5, 100000, EnabledBitsMode.NoEnableableComponents);
           yield return new TestCaseData(5, 5, 100000, EnabledBitsMode.NoComponentsDisabled);
            yield return new TestCaseData(5, 5, 100000, EnabledBitsMode.FewComponentsDisabled);
            yield return new TestCaseData(5, 5, 100000, EnabledBitsMode.ManyComponentsDisabled);
            yield return new TestCaseData(5, 5, 100000, EnabledBitsMode.MostComponentsDisabled);
        }


        unsafe public partial class BenchMarkReferenceClassArrayLoop : SystemBase
        {
            // Assign values to these fields post-OnCreate() based on test case settings, before the first Update()
            public int IterationCount;
            public int LoopsPerSystem;
            public int EntityCount;

            class OtherThing
            {
                public float Value;
            }

            class MyThing
            {
                public float3 Value;

                public OtherThing Reference;

            }
            private MyThing[] MyArray;

            void UpdateThing()
            {
                for (int l = 0; l != LoopsPerSystem; l++)
                {
                    int len = MyArray.Length;
                    for (int i = 0; i != len; i++)
                    {
                        MyThing thing = MyArray[i];
                        thing.Value += thing.Reference.Value + 1;
                    }
                }
            }

            protected override void OnUpdate()
            {
                for (int i = 0; i != IterationCount; i++)
                    UpdateThing();
            }

            protected override void OnStartRunning()
            {
                base.OnStartRunning();

                MyArray = new MyThing[EntityCount];
                for (int i = 0; i != MyArray.Length; i++)
                {
                    MyArray[i] = new MyThing();
                    MyArray[i].Reference = new OtherThing();
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        unsafe public partial class BenchmarkReferenceBurstArrayLoop : SystemBase
        {
            // Assign values to these fields post-OnCreate() based on test case settings, before the first Update()
            public int IterationCount;
            public int LoopsPerSystem;
            public int EntityCount;

            struct Data
            {
                [NoAlias]
                public float3* array;
                [NoAlias]
                public float*  array2;
                public int Count;
            }

            [BurstCompile(CompileSynchronously = true)]
            public static void RunBenchmark(int loopsPerSystem, void* ptr)
            {
                for (int l = 0; l != loopsPerSystem; l++)
                {
                    Data* data = (Data*)ptr;
                    int count = data->Count;
                    for (int i = 0; i != count; i++)
                    {
                        data->array[i] += data->array2[i] + 1;
                    }
                }
            }

            delegate void BenchDelegate(int loopsPerSystem, void* ptr);
            private Data _data;
            private FunctionPointer<BenchDelegate> _BenchFunc;

            protected override void OnStartRunning()
            {
                base.OnCreate();

                _data.Count = EntityCount;
                _data.array = (float3*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<float3>() * _data.Count, 16, Allocator.Persistent);
                _data.array2 = (float*)UnsafeUtility.Malloc(UnsafeUtility.SizeOf<float>() * _data.Count, 16, Allocator.Persistent);
                _BenchFunc = BurstCompiler.CompileFunctionPointer<BenchDelegate>(RunBenchmark);
            }

            protected override void OnStopRunning()
            {
                base.OnStopRunning();

                UnsafeUtility.Free(_data.array, Allocator.Persistent);
                UnsafeUtility.Free(_data.array2, Allocator.Persistent);
            }

            protected override void OnUpdate()
            {
                var delegateFunc = _BenchFunc.Invoke;
                fixed (Data* data = &_data)
                {
                    for (int i = 0; i != IterationCount; i++)
                        delegateFunc(LoopsPerSystem, data);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        unsafe struct MySystemBenchmarkJobChunk : IJobChunk
        {
            public ComponentTypeHandle<EcsTestFloatData3> RotationTypeHandle;
            [ReadOnly] public ComponentTypeHandle<EcsTestFloatData> RotationSpeedTypeHandle;
            public float Singleton;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var chunkRotations = (float3*)chunk.GetComponentDataPtrRW(ref RotationTypeHandle);
                var chunkRotationSpeeds = (float*)chunk.GetComponentDataPtrRO(ref RotationSpeedTypeHandle);

                ChunkEntityEnumerator enumerator =
                    new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                while(enumerator.NextEntityIndex(out var i))
                {
                    chunkRotations[i] += chunkRotationSpeeds[i] + Singleton;
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        unsafe partial struct MySystemBenchmarkJobEntity : IJobEntity
        {
            public void Execute(ref EcsTestFloatData3 rotation, in EcsTestFloatData rotationSpeed)
            {
                rotation.Value0 += rotationSpeed.Value;
                rotation.Value1 += rotationSpeed.Value;
                rotation.Value2 += rotationSpeed.Value;
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public partial struct MyBufferSystem : ISystem, ISystemStartStop, ISetLoopMode
        {
            [InternalBufferCapacity(10)]
            public struct EcsFloatElement : IBufferElementData
            {
                public float Value;
            }

            [BurstCompile(CompileSynchronously = true)]
            unsafe struct MySystemBufferBenchmarkJob : IJobChunk
            {
                public BufferTypeHandle<EcsFloatElement> BufferHandle;
                [ReadOnly] public ComponentTypeHandle<EcsTestFloatData> DataHandle;
                public float Singleton;

                public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
                {
                    var buffers = chunk.GetBufferAccessor(ref BufferHandle);
                    var datas = (EcsTestData*)chunk.GetComponentDataPtrRO(ref DataHandle);

                    var enumerator =
                        new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                    while(enumerator.NextEntityIndex(out var e))
                    {
                        var buffer = buffers[e];
                        for (int i = 0; i != buffer.Length; i++)
                        {
                            buffer.ElementAt(i).Value += buffer[i].Value + datas[i].value + Singleton;
                        }
                    }
                }
            }

            public void Set(int loopsPerSystem, EnabledBitsMode mode)
            {
                LoopsPerSystem = loopsPerSystem;
                Mode = mode;
            }
            public int LoopsPerSystem;
            public EnabledBitsMode Mode;

            private EntityQuery _Query;

            BufferTypeHandle<EcsFloatElement> _FloatBufferHandle;
            ComponentTypeHandle<EcsTestFloatData> _ComponentFloatHandle;
            public void OnCreate(ref SystemState state)
            {
                _FloatBufferHandle = state.GetBufferTypeHandle<EcsFloatElement>();
                _ComponentFloatHandle = state.GetComponentTypeHandle<EcsTestFloatData>();
            }

            public void OnStartRunning(ref SystemState state)
            {
                if (Mode == EnabledBitsMode.NoEnableableComponents)
                    _Query = state.GetEntityQuery(ComponentType.ReadWrite<EcsFloatElement>(), ComponentType.ReadOnly<EcsTestFloatData>());
                else
                    _Query = state.GetEntityQuery(ComponentType.ReadWrite<EcsFloatElement>(), ComponentType.ReadOnly<EcsTestFloatData>(), ComponentType.ReadOnly<EcsTestDataEnableable>());
            }

            public void OnStopRunning(ref SystemState state)
            {
            }

            [BurstDiscard]
            static void CheckRunningBurst()
            {
                throw new ArgumentException("Not running burst");
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                CheckRunningBurst();

                for (int i = 0; i != LoopsPerSystem; i++)
                {
                    _FloatBufferHandle.Update(ref state);
                    _ComponentFloatHandle.Update(ref state);
                    var job = new MySystemBufferBenchmarkJob
                    {
                        BufferHandle = _FloatBufferHandle,
                        DataHandle = _ComponentFloatHandle,
                    };
                    Internal.InternalCompilerInterface.JobChunkInterface.RunWithoutJobs(ref job, _Query);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public partial struct MyStructSystem_IJobChunk : ISystem, ISystemStartStop, ISetLoopMode
        {
            // Assign values to these fields post-OnCreate() based on test case settings, before the first Update()
            public int LoopsPerSystem;
            public EnabledBitsMode Mode;

            private EntityQuery _Query;

            public void Set(int loopsPerSystem, EnabledBitsMode mode)
            {
                LoopsPerSystem = loopsPerSystem;
                Mode = mode;
            }
            ComponentTypeHandle<EcsTestFloatData3> _RotationTypeHandle;
            ComponentTypeHandle<EcsTestFloatData> _RotationSpeedTypeHandle;
            public void OnCreate(ref SystemState state)
            {
                _RotationTypeHandle = state.GetComponentTypeHandle<EcsTestFloatData3>();
                _RotationSpeedTypeHandle = state.GetComponentTypeHandle<EcsTestFloatData>();
            }

            public void OnStartRunning(ref SystemState state)
            {
                if (Mode == EnabledBitsMode.NoEnableableComponents)
                    _Query = state.GetEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>(),
                        ComponentType.ReadOnly<EcsTestFloatData>());
                else
                    _Query = state.GetEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>(),
                        ComponentType.ReadOnly<EcsTestFloatData>(), ComponentType.ReadOnly<EcsTestDataEnableable>());
            }

            public void OnStopRunning(ref SystemState state)
            {
            }

            [BurstDiscard]
            static void CheckRunningBurst()
            {
                throw new ArgumentException("Not running burst");
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                CheckRunningBurst();

                for (int i = 0; i != LoopsPerSystem; i++)
                {
                    _RotationTypeHandle.Update(ref state);
                    _RotationSpeedTypeHandle.Update(ref state);
                    var job = new MySystemBenchmarkJobChunk
                    {
                        RotationTypeHandle = _RotationTypeHandle,
                        RotationSpeedTypeHandle = _RotationSpeedTypeHandle,
                    };
                    Internal.InternalCompilerInterface.JobChunkInterface.RunWithoutJobs(ref job, _Query);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public partial struct MyStructSystem_IJobEntity : ISystem, ISystemStartStop, ISetLoopMode
        {
            // Assign values to these fields post-OnCreate() based on test case settings, before the first Update()
            public int LoopsPerSystem;
            public EnabledBitsMode Mode;

            private EntityQuery _Query;

            public void Set(int loopsPerSystem, EnabledBitsMode mode)
            {
                LoopsPerSystem = loopsPerSystem;
                Mode = mode;
            }
            public void OnStartRunning(ref SystemState state)
            {
                if (Mode == EnabledBitsMode.NoEnableableComponents)
                    _Query = state.GetEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>(),
                        ComponentType.ReadOnly<EcsTestFloatData>());
                else
                    _Query = state.GetEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>(),
                        ComponentType.ReadOnly<EcsTestFloatData>(), ComponentType.ReadOnly<EcsTestDataEnableable>());
            }

            public void OnStopRunning(ref SystemState state)
            {
            }

            [BurstDiscard]
            static void CheckRunningBurst()
            {
                throw new ArgumentException("Not running burst");
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                CheckRunningBurst();

                for (int i = 0; i != LoopsPerSystem; i++)
                {
                    new MySystemBenchmarkJobEntity().Run(_Query);
                }
            }
        }

        struct SingletonData : IComponentData
        {
            public float Value;
        }

        [BurstCompile(CompileSynchronously = true)]
        public partial struct MyStructSystemWithSingleton : ISystem, ISystemStartStop, ISetLoopMode
        {
            // Assign values to these fields post-OnCreate() based on test case settings, before the first Update()
            public int LoopsPerSystem;
            public EnabledBitsMode Mode;

            private EntityQuery _Query;
            private EntityQuery _SingletonData;

            public void Set(int loopsPerSystem, EnabledBitsMode mode)
            {
                LoopsPerSystem = loopsPerSystem;
                Mode = mode;
            }
            ComponentTypeHandle<EcsTestFloatData3> _RotationTypeHandle;
            ComponentTypeHandle<EcsTestFloatData> _RotationSpeedTypeHandle;
            public void OnCreate(ref SystemState state)
            {
                _SingletonData = state.GetEntityQuery(typeof(SingletonData));
                _RotationTypeHandle = state.GetComponentTypeHandle<EcsTestFloatData3>();
                _RotationSpeedTypeHandle = state.GetComponentTypeHandle<EcsTestFloatData>();
            }

            public void OnStartRunning(ref SystemState state)
            {
                if (Mode == EnabledBitsMode.NoEnableableComponents)
                    _Query = state.GetEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>(),
                        ComponentType.ReadOnly<EcsTestFloatData>());
                else
                    _Query = state.GetEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>(),
                        ComponentType.ReadOnly<EcsTestFloatData>(), ComponentType.ReadOnly<EcsTestDataEnableable>());
            }

            public void OnStopRunning(ref SystemState state)
            {
            }

            [BurstDiscard]
            static void CheckRunningBurst()
            {
                throw new ArgumentException("Not running burst");
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                CheckRunningBurst();

                for (int i = 0; i != LoopsPerSystem; i++)
                {
                    _RotationTypeHandle.Update(ref state);
                    _RotationSpeedTypeHandle.Update(ref state);
                    var singletonData = _SingletonData.GetSingleton<SingletonData>();

                    var job = new MySystemBenchmarkJobChunk
                    {
                        RotationTypeHandle = _RotationTypeHandle,
                        RotationSpeedTypeHandle = _RotationSpeedTypeHandle,
                        Singleton = singletonData.Value
                    };
                    Internal.InternalCompilerInterface.JobChunkInterface.RunWithoutJobs(ref job, _Query);
                }
            }
        }


        [BurstCompile(CompileSynchronously = true)]
        public partial struct MyStructRunSystem : ISystem, ISystemStartStop, ISetLoopMode
        {
            // Assign values to these fields post-OnCreate() based on test case settings, before the first Update()
            public int LoopsPerSystem;
            public EnabledBitsMode Mode;

            private EntityQuery _Query;

            public void Set(int loopsPerSystem, EnabledBitsMode mode)
            {
                LoopsPerSystem = loopsPerSystem;
                Mode = mode;
            }
            ComponentTypeHandle<EcsTestFloatData3> _RotationTypeHandle;
            ComponentTypeHandle<EcsTestFloatData> _RotationSpeedTypeHandle;
            public void OnCreate(ref SystemState state)
            {
                _RotationTypeHandle = state.GetComponentTypeHandle<EcsTestFloatData3>();
                _RotationSpeedTypeHandle = state.GetComponentTypeHandle<EcsTestFloatData>();
            }

            public void OnStartRunning(ref SystemState state)
            {
                if (Mode == EnabledBitsMode.NoEnableableComponents)
                    _Query = state.GetEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>(),
                        ComponentType.ReadOnly<EcsTestFloatData>());
                else
                    _Query = state.GetEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>(),
                        ComponentType.ReadOnly<EcsTestFloatData>(), ComponentType.ReadOnly<EcsTestDataEnableable>());
            }

            public void OnStopRunning(ref SystemState state)
            {
            }

            [BurstDiscard]
            static void CheckRunningBurst()
            {
                throw new ArgumentException("Not running burst");
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                CheckRunningBurst();

                for (int i = 0; i != LoopsPerSystem; i++)
                {
                    _RotationTypeHandle.Update(ref state);
                    _RotationSpeedTypeHandle.Update(ref state);
                    var job = new MySystemBenchmarkJobChunk
                    {
                        RotationTypeHandle = _RotationTypeHandle,
                        RotationSpeedTypeHandle = _RotationSpeedTypeHandle,
                    };
                    job.RunByRef(_Query);
                }
            }
        }


        [BurstCompile(CompileSynchronously = true)]
        public partial struct StructSystem_Aspect_foreach : ISystem, ISystemStartStop, ISetLoopMode
        {
            // Assign values to these fields post-OnCreate() based on test case settings, before the first Update()
            public int LoopsPerSystem;
            public EnabledBitsMode Mode;

            private EntityQuery _Query;
            PerfTestAspect.TypeHandle _TypeHandle;
            float singleton;

            public void Set(int loopsPerSystem, EnabledBitsMode mode)
            {
                LoopsPerSystem = loopsPerSystem;
                Mode = mode;
            }
            public void OnCreate(ref SystemState state)
            {
                _TypeHandle = new PerfTestAspect.TypeHandle(ref state);
                singleton = 1.0F;
            }

            public void OnStartRunning(ref SystemState state)
            {
                if (Mode == EnabledBitsMode.NoEnableableComponents)
                    _Query = state.GetEntityQuery(GetRequiredComponents<PerfTestAspect>());
                else
                    _Query = state.GetEntityQuery(ComponentType.Combine(GetRequiredComponents<PerfTestAspect>(), new ComponentType[] { ComponentType.ReadOnly<EcsTestDataEnableable>() } ));
            }

            public void OnStopRunning(ref SystemState state)
            {
            }

            [BurstDiscard]
            static void CheckRunningBurst()
            {
                throw new ArgumentException("Not running burst");
            }

            [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
            public void OnUpdate(ref SystemState state)
            {
                CheckRunningBurst();

                float s = singleton;
                for (int i = 0; i != LoopsPerSystem; i++)
                {
                    _TypeHandle.Update(ref state);
                    foreach (var a in PerfTestAspect.Query(_Query, _TypeHandle))
                    {
                        ref var output = ref a.Output.ValueRW;
                        var input = a.Input.ValueRO;
                        output.Value0  += input.Value + s;
                        output.Value1  += input.Value + s;
                        output.Value2  += input.Value + s;
                    }
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        public partial struct StructSystem_Aspect_IJobChunk_RunWithoutJobs : ISystem, ISystemStartStop, ISetLoopMode
        {
            // Assign values to these fields post-OnCreate() based on test case settings, before the first Update()
            public int LoopsPerSystem;
            public EnabledBitsMode Mode;

            private EntityQuery _Query;
            PerfTestAspect.TypeHandle _TypeHandle;
            float singleton;

            unsafe struct AspectJob : IJobChunk
            {
                public PerfTestAspect.TypeHandle Aspect;
                public float Singleton;

                public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
                {
                    var aspects = Aspect.Resolve(chunk);
                    int count = aspects.Length;
                    for (var i = 0; i < count; i++)
                    {
                        ref var output = ref aspects[i].Output.ValueRW;
                        var input = aspects[i].Input.ValueRO;
                        output.Value0  += input.Value + Singleton;
                        output.Value1  += input.Value + Singleton;
                        output.Value2  += input.Value + Singleton;
                    }
                }
            }

            public void Set(int loopsPerSystem, EnabledBitsMode mode)
            {
                LoopsPerSystem = loopsPerSystem;
                Mode = mode;
            }
            public void OnCreate(ref SystemState state)
            {
                _TypeHandle = new PerfTestAspect.TypeHandle(ref state);
                singleton = 1.0F;
            }

            public void OnStartRunning(ref SystemState state)
            {
                if (Mode == EnabledBitsMode.NoEnableableComponents)
                    _Query = state.GetEntityQuery(GetRequiredComponents<PerfTestAspect>());
                else
                    _Query = state.GetEntityQuery(ComponentType.Combine(GetRequiredComponents<PerfTestAspect>(), new ComponentType[] { ComponentType.ReadOnly<EcsTestDataEnableable>() } ));
            }

            public void OnStopRunning(ref SystemState state)
            {
            }

            [BurstDiscard]
            static void CheckRunningBurst()
            {
                throw new ArgumentException("Not running burst");
            }

            [BurstCompile(CompileSynchronously = true, DisableSafetyChecks = true)]
            public void OnUpdate(ref SystemState state)
            {
                CheckRunningBurst();

                float s = singleton;
                for (int i = 0; i != LoopsPerSystem; i++)
                {
                    _TypeHandle.Update(ref state);

                    var job = new AspectJob {Singleton = singleton, Aspect = _TypeHandle};
                    Internal.InternalCompilerInterface.JobChunkInterface.RunByRefWithoutJobs(ref job, _Query);
                }
            }
        }


        [BurstCompile(CompileSynchronously = true)]
        public partial struct MyStructScheduleSystem : ISystem, ISystemStartStop, ISetLoopMode
        {
            // Assign values to these fields post-OnCreate() based on test case settings, before the first Update()
            public int LoopsPerSystem;
            public EnabledBitsMode Mode;

            private EntityQuery _Query;
            public void Set(int loopsPerSystem, EnabledBitsMode mode)
            {
                LoopsPerSystem = loopsPerSystem;
                Mode = mode;
            }
            ComponentTypeHandle<EcsTestFloatData3> _RotationTypeHandle;
            ComponentTypeHandle<EcsTestFloatData> _RotationSpeedTypeHandle;
            public void OnCreate(ref SystemState state)
            {
                _RotationTypeHandle = state.GetComponentTypeHandle<EcsTestFloatData3>();
                _RotationSpeedTypeHandle = state.GetComponentTypeHandle<EcsTestFloatData>();
            }

            public void OnStartRunning(ref SystemState state)
            {
                if (Mode == EnabledBitsMode.NoEnableableComponents)
                    _Query = state.GetEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>(),
                        ComponentType.ReadOnly<EcsTestFloatData>());
                else
                    _Query = state.GetEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>(),
                        ComponentType.ReadOnly<EcsTestFloatData>(), ComponentType.ReadOnly<EcsTestDataEnableable>());
            }

            public void OnStopRunning(ref SystemState state)
            {
            }

            [BurstDiscard]
            static void CheckRunningBurst()
            {
                throw new ArgumentException("Not running burst");
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                CheckRunningBurst();

                for (int i = 0; i != LoopsPerSystem; i++)
                {
                    _RotationTypeHandle.Update(ref state);
                    _RotationSpeedTypeHandle.Update(ref state);
                    var job = new MySystemBenchmarkJobChunk
                    {
                        RotationTypeHandle = _RotationTypeHandle,
                        RotationSpeedTypeHandle = _RotationSpeedTypeHandle,
                    };
                    state.Dependency = job.ScheduleByRef(_Query, state.Dependency);
                }
            }
        }

        public partial class MyClassSystem : SystemBase, ISetLoopMode
        {
            // Assign values to these fields post-OnCreate() based on test case settings, before the first Update()
            public int LoopsPerSystem;
            public EnabledBitsMode Mode;

            private EntityQuery _Query;

            ComponentTypeHandle<EcsTestFloatData3> _RotationTypeHandle;
            ComponentTypeHandle<EcsTestFloatData> _RotationSpeedTypeHandle;

            public void Set(int loopsPerSystem, EnabledBitsMode mode)
            {
                LoopsPerSystem = loopsPerSystem;
                Mode = mode;
            }
            protected override void OnCreate()
            {
                _RotationTypeHandle = GetComponentTypeHandle<EcsTestFloatData3>();
                _RotationSpeedTypeHandle = GetComponentTypeHandle<EcsTestFloatData>();
            }

            protected override void OnStartRunning()
            {
                base.OnStartRunning();
                if (Mode == EnabledBitsMode.NoEnableableComponents)
                    _Query = GetEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>(),
                        ComponentType.ReadOnly<EcsTestFloatData>());
                else
                    _Query = GetEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>(),
                        ComponentType.ReadOnly<EcsTestFloatData>(), ComponentType.ReadOnly<EcsTestDataEnableable>());
            }

            protected override void OnUpdate()
            {
                for (int i = 0; i != LoopsPerSystem; i++)
                {
                    _RotationTypeHandle.Update(this);
                    _RotationSpeedTypeHandle.Update(this);
                    var job = new MySystemBenchmarkJobChunk
                    {
                        RotationTypeHandle = _RotationTypeHandle,
                        RotationSpeedTypeHandle = _RotationSpeedTypeHandle,
                    };
                    Internal.InternalCompilerInterface.JobChunkInterface.RunWithoutJobs(ref job, _Query);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        unsafe public partial class MyClassSystemWithBurstForEach : SystemBase, ISetLoopMode
        {
            // Assign values to these fields post-OnCreate() based on test case settings, before the first Update()
            public int LoopsPerSystem;
            public EnabledBitsMode Mode;

            EntityQuery _Query;
            ComponentTypeHandle<EcsTestFloatData3> _RotationTypeHandle;
            ComponentTypeHandle<EcsTestFloatData> _RotationSpeedTypeHandle;

            public void Set(int loopsPerSystem, EnabledBitsMode mode)
            {
                LoopsPerSystem = loopsPerSystem;
                Mode = mode;
            }

            [BurstCompile(CompileSynchronously = true)]
            static void RunBursted(void* jobPtr, EntityQuery* query)
            {
                ref var job = ref *(MySystemBenchmarkJobChunk*)jobPtr;
                Internal.InternalCompilerInterface.JobChunkInterface.RunByRefWithoutJobs(ref job, *query);
            }

            unsafe delegate void RunBurstedCallback(void* job, EntityQuery* query);


            static private RunBurstedCallback _Callback;
            protected override void OnCreate()
            {
                _RotationTypeHandle = GetComponentTypeHandle<EcsTestFloatData3>();
                _RotationSpeedTypeHandle = GetComponentTypeHandle<EcsTestFloatData>();

                if (_Callback == null)
                    _Callback = BurstCompiler.CompileFunctionPointer<RunBurstedCallback>(RunBursted).Invoke;
            }

            protected override void OnStartRunning()
            {
                base.OnStartRunning();
                if (Mode == EnabledBitsMode.NoEnableableComponents)
                    _Query = GetEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>(),
                        ComponentType.ReadOnly<EcsTestFloatData>());
                else
                    _Query = GetEntityQuery(ComponentType.ReadWrite<EcsTestFloatData3>(),
                        ComponentType.ReadOnly<EcsTestFloatData>(), ComponentType.ReadOnly<EcsTestDataEnableable>());
            }

            protected override void OnUpdate()
            {
                for (int i = 0; i != LoopsPerSystem; i++)
                {
                    _RotationTypeHandle.Update(this);
                    _RotationSpeedTypeHandle.Update(this);
                    var job = new MySystemBenchmarkJobChunk
                    {
                        RotationTypeHandle = _RotationTypeHandle,
                        RotationSpeedTypeHandle = _RotationSpeedTypeHandle,
                    };

                    _Callback(UnsafeUtility.AddressOf(ref job), (EntityQuery*)UnsafeUtility.AddressOf(ref _Query));
                }
            }
        }

        public interface ISetLoopMode
        {
            void Set(int loopsPerSystem, EnabledBitsMode mode);
        }

        public partial class BenchmarkSystemGroup : ComponentSystemGroup
        {
            public unsafe void CreateUnmanagedSystems<T>(int iterationCount, int loopsPerSystem, EnabledBitsMode mode) where T : unmanaged, ISystem, ISetLoopMode
            {
                for (int i = 0; i != iterationCount; i++)
                {
                    var res = World.CreateSystem<T>();
                    World.Unmanaged.GetUnsafeSystemRef<T>(res).Set(loopsPerSystem, mode);
                    AddSystemToUpdateList(res);
                }
            }

            public void CreateManagedSystems<T>(int iterationCount, int loopsPerSystem, EnabledBitsMode mode) where T : ComponentSystemBase, ISetLoopMode, new()
            {
                for (int i = 0; i != iterationCount; i++)
                {
                    var res = World.CreateSystemManaged<T>();
                    res.Set(loopsPerSystem, mode);
                    AddSystemToUpdateList(res);
                }
            }

            protected override void OnUpdate()
            {
                base.OnUpdate();
                EntityManager.CompleteAllTrackedJobs();
            }
        }

        public enum EnabledBitsMode
        {
            NoEnableableComponents,
            NoComponentsDisabled,
            FewComponentsDisabled,
            ManyComponentsDisabled,
            MostComponentsDisabled,
        }

        void CreateBufferTestEntities(int entityCount, EnabledBitsMode enabledBitsMode)
        {
            // Create the entities that will match the test queries
            var types = new List<ComponentType>
            {
                typeof(MyBufferSystem.EcsFloatElement), typeof(EcsTestFloatData), typeof(SceneTag)
            };

            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                types.Add(typeof(EcsTestDataEnableable));

            var archPos = m_Manager.CreateArchetype(types.ToArray());
            var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, m_World.UpdateAllocator.ToAllocator);
            m_Manager.CreateEntity(archPos, entities);
            for (int i = 0; i != entities.Length; i++)
            {
                m_Manager.GetBuffer<MyBufferSystem.EcsFloatElement>(entities[i]).Resize(10, NativeArrayOptions.ClearMemory);
            }

            CreateAdditionalEntityData(entityCount, enabledBitsMode, archPos, entities);
        }
        void CreateTestEntities(int entityCount, EnabledBitsMode enabledBitsMode)
        {
            // Create the entities that will match the test queries
            var types = new List<ComponentType>
            {
                typeof(EcsTestFloatData3), typeof(EcsTestFloatData),
                typeof(EcsTestData), typeof(EcsTestData2), typeof(SceneTag)
            };
            if (enabledBitsMode != EnabledBitsMode.NoEnableableComponents)
                types.Add(typeof(EcsTestDataEnableable));
            var archPos = m_Manager.CreateArchetype(types.ToArray());
            var entities = CollectionHelper.CreateNativeArray<Entity>(entityCount, m_World.UpdateAllocator.ToAllocator);
            m_Manager.CreateEntity(archPos, entities);

            CreateAdditionalEntityData(entityCount, enabledBitsMode, archPos, entities);
        }

        private void CreateAdditionalEntityData(int entityCount, EnabledBitsMode enabledBitsMode, EntityArchetype archPos, NativeArray<Entity> entities)
        {
            if (enabledBitsMode == EnabledBitsMode.FewComponentsDisabled)
            {
                for (int i = 0; i < entityCount; i += archPos.ChunkCapacity)
                {
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
                }
            }
            else if (enabledBitsMode == EnabledBitsMode.ManyComponentsDisabled)
            {
                for (int i = 0; i < entityCount; i += 2)
                {
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
                }
            }
            else if (enabledBitsMode == EnabledBitsMode.MostComponentsDisabled)
            {
                // Disable component on all entities
                for (int i = 0; i < entityCount; i++)
                {
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], false);
                }
                // Re-enable one entity every few chunks
                for (int i = 0; i < entityCount; i += 10*archPos.ChunkCapacity)
                {
                    m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i], true);
                }
            }

            // Create a bunch of entities in a different archetype that won't match the test queries
            var archNeg = m_Manager.CreateArchetype(typeof(EcsTestData4), typeof(EcsTestData3));
            m_Manager.CreateEntity(archNeg, 100000);

            // Create the singleton used to conditionally execute certain test systems
            m_Manager.CreateEntity(typeof(SingletonData));
        }

        [TestCaseSource(nameof(TestCombinations_WithoutEnable))]
        [Performance]
        public void SystemUpdatePerformance_ReferenceArrayLoop(int iterationCount, int loopsPerSystem, int entityCount)
        {
            var sys = m_World.CreateSystemManaged<BenchMarkReferenceClassArrayLoop>();
            sys.IterationCount = iterationCount;
            sys.LoopsPerSystem = loopsPerSystem;
            sys.EntityCount = entityCount;
            Measure.Method(
                    () =>
                    {
                        sys.Update();
                    })
                .WarmupCount(1)
                .MeasurementCount(9)
                .Run();
        }

        [TestCaseSource(nameof(TestCombinations_WithoutEnable))]
        [Performance]
        public void SystemUpdatePerformance_ReferenceArrayLoop_WithBurst(int iterationCount, int loopsPerSystem, int entityCount)
        {
            var sys = m_World.CreateSystemManaged<BenchmarkReferenceBurstArrayLoop>();
            sys.IterationCount = iterationCount;
            sys.LoopsPerSystem = loopsPerSystem;
            sys.EntityCount = entityCount;
            Measure.Method(
                    () =>
                    {
                        sys.Update();
                    })
                .WarmupCount(1)
                .MeasurementCount(9)
                .Run();
        }



        [Performance]
        [TestCaseSource(nameof(TestCombinations))]
        public void SystemUpdatePerformance_StructSystem_IJobChunk_RunWithoutJobs(int iterationCount, int loopsPerSystem, int entityCount, EnabledBitsMode enabledBitsMode)
        {
            CreateTestEntities(entityCount, enabledBitsMode);

            var group = m_World.CreateSystemManaged<BenchmarkSystemGroup>();
            group.CreateUnmanagedSystems<MyStructSystem_IJobChunk>(iterationCount, loopsPerSystem, enabledBitsMode);

            Measure.Method(
                    () =>
                    {
                        group.Update();
                    })
                .WarmupCount(1)
                .MeasurementCount(9)
                .CleanUp(() => { m_World.UpdateAllocator.Rewind();})
                .Run();
        }

        [Performance]
        [TestCaseSource(nameof(TestCombinations))]
        public void SystemUpdatePerformance_StructSystem_IJobEntity_Run(int iterationCount, int loopsPerSystem, int entityCount, EnabledBitsMode enabledBitsMode)
        {
            CreateTestEntities(entityCount, enabledBitsMode);

            var group = m_World.CreateSystemManaged<BenchmarkSystemGroup>();
            group.CreateUnmanagedSystems<MyStructSystem_IJobEntity>(iterationCount, loopsPerSystem, enabledBitsMode);

            Measure.Method(
                    () =>
                    {
                        group.Update();
                    })
                .WarmupCount(1)
                .MeasurementCount(9)
                .CleanUp(() => { m_World.UpdateAllocator.Rewind();})
                .Run();
        }

        [Performance]
        [TestCaseSource(nameof(TestCombinations))]
        public void SystemUpdatePerformance_StructSystem_Aspect_foreach(int iterationCount, int loopsPerSystem, int entityCount, EnabledBitsMode enabledBitsMode)
        {
            CreateTestEntities(entityCount, enabledBitsMode);

            var group = m_World.CreateSystemManaged<BenchmarkSystemGroup>();
            group.CreateUnmanagedSystems<StructSystem_Aspect_foreach>(iterationCount, loopsPerSystem, enabledBitsMode);

            Measure.Method(
                    () =>
                    {
                        group.Update();
                    })
                .WarmupCount(1)
                .MeasurementCount(9)
                .CleanUp(() => { m_World.UpdateAllocator.Rewind();})
                .Run();
        }

        [Performance]
        [TestCaseSource(nameof(TestCombinations))]
        public void SystemUpdatePerformance_StructSystem_Aspect_IJobChunkRunWithoutJobs(int iterationCount, int loopsPerSystem, int entityCount, EnabledBitsMode enabledBitsMode)
        {
            CreateTestEntities(entityCount, enabledBitsMode);

            var group = m_World.CreateSystemManaged<BenchmarkSystemGroup>();
            group.CreateUnmanagedSystems<StructSystem_Aspect_IJobChunk_RunWithoutJobs>(iterationCount, loopsPerSystem, enabledBitsMode);

            Measure.Method(
                    () =>
                    {
                        group.Update();
                    })
                .WarmupCount(1)
                .MeasurementCount(9)
                .CleanUp(() => { m_World.UpdateAllocator.Rewind();})
                .Run();
        }


        [TestCaseSource(nameof(TestCombinations))]
        [Performance]
        public void SystemUpdatePerformance_StructBuffer_10X_System_RunWithoutJobs(int iterationCount, int loopsPerSystem, int entityCount, EnabledBitsMode enabledBitsMode)
        {
            // NOTE this test iterates over 10 elements hence it is expected to be roughly 10x slower than all the other tests
            CreateBufferTestEntities(entityCount, enabledBitsMode);
            var group = m_World.CreateSystemManaged<BenchmarkSystemGroup>();
            group.CreateUnmanagedSystems<MyBufferSystem>(iterationCount, loopsPerSystem, enabledBitsMode);

            Measure.Method(
                    () =>
                    {
                        group.Update();
                    })
                .WarmupCount(1)
                .MeasurementCount(9)
                .CleanUp(() => { m_World.UpdateAllocator.Rewind();})
                .Run();
        }

        [TestCaseSource(nameof(TestCombinations))]
        [Performance]
        public void SystemUpdatePerformance_StructSystem_RunWithoutJobs_WithSingleton(int iterationCount, int loopsPerSystem, int entityCount, EnabledBitsMode enabledBitsMode)
        {
            CreateTestEntities(entityCount, enabledBitsMode);
            var group = m_World.CreateSystemManaged<BenchmarkSystemGroup>();
            group.CreateUnmanagedSystems<MyStructSystemWithSingleton>(iterationCount, loopsPerSystem, enabledBitsMode);

            Measure.Method(
                    () =>
                    {
                        group.Update();
                    })
                .WarmupCount(1)
                .MeasurementCount(9)
                .CleanUp(() => { m_World.UpdateAllocator.Rewind();})
                .Run();
        }

        [TestCaseSource(nameof(TestCombinations))]
        [Performance]
        public void SystemUpdatePerformance_SystemBase(int iterationCount, int loopsPerSystem, int entityCount, EnabledBitsMode enabledBitsMode)
        {
            CreateTestEntities(entityCount, enabledBitsMode);
            var group = m_World.CreateSystemManaged<BenchmarkSystemGroup>();
            group.CreateManagedSystems<MyClassSystem>(iterationCount, loopsPerSystem, enabledBitsMode);
            Measure.Method(
                    () =>
                    {
                        group.Update();
                    })
                .WarmupCount(1)
                .MeasurementCount(9)
                .CleanUp(() => { m_World.UpdateAllocator.Rewind();})
                .Run();
        }

        [TestCaseSource(nameof(TestCombinations))]
        [Performance]
        public void SystemUpdatePerformance_SystemBase_WithBurstForEach(int iterationCount, int loopsPerSystem, int entityCount, EnabledBitsMode enabledBitsMode)
        {
            CreateTestEntities(entityCount, enabledBitsMode);
            var group = m_World.CreateSystemManaged<BenchmarkSystemGroup>();
            group.CreateManagedSystems<MyClassSystemWithBurstForEach>(iterationCount, loopsPerSystem, enabledBitsMode);
            Measure.Method(
                    () =>
                    {
                        group.Update();
                    })
                .WarmupCount(1)
                .MeasurementCount(9)
                .CleanUp(() => { m_World.UpdateAllocator.Rewind();})
                .Run();
        }

        [TestCaseSource(nameof(TestCombinations))]
        [Performance]
        public void SystemUpdatePerformance_StructSystem_Run(int iterationCount, int loopsPerSystem, int entityCount, EnabledBitsMode enabledBitsMode)
        {
            CreateTestEntities(entityCount, enabledBitsMode);
            var group = m_World.CreateSystemManaged<BenchmarkSystemGroup>();
            group.CreateUnmanagedSystems<MyStructRunSystem>(iterationCount, loopsPerSystem, enabledBitsMode);

            Measure.Method(
                    () =>
                    {
                        group.Update();
                    })
                .WarmupCount(1)
                .MeasurementCount(9)
                .Run();
        }

        [TestCaseSource(nameof(TestCombinations))]
        [Performance]
        public void SystemUpdatePerformance_StructSystem_Schedule(int iterationCount, int loopsPerSystem, int entityCount, EnabledBitsMode enabledBitsMode)
        {
            CreateTestEntities(entityCount, enabledBitsMode);

            var group = m_World.CreateSystemManaged<BenchmarkSystemGroup>();
            group.CreateUnmanagedSystems<MyStructScheduleSystem>(iterationCount, loopsPerSystem, enabledBitsMode);

            Measure.Method(
                    () =>
                    {
                        group.Update();
                    })
                .WarmupCount(1)
                .MeasurementCount(9)
                .Run();
        }
    }
}
