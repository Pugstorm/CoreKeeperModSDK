using NUnit.Framework;
using System;
using System.Runtime.CompilerServices;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Benchmark;

namespace Unity.Collections.PerformanceTests
{
    struct RewindableAllocationInfo
    {
        public AllocatorHelper<RewindableAllocator> customAllocator;
        BenchmarkAllocatorUtil allocInfo;
        AllocatorManager.AllocatorHandle allocatorHandle;

        public void CreateAllocator(Allocator builtinOverride)
        {
            if (builtinOverride != Allocator.None)
                allocatorHandle = builtinOverride;
            else
            {
                customAllocator = new AllocatorHelper<RewindableAllocator>(Allocator.Persistent);
                customAllocator.Allocator.Initialize(128 * 1024, true);
                allocatorHandle = customAllocator.Allocator.Handle;
            }
        }

        public void DestroyAllocator()
        {
            if (allocatorHandle.IsCustomAllocator)
            {
                customAllocator.Allocator.Dispose();
                customAllocator.Dispose();
            }
            allocatorHandle = Allocator.Invalid;
        }

        public void Setup(int workers, int baseSize, int growthRate, int allocations)
        {
            allocInfo.Setup(workers, baseSize, growthRate, allocations);
        }

        public void Teardown()
        {
            allocInfo.Teardown(allocatorHandle);

            // Here we presume allocatorHandle == customAllocator.Handle, though there's currently no functionality
            // to check that in custom allocatorHandle API
            if (allocatorHandle.IsCustomAllocator)
            {
                customAllocator.Allocator.Rewind();

                // Rewinding invalidates the handle, so reassign
                allocatorHandle = customAllocator.Allocator.Handle;
            }
        }

        unsafe public void Allocate(int workerI)
        {
            var inner = allocInfo.AllocPtr[workerI];
            for (int i = 0; i < inner.Length; i++)
                inner[i] = (IntPtr)AllocatorManager.Allocate(allocatorHandle, allocInfo.AllocSize[i], 0);
        }
    }

    struct Rewindable_FixedSize : IBenchmarkAllocator
    {
        RewindableAllocationInfo allocInfo;

        public void CreateAllocator(Allocator builtinOverride) => allocInfo.CreateAllocator(builtinOverride);
        public void DestroyAllocator() => allocInfo.DestroyAllocator();
        public void Setup(int workers, int size, int allocations) =>
            allocInfo.Setup(workers, size, 0, allocations);
        public void Teardown() => allocInfo.Teardown();
        public void Measure(int workerI) => allocInfo.Allocate(workerI);
    }

    struct Rewindable_IncSize : IBenchmarkAllocator
    {
        RewindableAllocationInfo allocInfo;

        public void CreateAllocator(Allocator builtinOverride) => allocInfo.CreateAllocator(builtinOverride);
        public void DestroyAllocator() => allocInfo.DestroyAllocator();
        public void Setup(int workers, int size, int allocations) =>
            allocInfo.Setup(workers, size, size, allocations);
        public void Teardown() => allocInfo.Teardown();
        public void Measure(int workerI) => allocInfo.Allocate(workerI);
    }

    struct Rewindable_DecSize : IBenchmarkAllocator
    {
        RewindableAllocationInfo allocInfo;

        public void CreateAllocator(Allocator builtinOverride) => allocInfo.CreateAllocator(builtinOverride);
        public void DestroyAllocator() => allocInfo.DestroyAllocator();
        public void Setup(int workers, int size, int allocations) =>
            allocInfo.Setup(workers, size, -size, allocations);
        public void Teardown() => allocInfo.Teardown();
        public void Measure(int workerI) => allocInfo.Allocate(workerI);
    }


    [Benchmark(typeof(BenchmarkAllocatorType))]
    [BenchmarkNameOverride("RewindableAllocator")]
    class RewindableAllocatorBenchmark
    {
        [Test, Performance]
        [Category("Performance")]
        [BenchmarkTestFootnote]
        public void FixedSize(
            [Values(1, 2, 4, 8)] int workerThreads,
            [Values(1024, 1024 * 1024)] int allocSize,
            [Values] BenchmarkAllocatorType type)
        {
            BenchmarkAllocatorRunner<Rewindable_FixedSize>.Run(type, allocSize, workerThreads);
        }

        [Test, Performance]
        [Category("Performance")]
        [BenchmarkTestFootnote("Makes linearly increasing allocations [1⋅allocSize, 2⋅allocSize ... N⋅allocSize]")]
        public void IncSize(
            [Values(1, 2, 4, 8)] int workerThreads,
            [Values(4096, 65536)] int allocSize,
            [Values] BenchmarkAllocatorType type)
        {
            BenchmarkAllocatorRunner<Rewindable_IncSize>.Run(type, allocSize, workerThreads);
        }

        [Test, Performance]
        [Category("Performance")]
        [BenchmarkTestFootnote("Makes linearly decreasing allocations [N⋅allocSize ... 2⋅allocSize, 1⋅allocSize]")]
        public void DecSize(
            [Values(1, 2, 4, 8)] int workerThreads,
            [Values(4096, 65536)] int allocSize,
            [Values] BenchmarkAllocatorType type)
        {
            BenchmarkAllocatorRunner<Rewindable_DecSize>.Run(type, allocSize, workerThreads);
        }
    }
}
