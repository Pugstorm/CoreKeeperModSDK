using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Unity.Physics
{
    /// <summary>   A collection of motion information used during physics simulation. </summary>
    [NoAlias]
    public struct DynamicsWorld : IDisposable
    {
        [NoAlias]
        internal NativeArray<MotionData> m_MotionDatas;
        [NoAlias]
        internal NativeArray<MotionVelocity> m_MotionVelocities;
        internal NativeReference<int> m_NumMotions; // number of motionDatas and motionVelocities currently in use

#if !UNITY_PHYSICS_DISABLE_JOINTS
        [NoAlias]
        private NativeArray<Joint> m_Joints;
        private int m_NumJoints; // number of joints currently in use
        [NoAlias] internal NativeParallelHashMap<Entity, int> EntityJointIndexMap;
#endif

        /// <summary>   Gets the motion datas. </summary>
        ///
        /// <value> The motion datas. </value>
        public NativeArray<MotionData> MotionDatas => m_MotionDatas.GetSubArray(0, m_NumMotions.Value);

        /// <summary>   Gets the motion velocities. </summary>
        ///
        /// <value> The motion velocities. </value>
        public NativeArray<MotionVelocity> MotionVelocities => m_MotionVelocities.GetSubArray(0, m_NumMotions.Value);

#if !UNITY_PHYSICS_DISABLE_JOINTS
        /// <summary>   Gets the joints. </summary>
        ///
        /// <value> The joints. </value>
        public NativeArray<Joint> Joints => m_Joints.GetSubArray(0, m_NumJoints);
#endif

        /// <summary>   Gets the number of motions. </summary>
        ///
        /// <value> The total number of motions. </value>
        public int NumMotions => m_NumMotions.Value;

#if !UNITY_PHYSICS_DISABLE_JOINTS
        /// <summary>   Gets the number of joints. </summary>
        ///
        /// <value> The total number of joints. </value>
        public int NumJoints => m_NumJoints;
#endif

        /// <summary>   Construct a dynamics world with the given number of uninitialized motions. </summary>
        ///
        /// <param name="numMotions">   Number of motions. </param>
        /// <param name="numJoints">    Number of joints. </param>
        public DynamicsWorld(int numMotions, int numJoints)
        {
            m_MotionDatas = new NativeArray<MotionData>(numMotions, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_MotionVelocities = new NativeArray<MotionVelocity>(numMotions, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_NumMotions = new NativeReference<int>(Allocator.Persistent, NativeArrayOptions.ClearMemory);

#if !UNITY_PHYSICS_DISABLE_JOINTS
            m_Joints = new NativeArray<Joint>(numJoints, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_NumJoints = numJoints;

            EntityJointIndexMap = new NativeParallelHashMap<Entity, int>(numJoints, Allocator.Persistent);
#endif
        }

        /// <summary>   Resets this object. </summary>
        ///
        /// <param name="numMotions">   Number of motions. </param>
        /// <param name="numJoints">    Number of joints. </param>
        public void Reset(int numMotions, int numJoints)
        {
            m_NumMotions.Value = numMotions;
            if (m_MotionDatas.Length < NumMotions)
            {
                m_MotionDatas.Dispose();
                m_MotionDatas = new NativeArray<MotionData>(Mathematics.math.ceilpow2(NumMotions), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }
            if (m_MotionVelocities.Length < NumMotions)
            {
                m_MotionVelocities.Dispose();
                m_MotionVelocities = new NativeArray<MotionVelocity>(Mathematics.math.ceilpow2(NumMotions), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

#if !UNITY_PHYSICS_DISABLE_JOINTS
            m_NumJoints = numJoints;
            if (m_Joints.Length < m_NumJoints)
            {
                m_Joints.Dispose();
                m_Joints = new NativeArray<Joint>(Mathematics.math.ceilpow2(m_NumJoints), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                EntityJointIndexMap.Capacity = m_NumJoints;
            }
            EntityJointIndexMap.Clear();
#endif
        }

        /// <summary>   Free internal memory. </summary>
        public void Dispose()
        {
            if (m_MotionDatas.IsCreated)
            {
                m_MotionDatas.Dispose();
            }

            if (m_MotionVelocities.IsCreated)
            {
                m_MotionVelocities.Dispose();
            }

            if (m_NumMotions.IsCreated)
            {
                m_NumMotions.Dispose();
            }

#if !UNITY_PHYSICS_DISABLE_JOINTS
            if (m_Joints.IsCreated)
            {
                m_Joints.Dispose();
            }

            if (EntityJointIndexMap.IsCreated)
            {
                EntityJointIndexMap.Dispose();
            }
#endif
        }

        /// <summary>   Clone the world. </summary>
        ///
        /// <returns>   A copy of this object. </returns>
        public DynamicsWorld Clone()
        {
            DynamicsWorld clone = new DynamicsWorld
            {
                m_MotionDatas = new NativeArray<MotionData>(m_MotionDatas.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                m_MotionVelocities = new NativeArray<MotionVelocity>(m_MotionVelocities.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                m_NumMotions = m_NumMotions,
#if !UNITY_PHYSICS_DISABLE_JOINTS
                m_Joints = new NativeArray<Joint>(m_Joints.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                m_NumJoints = m_NumJoints,
                EntityJointIndexMap = new NativeParallelHashMap<Entity, int>(m_Joints.Length, Allocator.Persistent),
#endif
            };
            clone.m_MotionDatas.CopyFrom(m_MotionDatas);
            clone.m_MotionVelocities.CopyFrom(m_MotionVelocities);
#if !UNITY_PHYSICS_DISABLE_JOINTS
            clone.m_Joints.CopyFrom(m_Joints);
            clone.UpdateJointIndexMap();
#endif
            return clone;
        }

#if !UNITY_PHYSICS_DISABLE_JOINTS
        /// <summary>   Updates the joint index map. </summary>
        public void UpdateJointIndexMap()
        {
            EntityJointIndexMap.Clear();
            for (int i = 0; i < m_Joints.Length; i++)
            {
                EntityJointIndexMap.TryAdd(m_Joints[i].Entity, i);
            }
        }

        /// <summary>   Gets the zero-based index of the joint. </summary>
        ///
        /// <param name="entity">   The entity. </param>
        ///
        /// <returns>   The joint index. </returns>
        public int GetJointIndex(Entity entity)
        {
            return EntityJointIndexMap.TryGetValue(entity, out var index) ? index : -1;
        }
#endif
    }
}
