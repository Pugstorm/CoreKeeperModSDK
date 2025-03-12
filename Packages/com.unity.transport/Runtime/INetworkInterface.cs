using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Networking.Transport
{
    /// <summary>Obsolete. Part of the old <c>INetworkInterface</c> API.</summary>
    [Obsolete("Use ReceiveJobArguments.ReceiveQueue instead", true)]
    public struct NetworkPacketReceiver
    {
        public IntPtr AllocateMemory(ref int dataLen)
            => throw new NotImplementedException();

        /// <summary>Obsolete. Part of the old <c>INetworkInterface</c> API.</summary>
        [Flags]
        public enum AppendPacketMode
        {
            None = 0,
            NoCopyNeeded = 1
        }

        public bool AppendPacket(IntPtr data, ref NetworkEndpoint address, int dataLen, AppendPacketMode mode = AppendPacketMode.None)
            => throw new NotImplementedException();

        public bool IsAddressUsed(NetworkEndpoint address)
            => throw new NotImplementedException();

        public long LastUpdateTime
            => throw new NotImplementedException();

        public int ReceiveErrorCode
            => throw new NotImplementedException();
    }

    [Flags]
    internal enum SendHandleFlags
    {
        AllocatedByDriver = 1 << 0
    }


    internal struct NetworkInterfaceSendHandle
    {
        public IntPtr data;
        public int capacity;
        public int size;
        public int id;
        public SendHandleFlags flags;
    }

    /// <summary>
    /// <para>
    /// Network interfaces are the lowest level of the Unity Transport library. They are responsible
    /// for sending and receiving packets directly to/from the network. Conceptually, they act like
    /// sockets. Users can provide their own network interfaces by implementing this interface and
    /// passing a new instance of it to <see cref="NetworkDriver.Create{N}(N)"/>.
    /// </para>
    /// <para>
    /// Note that network interfaces are expected to be unmanaged types compatible with Burst.
    /// However, it is possible to write them using managed types and code. Simply wrap them with
    /// <see cref="ManagedNetworkInterfaceExtensions.WrapToUnmanaged"/> before passing them to
    /// <see cref="NetworkDriver.Create{N}(N)"/>. This comes at a small performance cost, but allows
    /// writing network interfaces that interact with managed C# libraries.
    /// </para>
    /// </summary>
    public interface INetworkInterface : IDisposable
    {
        /// <summary>
        /// Gets the local endpoint that the interface will use to communicate on the network.
        /// This call only makes sense after <see cref="Bind"/> has already been called, and
        /// represents the endpoint the interface is actually bound to. This property serves the
        /// same purpose as <c>getsockname</c> in the BSD socket world.
        /// </summary>
        /// <value>Local endpoint.</value>
        NetworkEndpoint LocalEndpoint { get; }

        /// <summary>Initialize the network interface with the given settings.</summary>
        /// <param name="settings">Configuration settings provided to the driver.</param>
        /// <param name="packetPadding">
        /// Return value parameter for how much padding the interface adds to packets. Note that
        /// this parameter is only concerned about padding that would be added directly in the
        /// packets stored in the send and receive queues, not to padding that would be added by
        /// lower levels of the network stack (e.g. IP headers).
        /// </param>
        /// <returns>0 on success, a negative number on error.</returns>
        int Initialize(ref NetworkSettings settings, ref int packetPadding);

        /// <summary>
        /// Schedule a receive job. This job's responsibility is to read data from the network and
        /// enqueue it in <see cref="ReceiveJobArguments.ReceiveQueue"/>.
        /// </summary>
        /// <param name="arguments">Arguments to be passed to the receive job.</param>
        /// <param name="dep">Handle to any dependency the receive job has (use default if none).</param>
        /// <returns>Handle to the newly-schedule job.</returns>
        JobHandle ScheduleReceive(ref ReceiveJobArguments arguments, JobHandle dep);

        /// <summary>
        /// Schedule a send job. This job's responsibility is to flush any data stored in
        /// <see cref="SendJobArguments.SendQueue"/> to the network.
        /// </summary>
        /// <param name="arguments">Arguments to be passed to the send job.</param>
        /// <param name="dep">Handle to any dependency the send job has (use default if none).</param>
        /// <returns>Handle to the newly-schedule job.</returns>
        JobHandle ScheduleSend(ref SendJobArguments arguments, JobHandle dep);

        /// <summary>
        /// Binds the network interface to an endpoint. This is meant to act the same way as the
        /// <c>bind</c> call in the BSD socket world. One way to see it is that it "attaches" the
        /// network interface to a specific local address on the machine.
        /// </summary>
        /// <param name="endpoint">Endpoint to bind to.</param>
        /// <returns>0 on success, a negative number on error.</returns>
        int Bind(NetworkEndpoint endpoint);

        /// <summary>
        /// Start listening for incoming connections. Unlike <see cref="Bind"/> which will always be
        /// called on clients and servers, this is only meant to be called on servers.
        /// </summary>
        /// <returns>0 on success, a negative number on error.</returns>
        int Listen();
    }
}
