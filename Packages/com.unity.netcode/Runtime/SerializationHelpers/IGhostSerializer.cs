using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.NetCode.LowLevel.Unsafe;
using UnityEngine.Scripting;

namespace Unity.NetCode
{
    /// <summary>
    /// Interface that expose a raw, unsafe interface to copy all the component ghost fields to
    /// the snapshot buffer. It is mostly for internal use by code-gen and should not be used direcly nor implemented
    /// by user code.
    /// </summary>
    public interface IGhostSerializer
    {
        /// <summary>
        /// The number of bits necessary for change mask
        /// </summary>
        public int ChangeMaskSizeInBits { get; }

        /// <summary>
        /// True if the serialized component has some serialized fields.
        /// </summary>
        public bool HasGhostFields { get; }

        /// <summary>
        /// The size of the serialized data in the snapshot buffer.
        /// </summary>
        public int SizeInSnapshot { get; }

        /// <summary>
        /// Copy/Convert the component data to the snapshot.
        /// </summary>
        /// <param name="serializerState"></param>
        /// <param name="snapshot"></param>
        /// <param name="component"></param>
        void CopyToSnapshot(in GhostSerializerState serializerState, [NoAlias]IntPtr snapshot, [ReadOnly][NoAlias]IntPtr component);

        /// <summary>
        /// Copy/Convert the snapshot to component. Perform interpolation if necessary.
        /// </summary>
        /// <param name="serializerState"></param>
        /// <param name="component"></param>
        /// <param name="snapshotInterpolationFactor"></param>
        /// <param name="snapshotInterpolationFactorRaw"></param>
        /// <param name="snapshotBefore"></param>
        /// <param name="snapshotAfter"></param>
        public void CopyFromSnapshot(in GhostDeserializerState serializerState, [NoAlias] IntPtr component,
            float snapshotInterpolationFactor, float snapshotInterpolationFactorRaw,
            [NoAlias] [ReadOnly] IntPtr snapshotBefore, [NoAlias] [ReadOnly] IntPtr snapshotAfter);

        /// <summary>
        /// Compute the change mask for the snapshot in respect to the given baseline
        /// </summary>
        /// <param name="snapshot"></param>
        /// <param name="baseline"></param>
        /// <param name="changeMaskData"></param>
        /// <param name="startOffset"></param>
        void CalculateChangeMask([NoAlias][ReadOnly]IntPtr snapshot, [NoAlias][ReadOnly]IntPtr baseline, [NoAlias]IntPtr changeMaskData, int startOffset);

        /// <summary>
        /// Serialise the snapshot data to the <param name="writer"></param> and calculate the current changemask.
        /// </summary>
        /// <param name="snapshot"></param>
        /// <param name="baseline"></param>
        /// <param name="changeMaskData"></param>
        /// <param name="startOffset"></param>
        /// <param name="writer"></param>
        /// <param name="compressionModel"></param>
        void SerializeCombined([ReadOnly][NoAlias] IntPtr snapshot, [ReadOnly][NoAlias] IntPtr baseline,
            [NoAlias][ReadOnly]IntPtr changeMaskData, int startOffset,
            ref DataStreamWriter writer, in StreamCompressionModel compressionModel);

        /// <summary>
        /// Serialise the snapshot dato to the <param name="writer"></param> and calculate the current changemask.
        /// </summary>
        /// <param name="snapshot"></param>
        /// <param name="baseline0"></param>
        /// <param name="baseline1"></param>
        /// <param name="baseline2"></param>
        /// <param name="predictor"></param>
        /// <param name="changeMaskData"></param>
        /// <param name="startOffset"></param>
        /// <param name="writer"></param>
        /// <param name="compressionModel"></param>
        void SerializeWithPredictedBaseline([ReadOnly] [NoAlias] IntPtr snapshot,
            [ReadOnly] [NoAlias] IntPtr baseline0,
            [ReadOnly] [NoAlias] IntPtr baseline1,
            [ReadOnly] [NoAlias] IntPtr baseline2,
            ref GhostDeltaPredictor predictor,
            [NoAlias] [ReadOnly] IntPtr changeMaskData, int startOffset,
            ref DataStreamWriter writer, in StreamCompressionModel compressionModel);

