using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Physics;
using Unity.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using Unity.Jobs;
using Unity.Burst;
using Collider = Unity.Physics.Collider;
using UnityEngine.Assertions;
using Material = Unity.Physics.Material;

#region Point clouds

interface IPointGenerator
{
    // Gets the number of point sets
    int NumSets { get; }

    // Returns a friendly name for the ith set
    string GetName(int i);

    // Gets an upper bound on the number of points in the ith set
    int GetMaxNumPoints(int i);

    // Writes the ith point set into dest
    // Returns the number of points
    // Size of dest must be at least GetMaxNumPoints(i);
    unsafe int GetPoints(int i, float3* dest);

    // Returns the source mesh for the ith point set, if any
    UnityEngine.Mesh GetMesh(int i);
}

class MeshPointGenerator : IPointGenerator
{
    List<UnityEngine.Mesh> m_Meshes;

    public MeshPointGenerator(List<UnityEngine.Mesh> meshes)
    {
        m_Meshes = meshes;
    }

    public int NumSets { get { return m_Meshes == null ? 0 : m_Meshes.Count; } }

    public int GetMaxNumPoints(int i)
    {
        return m_Meshes[i] == null ? 0 : m_Meshes[i].vertexCount;
    }

    public string GetName(int i)
    {
        return m_Meshes[i].name + " (" + i + ")";
    }

    public unsafe int GetPoints(int i, float3* dest)
    {
        // Copy the mesh vertices
        UnityEngine.Mesh mesh = m_Meshes[i];
        Vector3[] vertices = mesh.vertices;
        int numVertices = 0;
        for (int j = 0; j < vertices.Length; j++)
        {
            dest[numVertices++] = vertices[j];
        }
        return numVertices;
    }

    public UnityEngine.Mesh GetMesh(int i)
    {
        return m_Meshes[i];
    }
}

class RandomConvexGenerator : IPointGenerator
{
    private uint[] m_Seeds;

    public RandomConvexGenerator()
    {
        uint seed = 123456789;
        m_Seeds = new uint[NumSets];
        for (int i = 0; i < NumSets; i++)
        {
            seed = 1664525 * seed + 1013904223;
            m_Seeds[i] = seed;
        }
    }

    public int NumSets => 500;

    private int GetNumPoints(ref Unity.Mathematics.Random rng)
    {
        return rng.NextInt(1, 100);
    }

    public int GetMaxNumPoints(int i)
    {
        Unity.Mathematics.Random rng = new Unity.Mathematics.Random(m_Seeds[i]);
        return GetNumPoints(ref rng);
    }

    public string GetName(int i)
    {
        return "RandomConvex" + i;
    }

    public unsafe int GetPoints(int i, float3* dest)
    {
        Unity.Mathematics.Random rng = new Unity.Mathematics.Random(m_Seeds[i]);
        int numPoints = GetNumPoints(ref rng);
        int count = 0;
        while (count < numPoints)
        {
            float3 n = rng.NextFloat3Direction();
            float3 c = n * rng.NextFloat(0.0f, 1.0f);
            Math.CalculatePerpendicularNormalized(n, out float3 u, out float3 v);
            int planePoints = math.min(rng.NextInt(3, 20), numPoints - count);
            for (int iPoint = 0; iPoint < planePoints; iPoint++)
            {
                dest[count++] = c + u * rng.NextFloat(-1.0f, 1.0f) + v * rng.NextFloat(-1.0f, 1.0f);
            }
        }
        return numPoints;
    }

    public UnityEngine.Mesh GetMesh(int i)
    {
        return null;
    }
}

class RandomEllipsoidGenerator : IPointGenerator
{
    private uint[] m_Seeds;

    public RandomEllipsoidGenerator()
    {
        uint seed = 987654321;
        m_Seeds = new uint[NumSets];
        for (int i = 0; i < NumSets; i++)
        {
            seed = 1664525 * seed + 1013904223;
            m_Seeds[i] = seed;
        }
    }

    public int NumSets => 100;

    private void GetResolution(ref Unity.Mathematics.Random rng, out int res0, out int res1)
    {
        bool random = rng.NextBool();
        if (random)
        {
            res0 = rng.NextInt(1, 500);
            res1 = 0;
        }
        else
        {
            res0 = rng.NextInt(1, 20);
            res1 = rng.NextInt(1, 20);
        }
    }

    public int GetMaxNumPoints(int i)
    {
        Unity.Mathematics.Random rng = new Unity.Mathematics.Random(m_Seeds[i]);
        GetResolution(ref rng, out int res0, out int res1);
        return res0 * math.max(res1, 1);
    }

    public string GetName(int i)
    {
        return "RandomEllipsoid" + i;
    }

