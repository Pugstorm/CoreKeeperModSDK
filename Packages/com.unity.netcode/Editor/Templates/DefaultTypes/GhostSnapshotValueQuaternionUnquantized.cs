#region __GHOST_IMPORTS__
#endregion
namespace Generated
{
    public struct GhostSnapshotData
    {
        public struct Snapshot
        {
            #region __GHOST_FIELD__
            public float __GHOST_FIELD_NAME__X;
            public float __GHOST_FIELD_NAME__Y;
            public float __GHOST_FIELD_NAME__Z;
            public float __GHOST_FIELD_NAME__W;
            #endregion
        }

        public void PredictDelta(uint tick, ref GhostSnapshotData baseline1, ref GhostSnapshotData baseline2)
        {
            var predictor = new GhostDeltaPredictor(tick, this.tick, baseline1.tick, baseline2.tick);
            #region __GHOST_PREDICT__
            #endregion
        }

        public void SerializeCommand(ref DataStreamWriter writer, in IComponentData data, in IComponentData baseline, StreamCompressionModel compressionModel)
        {
            #region __COMMAND_WRITE__
            writer.WriteFloat(data.__COMMAND_FIELD_NAME__.value.x);
            writer.WriteFloat(data.__COMMAND_FIELD_NAME__.value.y);
            writer.WriteFloat(data.__COMMAND_FIELD_NAME__.value.z);
            writer.WriteFloat(data.__COMMAND_FIELD_NAME__.value.w);
            #endregion
            #region __COMMAND_WRITE_PACKED__
            writer.WritePackedFloatDelta(data.__COMMAND_FIELD_NAME__.value.x, baseline.__COMMAND_FIELD_NAME__.value.x, compressionModel);
            writer.WritePackedFloatDelta(data.__COMMAND_FIELD_NAME__.value.y, baseline.__COMMAND_FIELD_NAME__.value.y, compressionModel);
            writer.WritePackedFloatDelta(data.__COMMAND_FIELD_NAME__.value.z, baseline.__COMMAND_FIELD_NAME__.value.z, compressionModel);
            writer.WritePackedFloatDelta(data.__COMMAND_FIELD_NAME__.value.w, baseline.__COMMAND_FIELD_NAME__.value.w, compressionModel);
            #endregion
        }

        public void DeserializeCommand(ref DataStreamReader reader, ref IComponentData data, in IComponentData baseline, StreamCompressionModel compressionModel)
        {
            #region __COMMAND_READ__
            data.__COMMAND_FIELD_NAME__.value.x = reader.ReadFloat();
            data.__COMMAND_FIELD_NAME__.value.y = reader.ReadFloat();
            data.__COMMAND_FIELD_NAME__.value.z = reader.ReadFloat();
            data.__COMMAND_FIELD_NAME__.value.w = reader.ReadFloat();
            #endregion
            #region __COMMAND_READ_PACKED__
            data.__COMMAND_FIELD_NAME__.value.x = reader.ReadPackedFloatDelta(baseline.__COMMAND_FIELD_NAME__.value.x, compressionModel);
            data.__COMMAND_FIELD_NAME__.value.y = reader.ReadPackedFloatDelta(baseline.__COMMAND_FIELD_NAME__.value.y, compressionModel);
            data.__COMMAND_FIELD_NAME__.value.z = reader.ReadPackedFloatDelta(baseline.__COMMAND_FIELD_NAME__.value.z, compressionModel);
            data.__COMMAND_FIELD_NAME__.value.w = reader.ReadPackedFloatDelta(baseline.__COMMAND_FIELD_NAME__.value.w, compressionModel);
            #endregion
        }
        public void Serialize(ref Snapshot snapshot, ref Snapshot baseline, ref DataStreamWriter writer, ref StreamCompressionModel compressionModel, uint changeMask)
        {
            #region __GHOST_WRITE__
            if ((changeMask & (1 << __GHOST_MASK_INDEX__)) != 0)
            {
                writer.WritePackedFloatDelta(snapshot.__GHOST_FIELD_NAME__X, baseline.__GHOST_FIELD_NAME__X, compressionModel);
                writer.WritePackedFloatDelta(snapshot.__GHOST_FIELD_NAME__Y, baseline.__GHOST_FIELD_NAME__Y, compressionModel);
                writer.WritePackedFloatDelta(snapshot.__GHOST_FIELD_NAME__Z, baseline.__GHOST_FIELD_NAME__Z, compressionModel);
                writer.WritePackedFloatDelta(snapshot.__GHOST_FIELD_NAME__W, baseline.__GHOST_FIELD_NAME__W, compressionModel);
            }
            #endregion
        }