        /// <summary>
        /// Serialise the snapshot dato to the <param name="writer"></param> based on the calculated changemask.
        /// Expecte the changemask bits be all already set.
        /// </summary>
        /// <param name="snapshot"></param>
        /// <param name="baseline"></param>
        /// <param name="changeMaskData"></param>
        /// <param name="startOffset"></param>
        /// <param name="writer"></param>
        /// <param name="compressionModel"></param>
        void Serialize([ReadOnly][NoAlias] IntPtr snapshot, [ReadOnly][NoAlias] IntPtr baseline,
            [NoAlias][ReadOnly]IntPtr changeMaskData, int startOffset,
            ref DataStreamWriter writer, in StreamCompressionModel compressionModel);

        /// <summary>
        /// Calculate the predicted snapshot from the two baseline
        /// </summary>
        /// <param name="snapshotData"></param>
        /// <param name="baseline1Data"></param>
        /// <param name="baseline2Data"></param>
        /// <param name="predictor"></param>
        void PredictDelta([NoAlias] IntPtr snapshotData, [NoAlias] IntPtr baseline1Data, [NoAlias] IntPtr baseline2Data, ref GhostDeltaPredictor predictor);

        /// <summary>
        /// Read the data from the <param name="reader"></param> stream into the snapshot data.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="compressionModel"></param>
        /// <param name="changeMask"></param>
        /// <param name="startOffset"></param>
        /// <param name="snapshot"></param>
        /// <param name="baseline"></param>
        void Deserialize(ref DataStreamReader reader, in StreamCompressionModel compressionModel,
            IntPtr changeMask,
            int startOffset, [NoAlias]IntPtr snapshot, [NoAlias][ReadOnly]IntPtr baseline);

        /// <summary>
        /// Restore the component data from the prediction backup buffer. Only serialised fields are restored.
        /// </summary>
        /// <param name="component"></param>
        /// <param name="backup"></param>
        void RestoreFromBackup([NoAlias]IntPtr component, [NoAlias][ReadOnly]IntPtr backup);

#if UNITY_EDITOR || NETCODE_DEBUG
        /// <summary>
        /// Calculate the prediction error for this component.
        /// </summary>
        /// <param name="component"></param>
        /// <param name="backup"></param>
        /// <param name="errorsList"></param>
        /// <param name="errorsCount"></param>
        void ReportPredictionErrors([NoAlias][ReadOnly]IntPtr component, [NoAlias][ReadOnly]IntPtr backup, IntPtr errorsList,
            int errorsCount);
#endif
    }