    public unsafe int GetPoints(int i, float3* dest)
    {
        Unity.Mathematics.Random rng = new Unity.Mathematics.Random(m_Seeds[i]);
        GetResolution(ref rng, out int res0, out int res1);
        float scale = rng.NextBool() ? 1.0f : rng.NextFloat(1.0f, 5.0f);
        float radius = rng.NextFloat(0.01f, 1.0f / scale);

        // Generate points
        int numPoints;
        if (res1 == 0)
        {
            // Random scattered points
            for (int iPoint = 0; iPoint < res0; iPoint++)
            {
                dest[iPoint] = math.normalize(rng.NextFloat3(-1, 1)) * radius;
            }
            numPoints = res0;
        }
        else
        {
            // Spherical coordinates
            int iPoint = 0;
            for (int iPhi = 0; iPhi < res0; iPhi++)
            {
                float phi = (2 * iPhi + 1) * math.PI / res0;
                float sinPhi = math.sin(phi);
                float cosPhi = math.cos(phi);
                for (int iTheta = 0; iTheta < res1; iTheta++)
                {
                    float theta = 0.5f * (2 * iTheta + 1) * math.PI / res1;
                    float sinTheta = math.sin(theta);
                    float cosTheta = math.cos(theta);
                    dest[iPoint++] = new float3(radius * sinTheta * cosPhi, radius * sinTheta * sinPhi, radius * cosTheta);
                }
            }
            numPoints = iPoint;
        }

        // Offset, scale and rotate
        float3 center = rng.NextBool() ? float3.zero : rng.NextFloat3(1.0f);
        quaternion orientation = rng.NextBool() ? quaternion.identity : rng.NextQuaternionRotation();
        for (int iPoint = 0; iPoint < numPoints; iPoint++)
        {
            dest[iPoint] = math.mul(orientation, dest[iPoint] * new float3(scale, 1, 1) + center);
        }

        return numPoints;
    }

    public UnityEngine.Mesh GetMesh(int i)
    {
        return null;
    }
}


class ConeGenerator : IPointGenerator
{
    private int[] m_Resolutions;
    private float[] m_Heights;
    private float[] m_Radii;

    public ConeGenerator()
    {
        m_Resolutions = new int[] {  4, 8, 16, 32 };
        m_Heights = new float[] { 0.01f, 0.05f, 0.1f, 0.5f };
        m_Radii = new float[] { 0.0f, 0.01f, 0.02f, 0.05f, 0.1f, 0.5f, 1.0f };
    }

    private int numRadiusPairs => m_Radii.Length * (m_Radii.Length + 1) / 2;

    public int NumSets => m_Resolutions.Length * m_Heights.Length * numRadiusPairs;

    private int GetResolution(int i)
    {
        return m_Resolutions[i / (numRadiusPairs * m_Heights.Length)];
    }

    public int GetMaxNumPoints(int i)
    {
        return GetResolution(i) * 2;
    }

    public string GetName(int i)
    {
        return "Cone" + i;
    }

    public unsafe int GetPoints(int i, float3* dest)
    {
        // Get the resolution and dimensions
        int resolution = GetResolution(i);
        float height = m_Heights[(i / numRadiusPairs) % m_Heights.Length];
        int radiusIndex1 = i % numRadiusPairs;
        int radiusIndex0 = 0;
        while (radiusIndex1 >= m_Radii.Length - radiusIndex0)
        {
            radiusIndex1 -= (m_Radii.Length - radiusIndex0);
            radiusIndex0++;
        }
        radiusIndex1 += radiusIndex0;
        float radius0 = m_Radii[radiusIndex0];
        float radius1 = m_Radii[radiusIndex1];

        // Build points
        float3 c0 = new float3(0, -height / 2, 0);
        float3 c1 = new float3(0,  height / 2, 0);
        float3 arm0 = new float3(radius0, 0, 0);
        float3 arm1 = new float3(radius1, 0, 0);
        quaternion q = quaternion.AxisAngle(new float3(0, 1, 0), (float)math.PI * 2.0f / resolution);
        for (int j = 0; j < resolution; j++)
        {
            dest[j * 2] = c0 + arm0;
            dest[j * 2 + 1] = c1 + arm1;
            arm0 = math.mul(q, arm0);
            arm1 = math.mul(q, arm1);
        }

        return resolution * 2;
    }

    public UnityEngine.Mesh GetMesh(int i)
    {
        return null;
    }
}

#endregion

struct HullStats
{
    public int OriginalNumVertices; // Number of vertices before simplification
    public int OriginalNumFaces; // Number of faces before simplification
    public int NumVertices; // Number of vertices after simplification
    public int NumFaces; // Number of faces after simplification
    public float MinError; // Minimum signed distance from a vertex on the original hull to the simplified hull or vice versa
    public float MaxError; // Maximum signed distance from a vertex on the original hull to the simplified hull or vice versa
    public float PlaneError; // Maximum absolute distance from a vertex on the final collider to any of its faces' planes
    public float ConvexRadius; // Convex radius of the final collider
    public float MinAngle; // Minimum angle between faces on the simplified hull
    public override string ToString()
    {
        return OriginalNumVertices + "->" + NumVertices + "v, " + OriginalNumFaces + "->" + NumFaces + "f, error [" + MinError + ", " + MaxError + "] ";
    }
}


