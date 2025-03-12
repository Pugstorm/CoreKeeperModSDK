using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;
using System.IO;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;

namespace Unity.DebugDisplay
{
    static class UnsafeListExtensions
    {
        /// <summary>
        /// Returns a native array that aliases the content of this list.
        /// </summary>
        /// <returns>A native array that aliases the content of this list.</returns>
        internal static NativeArray<T> AsNativeArray<T>(this UnsafeList<T> list)
            where T : unmanaged
        {
            unsafe
            {
                var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(list.Ptr, list.Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
#endif
                return array;
            }
        }
    }

    internal struct Unmanaged
    {
        private const int kInitialBufferSize = 100000;
        const int kMaxBufferSize = 10000000;
        const float kBufferGrowthFactor = 1.618f;

        internal void Clear()
        {
            // resize buffers if needed:
            if (m_LineBuffer.ResizeRequested)
            {
                var lastSize = m_LineBuffer.Size;
                m_LineBuffer.Dispose();
                m_LineBuffer.Initialize(math.min(kMaxBufferSize, (int)(lastSize * kBufferGrowthFactor)));
            }

            if (m_TriangleBuffer.ResizeRequested)
            {
                var lastSize = m_TriangleBuffer.Size;
                m_TriangleBuffer.Dispose();
                m_TriangleBuffer.Initialize(math.min(kMaxBufferSize, (int)(lastSize * kBufferGrowthFactor)));
            }

            // clear out all the lines and triangles:
            m_LineBufferAllocations = m_LineBuffer.AllocateAll();
            m_TriangleBufferAllocations = m_TriangleBuffer.AllocateAll();
        }

        internal Unit m_LineBufferAllocations;
        internal Unit m_TriangleBufferAllocations;

        internal LineBuffer  m_LineBuffer;
        internal TriangleBuffer m_TriangleBuffer;

        private bool initialized;

        internal NativeArray<float4> m_ColorData;

