using NUnit.Framework;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Physics.Math;

namespace Unity.Physics.Tests.Base.Math
{
    class MathTests
    {
        [BurstCompile(CompileSynchronously = true)]
        private struct IndexOfMinComponent2Job : IJob, IDisposable
        {
            [ReadOnly]
            public NativeArray<float2> In;
            [WriteOnly]
            public NativeArray<int> Out;

            public static IndexOfMinComponent2Job Init()
            {
                var length = 4;
                var job = new IndexOfMinComponent2Job
                {
                    In = new NativeArray<float2>(length, Allocator.TempJob),
                    Out = new NativeArray<int>(length, Allocator.TempJob)
                };

                job.In[0] = new float2(-1, 1);
                job.In[1] = new float2(1, -1);
                job.In[2] = new float2(float.NaN, 1);
                job.In[3] = new float2(float.PositiveInfinity, float.NegativeInfinity);

                return job;
            }

            public void Dispose()
            {
                Assert.AreEqual(0, Out[0]);
                Assert.AreEqual(1, Out[1]);
                Assert.AreEqual(1, Out[2]);
                Assert.AreEqual(1, Out[3]);

                In.Dispose();
                Out.Dispose();
            }

            public void Execute()
            {
                for (int i = 0; i < In.Length; i++)
                {
                    Out[i] = IndexOfMinComponent(In[i]);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct IndexOfMinComponent3Job : IJob, IDisposable
        {
            [ReadOnly]
            public NativeArray<float3> In;
            [WriteOnly]
            public NativeArray<int> Out;

            public static IndexOfMinComponent3Job Init()
            {
                var length = 4;
                var job = new IndexOfMinComponent3Job
                {
                    In = new NativeArray<float3>(length, Allocator.TempJob),
                    Out = new NativeArray<int>(length, Allocator.TempJob)
                };

                job.In[0] = new float3(-1, 1, 1);
                job.In[1] = new float3(1, -1, 1);
                job.In[2] = new float3(1, 1, -1);
                job.In[3] = new float3(float.NaN, float.PositiveInfinity, float.NegativeInfinity);

                return job;
            }

            public void Dispose()
            {
                Assert.AreEqual(0, Out[0]);
                Assert.AreEqual(1, Out[1]);
                Assert.AreEqual(2, Out[2]);
                Assert.AreEqual(2, Out[3]);

                In.Dispose();
                Out.Dispose();
            }

            public void Execute()
            {
                for (int i = 0; i < In.Length; i++)
                {
                    Out[i] = IndexOfMinComponent(In[i]);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct IndexOfMinComponent4Job : IJob, IDisposable
        {
            [ReadOnly]
            public NativeArray<float4> In;
            [WriteOnly]
            public NativeArray<int> Out;

            public static IndexOfMinComponent4Job Init()
            {
                var length = 5;
                var job = new IndexOfMinComponent4Job
                {
                    In = new NativeArray<float4>(length, Allocator.TempJob),
                    Out = new NativeArray<int>(length, Allocator.TempJob)
                };

                job.In[0] = new float4(-1, 1, 1, 1);
                job.In[1] = new float4(1, -1, 1, 1);
                job.In[2] = new float4(1, 1, -1, 1);
                job.In[3] = new float4(1, 1, 1, -1);
                job.In[4] = new float4(float.NaN, float.PositiveInfinity, float.NegativeInfinity, -float.NaN);

                return job;
            }

            public void Dispose()
            {
                Assert.AreEqual(0, Out[0]);
                Assert.AreEqual(1, Out[1]);
                Assert.AreEqual(2, Out[2]);
                Assert.AreEqual(3, Out[3]);
                Assert.AreEqual(3, Out[4]); // NaNs throw the result!

                In.Dispose();
                Out.Dispose();
            }

            public void Execute()
            {
                for (int i = 0; i < In.Length; i++)
                {
                    Out[i] = IndexOfMinComponent(In[i]);
                }
            }
        }

        [Test]
        public void TestIndexOfMinComponent()
        {
            var indexOfMinComponent2Job = IndexOfMinComponent2Job.Init();
            indexOfMinComponent2Job.Run();
            indexOfMinComponent2Job.Dispose();

            var indexOfMinComponent3Job = IndexOfMinComponent3Job.Init();
            indexOfMinComponent3Job.Run();
            indexOfMinComponent3Job.Dispose();

            var indexOfMinComponent4Job = IndexOfMinComponent4Job.Init();
            indexOfMinComponent4Job.Run();
            indexOfMinComponent4Job.Dispose();
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct IndexOfMaxComponent2Job : IJob, IDisposable
        {
            [ReadOnly]
            public NativeArray<float2> In;
            [WriteOnly]
            public NativeArray<int> Out;

            public static IndexOfMaxComponent2Job Init()
            {
                var length = 4;
                var job = new IndexOfMaxComponent2Job
                {
                    In = new NativeArray<float2>(length, Allocator.TempJob),
                    Out = new NativeArray<int>(length, Allocator.TempJob)
                };

                job.In[0] = new float2(-1, 1);
                job.In[1] = new float2(1, -1);
                job.In[2] = new float2(float.NaN, 1);
                job.In[3] = new float2(float.PositiveInfinity, float.NegativeInfinity);

                return job;
            }

            public void Dispose()
            {
                Assert.AreEqual(1, Out[0]);
                Assert.AreEqual(0, Out[1]);
                Assert.AreEqual(1, Out[2]);
                Assert.AreEqual(0, Out[3]);

                In.Dispose();
                Out.Dispose();
            }

            public void Execute()
            {
                for (int i = 0; i < In.Length; i++)
                {
                    Out[i] = IndexOfMaxComponent(In[i]);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct IndexOfMaxComponent3Job : IJob, IDisposable
        {
            [ReadOnly]
            public NativeArray<float3> In;
            [WriteOnly]
            public NativeArray<int> Out;

            public static IndexOfMaxComponent3Job Init()
            {
                var length = 4;
                var job = new IndexOfMaxComponent3Job
                {
                    In = new NativeArray<float3>(length, Allocator.TempJob),
                    Out = new NativeArray<int>(length, Allocator.TempJob)
                };

                job.In[0] = new float3(-1, -1, 1);
                job.In[1] = new float3(1, -1, -1);
                job.In[2] = new float3(-1, 1, -1);
                job.In[3] = new float3(float.NaN, float.PositiveInfinity, float.NegativeInfinity);

                return job;
            }

            public void Dispose()
            {
                Assert.AreEqual(2, Out[0]);
                Assert.AreEqual(0, Out[1]);
                Assert.AreEqual(1, Out[2]);
                Assert.AreEqual(1, Out[3]);

                In.Dispose();
                Out.Dispose();
            }

            public void Execute()
            {
                for (int i = 0; i < In.Length; i++)
                {
                    Out[i] = IndexOfMaxComponent(In[i]);
                }
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct IndexOfMaxComponent4Job : IJob, IDisposable
        {
            [ReadOnly]
            public NativeArray<float4> In;
            [WriteOnly]
            public NativeArray<int> Out;

            public static IndexOfMaxComponent4Job Init()
            {
                var length = 5;
                var job = new IndexOfMaxComponent4Job
                {
                    In = new NativeArray<float4>(length, Allocator.TempJob),
                    Out = new NativeArray<int>(length, Allocator.TempJob)
                };

                job.In[0] = new float4(-1, -1, -1, 1);
                job.In[1] = new float4(1, -1, -1, -1);
                job.In[2] = new float4(-1, 1, -1, -1);
                job.In[3] = new float4(-1, -1, 1, -1);
                job.In[4] = new float4(float.NaN, float.PositiveInfinity, float.NegativeInfinity, -float.NaN);

                return job;
            }

            public void Dispose()
            {
                Assert.AreEqual(3, Out[0]);
                Assert.AreEqual(0, Out[1]);
                Assert.AreEqual(1, Out[2]);
                Assert.AreEqual(2, Out[3]);
                Assert.AreEqual(3, Out[4]); // NaNs throw the result!

                In.Dispose();
                Out.Dispose();
            }

            public void Execute()
            {
                for (int i = 0; i < In.Length; i++)
                {
                    Out[i] = IndexOfMaxComponent(In[i]);
                }
            }
        }

        [Test]
        [Timeout(360000)]
        public void TestIndexOfMaxComponent()
        {
            var indexOfMaxComponent2Job = IndexOfMaxComponent2Job.Init();
            indexOfMaxComponent2Job.Run();
            indexOfMaxComponent2Job.Dispose();

            var indexOfMaxComponent3Job = IndexOfMaxComponent3Job.Init();
            indexOfMaxComponent3Job.Run();
            indexOfMaxComponent3Job.Dispose();

            var indexOfMaxComponent4Job = IndexOfMaxComponent4Job.Init();
            indexOfMaxComponent4Job.Run();
            indexOfMaxComponent4Job.Dispose();
        }
    }
}