struct TestResult
{
    public int GeneratorIndex;
    public int PointSetIndex;
    public string Name;
    public HullStats Stats;

    public override string ToString()
    {
        return Name + " (" + GeneratorIndex + ", " + PointSetIndex + ") | " + Stats;
    }
}

[BurstCompile(CompileSynchronously = true)]
unsafe struct BuildJob : IJob
{
    [ReadOnly] public NativeArray<float3> Points;
    public float SimplificationTolerance; // Sum of tolerances for all simplification operations
    [NativeDisableUnsafePtrRestriction] public ConvexHullBuilder* Builder;

    public void Execute()
    {
        // Add points to the builder
        ConvexHullBuilder builder = Builder[0];
        for (int i = 0; i < Points.Length; i++)
        {
            builder.AddPoint(Points[i]);
        }
        builder.BuildFaceIndices();

        // Write back the builder
        Builder[0] = builder;
    }
}

[BurstCompile(CompileSynchronously = true)]
unsafe struct SimplifyJob : IJob
{
    [NativeDisableUnsafePtrRestriction] public ConvexHullBuilder* Builder;
    public float SimplificationTolerance;

    public void Execute()
    {
        // Simplify vertices
        Builder->RemoveRedundantVertices();
        Builder->BuildFaceIndices();
        Builder->SimplifyVertices(SimplificationTolerance, ConvexCollider.k_MaxVertices);
        Builder->BuildFaceIndices();
    }
}

[BurstCompile(CompileSynchronously = true)]
unsafe struct MergeAndShrinkJob : IJob
{
    [NativeDisableUnsafePtrRestriction] public ConvexHullBuilder* Builder;
    [NativeDisableUnsafePtrRestriction] public float* Radius;
    public float MergeTolerance;
    public float MinAngleBetweenFaces;

    public void Execute()
    {
        // Merge planes and shrink
        *Radius = Builder->SimplifyFacesAndShrink(MergeTolerance, MinAngleBetweenFaces, *Radius, ConvexCollider.k_MaxFaces, ConvexCollider.k_MaxVertices);
    }
}

class HullGenerationTest : MonoBehaviour
{
    public const float k_DefaultSimplificationTolerance = 0.01f;
    public const float k_DefaultMergeTolerance = 0.01f;
    public const float k_DefaultMinAngle = 2.5f;
    public const float k_DefaultConvexRadius = 0.05f;

    public UnityEngine.Material MeshMaterial;

    public TextAsset ResultsFile;

    public List<UnityEngine.Mesh> Meshes;
    public List<TestResult> Results;

    [HideInInspector]
    public int ResultIndex;

    [HideInInspector]
    public float SimplificationTolerance = k_DefaultSimplificationTolerance;

    [HideInInspector]
    public float MergeTolerance = k_DefaultMergeTolerance;

    [HideInInspector]
    public float MinAngle = k_DefaultMinAngle;

    [HideInInspector]
    public float Radius = k_DefaultConvexRadius;

    public bool DrawFaceIds = false;
    public bool DrawVertexIds = false;

    private ConvexHullBuilderStorage m_InitialHull;
    private ConvexHullBuilderStorage m_SimplifiedHull;

    private GameObject m_MeshDisplayObject;

    private IPointGenerator[] m_Generators;

    public IPointGenerator[] GetPointGenerators()
    {
        return m_Generators;
    }

    public void Reset()
    {
        m_Generators = new IPointGenerator[]
        {
            new MeshPointGenerator(Meshes),
            new RandomConvexGenerator(),
            new RandomEllipsoidGenerator(),
            new ConeGenerator()
        };

        Results = new List<TestResult>();
        ResultIndex = 0;

        IPointGenerator[] generators = GetPointGenerators();
        for (int i = 0; i < generators.Length; i++)
        {
            IPointGenerator gen = generators[i];
            int numSets = gen.NumSets;
            for (int j = 0; j < numSets; j++)
            {
                Results.Add(new TestResult
                {
                    GeneratorIndex = i,
                    PointSetIndex = j,
                    Name = gen.GetName(j)
                });
            }
        }
    }