        unsafe internal void Initialize()
        {
            if (initialized == false)
            {
                var pal = stackalloc byte[ColorIndex.staticColorCount * 3]
                {
                    0x00, 0x00, 0x00, 0x00, 0x00, 0xaa, 0x00, 0xaa, 0x00, 0x00, 0xaa, 0xaa,
                    0xaa, 0x00, 0x00, 0xaa, 0x00, 0xaa, 0xaa, 0x55, 0x00, 0xaa, 0xaa, 0xaa,
                    0x55, 0x55, 0x55, 0x55, 0x55, 0xff, 0x55, 0xff, 0x55, 0x55, 0xff, 0xff,
                    0xff, 0x55, 0x55, 0xff, 0x55, 0xff, 0xff, 0xff, 0x55, 0xff, 0xff, 0xff,
                    0x00, 0x00, 0x00, 0x14, 0x14, 0x14, 0x20, 0x20, 0x20, 0x2c, 0x2c, 0x2c,
                    0x38, 0x38, 0x38, 0x45, 0x45, 0x45, 0x51, 0x51, 0x51, 0x61, 0x61, 0x61,
                    0x71, 0x71, 0x71, 0x82, 0x82, 0x82, 0x92, 0x92, 0x92, 0xa2, 0xa2, 0xa2,
                    0xb6, 0xb6, 0xb6, 0xcb, 0xcb, 0xcb, 0xe3, 0xe3, 0xe3, 0xff, 0xff, 0xff,
                    0x00, 0x00, 0xff, 0x41, 0x00, 0xff, 0x7d, 0x00, 0xff, 0xbe, 0x00, 0xff,
                    0xff, 0x00, 0xff, 0xff, 0x00, 0xbe, 0xff, 0x00, 0x7d, 0xff, 0x00, 0x41,
                    0xff, 0x00, 0x00, 0xff, 0x41, 0x00, 0xff, 0x7d, 0x00, 0xff, 0xbe, 0x00,
                    0xff, 0xff, 0x00, 0xbe, 0xff, 0x00, 0x7d, 0xff, 0x00, 0x41, 0xff, 0x00,
                    0x00, 0xff, 0x00, 0x00, 0xff, 0x41, 0x00, 0xff, 0x7d, 0x00, 0xff, 0xbe,
                    0x00, 0xff, 0xff, 0x00, 0xbe, 0xff, 0x00, 0x7d, 0xff, 0x00, 0x41, 0xff,
                    0x7d, 0x7d, 0xff, 0x9e, 0x7d, 0xff, 0xbe, 0x7d, 0xff, 0xdf, 0x7d, 0xff,
                    0xff, 0x7d, 0xff, 0xff, 0x7d, 0xdf, 0xff, 0x7d, 0xbe, 0xff, 0x7d, 0x9e,
                    0xff, 0x7d, 0x7d, 0xff, 0x9e, 0x7d, 0xff, 0xbe, 0x7d, 0xff, 0xdf, 0x7d,
                    0xff, 0xff, 0x7d, 0xdf, 0xff, 0x7d, 0xbe, 0xff, 0x7d, 0x9e, 0xff, 0x7d,
                    0x7d, 0xff, 0x7d, 0x7d, 0xff, 0x9e, 0x7d, 0xff, 0xbe, 0x7d, 0xff, 0xdf,
                    0x7d, 0xff, 0xff, 0x7d, 0xdf, 0xff, 0x7d, 0xbe, 0xff, 0x7d, 0x9e, 0xff,
                    0xb6, 0xb6, 0xff, 0xc7, 0xb6, 0xff, 0xdb, 0xb6, 0xff, 0xeb, 0xb6, 0xff,
                    0xff, 0xb6, 0xff, 0xff, 0xb6, 0xeb, 0xff, 0xb6, 0xdb, 0xff, 0xb6, 0xc7,
                    0xff, 0xb6, 0xb6, 0xff, 0xc7, 0xb6, 0xff, 0xdb, 0xb6, 0xff, 0xeb, 0xb6,
                    0xff, 0xff, 0xb6, 0xeb, 0xff, 0xb6, 0xdb, 0xff, 0xb6, 0xc7, 0xff, 0xb6,
                    0xb6, 0xdf, 0xb6, 0xb6, 0xff, 0xc7, 0xb6, 0xff, 0xdb, 0xb6, 0xff, 0xeb,
                    0xb6, 0xff, 0xff, 0xb6, 0xeb, 0xff, 0xb6, 0xdb, 0xff, 0xb6, 0xc7, 0xff,
                    0x00, 0x00, 0x71, 0x1c, 0x00, 0x71, 0x38, 0x00, 0x71, 0x55, 0x00, 0x71,
                    0x71, 0x00, 0x71, 0x71, 0x00, 0x55, 0x71, 0x00, 0x38, 0x71, 0x00, 0x1c,
                    0x71, 0x00, 0x00, 0x71, 0x1c, 0x00, 0x71, 0x38, 0x00, 0x71, 0x55, 0x00,
                    0x71, 0x71, 0x00, 0x55, 0x71, 0x00, 0x38, 0x71, 0x00, 0x1c, 0x71, 0x00,
                    0x00, 0x71, 0x00, 0x00, 0x71, 0x1c, 0x00, 0x71, 0x38, 0x00, 0x71, 0x55,
                    0x00, 0x71, 0x71, 0x00, 0x55, 0x71, 0x00, 0x38, 0x71, 0x00, 0x1c, 0x71,
                    0x38, 0x38, 0x71, 0x45, 0x38, 0x71, 0x55, 0x38, 0x71, 0x61, 0x38, 0x71,
                    0x71, 0x38, 0x71, 0x71, 0x38, 0x61, 0x71, 0x38, 0x55, 0x71, 0x38, 0x45,
                    0x71, 0x38, 0x38, 0x71, 0x45, 0x38, 0x71, 0x55, 0x38, 0x71, 0x61, 0x38,
                    0x71, 0x71, 0x38, 0x61, 0x71, 0x38, 0x55, 0x71, 0x38, 0x45, 0x71, 0x38,
                    0x38, 0x71, 0x38, 0x38, 0x71, 0x45, 0x38, 0x71, 0x55, 0x38, 0x71, 0x61,
                    0x38, 0x71, 0x71, 0x38, 0x61, 0x71, 0x38, 0x55, 0x71, 0x38, 0x45, 0x71,
                    0x51, 0x51, 0x71, 0x59, 0x51, 0x71, 0x61, 0x51, 0x71, 0x69, 0x51, 0x71,
                    0x71, 0x51, 0x71, 0x71, 0x51, 0x69, 0x71, 0x51, 0x61, 0x71, 0x51, 0x59,
                    0x71, 0x51, 0x51, 0x71, 0x59, 0x51, 0x71, 0x61, 0x51, 0x71, 0x69, 0x51,
                    0x71, 0x71, 0x51, 0x69, 0x71, 0x51, 0x61, 0x71, 0x51, 0x59, 0x71, 0x51,
                    0x51, 0x71, 0x51, 0x51, 0x71, 0x59, 0x51, 0x71, 0x61, 0x51, 0x71, 0x69,
                    0x51, 0x71, 0x71, 0x51, 0x69, 0x71, 0x51, 0x61, 0x71, 0x51, 0x59, 0x71,
                    0x00, 0x00, 0x41, 0x10, 0x00, 0x41, 0x20, 0x00, 0x41, 0x30, 0x00, 0x41,
                    0x41, 0x00, 0x41, 0x41, 0x00, 0x30, 0x41, 0x00, 0x20, 0x41, 0x00, 0x10,
                    0x41, 0x00, 0x00, 0x41, 0x10, 0x00, 0x41, 0x20, 0x00, 0x41, 0x30, 0x00,
                    0x41, 0x41, 0x00, 0x30, 0x41, 0x00, 0x20, 0x41, 0x00, 0x10, 0x41, 0x00,
                    0x00, 0x41, 0x00, 0x00, 0x41, 0x10, 0x00, 0x41, 0x20, 0x00, 0x41, 0x30,
                    0x00, 0x41, 0x41, 0x00, 0x30, 0x41, 0x00, 0x20, 0x41, 0x00, 0x10, 0x41,
                    0x20, 0x20, 0x41, 0x28, 0x20, 0x41, 0x30, 0x20, 0x41, 0x38, 0x20, 0x41,
                    0x41, 0x20, 0x41, 0x41, 0x20, 0x38, 0x41, 0x20, 0x30, 0x41, 0x20, 0x28,
                    0x41, 0x20, 0x20, 0x41, 0x28, 0x20, 0x41, 0x30, 0x20, 0x41, 0x38, 0x20,
                    0x41, 0x41, 0x20, 0x38, 0x41, 0x20, 0x30, 0x41, 0x20, 0x28, 0x41, 0x20,
                    0x20, 0x41, 0x20, 0x20, 0x41, 0x28, 0x20, 0x41, 0x30, 0x20, 0x41, 0x38,
                    0x20, 0x41, 0x41, 0x20, 0x38, 0x41, 0x20, 0x30, 0x41, 0x20, 0x28, 0x41,
                    0x2c, 0x2c, 0x41, 0x30, 0x2c, 0x41, 0x34, 0x2c, 0x41, 0x3c, 0x2c, 0x41,
                    0x41, 0x2c, 0x41, 0x41, 0x2c, 0x3c, 0x41, 0x2c, 0x34, 0x41, 0x2c, 0x30,
                    0x41, 0x2c, 0x2c, 0x41, 0x30, 0x2c, 0x41, 0x34, 0x2c, 0x41, 0x3c, 0x2c,
                    0x41, 0x41, 0x2c, 0x3c, 0x41, 0x2c, 0x34, 0x41, 0x2c, 0x30, 0x41, 0x2c,
                    0x2c, 0x41, 0x2c, 0x2c, 0x41, 0x30, 0x2c, 0x41, 0x34, 0x2c, 0x41, 0x3c,
                    0x2c, 0x41, 0x41, 0x2c, 0x3c, 0x41, 0x2c, 0x34, 0x41, 0x2c, 0x30, 0x41
                };

                // Initialize and fill color data with static colors, and placeholders dynamic colors
                m_ColorData = new NativeArray<float4>(ColorIndex.kMaxColors, Allocator.Persistent);
                for (var i = 0; i < ColorIndex.staticColorCount; ++i)
                    m_ColorData[i] = new float4(pal[i * 3 + 2],  pal[i * 3 + 1], pal[i * 3 + 0], 255) / 255.0f;
                for (var i = 0; i < ColorIndex.dynamicColorCount; ++i)
                    m_ColorData[ColorIndex.staticColorCount + i] = float4.zero;

                m_LineBuffer.Initialize(kInitialBufferSize);
                m_TriangleBuffer.Initialize(kInitialBufferSize);

                m_LineBufferAllocations = m_LineBuffer.AllocateAll();
                m_TriangleBufferAllocations = m_TriangleBuffer.AllocateAll();

                initialized = true;
            }
        }

