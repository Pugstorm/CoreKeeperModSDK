using System;
using Microsoft.CodeAnalysis;
using Unity.Entities.SourceGen.Common;

namespace Unity.Entities.SourceGen.Aspect
{
    // TODO: there is a list of field name that are reserved by our generator.
    // these field name must yield a clear error:
    // "Lookup"
    // "ResolvedChunk"
    // "TypeHandle"
    // "Enumerator"
    // A field of the same name as the aspect.

    public static class AspectErrors
    {
        public static void SGAICE0000(string aspectName, Location location, Exception exception)
        {
            Service<IDiagnosticLogger>.Instance?.LogError("SGAICE0000", "Aspect Generator Exception", $"Exception while generating aspect '{aspectName}': {exception}\n{exception.StackTrace}", location);
        }
        public static void SGA0001(Location location, string componentType, Location conflictingFieldLocation)
        {
            Service<IDiagnosticLogger>.Instance?.LogError("SGA0001", "Component Type Duplicate", $"A field of type RefRO<{componentType}>/RefRW<{componentType}> is already defined at {conflictingFieldLocation}. " +
                "An aspect struct must not contain more than one field of the same component type.", location);
        }
        public static void SGA0002(Location location, string structName)
        {
            Service<IDiagnosticLogger>.Instance?.LogError("SGA0002", "IAspect<Self>", $"Aspect struct must implement IAspect<{structName}>", location);
        }
        public static void SGA0004(Location location)
        {
            Service<IDiagnosticLogger>.Instance?.LogError("SGA0004", "Empty Aspect", $"An aspect struct must contain at least 1 field of type RefRO<ComponentType>/RefRW<ComponentType> or embed another aspect.", location);
        }
        public static void SGA0005(Location location)
        {
            Service<IDiagnosticLogger>.Instance?.LogError("SGA0005", "Aspect not marked 'readonly'", $"Aspect struct declarations and all containing fields therein must be marked readonly. Please add the readonly keyword before these declarations.", location);
        }
        public static void SGA0006(Location location)
        {
            Service<IDiagnosticLogger>.Instance?.LogError("SGA0006", "Aspect Entity Field Duplicate", $"Aspects cannot contain more than one instance field of type Unity.Entities.Entity", location);
        }
        public static void SGA0007(Location location)
        {
            Service<IDiagnosticLogger>.Instance?.LogError(
                "SGA0007",
                "Aspect Data Field",
                "Aspects cannot contain instance fields of type other than RefRW<IComponentData>, RefRO<IComponentData>, EnabledRefRW<IComponentData>, EnabledRefRO<IComponentData>, DynamicBuffer<T>, or Entity.",
                location);
        }
        public static void SGA0009(Location location)
        {
            Service<IDiagnosticLogger>.Instance?.LogError(nameof(SGA0009), "Generic aspects are not supported.", "Generic aspects are not supported.", location);
        }

        public static void SGA0011(Location location)
        {
            Service<IDiagnosticLogger>.Instance?.LogError(
                nameof(SGA0011),
                title: "Read-only RefRW<IComponentData>",
                errorMessage: "You may not use Unity.Collections.ReadOnlyAttribute on RefRW<IComponentData> fields. If you want read-only access to an IComponentData type, please use RefRO<IComponentData> instead.",
                location);
        }
    }
}
