#region __GHOST_IMPORTS__
#endregion
namespace Generated
{
    public struct GhostSnapshotData
    {
        struct Snapshot
        {
            #region __GHOST_FIELD__
            public double __GHOST_FIELD_NAME__;
            #endregion
        }

        public void PredictDelta(uint tick, ref GhostSnapshotData baseline1, ref GhostSnapshotData baseline2)
        {
            #region __GHOST_PREDICT__
            #endregion
        }

        public void Serialize(int networkId, ref GhostSnapshotData baseline, ref DataStreamWriter writer, StreamCompressionModel compressionModel)
        {
            #region __GHOST_WRITE__
            if ((changeMask & (1 << __GHOST_MASK_INDEX__)) != 0)
                writer.WritePackedDoubleDelta(snapshot.__GHOST_FIELD_NAME__, baseline.__GHOST_FIELD_NAME__, compressionModel);
            #endregion
        }

        public void Deserialize(uint tick, ref GhostSnapshotData baseline, ref DataStreamReader reader,
            StreamCompressionModel compressionModel)
        {
            #region __GHOST_READ__
            if ((changeMask & (1 << __GHOST_MASK_INDEX__)) != 0)
                snapshot.__GHOST_FIELD_NAME__ = reader.ReadPackedDoubleDelta(baseline.__GHOST_FIELD_NAME__, compressionModel);
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
        public unsafe void CopyFromSnapshot(ref Snapshot snapshot, ref IComponentData component)
        {
            if (true)
            {
                #region __GHOST_COPY_FROM_SNAPSHOT__
                component.__GHOST_FIELD_REFERENCE__ = snapshotBefore.__GHOST_FIELD_NAME__;
                #endregion

                #region __GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE_SETUP__
                var __GHOST_FIELD_NAME___Before = snapshotBefore.__GHOST_FIELD_NAME__;
                var __GHOST_FIELD_NAME___After = snapshotAfter.__GHOST_FIELD_NAME__;
                #endregion
                #region __GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE_DISTSQ__
                var __GHOST_FIELD_NAME___DistSq = math.distancesq(__GHOST_FIELD_NAME___Before, __GHOST_FIELD_NAME___After);
                #endregion
                #region __GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE__
                component.__GHOST_FIELD_REFERENCE__ = math.lerp(__GHOST_FIELD_NAME___Before, __GHOST_FIELD_NAME___After, snapshotInterpolationFactor);
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
            changeMask = (snapshot.__GHOST_FIELD_NAME__ != baseline.__GHOST_FIELD_NAME__) ? 1u : 0;
            #endregion
            #region __GHOST_CALCULATE_CHANGE_MASK__
            changeMask |= (snapshot.__GHOST_FIELD_NAME__ != baseline.__GHOST_FIELD_NAME__) ? (1u<<__GHOST_MASK_INDEX__) : 0;
            #endregion
        }

        public void SerializeCommand(ref DataStreamWriter writer, in IComponentData data, in IComponentData baseline, StreamCompressionModel compressionModel)
        {
            #region __COMMAND_WRITE__
            writer.WriteDouble(data.__COMMAND_FIELD_NAME__);
            #endregion
            #region __COMMAND_WRITE_PACKED__
            writer.WritePackedDoubleDelta(data.__COMMAND_FIELD_NAME__, baseline.__COMMAND_FIELD_NAME__, compressionModel);
            #endregion
        }

        public void DeserializeCommand(ref DataStreamReader reader, ref IComponentData data, in IComponentData baseline, StreamCompressionModel compressionModel)
        {
            #region __COMMAND_READ__
            data.__COMMAND_FIELD_NAME__ = (__COMMAND_FIELD_TYPE_NAME__) reader.ReadDouble();
            #endregion
            #region __COMMAND_READ_PACKED__
            data.__COMMAND_FIELD_NAME__ = (__COMMAND_FIELD_TYPE_NAME__) reader.ReadPackedDoubleDelta(baseline.__COMMAND_FIELD_NAME__, compressionModel);
            #endregion
        }

        #if UNITY_EDITOR || NETCODE_DEBUG
        private static void ReportPredictionErrors(ref IComponentData component, in IComponentData backup, ref UnsafeList<float> errors, ref int errorIndex)
        {
            #region __GHOST_REPORT_PREDICTION_ERROR__
            errors[errorIndex] = math.max(errors[errorIndex], (float)math.abs(component.__GHOST_FIELD_REFERENCE__ - backup.__GHOST_FIELD_REFERENCE__));
            ++errorIndex;
            #endregion
        }
        private static int GetPredictionErrorNames(ref FixedString512Bytes names, ref int nameCount)
        {
            #region __GHOST_GET_PREDICTION_ERROR_NAME__
            if (nameCount != 0)
                names.Append(new FixedString32Bytes(","));
            names.Append((FixedString512Bytes)"__GHOST_FIELD_REFERENCE__");
            ++nameCount;
            #endregion
        }
        #endif
    }
}