        internal void Dispose()
        {
            if (initialized)
            {
                m_LineBuffer.Dispose();
                m_TriangleBuffer.Dispose();

                m_ColorData.Dispose();
                initialized = false;
            }
        }

        private class InstanceFieldKey {}

        internal static readonly SharedStatic<Unmanaged> Instance = SharedStatic<Unmanaged>.GetOrCreate<Unmanaged, InstanceFieldKey>();
    }


    internal class Managed : IDisposable
    {
        internal struct Objects
        {
            internal Material lineMaterial;
            internal Material meshMaterial;
        }
        internal Objects resources;

        internal static int PixelsWide => Screen.width;
        internal static int PixelsTall => Screen.height;

        internal const int kPixelsWide = 8;
        internal const int kPixelsTall = 16;

        internal static float FractionalCellsWide => (float)PixelsWide / kPixelsWide;
        internal static float FractionalCellsTall => (float)PixelsTall / kPixelsTall;

#if UNITY_EDITOR
        internal static string debugDirName =
            "Packages/com.unity.physics/Unity.Physics.Hybrid/Assets/DebugDisplay/DebugDisplayResources/";
#endif


        internal unsafe Managed()
        {
            Unmanaged.Instance.Data.Initialize();

#if UNITY_EDITOR
            resources.lineMaterial = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(debugDirName, "LineMaterial.mat"));
            resources.meshMaterial = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(debugDirName, "MeshMaterial.mat"));

