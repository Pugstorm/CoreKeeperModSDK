using System;
using Unity.Burst;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Jobs;

namespace Unity.Entities.CodeGeneratedJobForEach
{
    /// <summary>Attribute indicating that this parameter can be a dynamic value (coming from a variable instead of compile-type value).
    /// Only used by types in the Entities package.</summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    class AllowDynamicValueAttribute : Attribute { }

    /// <summary>Interface indicating that this type is used to construct a lambda job.  Only used by types in the Entities package.</summary>
    public interface ILambdaJobDescription { }

    /// <summary>Interface indicating that this type is used to construct a lambda job.  Only used by types in the Entities package.</summary>
    public interface ILambdaJobExecutionDescription { }

    /// <summary>Interface indicating that this type is used to construct a lambda job.  Only used by types in the Entities package.</summary>
    public interface ILambdaSingleJobExecutionDescription { }

    /// <summary>
    /// Interface that allows you to define your own delegate type and ForEach overload.
    /// </summary>
    /// <remarks>
    /// This interface allows you to use as many arguments as you want and to put the ref/in/value parameters in any order you want.
    /// For more information, see the user manual documentation on [Custom delegates](xref:iterating-data-entities-foreach).
    /// </remarks>
    public interface ISupportForEachWithUniversalDelegate { }

    /// <summary>Interface indicating that this type is used to construct a lambda job.  Only used by types in the Entities package.</summary>
    public interface ISingleJobDescription {}

    /// <summary>Description type used to define an Entities.ForEach.  This is typically accessed via the Entity property on a system.</summary>
    public struct ForEachLambdaJobDescription : ILambdaJobDescription, ILambdaJobExecutionDescription, ISupportForEachWithUniversalDelegate
    {
        //this overload exists here with the sole purpose of being able to give the user a not-totally-horrible
        //experience when they try to use an unsupported lambda signature. When this happens, the C# compiler
        //will go through its overload resolution, take the first candidate, and explain the user why the users
        //lambda is incompatible with that first candidates' parametertype.  We put this method here, instead
        //of with the other .ForEach overloads, to make sure this is the overload that the c# compiler will pick
        //when generating its compiler error.  If we didn't do that, it might pick a completely unrelated .ForEach
        //extension method, like the one for IJobChunk.
        //
        //The only communication channel we have to the user to guide them to figuring out what their problem is
        //is the name of the expected delegate type, as the c# compiler will put that in its compiler error message.
        //so we take this very unconventional approach here of encoding a message for the user in that type name,
        //that is easily googlable, so they will end up at a documentation page that describes why some lambda
        //signatures are compatible, and why some aren't, and what to do about that.
        //
        //the reason the delegate type is in the global namespace, is that it makes for a cleaner error message
        //it's marked with an attribute to prevent it from showing up in intellisense.
        public void ForEach(Invalid_ForEach_Signature_See_ForEach_Documentation_For_Rules_And_Restrictions ed)
        {
        }
    }

    /// <summary>Description type used to define a Job.WithCode.  This is typically accessed via the Job property on a system.</summary>
    public struct LambdaSingleJobDescription : ILambdaJobDescription, ILambdaSingleJobExecutionDescription, ISingleJobDescription
    {
    }
}

namespace Unity.Entities
{
    /// <summary>
    /// Static class holding methods to construct lambda jobs (Entities.ForEach and Job.WithCode)
    /// </summary>
    public static class LambdaJobDescriptionConstructionMethods
    {
        /// <summary>Attribute indicating that this parameter can be a dynamic value (coming from a variable instead of compile-type value).
        /// Only used by types in the Entities package.</summary>
        [AttributeUsage(AttributeTargets.Method)]
        internal class AllowMultipleInvocationsAttribute : Attribute {}

        /// <summary>
        /// Disable Burst for the Entities.ForEach or Job.WithCode that this invocation constructs.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        public static TDescription WithoutBurst<TDescription>(this TDescription description) where TDescription : ILambdaJobDescription => description;

        /// <summary>
        /// Enables Burst for the Entities.ForEach or Job.WithCode that this invocation constructs.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <param name="floatMode">Floating point optimization mode for Burst compilation</param>
        /// <param name="floatPrecision">Floating point precision used for certain builtin operations</param>
        /// <param name="synchronousCompilation">Whether this invocation should be compiled by Burst before execution begins</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        public static TDescription WithBurst<TDescription>(this TDescription description, FloatMode floatMode = FloatMode.Default, FloatPrecision floatPrecision = FloatPrecision.Standard, bool synchronousCompilation = false) where TDescription : ILambdaJobDescription => description;

