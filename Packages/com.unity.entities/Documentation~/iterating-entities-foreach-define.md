# Define and execute an Entities.ForEach lambda expression

To use `Entities.ForEach`, you must pass it a lambda expression, which Unity uses to generate an [entity query](systems-entityquery.md) based on the lambda parameter types. When the generated job runs, Unity calls the lambda expression once for each entity that matches the query. `ForEachLambdaJobDescription` represents this generated job.

When you define the [`Entities.ForEach`](xref:Unity.Entities.SystemBase.Entities) lambda expression, you can declare parameters that the [`SystemBase`](xref:Unity.Entities.SystemBase) class uses to pass in information about the current entity when it executes the method.

A typical lambda expression looks like this:

[!code-cs[lambda-params](../DocCodeSamples.Tests/LambdaJobExamples.cs#lambda-params)]

You can pass up to eight parameters to an `Entities.ForEach` lambda expression. If you need to pass more parameters, you can define a custom delegate. For more information, refer to the section on [Custom delegates](#custom-delegates) in this document. 

When you use the standard delegates, you must group the parameters in the following order:

1. Parameters passed-by-value (no parameter modifiers)
1. Writable parameters (`ref` parameter modifier)
1. Read-only parameters (`in` parameter modifier)

You must use the `ref` or `in` parameter modify keyword on all components. If you don't, the component struct that Unity passes to your method is a copy instead of a reference. This means that it takes up extra memory for the read-ony parameters, and Unity silently throws any changes you make to the components when the copied struct goes out of scope after the function returns.

If the lambda expression doesn't follow this order, and you haven't created a suitable delegate, the compiler provides an error similar to:

`error CS1593: Delegate 'Invalid_ForEach_Signature_See_ForEach_Documentation_For_Rules_And_Restrictions' does not take N arguments`

This error message cites the number of arguments as the issue even when though the problem is the parameter order.

## Custom delegates

If you want to use more than eight arguments in a `ForEach` lambda expression, you must declare your own delegate type and `ForEach` overload. Declaring your own type means you can use an unlimited amount of arguments and put the `ref`, `in`, and `value` parameters in any order you want.

You can also declare the three [named parameters](#named-parameters) `entity`, `entityInQueryIndex`, and `nativeThreadIndex` anywhere in your parameter list. Don't use `ref` or `in` modifiers for these parameters. 

The following example shows 12 arguments, and uses the `entity` parameter within the lambda expression:

[!code-cs[lambda-params](../DocCodeSamples.Tests/LambdaJobExamples.cs#lambda-params-many)]

## Component parameters

To access a component associated with an entity, you must pass a parameter of that component type to the lambda expression. The compiler automatically adds all components passed to the lambda expression to the entity query as required components. 

To update a component value, you must use the `ref` keyword in the parameter list to pass it to the lambda expression. Without the `ref` keyword, Unity makes any modifications to a temporary copy of the component.

To declare a read-only component passed to the lambda expression use the `in` keyword in the parameter list.

When you use `ref`, Unity marks the components in the current chunk as changed, even if the lambda expression doesn't actually change them. For efficiency, always use the `in` keyword to declare components that your lambda expression doesn't change as read only.

The following example passes a `Source` component parameter to a job as read-only, and a `Destination` component parameter as writable: 

[!code-cs[read-write-modifiers](../DocCodeSamples.Tests/LambdaJobExamples.cs#read-write-modifiers)]

> [!IMPORTANT]
> You can't pass [chunk components](components-chunk.md) to the `Entities.ForEach` lambda expression.

For dynamic buffers, use `DynamicBuffer<T>` rather than the component type stored in the buffer:

[!code-cs[dynamicbuffer](../DocCodeSamples.Tests/LambdaJobExamples.cs#dynamicbuffer)]

## Named parameters

You can also pass the following named parameters to the `Entities.ForEach` lambda expression, which Unity assigns values based on the entity the job is processing.

|**Parameter**|**Description**|
|---|---|
|`Entity entity`| The entity instance of the current entity. You can name the parameter anything as long as the type is `Entity`.|
|`int entityInQueryIndex`| The index of the entity in the list of all entities that the query selected. Use the entity index value when you have a native array that you need to fill with a unique value for each entity. You can use the `entityInQueryIndex` as the index in that array. Use `entityInQueryIndex` as the `sortKey` to add commands to a concurrent [entity command buffer](systems-entity-command-buffers.md).|
|`int nativeThreadIndex`| A unique index of the thread executing the current iteration of the lambda expression. When you use `Run` to execute the lambda expression, `nativeThreadIndex` is always zero. Don't use `nativeThreadIndex` as the `sortKey` of a concurrent [entity command buffer](systems-entity-command-buffers.md); use `entityInQueryIndex`instead.|
|`EntityCommands commands`| You can name this parameter anything as long as the type is `EntityCommands`. Use this parameter only in conjunction with either `WithDeferredPlaybackSystem<T>` or `WithImmediatePlayback`. The `EntityCommands` type has several methods that mirror their counterparts in the `EntityCommandBuffer` type. If you use an `EntityCommands` instance inside `Entities.ForEach`, the compiler creates extra code where appropriate to handle the creation, scheduling, playback, and disposal of entity command buffers, on which counterparts to `EntityCommands` methods are invoked.|

## Execute an Entities.ForEach lambda expression
You can execute a job lambda expression in the following ways:

* Use `Schedule` and `ScheduleParallel` to schedule the job
* Use `Run` to execute the job immediately on the main thread. 

The following example illustrates a `SystemBase` implementation that uses `Entities.ForEach` to read the `Velocity` component and write to the `ObjectPosition` component:

[!code-cs[entities-foreach-example](../DocCodeSamples.Tests/LambdaJobExamples.cs#entities-foreach-example)]

## Capture variables

You can capture local variables for the `Entities.ForEach` lambda expression. When you call one of the `Schedule` methods instead of `Run` to use a job to execute the lambda expression, there are some restrictions on the captured variables and how you use them:

* You can only capture [native containers](https://docs.unity3d.com/Manual/JobSystemNativeContainer.html) and blittable types.
* A job can only write to captured variables that are native containers. To return a single value, create a native array with one element.

If you read a native container, but don't write to it, always use `WithReadOnly(variable)` to specify read-only access. For more information about setting attributes for captured variables, see [`SystemBase.Entities`](xref:Unity.Entities.SystemBase.Entities). `Entities.ForEach` provides these as methods because the C# language doesn't allow attributes on local variables.

To dispose of captured native containers or types that contain native containers after `Entities.ForEach` runs, use `WithDisposeOnCompletion(variable)`. If you call this in `Run`, this disposes of the types immediately after the lambda expression runs. If you call this in `Schedule` and`ScheduleParallel`, it schedules them to be disposed of later with a job, and returns the JobHandle.

> [!NOTE]
> When you execute the method with `Run` you can write to captured variables that aren't native containers. However, you should still use blittable types where possible so that the method can be compiled with [Burst](https://docs.unity3d.com/Packages/com.unity.burst@latest/index.html).