            EditorApplication.wantsToQuit += OnEditorApplicationWantsToQuit;
#endif
            AppDomain.CurrentDomain.DomainUnload += OnDomainUnload;
        }

#if UNITY_EDITOR
        bool OnEditorApplicationWantsToQuit()
        {
            AppDomain.CurrentDomain.DomainUnload -= OnDomainUnload;
            instance?.Dispose();
            return true;
        }

#endif

        static void OnDomainUnload(object sender, EventArgs e)
        {
            instance?.Dispose();
        }

        private void UpdateDynamicColorsData()
        {
#if ENABLE_PHYSICS && UNITY_EDITOR
            // Update ColorData for meshes
            Unmanaged.Instance.Data.m_ColorData[ColorIndex.DynamicMesh.value] = new float4(
                PhysicsVisualizationSettings.rigidbodyColor.r,
                PhysicsVisualizationSettings.rigidbodyColor.g,
                PhysicsVisualizationSettings.rigidbodyColor.b,
                PhysicsVisualizationSettings.rigidbodyColor.a); // Dynamic
            Unmanaged.Instance.Data.m_ColorData[ColorIndex.StaticMesh.value] = new float4(
                PhysicsVisualizationSettings.staticColor.r,
                PhysicsVisualizationSettings.staticColor.g,
                PhysicsVisualizationSettings.staticColor.b,
                PhysicsVisualizationSettings.staticColor.a); // Static
            Unmanaged.Instance.Data.m_ColorData[ColorIndex.KinematicMesh.value] = new float4(
                PhysicsVisualizationSettings.kinematicColor.r,
                PhysicsVisualizationSettings.kinematicColor.g,
                PhysicsVisualizationSettings.kinematicColor.b,
                PhysicsVisualizationSettings.kinematicColor.a); // Kinematic
#else
            Unmanaged.Instance.Data.m_ColorData[ColorIndex.DynamicMesh.value] = Unmanaged.Instance.Data.m_ColorData[ColorIndex.BrightRed.value];
            Unmanaged.Instance.Data.m_ColorData[ColorIndex.StaticMesh.value] = Unmanaged.Instance.Data.m_ColorData[ColorIndex.BrightBlack.value];
            Unmanaged.Instance.Data.m_ColorData[ColorIndex.KinematicMesh.value] = Unmanaged.Instance.Data.m_ColorData[ColorIndex.BrightBlue.value];
#endif
        }