        public void Deserialize(ref Snapshot snapshot, ref Snapshot baseline, ref DataStreamReader reader, ref StreamCompressionModel compressionModel, uint changeMask)
        {
            #region __GHOST_READ__
            if ((changeMask & (1 << __GHOST_MASK_INDEX__)) != 0)
            {
                snapshot.__GHOST_FIELD_NAME__X = reader.ReadPackedFloatDelta(baseline.__GHOST_FIELD_NAME__X, compressionModel);
                snapshot.__GHOST_FIELD_NAME__Y = reader.ReadPackedFloatDelta(baseline.__GHOST_FIELD_NAME__Y, compressionModel);
                snapshot.__GHOST_FIELD_NAME__Z = reader.ReadPackedFloatDelta(baseline.__GHOST_FIELD_NAME__Z, compressionModel);
                snapshot.__GHOST_FIELD_NAME__W = reader.ReadPackedFloatDelta(baseline.__GHOST_FIELD_NAME__W, compressionModel);
            }
            else
            {
                snapshot.__GHOST_FIELD_NAME__X = baseline.__GHOST_FIELD_NAME__X;
                snapshot.__GHOST_FIELD_NAME__Y = baseline.__GHOST_FIELD_NAME__Y;
                snapshot.__GHOST_FIELD_NAME__Z = baseline.__GHOST_FIELD_NAME__Z;
                snapshot.__GHOST_FIELD_NAME__W = baseline.__GHOST_FIELD_NAME__W;
            }
            #endregion
        }
        public unsafe void CopyToSnapshot(ref Snapshot snapshot, ref IComponentData component)
        {
            if (true)
            {
                #region __GHOST_COPY_TO_SNAPSHOT__
                snapshot.__GHOST_FIELD_NAME__X = component.__GHOST_FIELD_REFERENCE__.value.x;
                snapshot.__GHOST_FIELD_NAME__Y = component.__GHOST_FIELD_REFERENCE__.value.y;
                snapshot.__GHOST_FIELD_NAME__Z = component.__GHOST_FIELD_REFERENCE__.value.z;
                snapshot.__GHOST_FIELD_NAME__W = component.__GHOST_FIELD_REFERENCE__.value.w;
                #endregion
            }
        }
        public unsafe void CopyFromSnapshot(ref GhostDeserializerState deserializerState, ref Snapshot snapshotBefore, ref Snapshot snapshotAfter, float snapshotInterpolationFactor, ref IComponentData component)
        {
            if (true)
            {
                #region __GHOST_COPY_FROM_SNAPSHOT__
                component.__GHOST_FIELD_REFERENCE__ = new quaternion(snapshotBefore.__GHOST_FIELD_NAME__X, snapshotBefore.__GHOST_FIELD_NAME__Y, snapshotBefore.__GHOST_FIELD_NAME__Z, snapshotBefore.__GHOST_FIELD_NAME__W);
                #endregion

                #region __GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE_SETUP__
                var __GHOST_FIELD_NAME___Before = new quaternion(snapshotBefore.__GHOST_FIELD_NAME__X, snapshotBefore.__GHOST_FIELD_NAME__Y, snapshotBefore.__GHOST_FIELD_NAME__Z, snapshotBefore.__GHOST_FIELD_NAME__W);
                var __GHOST_FIELD_NAME___After = new quaternion(snapshotAfter.__GHOST_FIELD_NAME__X, snapshotAfter.__GHOST_FIELD_NAME__Y, snapshotAfter.__GHOST_FIELD_NAME__Z, snapshotAfter.__GHOST_FIELD_NAME__W);
                #endregion
                #region __GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE_DISTSQ__
                var __GHOST_FIELD_NAME___DistSq = math.dot(__GHOST_FIELD_NAME___Before, __GHOST_FIELD_NAME___After);
                __GHOST_FIELD_NAME___DistSq = 1 - __GHOST_FIELD_NAME___DistSq*__GHOST_FIELD_NAME___DistSq;
                #endregion
                #region __GHOST_COPY_FROM_SNAPSHOT_INTERPOLATE__
                component.__GHOST_FIELD_REFERENCE__ = math.slerp(__GHOST_FIELD_NAME___Before,
                    __GHOST_FIELD_NAME___After, snapshotInterpolationFactor);
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
			#region __GHOST_CALCULATE_INPUT_CHANGE_MASK__
			changeMask |= (snapshot.__COMMAND_FIELD_NAME__.value.x != baseline.__COMMAND_FIELD_NAME__.value.x ||
                        snapshot.__COMMAND_FIELD_NAME__.value.y != baseline.__COMMAND_FIELD_NAME__.value.y||
                        snapshot.__COMMAND_FIELD_NAME__.value.z != baseline.__COMMAND_FIELD_NAME__.value.z ||
                        snapshot.__COMMAND_FIELD_NAME__.value.w != baseline.__COMMAND_FIELD_NAME__.value.w) ? 1u : 0;
			#endregion
            #region __GHOST_CALCULATE_CHANGE_MASK_ZERO__
            changeMask = (snapshot.__GHOST_FIELD_NAME__X != baseline.__GHOST_FIELD_NAME__X ||
                        snapshot.__GHOST_FIELD_NAME__Y != baseline.__GHOST_FIELD_NAME__Y ||
                        snapshot.__GHOST_FIELD_NAME__Z != baseline.__GHOST_FIELD_NAME__Z ||
                        snapshot.__GHOST_FIELD_NAME__W != baseline.__GHOST_FIELD_NAME__W) ? 1u : 0;
            #endregion
            #region __GHOST_CALCULATE_CHANGE_MASK__
            changeMask |= (snapshot.__GHOST_FIELD_NAME__X != baseline.__GHOST_FIELD_NAME__X ||
                        snapshot.__GHOST_FIELD_NAME__Y != baseline.__GHOST_FIELD_NAME__Y ||
                        snapshot.__GHOST_FIELD_NAME__Z != baseline.__GHOST_FIELD_NAME__Z ||
                        snapshot.__GHOST_FIELD_NAME__W != baseline.__GHOST_FIELD_NAME__W) ? (1u<<__GHOST_MASK_INDEX__) : 0;
            #endregion
        }
        #if UNITY_EDITOR || NETCODE_DEBUG
        private static void ReportPredictionErrors(ref IComponentData component, in IComponentData backup, ref UnsafeList<float> errors, ref int errorIndex)
        {
            #region __GHOST_REPORT_PREDICTION_ERROR__
            errors[errorIndex] = math.max(errors[errorIndex], math.distance(component.__GHOST_FIELD_REFERENCE__.value, backup.__GHOST_FIELD_REFERENCE__.value));
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
