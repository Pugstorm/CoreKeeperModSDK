using System.Runtime.InteropServices;

namespace Unity.Networking.Transport.Relay
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RelayMessageAccepted
    {
        public const int k_Length = RelayMessageHeader.k_Length + RelayAllocationId.k_Length * 2; // Header + FromAllocationId + ToAllocationId

        public RelayMessageHeader Header;

        public RelayAllocationId FromAllocationId;
        public RelayAllocationId ToAllocationId;
    }
}
