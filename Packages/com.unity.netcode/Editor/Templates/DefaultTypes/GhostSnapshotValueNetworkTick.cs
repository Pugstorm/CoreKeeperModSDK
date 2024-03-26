#region __GHOST_IMPORTS__
#endregion
namespace Generated
{
    public struct GhostSnapshotData
    {
        public unsafe void CopyToSnapshot(ref Snapshot snapshot, ref IComponentData component)
        {
            if (true)
            {
                #region __GHOST_COPY_TO_SNAPSHOT__
                snapshot.__GHOST_FIELD_NAME__ = component.__GHOST_FIELD_REFERENCE__.SerializedData;
                #endregion
            }
        }
        public unsafe void CopyFromSnapshot(ref Snapshot snapshot, ref IComponentData component)
        {
            if (true)
            {
                #region __GHOST_COPY_FROM_SNAPSHOT__
                component.__GHOST_FIELD_REFERENCE__ = new NetworkTick{SerializedData = snapshotBefore.__GHOST_FIELD_NAME__};
                #endregion
            }
        }
        public void SerializeCommand(ref DataStreamWriter writer, in IComponentData data, in IComponentData baseline, StreamCompressionModel compressionModel)
        {
            #region __COMMAND_WRITE__
            writer.WriteUInt((uint)data.__COMMAND_FIELD_NAME__.SerializedData);
            #endregion
            #region __COMMAND_WRITE_PACKED__
            writer.WritePackedUIntDelta((uint)data.__COMMAND_FIELD_NAME__.SerializedData, (uint)baseline.__COMMAND_FIELD_NAME__.SerializedData, compressionModel);
            #endregion
        }

        public void DeserializeCommand(ref DataStreamReader reader, ref IComponentData data, in IComponentData baseline, StreamCompressionModel compressionModel)
        {
            #region __COMMAND_READ__
            data.__COMMAND_FIELD_NAME__ = new NetworkTick{SerializedData = reader.ReadUInt()};
            #endregion
            #region __COMMAND_READ_PACKED__
            data.__COMMAND_FIELD_NAME__ = new NetworkTick{SerializedData = reader.ReadPackedUIntDelta(baseline.__COMMAND_FIELD_NAME__.SerializedData, compressionModel)};
            #endregion
        }
        #if UNITY_EDITOR || NETCODE_DEBUG
        private static void ReportPredictionErrors(ref IComponentData component, in IComponentData backup, ref UnsafeList<float> errors, ref int errorIndex)
        {
            #region __GHOST_REPORT_PREDICTION_ERROR__
            {
            int tickErr = 0;
            if (component.__GHOST_FIELD_REFERENCE__.IsValid != backup.__GHOST_FIELD_REFERENCE__.IsValid)
            {
                // TODO: what is a good value for this?
                tickErr = 100;
            }
            else if (component.__GHOST_FIELD_REFERENCE__.IsValid)
                tickErr = math.abs(component.__GHOST_FIELD_REFERENCE__.TicksSince(backup.__GHOST_FIELD_REFERENCE__));
            errors[errorIndex] = math.max(errors[errorIndex], tickErr);
            ++errorIndex;
            }
            #endregion
        }
        #endif
    }
}