    private static unsafe void CheckMaxDistance(BlobAssetReference<Collider> convexColliderARef, BlobAssetReference<Collider> convexColliderBRef, float maxDistance)
    {
        ref ConvexHull hullA = ref ((ConvexCollider*)convexColliderARef.GetUnsafePtr())->ConvexHull;
        ConvexCollider* colliderB = (ConvexCollider*)convexColliderBRef.GetUnsafePtr();

        for (int i = 0; i < hullA.NumVertices; i++)
        {
            PointDistanceInput distanceQuery = new PointDistanceInput
            {
                Filter = CollisionFilter.Default,
                MaxDistance = float.MaxValue,
                Position = hullA.Vertices[i]
            };
            convexColliderBRef.Value.CalculateDistance(distanceQuery, out DistanceHit hit);
            if (math.abs(hit.Distance) > maxDistance)
            {
                System.Diagnostics.Debug.WriteLine("ohno!");
            }
            Assert.IsTrue(math.abs(hit.Distance) <= maxDistance);
        }
    }

    private static float GetCosMinAngleBetweenFaces(ref ConvexHull hull)
    {
        // Find the minimum angle between adjacent faces in the simplified shape
        float cosMinAngle = -1.0f;
        for (int iFace = 0; iFace < hull.Faces.Length; iFace++)
        {
            ConvexHull.Face face = hull.Faces[iFace];
            float3 normal = hull.Planes[iFace].Normal;
            for (int iFaceLink = face.FirstIndex; iFaceLink < face.FirstIndex + face.NumVertices; iFaceLink++)
            {
                int oppositeFace = hull.FaceLinks[iFaceLink].FaceIndex;
                float3 oppositeNormal = hull.Planes[oppositeFace].Normal;
                float cosAngle = math.dot(normal, oppositeNormal);
                if (cosAngle > cosMinAngle)
                {
                    cosMinAngle = cosAngle;
                }
            }
        }
        return cosMinAngle;
    }

    unsafe void OnEnable()
    {
        if (Application.IsPlaying(this))
        {
            // Runtime test
            IPointGenerator generator = new MeshPointGenerator(Meshes);
            for (int i = 0; i < generator.NumSets; i++)
            {
                const float minAngle = 2.0f * math.PI / 180.0f;
                const float simplificationTolerance = 0.01f;

                NativeArray<float3> points = GetPointSet(generator, i);

                {
                    // Build the collider with and without simplification
                    BlobAssetReference<Collider> colliderRef = BuildCollider(points, 0.0f, minAngle, ConvexCollider.k_MaxVertices, ConvexCollider.k_MaxFaces, ConvexCollider.k_MaxFaceVertices);
                    BlobAssetReference<Collider> simplifiedColliderRef = BuildCollider(points, simplificationTolerance, minAngle, ConvexCollider.k_MaxVertices, ConvexCollider.k_MaxFaces, ConvexCollider.k_MaxFaceVertices);

                    // Test the maximum error from simplification and the minimum angle between faces.
                    // These are not hard guarantees, the hull builder might violate simplificationTolerance to satisfy minAngle or violate minAngle
                    // to satisfy maxVertices.  The majority of meshes tested here satisfy all of the requested limits, but for the sake of the few
                    // that don't, we test 2x the simplification tolerance and skip the quality tests for hulls with many vertices or faces (which
                    // probably had to be oversimplified due to maxVertices or maxFaces)
                    ref ConvexHull simplifiedHull = ref ((ConvexCollider*)simplifiedColliderRef.GetUnsafePtr())->ConvexHull;
                    if (simplifiedHull.Vertices.Length < ConvexCollider.k_MaxVertices * 0.95f && simplifiedHull.Faces.Length < ConvexCollider.k_MaxFaces * 0.95f)
                    {
                        CheckMaxDistance(colliderRef, simplifiedColliderRef, simplificationTolerance * 2);
                        CheckMaxDistance(simplifiedColliderRef, colliderRef, simplificationTolerance * 2);

                        float cosMinAngle = GetCosMinAngleBetweenFaces(ref simplifiedHull);
                        Assert.IsTrue(cosMinAngle < math.cos(minAngle));
                    }

                    colliderRef.Dispose();
                    simplifiedColliderRef.Dispose();
                }

                // Build the collider again with reduced output size limits to make sure it obeys the limits and doesn't fall over
                {
                    const int maxVertices = 20;
                    const int maxFaces = 30;
                    const int maxFaceVertices = 10;
                    BlobAssetReference<Collider> reducedColliderRef = BuildCollider(points, simplificationTolerance, minAngle, maxVertices, maxFaces, maxFaceVertices);
                    ConvexCollider* reducedCollider = (ConvexCollider*)reducedColliderRef.GetUnsafePtr();
                    Assert.IsTrue(reducedCollider->ConvexHull.Vertices.Length <= maxVertices);
                    Assert.IsTrue(reducedCollider->ConvexHull.Faces.Length <= maxFaces);

                    reducedColliderRef.Dispose();
                }

                points.Dispose();
            }
        }

        Reset();
    }

