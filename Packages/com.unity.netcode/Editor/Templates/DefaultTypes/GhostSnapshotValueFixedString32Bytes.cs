#region __GHOST_IMPORTS__
#endregion
namespace Generated
{
    public struct GhostSnapshotData
    {
        struct Snapshot
        {
            #region __GHOST_FIELD__
            public FixedString32Bytes __GHOST_FIELD_NAME__;
            #endregion
        }

        public void PredictDelta(uint tick, ref GhostSnapshotData baseline1, ref GhostSnapshotData baseline2)
        {
            var predictor = new GhostDeltaPredictor(tick, this.tick, baseline1.tick, baseline2.tick);
            #region __GHOST_PREDICT__
            #endregion
        }

        public void Serialize(ref Snapshot snapshot, ref Snapshot baseline, ref DataStreamWriter writer, ref StreamCompressionModel compressionModel, uint changeMask)
        {
            #region __GHOST_WRITE__
            if ((changeMask & (1 << __GHOST_MASK_INDEX__)) != 0)
                writer.WritePackedFixedString32Delta(snapshot.__GHOST_FIELD_NAME__, baseline.__GHOST_FIELD_NAME__, compressionModel);
            #endregion
        }

        public void Deserialize(ref Snapshot snapshot, ref Snapshot baseline, ref DataStreamReader reader, ref StreamCompressionModel compressionModel, uint changeMask)
        {
            #region __GHOST_READ__
            if ((changeMask & (1 << __GHOST_MASK_INDEX__)) != 0)
                snapshot.__GHOST_FIELD_NAME__ = reader.ReadPackedFixedString32Delta(baseline.__GHOST_FIELD_NAME__, compressionModel);
            else
                snapshot.__GHOST_FIELD_NAME__ = baseline.__GHOST_FIELD_NAME__;
            #endregion
        }
        public unsafe void CopyToSnapshot(ref Snapshot snapshot, ref IComponentData component)
        {
            if (true)
            {
                #region __GHOST_COPY_TO_SNAPSHOT__
                snapshot.__GHOST_FIELD_NAME__ = component.__GHOST_FIELD_REFERENCE__;
                #endregion
            }
        }
        public unsafe void CopyFromSnapshot(ref GhostDeserializerState deserializerState, ref Snapshot snapshotBefore, ref Snapshot snapshotAfter, float snapshotInterpolationFactor, ref IComponentData component)
        {
            if (true)
            {
                #region __GHOST_COPY_FROM_SNAPSHOT__
                component.__GHOST_FIELD_REFERENCE__ = snapshotBefore.__GHOST_FIELD_NAME__;
                #endregion
            }
        }
        public unsafe void RestoreFromBackup(ref IComponentData component, in IComponentData backup)
        {
            #region __GHOST_RESTORE_FROM_BACKUP__
            component.__GHOST_FIELD_REFERENCE__ = backup.__GHOST_FIELD_REFERENCE__;
            #endregion
        }
        public void CalculateChangeMask(ref Snapshot snapshot, ref Snapshot baseline, uint changeMask)
        {
            #region __GHOST_CALCULATE_CHANGE_MASK_ZERO__
            changeMask = snapshot.__GHOST_FIELD_NAME__.Equals(baseline.__GHOST_FIELD_NAME__) ? 0 : 1u;
            #endregion
            #region __GHOST_CALCULATE_CHANGE_MASK__
            changeMask |= snapshot.__GHOST_FIELD_NAME__.Equals(baseline.__GHOST_FIELD_NAME__) ? 0 : (1u<<__GHOST_MASK_INDEX__);
            #endregion
        }

        public void SerializeCommand(ref DataStreamWriter writer, in IComponentData data, in IComponentData baseline, StreamCompressionModel compressionModel)
        {
            #region __COMMAND_WRITE__
            writer.WriteFixedString32(data.__COMMAND_FIELD_NAME__);
            #endregion
            #region __COMMAND_WRITE_PACKED__
            writer.WritePackedFixedString32Delta(data.__COMMAND_FIELD_NAME__, baseline.__COMMAND_FIELD_NAME__, compressionModel);
            #endregion
        }

        public void DeserializeCommand(ref DataStreamReader reader, ref IComponentData data, in IComponentData baseline, StreamCompressionModel compressionModel)
        {
            #region __COMMAND_READ__
            data.__COMMAND_FIELD_NAME__ = reader.ReadFixedString32();
            #endregion
            #region __COMMAND_READ_PACKED__
            data.__COMMAND_FIELD_NAME__ = reader.ReadPackedFixedString32Delta(baseline.__COMMAND_FIELD_NAME__, compressionModel);
            #endregion
        }
        #if UNITY_EDITOR || NETCODE_DEBUG
        private static void ReportPredictionErrors(ref IComponentData component, in IComponentData backup, ref UnsafeList<float> errors, ref int errorIndex)
        {
            #region __GHOST_REPORT_PREDICTION_ERROR__
            #endregion
        }
        private static int GetPredictionErrorNames(ref FixedString512Bytes names, ref int nameCount)
        {
            #region __GHOST_GET_PREDICTION_ERROR_NAME__
            #endregion
        }
        #endif
    }
}