    /// <summary>
    /// Interface implemented by all the component/buffer serialiser. For internal use only.
    /// </summary>
    /// <typeparam name="TSnapshot">The snapshot struct type that will contains the component data.</typeparam>
    /// <typeparam name="TComponent">The component type that this interface serialize.</typeparam>
    [RequireImplementors]
    [Obsolete("The IGhostSerializer<TComponent, TSnapshot> has been deprecated. Please use the IGhostComponentSerializer instead")]
    public interface IGhostSerializer<TComponent, TSnapshot>
        where TSnapshot: unmanaged
        where TComponent: unmanaged
    {
        /// <summary>
        /// Calculate the predicted baseline.
        /// </summary>
        /// <param name="snapshot"></param>
        /// <param name="baseline1"></param>
        /// <param name="baseline2"></param>
        /// <param name="predictor"></param>
        void PredictDeltaGenerated(ref TSnapshot snapshot, in TSnapshot baseline1, in TSnapshot baseline2, ref GhostDeltaPredictor predictor);

        /// <summary>
        /// Compute the change mask for the snapshot in respect to the given baseline
        /// </summary>
        /// <param name="snapshot"></param>
        /// <param name="baseline"></param>
        /// <param name="changeMaskData"></param>
        /// <param name="startOffset"></param>
        void CalculateChangeMaskGenerated(in TSnapshot snapshot, in TSnapshot baseline, IntPtr changeMaskData, int startOffset){}

        /// <summary>
        /// Copy/Convert the data form the snapshot to the component. Support interpolation and extrapolation.
        /// </summary>
        /// <param name="serializerState"></param>
        /// <param name="component"></param>
        /// <param name="interpolationFactor"></param>
        /// <param name="snapshotInterpolationFactorRaw"></param>
        /// <param name="snapshotBefore"></param>
        /// <param name="snapshotAfter"></param>
        void CopyFromSnapshotGenerated(in GhostDeserializerState serializerState, ref TComponent component,
            float interpolationFactor, float snapshotInterpolationFactorRaw, in TSnapshot snapshotBefore,
            in TSnapshot snapshotAfter);

        /// <summary>
        /// Copy/Convert the component data to the snapshot.
        /// </summary>
        /// <param name="serializerState"></param>
        /// <param name="snapshot"></param>
        /// <param name="component"></param>
        void CopyToSnapshotGenerated(in GhostSerializerState serializerState, ref TSnapshot snapshot,
            in TComponent component);

        /// <summary>
        /// Serialise the snapshot dato to the <param name="writer"></param> based on the calculated changemask.
        /// </summary>
        /// <param name="snapshot"></param>
        /// <param name="baseline"></param>
        /// <param name="changeMaskData"></param>
        /// <param name="startOffset"></param>
        /// <param name="writer"></param>
        /// <param name="compressionModel"></param>
        void SerializeGenerated(in TSnapshot snapshot, in TSnapshot baseline,
            [ReadOnly][NoAlias]IntPtr changeMaskData, int startOffset, ref DataStreamWriter writer,
            in StreamCompressionModel compressionModel);

        /// <summary>
        /// Serialise the snapshot dato to the <param name="writer"></param> based on the calculated changemask.
        /// </summary>
        /// <param name="snapshot"></param>
        /// <param name="baseline"></param>
        /// <param name="changeMaskData"></param>
        /// <param name="startOffset"></param>
        /// <param name="writer"></param>
        /// <param name="compressionModel"></param>
        void SerializeCombinedGenerated(in TSnapshot snapshot, in TSnapshot baseline,
            [NoAlias][ReadOnly]IntPtr changeMaskData, int startOffset,
            ref DataStreamWriter writer, in StreamCompressionModel compressionModel);

        /// <summary>
        /// Read the data from the <param name="reader"></param> stream into the snapshot data.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="compressionModel"></param>
        /// <param name="changeMask"></param>
        /// <param name="startOffset"></param>
        /// <param name="snapshot"></param>
        /// <param name="baseline"></param>
        void DeserializeGenerated(ref DataStreamReader reader, in StreamCompressionModel compressionModel,
            IntPtr changeMask,
            int startOffset, ref TSnapshot snapshot, in TSnapshot baseline);

        /// <summary>
        /// Restore the component data from the prediction backup buffer. Only serialised fields are restored.
        /// </summary>
        /// <param name="component"></param>
        /// <param name="backup"></param>
        void RestoreFromBackupGenerated(ref TComponent component, in TComponent backup);

#if UNITY_EDITOR || NETCODE_DEBUG
        /// <summary>
        /// Calculate the prediction error for this component.
        /// </summary>
        /// <param name="component"></param>
        /// <param name="backup"></param>
        /// <param name="errorsList"></param>
        /// <param name="errorsCount"></param>
        void ReportPredictionErrorsGenerated(in TComponent component, in TComponent backup, IntPtr errorsList,
            int errorsCount);
#endif
    }
}