    void OnDisable()
    {
        m_InitialHull.Dispose();
        m_SimplifiedHull.Dispose();
    }

    // Finds the distance from each vertex of hull to collider and returns some stats
    unsafe void CalcHullDistanceStats(ref ConvexHull hull, Unity.Physics.ConvexCollider* collider, float extraRadius, ref float minDistance, ref float maxDistance)
    {
        for (int i = 0; i < hull.Vertices.Length; i++)
        {
            PointDistanceInput input = new PointDistanceInput
            {
                Position = hull.Vertices[i],
                MaxDistance = float.MaxValue,
                Filter = CollisionFilter.Default
            };
            collider->CalculateDistance(input, out DistanceHit hit);
            float distance = hit.Distance - extraRadius;
            minDistance = math.min(minDistance, distance);
            maxDistance = math.max(maxDistance, distance);
        }
    }

    unsafe HullStats TestPointSet(NativeArray<float3> points, float simplificationTolerance, float mergeTolerance, float minAngle, float radius)
    {
        // Calculate the AABB
        Aabb domain = Aabb.Empty;
        for (int i = 0; i < points.Length; i++)
        {
            domain.Include(points[i]);
        }

        // Allocate stack space for the builder
        float sumTolerance = simplificationTolerance + mergeTolerance;
        ConvexHullBuilderStorage builderStorage = new ConvexHullBuilderStorage(points.Length, Allocator.Temp, domain, sumTolerance, ConvexHullBuilder.IntResolution.High);
        ref ConvexHullBuilder builder = ref builderStorage.Builder;

        // Build the initial hull
        new BuildJob
        {
            Builder = &builderStorage.Builder,
            Points = points,
            SimplificationTolerance = simplificationTolerance + mergeTolerance
        }.Schedule().Complete();
        m_InitialHull.Dispose();
        m_InitialHull = new ConvexHullBuilderStorage(builder.Vertices.PeakCount, Allocator.Persistent, ref builder);

        // Build a shape from the un-simplified hull, if possible
        int originalNumVertices = builder.Vertices.PeakCount;
        int originalNumFaces = builder.NumFaces;
        BlobAssetReference<Collider> originalColliderRef = default;
        if (builder.Vertices.PeakCount < ConvexCollider.k_MaxVertices)
        {
            originalColliderRef = ConvexCollider.CreateInternal(builder, 0.0f, CollisionFilter.Default, Material.Default);
        }

        // Simplify the hull
        new SimplifyJob
        {
            Builder = &builderStorage.Builder,
            SimplificationTolerance = simplificationTolerance,
        }.Schedule().Complete();

        // Merge faces and shrink if the hull is 3D
        if (builderStorage.Builder.Dimension == 3)
        {
            // Calculate the maximum number of vertices after merge (which can increase the count), then allocate storage for them
            int maxNumVertices = 0;
            foreach (int v in builderStorage.Builder.Vertices.Indices)
            {
                maxNumVertices += builderStorage.Builder.Vertices[v].Cardinality - 1;
            }
            ConvexHullBuilderStorage newBuilderStorage = new ConvexHullBuilderStorage(maxNumVertices, Allocator.Temp, ref builderStorage.Builder);
            builderStorage = newBuilderStorage;

            // Simplify the hull
            new MergeAndShrinkJob
            {
                Builder = &builderStorage.Builder,
                Radius = &radius,
                MergeTolerance = mergeTolerance,
                MinAngleBetweenFaces = minAngle * (float)math.PI / 180
            }.Schedule().Complete();
        }
        m_SimplifiedHull.Dispose();
        m_SimplifiedHull = new ConvexHullBuilderStorage(builder.Vertices.PeakCount, Allocator.Persistent, ref builder);

        // Build a shape from the simplified hull
        BlobAssetReference<Collider> simplifiedColliderRef = ConvexCollider.CreateInternal(builder, radius, CollisionFilter.Default, Material.Default);
        ConvexCollider* simplifiedCollider = (ConvexCollider*)simplifiedColliderRef.GetUnsafePtr();
        ref ConvexHull simplifiedHull = ref simplifiedCollider->ConvexHull;

        // Find the minimum and maximum distance from vertices of the original collider to the simplified collider
        ConvexCollider* originalCollider = (ConvexCollider*)originalColliderRef.GetUnsafePtr();
        float minDistance = 0.0f;
        float maxDistance = 0.0f;
        if (originalCollider != null)
        {
            ref ConvexHull originalHull = ref originalCollider->ConvexHull;

            // Find the maximum error from simplification by querying the distance from any vertex of the original hull to the simplified hull and vice versa
            minDistance = float.MaxValue;
            maxDistance = float.MinValue;
            CalcHullDistanceStats(ref originalHull, simplifiedCollider, 0.0f, ref minDistance, ref maxDistance);
            CalcHullDistanceStats(ref simplifiedHull, originalCollider, radius, ref minDistance, ref maxDistance);
        }

        // Hull has two representations, vertices and planes, which are not perfectly identical.  Find the maximum distance from any vertex to a plane that it should be on.
        float planeError = 0.0f;
        for (int i = 0; i < simplifiedHull.NumFaces; i++)
        {
            ConvexHull.Face face = simplifiedHull.Faces[i];
            Unity.Physics.Plane plane = simplifiedHull.Planes[i];
            for (int j = 0; j < face.NumVertices; j++)
            {
                float3 vertex = simplifiedHull.Vertices[simplifiedHull.FaceVertexIndices[face.FirstIndex + j]];
                planeError = math.max(planeError, math.abs(plane.SignedDistanceToPoint(vertex)));
            }
        }

        // Find the minimum angle between adjacent faces in the simplified shape
        float cosMinAngle = GetCosMinAngleBetweenFaces(ref simplifiedHull);

        originalColliderRef.Dispose();
        simplifiedColliderRef.Dispose();

        return new HullStats
        {
            OriginalNumVertices = originalNumVertices,
            OriginalNumFaces = originalNumFaces,
            NumVertices = builder.Vertices.PeakCount,
            NumFaces = builder.NumFaces,
            MinError = minDistance,
            MaxError = maxDistance,
            PlaneError = planeError,
            MinAngle = math.acos(cosMinAngle),
            ConvexRadius = simplifiedHull.ConvexRadius
        };
    }

