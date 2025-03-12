using System.Runtime.InteropServices;
using Unity.Networking.Transport.Logging;

namespace Unity.Networking.Transport.Relay
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct RelayMessageError
    {
        public const int k_Length = RelayMessageHeader.k_Length + RelayAllocationId.k_Length + sizeof(byte); // Header + AllocationId + ErrorCode

        public RelayMessageHeader Header;

        public RelayAllocationId AllocationId;
        public byte ErrorCode;

        public void LogError()
        {
            DebugLog.ErrorRelay(ErrorCode);
        }
    }
}