        /// <summary>
        /// Provides a name for the generated job.  This can be viewed in the profiler.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <param name="name">Name for the generated job</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        public static TDescription WithName<TDescription>(this TDescription description, string name) where TDescription : ILambdaJobDescription => description;

        /// <summary>
        /// Enables structural changes to occur in the lambda.
        /// </summary>
        /// <remarks>
        /// This mode adds additional safety checks and only works without Burst and with .Run.
        /// Important: This makes execution very slow.
        /// </remarks>
        /// <param name="description">The target object</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        public static TDescription WithStructuralChanges<TDescription>(this TDescription description) where TDescription : ILambdaJobDescription => description;

        /// <summary>
        /// Capture a variable that stores a native container with read-only access.
        /// This allows the job system to track this container as only being read from (potentially allowing more job scheduling).
        /// </summary>
        /// <param name="description">The target object</param>
        /// <param name="capturedVariable">Captured variable that stores a NativeContainer</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        /// <typeparam name="TCapturedVariableType">Type of captured variable</typeparam>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static TDescription WithReadOnly<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;

        /// <summary>
        /// Mark a captured Native Container or type that contains a Native Container to be disposed of after the job finishes
        /// (or immediately after with .Run()).
        /// </summary>
        /// <param name="description">The target object</param>
        /// <param name="capturedVariable">Captured variable that stores a NativeContainer</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        /// <typeparam name="TCapturedVariableType">Type of captured variable</typeparam>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static TDescription WithDisposeOnCompletion<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;

        /// <summary>
        /// Disable safety checks for a given captured variable that stores a native container.
        /// This will allow some jobs to run that wouldn't otherwise, but it does by-pass the safety system for this container.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <param name="capturedVariable">Captured variable that stores a NativeContainer</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        /// <typeparam name="TCapturedVariableType">Type of captured variable</typeparam>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static TDescription WithNativeDisableContainerSafetyRestriction<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;

        /// <summary>
        /// Allows capture of a native container to be passed to a job even though it contains a pointer, which is usually not allowed.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <param name="capturedVariable">Captured variable that stores a NativeContainer</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        /// <typeparam name="TCapturedVariableType">Type of captured variable</typeparam>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static unsafe TDescription WithNativeDisableUnsafePtrRestriction<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType* capturedVariable) where TDescription : ILambdaJobDescription where TCapturedVariableType : unmanaged => description;


        /// <summary>
        /// Disables safety checks for a captured native container for the generated parallel job
        /// that may write to the same container from other job workers for the same job instance.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <param name="capturedVariable">Captured variable that stores a NativeContainer</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        /// <typeparam name="TCapturedVariableType">Type of captured variable</typeparam>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static TDescription WithNativeDisableParallelForRestriction<TDescription, TCapturedVariableType>(this TDescription description, [AllowDynamicValue] TCapturedVariableType capturedVariable) where TDescription : ILambdaJobDescription => description;
    }

    /// <summary>
    /// Static class holding methods to construct lambda jobs (Entities.ForEach and Job.WithCode)
    /// </summary>
    public static class LambdaJobDescriptionExecutionMethods
    {
        /// <summary>
        /// Schedule the generated job to be run sequentially.
        /// This job may run at a later point, but will run the job execution sequentially instead of in parallel.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <param name="dependency">The handle identifying already scheduled jobs that could constrain this job.
        /// A job that writes to a component cannot run in parallel with other jobs that read or write that component.
        /// Jobs that only read the same components can run in parallel.</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        /// <returns>A handle that combines the current Job with previous dependencies identified by the `dependency`
        /// parameter.</returns>
        public static JobHandle Schedule<TDescription>(this TDescription description, [AllowDynamicValue] JobHandle dependency) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();

        /// <summary>
        /// Schedule the generated job to be run in parallel.
        /// This job may run at a later point and will run the job execution in parallel.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <param name="dependency">The handle identifying already scheduled jobs that could constrain this job.
        /// A job that writes to a component cannot run in parallel with other jobs that read or write that component.
        /// Jobs that only read the same components can run in parallel.</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        /// <returns>A handle that combines the current Job with previous dependencies identified by the `dependency`
        /// parameter.</returns>
        public static JobHandle ScheduleParallel<TDescription>(this TDescription description, [AllowDynamicValue] JobHandle dependency) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();

