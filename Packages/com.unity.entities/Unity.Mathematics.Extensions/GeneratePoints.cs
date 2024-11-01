using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Unity.Mathematics
{
    /// <summary>
    /// Tools for generating random points inside of a shape of interest
    /// </summary>
    public struct GeneratePoints
    {
        struct PointsInSphere : IJob
        {
            public float Radius;
            public float3 Center;
            public NativeArray<float3> Points;

            public void Execute()
            {
                var radiusSquared = Radius * Radius;
                var pointsFound = 0;
                var count = Points.Length;
                var random = new Random(0x6E624EB7u);

                while (pointsFound < count)
                {
                    var p = random.NextFloat3() * new float3(Radius + Radius) - new float3(Radius);
                    if (math.lengthsq(p) < radiusSquared)
                    {
                        Points[pointsFound] = Center + p;
                        pointsFound++;
                    }
                }
            }
        }

        /// <summary>
        /// Schedule Burst jobs to generate random points inside of a sphere
        /// </summary>
        /// <param name="center">The center of the sphere</param>
        /// <param name="radius">The radius of the sphere</param>
        /// <param name="points">An array into which the random points are stored</param>
        /// <param name="inputDeps">A JobHandle to wait for, before the jobs scheduled by this function</param>
        /// <returns>A JobHandle of the job that was created to generate random points inside a sphere</returns>
        public static JobHandle RandomPointsInSphere(float3 center, float radius, NativeArray<float3> points,
            JobHandle inputDeps)
        {
            var pointsInSphereJob = new PointsInSphere
            {
                Radius = radius,
                Center = center,
                Points = points
            };
            var pointsInSphereJobHandle = pointsInSphereJob.Schedule(inputDeps);
            return pointsInSphereJobHandle;
        }

        /// <summary>
        /// A function that generates random points inside of a sphere. Schedules and completes jobs,
        /// before returning to its caller.
        /// </summary>
        /// <param name="center">The center of the sphere</param>
        /// <param name="radius">The radius of the sphere</param>
        /// <param name="points">A NativeArray in which to store the randomly generated points</param>
        public static void RandomPointsInSphere(float3 center, float radius, NativeArray<float3> points)
        {
            var randomPointsInSphereJobHandle = RandomPointsInSphere(center, radius, points, new JobHandle());
            randomPointsInSphereJobHandle.Complete();
        }

        /// <summary>
        /// A function that generates random points inside of a unit sphere. Schedules and completes jobs,
        /// before returning to its caller.
        /// </summary>
        /// <param name="points">A NativeArray in which to store the randomly generated points</param>
        public static void RandomPointsInUnitSphere(NativeArray<float3> points)
        {
            var randomPointsInSphereJobHandle = RandomPointsInSphere(0.0f, 1.0f, points, new JobHandle());
            randomPointsInSphereJobHandle.Complete();
        }

        /// <summary>
        /// A function that returns a single random position, fairly distributed inside the unit sphere.
        /// </summary>
        /// <param name="seed">A seed to the random number generator</param>
        /// <returns>A point inside of the unit sphere, fairly distributed</returns>
        public static float3 RandomPositionInsideUnitSphere(uint seed)
        {
            var random = new Random(seed);
            while (true)
            {
                float3 randomPosition = random.NextFloat3();
                var doubled = randomPosition * new float3(2);
                var offset = doubled - new float3(1, 1, 1);
                if (math.lengthsq(offset) > 1)
                    continue;

                return offset;
            }
        }
    }
}
