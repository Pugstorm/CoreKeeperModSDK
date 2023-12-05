using System;
#if !UNITY_DOTSRUNTIME
using UnityEngine.Scripting.APIUpdating;
#endif

namespace Unity.Jobs
{
    /// <summary>
    /// When added as an assembly-level attribute, allows creating job reflection data for instances of generic jobs.
    /// </summary>
    /// <remarks>
    /// This attribute allows specific instances of generic jobs to be registered for reflection data generation.
    /// </remarks>
#if !UNITY_DOTSRUNTIME
    [MovedFrom(true, "Unity.Entities", "Unity.Entities")]
#endif
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class RegisterGenericJobTypeAttribute : Attribute
    {
        /// <summary>
        /// Fully closed generic job type to register with the job system
        /// </summary>
        public Type ConcreteType;

        /// <summary>
        /// Registers a fully closed generic job type with the job system
        /// </summary>
        /// <param name="type"></param>
        public RegisterGenericJobTypeAttribute(Type type)
        {
            ConcreteType = type;
        }
    }
    
    [AttributeUsage(AttributeTargets.Class)]
    internal class DOTSCompilerGeneratedAttribute : Attribute
    {}
}