        /// <summary>
        /// Schedule the generated job to be run sequentially and use the Dependency property of the containing system for both
        /// the input and output dependencies.
        /// This job may run at a later point, but will run the job execution sequentially instead of in parallel.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        public static void Schedule<TDescription>(this TDescription description) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();

        /// <summary>
        /// Schedule the generated job to be run in parallel and use the Dependency property of the containing system for both
        /// the input and output dependencies.
        /// This job may run at a later point and will run the job execution in parallel.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        public static void ScheduleParallel<TDescription>(this TDescription description) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();

        /// <summary>
        /// Runs the generated job immediately on the current thread.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        public static void Run<TDescription>(this TDescription description) where TDescription : ILambdaJobExecutionDescription => ThrowCodeGenException();

        /// <summary>
        /// Used internally. It throws an an Exception if source generation did not run.
        /// </summary>
        /// <returns>Not applicable; the function always throws.</returns>
        /// <exception cref="Exception">Source-gen not run</exception>
        static JobHandle ThrowCodeGenException() => throw new Exception("This SystemBase method should have been replaced by codegen");
    }

    /// <summary>
    /// Static class holding methods to construct lambda jobs (Entities.ForEach and Job.WithCode)
    /// </summary>
    public static class LambdaSingleJobDescriptionExecutionMethods
    {
        /// <summary>
        /// Schedule the generated job to be run sequentially.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <param name="dependency">The handle identifying already scheduled jobs that could constrain this job.
        /// A job that writes to a component cannot run in parallel with other jobs that read or write that component.
        /// Jobs that only read the same components can run in parallel.</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        /// <returns>A handle that combines the current Job with previous dependencies identified by the `dependency`
        /// parameter.</returns>
        public static JobHandle Schedule<TDescription>(this TDescription description, [AllowDynamicValue] JobHandle dependency) where TDescription : ILambdaSingleJobExecutionDescription => ThrowCodeGenException();

        /// <summary>
        /// Schedule the generated job to be run sequentially and use the Dependency property of the containing system for both
        /// the input and output dependencies.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        public static void Schedule<TDescription>(this TDescription description) where TDescription : ILambdaSingleJobExecutionDescription => ThrowCodeGenException();

        /// <summary>
        /// Runs the generated job immediately on the current thread.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        public static void Run<TDescription>(this TDescription description) where TDescription : ILambdaSingleJobExecutionDescription => ThrowCodeGenException();

        /// <summary>
        /// Used internally. It throws an an Exception if source generation did not run.
        /// </summary>
        /// <returns>Not applicable; the function always throws.</returns>
        /// <exception cref="Exception">Source-gen not run</exception>
        static JobHandle ThrowCodeGenException() => throw new Exception("This SystemBase method should have been replaced by codegen");
    }

    /// <summary>
    /// Static class holding methods to construct Job.WithCode jobs.
    /// </summary>
    public static class LambdaSingleJobDescriptionConstructionMethods
    {
        /// <summary>
        /// Delegate type used to define the execution of a Job.WithCode job.
        /// </summary>
        public delegate void WithCodeAction();

        /// <summary>
        /// Provides an easy way to run a function as a single background job.
        /// You can also run Job.WithCode on the main thread and still take advantage of Burst compilation to speed up execution.
        /// </summary>
        /// <param name="description">The target object</param>
        /// <param name="code">Lambda that provides the execution for the job.</param>
        /// <typeparam name="TDescription">Type of target object</typeparam>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        public static TDescription WithCode<TDescription>(this TDescription description,  [AllowDynamicValue] WithCodeAction code)
            where TDescription : ISingleJobDescription => description;
    }

    /// <summary>
    /// Provides methods for Constructing Entities.ForEach invocations.
    /// </summary>
    static partial class LambdaForEachDescriptionConstructionMethods
    {
        /// <summary>
        /// Used internally. It throws an an Exception if source generation did not run.
        /// </summary>
        /// <returns>Not applicable; the function always throws.</returns>
        /// <exception cref="Exception">Source-gen not run</exception>
        /// <typeparam name="TDescription">Type of target object.</typeparam>
        public static TDescription ThrowCodeGenException<TDescription>() => throw new Exception("This method should have been replaced by codegen");
    }

    /// <summary>
    /// Special delegate for overload to catch invalid Entities.ForEach signatures.  Do not use!
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public delegate object Invalid_ForEach_Signature_See_ForEach_Documentation_For_Rules_And_Restrictions(object o);
}
