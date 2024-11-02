using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;

[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public delegate object Invalid_ForEach_Signature_See_ForEach_Documentation_For_Rules_And_Restrictions(object o);

namespace Unity.Entities.UniversalDelegates
{
    public delegate void Empty();

    public delegate void R<T0>(ref T0 t0);

    public delegate void I<T0>(in T0 t0);

    public delegate void V<T0>(T0 t0);

    public delegate void RI<T0, T1>(ref T0 t0, in T1 t1);

    public delegate void RR<T0, T1>(ref T0 t0, ref T1 t1);

    public delegate void II<T0, T1>(in T0 t0, in T1 t1);

    public delegate void VI<T0, T1>(T0 t0, in T1 t1);

    public delegate void VR<T0, T1>(T0 t0, ref T1 t1);

    public delegate void VV<T0, T1>(T0 t0, T1 t1);

    public delegate void RRI<T0, T1, T2>(ref T0 t0, ref T1 t1, in T2 t2);
}

public static partial class LambdaForEachDescriptionConstructionMethods
{
    public static TDescription ForEach<TDescription>(this TDescription description, [AllowDynamicValue] Empty codeToRun) where TDescription : struct, ISupportForEachWithUniversalDelegate => ThrowCodeGenException<TDescription>();
    public static TDescription ForEach<TDescription, T0>(this TDescription description, [AllowDynamicValue] R<T0> codeToRun) where TDescription : struct, ISupportForEachWithUniversalDelegate => ThrowCodeGenException<TDescription>();
    public static TDescription ForEach<TDescription, T0>(this TDescription description, [AllowDynamicValue] I<T0> codeToRun) where TDescription : struct, ISupportForEachWithUniversalDelegate => ThrowCodeGenException<TDescription>();
    public static TDescription ForEach<TDescription, T0>(this TDescription description, [AllowDynamicValue] V<T0> codeToRun) where TDescription : struct, ISupportForEachWithUniversalDelegate => ThrowCodeGenException<TDescription>();
    public static TDescription ForEach<TDescription, T0, T1>(this TDescription description, [AllowDynamicValue] RI<T0, T1> codeToRun) where TDescription : struct, ISupportForEachWithUniversalDelegate => ThrowCodeGenException<TDescription>();
    public static TDescription ForEach<TDescription, T0, T1>(this TDescription description, [AllowDynamicValue] RR<T0, T1> codeToRun) where TDescription : struct, ISupportForEachWithUniversalDelegate => ThrowCodeGenException<TDescription>();
    public static TDescription ForEach<TDescription, T0, T1>(this TDescription description, [AllowDynamicValue] II<T0, T1> codeToRun) where TDescription : struct, ISupportForEachWithUniversalDelegate => ThrowCodeGenException<TDescription>();
    public static TDescription ForEach<TDescription, T0, T1>(this TDescription description, [AllowDynamicValue] VI<T0, T1> codeToRun) where TDescription : struct, ISupportForEachWithUniversalDelegate => ThrowCodeGenException<TDescription>();
    public static TDescription ForEach<TDescription, T0, T1>(this TDescription description, [AllowDynamicValue] VR<T0, T1> codeToRun) where TDescription : struct, ISupportForEachWithUniversalDelegate => ThrowCodeGenException<TDescription>();
    public static TDescription ForEach<TDescription, T0, T1>(this TDescription description, [AllowDynamicValue] VV<T0, T1> codeToRun) where TDescription : struct, ISupportForEachWithUniversalDelegate => ThrowCodeGenException<TDescription>();
    public static TDescription ForEach<TDescription,T0, T1, T2>(this TDescription description, [AllowDynamicValue] RRI<T0, T1, T2> codeToRun) where TDescription : struct, ISupportForEachWithUniversalDelegate => ThrowCodeGenException<TDescription>();
}

namespace Unity.Entities.CodeGeneratedJobForEach
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal class AllowDynamicValueAttribute : Attribute { }

    public interface ILambdaJobDescription { }
    public interface ILambdaJobExecutionDescription { }
    public interface ILambdaSingleJobExecutionDescription { }
    public interface ISupportForEachWithUniversalDelegate { }
    public interface ISingleJobDescription {}
    public struct LambdaSingleJobDescription : ILambdaJobDescription, ILambdaSingleJobExecutionDescription, ISingleJobDescription { }

    public struct ForEachLambdaJobDescription : ILambdaJobDescription, ILambdaJobExecutionDescription, ISupportForEachWithUniversalDelegate { }
}

namespace Unity.Entities
{
    public static class LambdaJobDescriptionConstructionMethods
    {
        [AttributeUsage(AttributeTargets.Method)]
        internal class AllowMultipleInvocationsAttribute : Attribute {}

        public static TDescription WithoutBurst<TDescription>(this TDescription description) where TDescription : ILambdaJobDescription => description;

        public static TDescription WithBurst<TDescription>(this TDescription description, FloatMode floatMode = FloatMode.Default, FloatPrecision floatPrecision = FloatPrecision.Standard, bool synchronousCompilation = false) where TDescription : ILambdaJobDescription => description;
        public static TDescription WithName<TDescription>(this TDescription description, string name) where TDescription : ILambdaJobDescription => description;
        public static TDescription WithStructuralChanges<TDescription>(this TDescription description) where TDescription : ILambdaJobDescription => description;

        [AllowMultipleInvocations]
        public static TDescription WithReadOnly<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;