        internal void CopyFromCpuToGpu()
        {
            const string colorBufferString = "colorBuffer";
            const string positionBufferString = "positionBuffer";
            const string meshBufferString = "meshBuffer";
            const string scalesString = "scales";

            // Recreate compute buffer if needed.

            // Color Buffer
            {
                var updateColorBuffer = RecreateBuffer<float4>(ref m_ColorBuffer, Unmanaged.Instance.Data.m_ColorData.Length);

                if (updateColorBuffer || resources.meshMaterial.GetBuffer(colorBufferString).value == 0)
                    resources.meshMaterial.SetBuffer(colorBufferString, m_ColorBuffer);
                if (updateColorBuffer || resources.lineMaterial.GetBuffer(colorBufferString).value == 0)
                    resources.lineMaterial.SetBuffer(colorBufferString, m_ColorBuffer);
            }
            UpdateDynamicColorsData();
            // Line Buffer
            {
                var updateLineBuffer = RecreateBuffer<LineBuffer.Instance>(ref m_LineVertexBuffer, Unmanaged.Instance.Data.m_LineBuffer.m_Instance.Length);

                if (updateLineBuffer || resources.lineMaterial.GetBuffer(positionBufferString).value == 0)
                    resources.lineMaterial.SetBuffer(positionBufferString, m_LineVertexBuffer);
            }

            // Triangle Buffer
            {
                var updateTriangleBuffer = RecreateBuffer<TriangleBuffer.Instance>(ref m_TriangleInstanceBuffer, Unmanaged.Instance.Data.m_TriangleBuffer.m_Instance.Length);

                if (updateTriangleBuffer || resources.meshMaterial.GetBuffer(meshBufferString).value == 0)
                    resources.meshMaterial.SetBuffer(meshBufferString, m_TriangleInstanceBuffer);
            }

            m_ColorBuffer.SetData(Unmanaged.Instance.Data.m_ColorData);
            m_NumLinesToDraw = Unmanaged.Instance.Data.m_LineBufferAllocations.Filled;
            m_NumTrianglesToDraw = Unmanaged.Instance.Data.m_TriangleBufferAllocations.Filled;

            m_LineVertexBuffer.SetData(Unmanaged.Instance.Data.m_LineBuffer.m_Instance.AsNativeArray(), 0, 0, m_NumLinesToDraw);
            m_TriangleInstanceBuffer.SetData(Unmanaged.Instance.Data.m_TriangleBuffer.m_Instance.AsNativeArray(), 0, 0, m_NumTrianglesToDraw);

            var scales = new float4(1.0f / FractionalCellsWide, 1.0f / FractionalCellsTall, 1.0f / PixelsWide, 1.0f / PixelsTall);
            resources.lineMaterial.SetVector(scalesString, scales);
            resources.meshMaterial.SetVector(scalesString, scales);
        }

        private bool RecreateBuffer<T>(ref ComputeBuffer computeBuffer, int expectedLength) where T : struct
        {
            bool updateBuffer = computeBuffer == null ||
                computeBuffer.count != expectedLength;
            if (updateBuffer)
            {
                if (computeBuffer != null)
                {
                    computeBuffer.Release();
                    computeBuffer = null;
                }

                computeBuffer = new ComputeBuffer(expectedLength,
                    UnsafeUtility.SizeOf<T>());
            }

            return updateBuffer;
        }

        internal void Clear()
        {
            Unmanaged.Instance.Data.Clear();
        }

        internal void Render()
        {
            resources.meshMaterial.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Triangles, m_NumTrianglesToDraw * 3, 1);
            resources.lineMaterial.SetPass(0);
            Graphics.DrawProceduralNow(MeshTopology.Lines, m_NumLinesToDraw * 2, 1);
        }

        int m_NumLinesToDraw = 0;
        int m_NumTrianglesToDraw = 0;

        ComputeBuffer m_LineVertexBuffer; // one big 1D array of line vertex positions.
        ComputeBuffer m_TriangleInstanceBuffer;

        ComputeBuffer m_ColorBuffer;

        static Managed instance;
        internal static Managed Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Managed();
                }
                return instance;
            }
            set
            {
                instance = value;
            }
        }

        public void Dispose()
        {
            m_LineVertexBuffer?.Dispose();
            m_ColorBuffer?.Dispose();
            m_TriangleInstanceBuffer?.Dispose();

            m_LineVertexBuffer = null;
            m_ColorBuffer = null;
            m_TriangleInstanceBuffer = null;

            Unmanaged.Instance.Data.Dispose();
            if (instance == this)
                instance = null;
        }
    }
}
