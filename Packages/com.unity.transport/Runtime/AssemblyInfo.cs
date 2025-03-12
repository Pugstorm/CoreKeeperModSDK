using System.Runtime.CompilerServices;
using Unity.Jobs;
using Unity.Networking.Transport;

[assembly: RegisterGenericJobType(typeof(SimpleConnectionLayer.ReceiveJob<UnderlyingConnectionList>))]
[assembly: RegisterGenericJobType(typeof(SimpleConnectionLayer.ReceiveJob<NullUnderlyingConnectionList>))]
[assembly: RegisterGenericJobType(typeof(RelayLayer.ReceiveJob<UnderlyingConnectionList>))]
[assembly: RegisterGenericJobType(typeof(RelayLayer.ReceiveJob<NullUnderlyingConnectionList>))]

[assembly: InternalsVisibleTo("Unity.Networking.Transport.Editor.Tests")]
[assembly: InternalsVisibleTo("Unity.Networking.Transport.Runtime.Tests")]
[assembly: InternalsVisibleTo("Unity.Networking.Transport.Tests.Integration")]
[assembly: InternalsVisibleTo("Unity.Networking.Transport.Tests.Utilities")]
[assembly: InternalsVisibleTo("Unity.Networking.Transport.PlayTests.Performance")]

// We are making certain things visible for certain projects that require
// access to Network Protocols thus not requiring the API be visible
[assembly: InternalsVisibleTo("Unity.InternalAPINetworkingBridge.001")]

// NetCode needs access to writeable Pipeline buffers, but the feature needs further thought.
// Thus, exception is made here.
[assembly: InternalsVisibleTo("Unity.NetCode")]
