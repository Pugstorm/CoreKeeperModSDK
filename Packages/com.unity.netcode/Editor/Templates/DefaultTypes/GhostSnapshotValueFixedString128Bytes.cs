namespace Generated
{
    public struct GhostSnapshotData
    {
        struct Snapshot
        {
            #region __GHOST_FIELD__
            public FixedString128Bytes __GHOST_FIELD_NAME__;
            #endregion
        }

        public void Serialize(ref Snapshot snapshot, ref Snapshot baseline, ref DataStreamWriter writer, ref StreamCompressionModel compressionModel, uint changeMask)
        {
            #region __GHOST_WRITE__
            if ((changeMask & (1 << __GHOST_MASK_INDEX__)) != 0)
                writer.WritePackedFixedString128Delta(snapshot.__GHOST_FIELD_NAME__, baseline.__GHOST_FIELD_NAME__, compressionModel);
            #endregion
        }

        public void Deserialize(ref Snapshot snapshot, ref Snapshot baseline, ref DataStreamReader reader, ref StreamCompressionModel compressionModel, uint changeMask)
        {
            #region __GHOST_READ__
            if ((changeMask & (1 << __GHOST_MASK_INDEX__)) != 0)
                snapshot.__GHOST_FIELD_NAME__ = reader.ReadPackedFixedString128Delta(baseline.__GHOST_FIELD_NAME__, compressionModel);
            else
                snapshot.__GHOST_FIELD_NAME__ = baseline.__GHOST_FIELD_NAME__;
            #endregion
        }

        public void SerializeCommand(ref DataStreamWriter writer, in IComponentData data, in IComponentData baseline, StreamCompressionModel compressionModel)
        {
            #region __COMMAND_WRITE__
            writer.WriteFixedString128(data.__COMMAND_FIELD_NAME__);
            #endregion
            #region __COMMAND_WRITE_PACKED__
            writer.WritePackedFixedString128Delta(data.__COMMAND_FIELD_NAME__, baseline.__COMMAND_FIELD_NAME__, compressionModel);
            #endregion
        }

        public void DeserializeCommand(ref DataStreamReader reader, ref IComponentData data, in IComponentData baseline, StreamCompressionModel compressionModel)
        {
            #region __COMMAND_READ__
            data.__COMMAND_FIELD_NAME__ = reader.ReadFixedString128();
            #endregion
            #region __COMMAND_READ_PACKED__
            data.__COMMAND_FIELD_NAME__ = reader.ReadPackedFixedString128Delta(baseline.__COMMAND_FIELD_NAME__, compressionModel);
            #endregion
        }
    }
}