    unsafe NativeArray<float3> GetPointSet(IPointGenerator generator, int pointSetIndex)
    {
        // Get the point set
        int numPoints = generator.GetMaxNumPoints(pointSetIndex);
        if (numPoints == 0)
        {
            return new NativeArray<float3>();
        }

        NativeArray<float3> points = new NativeArray<float3>(numPoints, Allocator.TempJob);
        numPoints = generator.GetPoints(pointSetIndex, (float3*)points.GetUnsafePtr());
        return points;
    }

    BlobAssetReference<Collider> BuildCollider(NativeArray<float3> points, float simplificationTolerance, float minAngle, int maxVertices, int maxFaces, int maxFaceVertices)
    {
        using (var output = new NativeArray<BlobAssetReference<Collider>>(1, Allocator.TempJob))
        {
            var jobHandle = new CreateConvexColliderJob
            {
                Points = points,
                GenerationParameters = new ConvexHullGenerationParameters
                {
                    SimplificationTolerance = simplificationTolerance,
                    MinimumAngle = minAngle
                },
                MaxVertices = maxVertices,
                MaxFaces = maxFaces,
                MaxFaceVertices = maxFaceVertices,
                Output = output
            }.Schedule();
            jobHandle.Complete();
            return output[0];
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    struct CreateConvexColliderJob : IJob
    {
        public NativeArray<float3> Points;
        public ConvexHullGenerationParameters GenerationParameters;
        public int MaxVertices;
        public int MaxFaces;
        public int MaxFaceVertices;

        public NativeArray<BlobAssetReference<Collider>> Output;

        public void Execute() => Output[0] = ConvexCollider.CreateInternal(
            Points, GenerationParameters, CollisionFilter.Default, Material.Default,
            MaxVertices, MaxFaces, MaxFaceVertices
        );
    }

    public HullStats TestCase(IPointGenerator generator, int pointSetIndex, float simplificationTolerance, float mergeTolerance, float minAngle, float radius)
    {
        NativeArray<float3> points = GetPointSet(generator, pointSetIndex);

        // Build a hull and save stats
        HullStats stats = TestPointSet(points, simplificationTolerance, mergeTolerance, minAngle, radius);

        // Destroy existing display object
        const string childName = "HullBuilderMeshDisplay";
        UnityEngine.Transform child = transform.Find(childName);
        if (child != null)
        {
            GameObject.DestroyImmediate(child.gameObject);
        }

        // Make a new display object
        UnityEngine.Mesh mesh = generator.GetMesh(pointSetIndex);
        if (mesh != null)
        {
            UnityEngine.Material[] materials = new UnityEngine.Material[mesh.subMeshCount];
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                materials[i] = MeshMaterial;
            }

            GameObject meshObj = new GameObject(childName);
            meshObj.transform.parent = transform;
            meshObj.AddComponent<MeshFilter>().mesh = mesh;
            meshObj.AddComponent<MeshRenderer>().materials = materials;
        }

        points.Dispose();
        return stats;
    }

#if UNITY_EDITOR

    public void Test()
    {
        Results = new List<TestResult>();
        ResultIndex = 0;
        const float convexRadius = 0.0f;

        IPointGenerator[] generators = GetPointGenerators();
        for (int i = 0; i < generators.Length; i++)
        {
            IPointGenerator generator = generators[i];
            int numSets = generator.NumSets;
            for (int j = 0; j < numSets; j++)
            {
                HullStats stats = TestCase(generator, j, k_DefaultSimplificationTolerance, k_DefaultMergeTolerance, k_DefaultMinAngle, convexRadius);
                Results.Add(new TestResult
                {
                    GeneratorIndex = i,
                    PointSetIndex = j,
                    Name = generator.GetName(j),
                    Stats = stats
                });
            }
        }

        if (ResultsFile != null)
        {
            string path = AssetDatabase.GetAssetPath(ResultsFile);
            System.IO.StreamWriter writer = new System.IO.StreamWriter(path);
            writer.WriteLine("Name,MinError,MaxError,PlaneError,MinAngle,Vertices,Faces");
            foreach (TestResult result in Results)
            {
                writer.WriteLine(
                    result.Name + "," +
                    result.Stats.MinError + "," +
                    result.Stats.MaxError + "," +
                    result.Stats.PlaneError + "," +
                    result.Stats.MinAngle + "," +
                    result.Stats.NumVertices + "," +
                    result.Stats.NumFaces);
            }
            writer.Close();
        }
    }

    static Aabb drawHull(ref ConvexHullBuilder builder, float3 position, Color color, bool drawFaceIds, bool drawVertexIds)
    {
        Aabb aabb = Aabb.Empty;

        Gizmos.color = color;
        if (builder.Dimension == 3)
        {
            for (ConvexHullBuilder.FaceEdge faceEdge = builder.GetFirstFace(); faceEdge.IsValid; faceEdge = builder.GetNextFace(faceEdge))
            {
                float3 first = float3.zero;
                float3 last = float3.zero;
                float3 average = float3.zero;
                int count = 0;
                for (ConvexHullBuilder.FaceEdge edge = new ConvexHullBuilder.FaceEdge { Start = faceEdge, Current = faceEdge }; edge.IsValid; edge = builder.GetNextFaceEdge(edge))
                {
                    float3 next = builder.Vertices[builder.Triangles[edge.Current.TriangleIndex].GetVertex(edge.Current.EdgeIndex)].Position;
                    if (edge.Current.Value == edge.Start.Value)
                    {
                        first = next;
                    }
                    else
                    {
                        Gizmos.DrawLine(last + position, next + position);
                    }
                    count++;
                    average += next;
                    aabb.Include(next);
                    last = next;
                }
                Gizmos.DrawLine(first, last);

                if (drawFaceIds)
                {
                    average *= (1.0f / count);
                    int faceIndex = builder.Triangles[faceEdge.Current.TriangleIndex].FaceIndex;
                    drawString(faceIndex.ToString(), average, Color.black);
                }
            }
        }
        else // point / line / polygon, no valid face info
        {
            bool isFirst = true;
            float3 first = float3.zero;
            float3 last = float3.zero;
            foreach (int v in builder.Vertices.Indices)
            {
                float3 next = builder.Vertices[v].Position;
                if (isFirst)
                {
                    first = next;
                    isFirst = false;
                }
                else
                {
                    Gizmos.DrawLine(last, next);
                }
                last = next;
            }
            Gizmos.DrawLine(last, first);
        }

        Gizmos.color = Color.green;
        if (drawVertexIds)
        {
            foreach (int v in builder.Vertices.Indices)
            {
                Gizmos.DrawSphere(builder.Vertices[v].Position, 0.001f);
                drawString(v.ToString(), builder.Vertices[v].Position, Color.green);
            }
        }

        return aabb;
    }

    static void drawString(string text, Vector3 worldPos, Color? colour = null)
    {
        UnityEditor.Handles.BeginGUI();

        var restoreColor = GUI.color;

        if (colour.HasValue) GUI.color = colour.Value;
        var view = UnityEditor.SceneView.currentDrawingSceneView;
        Vector3 screenPos = view.camera.WorldToScreenPoint(worldPos);

        if (screenPos.y < 0 || screenPos.y > Screen.height || screenPos.x < 0 || screenPos.x > Screen.width || screenPos.z < 0)
        {
            GUI.color = restoreColor;
            UnityEditor.Handles.EndGUI();
            return;
        }

        Vector2 size = GUI.skin.label.CalcSize(new GUIContent(text));
        GUI.Label(new Rect(screenPos.x - (size.x / 2), -screenPos.y + view.position.height + 4, size.x, size.y), text);
        GUI.color = restoreColor;
        UnityEditor.Handles.EndGUI();
    }

    public void OnDrawGizmos()
    {
        // If there is no mesh to display, draw the unsimplified hull instead
        if (Results != null)
        {
            if (ResultIndex >= 0 && ResultIndex < Results.Count)
            {
                TestResult result = Results[ResultIndex];
                IPointGenerator generator = GetPointGenerators()[result.GeneratorIndex];
                if (generator.GetMesh(result.PointSetIndex) == null)
                {
                    drawHull(ref m_InitialHull.Builder, float3.zero, Color.white, false, false);
                }
            }
        }

        // Draw the simplified hull
        drawHull(ref m_SimplifiedHull.Builder, float3.zero, Color.cyan, DrawFaceIds, DrawVertexIds);
    }

#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(HullGenerationTest))]
class ConvexTestEditor : Editor
{
    private string m_Filter;

    public ConvexTestEditor()
    {
        m_Filter = "";
    }

    float slider(string name, float value, float min, float max, float defaultValue, ref bool change)
    {
        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(name, GUILayout.Width(150));
        float newValue = EditorGUILayout.Slider(value, min, max);
        if (GUILayout.Button("Default"))
        {
            newValue = defaultValue;
        }
        change |= newValue != value;
        GUILayout.EndHorizontal();
        return newValue;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        HullGenerationTest demo = (HullGenerationTest)target;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset")) demo.Reset();
        if (GUILayout.Button("Test All")) demo.Test();
        GUILayout.EndHorizontal();

        if (demo.Results != null && demo.Results.Count > 0)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label("Filter");
            m_Filter = EditorGUILayout.TextField(m_Filter);

            GUILayout.EndHorizontal();

            demo.ResultIndex = math.min(demo.ResultIndex, demo.Results.Count - 1);
            List<string> names = new List<string>();
            List<int> indices = new List<int>();
            int currentIndex = -1;
            string currentName = demo.Results[demo.ResultIndex].Name;
            string filter = m_Filter.ToLower();
            for (int i = 0; i < demo.Results.Count; i++)
            {
                string name = demo.Results[i].Name;
                if (filter.Length > 0 && !name.ToLower().Contains(filter))
                {
                    continue;
                }
                if (name.CompareTo(currentName) == 0)
                {
                    currentIndex = names.Count;
                }
                names.Add(demo.Results[i].ToString());
                indices.Add(i);
            }
            if (names.Count > 0)
            {
                if (currentIndex < 0)
                {
                    currentIndex = 0;
                }

                GUILayout.BeginHorizontal();

                float buttonWidth = 60.0f;

                int popupIndex = EditorGUILayout.Popup(currentIndex, names.ToArray());

                if (GUILayout.Button("Last", GUILayout.Width(buttonWidth)))
                {
                    popupIndex = math.max(popupIndex - 1, 0);
                }

                if (GUILayout.Button("Next", GUILayout.Width(buttonWidth)))
                {
                    popupIndex = math.min(popupIndex + 1, indices.Count - 1);
                }

                int newResultIndex = indices[popupIndex];
                bool doTest = (demo.ResultIndex != newResultIndex);
                demo.ResultIndex = newResultIndex;
                TestResult result = demo.Results[newResultIndex];
                doTest |= GUILayout.Button("Test", GUILayout.Width(buttonWidth));

                GUILayout.EndHorizontal();

                demo.SimplificationTolerance = slider("Simplification tolerance", demo.SimplificationTolerance, 0.0f, 0.1f, HullGenerationTest.k_DefaultSimplificationTolerance, ref doTest);
                demo.MergeTolerance = slider("Merge tolerance", demo.MergeTolerance, 0.0f, 0.1f, HullGenerationTest.k_DefaultMergeTolerance, ref doTest);
                demo.MinAngle = slider("Min Angle", demo.MinAngle, 0.0f, 10.0f, HullGenerationTest.k_DefaultMinAngle, ref doTest);
                demo.Radius = slider("Shrink distance", demo.Radius, 0.0f, 0.5f, HullGenerationTest.k_DefaultConvexRadius, ref doTest);

                if (doTest)
                {
                    result.Stats = demo.TestCase(demo.GetPointGenerators()[result.GeneratorIndex], result.PointSetIndex, demo.SimplificationTolerance, demo.MergeTolerance, demo.MinAngle, demo.Radius);
                    demo.Results[demo.ResultIndex] = result;
                }

                // Stats text
                string text =
                    "NumVertices\t" + result.Stats.OriginalNumVertices + " -> " + result.Stats.NumVertices + "\n" +
                    "NumFaces\t\t" + result.Stats.OriginalNumFaces + " -> " + result.Stats.NumFaces + "\n" +
                    "MinError\t\t" + result.Stats.MinError + "\n" +
                    "MaxError\t\t" + result.Stats.MaxError + "\n" +
                    "PlaneError\t\t" + result.Stats.PlaneError + "\n" +
                    "ConvexRadius\t" + result.Stats.ConvexRadius + "\n" +
                    "MinAngle\t\t" + (result.Stats.MinAngle * 180 / (float)math.PI) + "°";

                GUILayout.TextArea(text);
            }
        }

        SceneView.RepaintAll();
    }
}
#endif