        [AllowMultipleInvocations]
        public static TDescription WithDisposeOnCompletion<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
        [AllowMultipleInvocations]
        public static TDescription WithNativeDisableContainerSafetyRestriction<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
        [AllowMultipleInvocations]
        public static unsafe TDescription WithNativeDisableUnsafePtrRestriction<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType* capturedVariable) where TDescription : ILambdaJobDescription where TCapturedVariableType : unmanaged => description;
        [AllowMultipleInvocations]
        public static TDescription WithNativeDisableParallelForRestriction<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
        public static TDescription WithScheduleGranularity<TDescription>(this TDescription description, ScheduleGranularity granularity) where TDescription : ILambdaJobDescription => description;
    }

    public static class LambdaJobDescriptionExecutionMethods
    {
        public static JobHandle Schedule<TDescription>(this TDescription description, [AllowDynamicValue] JobHandle dependency) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();
        public static JobHandle ScheduleParallel<TDescription>(this TDescription description, [AllowDynamicValue] JobHandle dependency) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();

        public static void Schedule<TDescription>(this TDescription description) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();
        public static void ScheduleParallel<TDescription>(this TDescription description) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();
        public static void Run<TDescription>(this TDescription description) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();

        static JobHandle ThrowCodeGenException() => throw new Exception("This SystemBase method should have been replaced by codegen");
    }

    public static class LambdaSingleJobDescriptionExecutionMethods
    {
        public static JobHandle Schedule<TDescription>(this TDescription description, [AllowDynamicValue] JobHandle dependency) where TDescription : ILambdaSingleJobExecutionDescription => ThrowCodeGenException();

        public static void Schedule<TDescription>(this TDescription description) where TDescription : ILambdaSingleJobExecutionDescription => ThrowCodeGenException();
        public static void Run<TDescription>(this TDescription description) where TDescription : ILambdaSingleJobExecutionDescription => ThrowCodeGenException();

        static JobHandle ThrowCodeGenException() => throw new Exception("This SystemBase method should have been replaced by codegen");
    }

    public static class LambdaSingleJobDescriptionConstructionMethods
    {
        public delegate void WithCodeAction();
        public static TDescription WithCode<TDescription>(this TDescription description,  [AllowDynamicValue] WithCodeAction code)
            where TDescription : ISingleJobDescription => description;
    }

    public static class ForEachLambdaJobDescription_SetSharedComponent
    {
        public static TDescription SetSharedComponentFilterOnQuery<TDescription, T>(this TDescription description, T sharedComponent, EntityQuery query)
            where TDescription : struct, ISupportForEachWithUniversalDelegate
            where T : struct, ISharedComponentData
            => throw default;
    }
}

public static partial class LambdaForEachDescriptionConstructionMethods
{
    public static TDescription ThrowCodeGenException<TDescription>() => throw new Exception("This method should have been replaced by codegen");
    public static void ThrowCodeGenInvalidMethodCalledException() => throw new Exception("This method was replaced during post-processing and should not have been called.  Please file a bug with us!");
}

public static class LambdaJobQueryConstructionMethods
{
    [LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute]
    public static ForEachLambdaJobDescription WithNone<T>(this ForEachLambdaJobDescription description) => description;

    [LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute]
    public static ForEachLambdaJobDescription WithNone<T1, T2>(this ForEachLambdaJobDescription description) => description;

    [LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute]
    public static ForEachLambdaJobDescription WithNone<T1, T2, T3>(this ForEachLambdaJobDescription description) => description;

    [LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute]
    public static ForEachLambdaJobDescription WithAny<T>(this ForEachLambdaJobDescription description) => description;

    [LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute]
    public static ForEachLambdaJobDescription WithAny<T1, T2>(this ForEachLambdaJobDescription description) => description;

    [LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute]
    public static ForEachLambdaJobDescription WithAny<T1, T2, T3>(this ForEachLambdaJobDescription description) => description;

    [LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute]
    public static ForEachLambdaJobDescription WithAll<T>(this ForEachLambdaJobDescription description) => description;

    [LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute]
    public static ForEachLambdaJobDescription WithAll<T1, T2>(this ForEachLambdaJobDescription description) => description;

    [LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute]
    public static ForEachLambdaJobDescription WithAll<T1, T2, T3>(this ForEachLambdaJobDescription description) => description;

    [LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute]
    public static ForEachLambdaJobDescription WithChangeFilter<T>(this ForEachLambdaJobDescription description) => description;

    [LambdaJobDescriptionConstructionMethods.AllowMultipleInvocationsAttribute]
    public static ForEachLambdaJobDescription WithChangeFilter<T1, T2>(this ForEachLambdaJobDescription description) => description;
    public static ForEachLambdaJobDescription WithDeferredPlaybackSystem<T>(this ForEachLambdaJobDescription description) where T : EntityCommandBufferSystem => description;
    public static ForEachLambdaJobDescription WithImmediatePlayback(this ForEachLambdaJobDescription description) => description;
    public static ForEachLambdaJobDescription WithEntityQueryOptions(this ForEachLambdaJobDescription description, EntityQueryOptions options) => description;
    public static ForEachLambdaJobDescription WithSharedComponentFilter<T>(this ForEachLambdaJobDescription description, [AllowDynamicValue] T sharedComponent) where T : struct, ISharedComponentData => description;
    public static ForEachLambdaJobDescription WithStoreEntityQueryInField(this ForEachLambdaJobDescription description, [AllowDynamicValue] ref EntityQuery query) => description;
    public static ForEachLambdaJobDescription WithSharedComponentFilter<T1, T2>(this ForEachLambdaJobDescription description, [AllowDynamicValue] T1 sharedComponent1, [AllowDynamicValue] T2 sharedComponent2) where T1 : struct, ISharedComponentData where T2 : struct, ISharedComponentData =>
        description;
    public static ForEachLambdaJobDescription WithFilter(this ForEachLambdaJobDescription description, [AllowDynamicValue] NativeArray<Entity> entities) => description;
    public static void DestroyEntity(this ForEachLambdaJobDescription _) {}
}

