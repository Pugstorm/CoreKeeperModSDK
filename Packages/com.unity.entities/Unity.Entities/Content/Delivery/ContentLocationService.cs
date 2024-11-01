using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Content
{
    /// <summary>
    /// RemoteContentLocationService is an abstract in order to allow for custom implementations of resolving the location of remote content.
    /// </summary>
    public abstract class ContentLocationService : IDisposable
    {
        /// <summary>
        /// State of resolving a location
        /// </summary>
        public enum ResolvingState
        {
            /// <summary>
            /// The location resolving process has not begun.
            /// </summary>
            None,
            /// <summary>
            /// The location is currently resolving.
            /// </summary>
            Resolving,
            /// <summary>
            /// The location has been resolved and is ready.
            /// </summary>
            Complete,
            /// <summary>
            /// The location failed to resolve.
            /// </summary>
            Failed
        }
        /// <summary>
        /// The download service name.  Each service name must be unique.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The priority of the service.  Higher values will place it at the front of the service list.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// The number of locations.
        /// </summary>
        public abstract int LocationCount { get; }

        /// <summary>
        /// The status of resolving a location.  When the state is completed, the location field will contain the resolved location.
        /// </summary>
        public struct LocationStatus
        {
            /// <summary>
            /// The current state.
            /// </summary>
            public ResolvingState State;
            /// <summary>
            /// The resolved location.  This is only valid when the State is Complete.
            /// </summary>
            public RemoteContentLocation Location;
        }

        /// <summary>
        /// Called when the RemoteContentLocationService is added to a delivery service.  This allows for any setup needed after being added to the delivery service.
        /// </summary>
        /// <param name="cds">The delivery service that the location service is being added to.</param>
        public virtual void OnAddedToDeliveryService(ContentDeliveryService cds) { }

        /// <summary>
        /// Get the current status of a resolving location.
        /// </summary>
        /// <param name="id">The id of the location.</param>
        /// <returns>The location status is returned for the specified id.</returns>
        public abstract LocationStatus GetLocationStatus(RemoteContentId id);

        /// <summary>
        /// Starts the resolving process for a location.  This may execute immediately and return a complete LocationStatus or it may be asynchronous.
        /// If the returned status Stat property is Resolving, you must periodically recheck the status to determine when it is completed.
        /// </summary>
        /// <param name="id">The remote content id.</param>
        /// <returns>The current status of the resolving process.</returns>
        public abstract LocationStatus ResolveLocation(RemoteContentId id);

        /// <summary>
        /// Retrieves all resolved content locations.  Depending on the implementation, this may not contain the entire set of locations.
        /// </summary>
        /// <param name="locs">The resolved locations.</param>
        /// <returns>False if the service does not have any resolved locations, True if it does.</returns>
        public abstract bool GetResolvedRemoteContentLocations(ref NativeHashSet<RemoteContentLocation> locs);

        /// <summary>
        /// Retrieves all resolved content ids.
        /// </summary>
        /// <param name="ids">The set of content ids.</param>
        /// <returns>False if the service does not have any resolved ids, True if it does.</returns>
        public abstract bool GetResolvedContentIds(ref UnsafeList<RemoteContentId> ids);

        /// <summary>
        /// Get a location set by name.
        /// </summary>
        /// <param name="setName">The name of the location set.  These are created during the publish process.</param>
        /// <param name="idPtr">The pointer to the <seealso cref="RemoteContentId"/> array.</param>
        /// <param name="count">The number of ids.</param>
        /// <returns>True if the set is found. False, otherwise.</returns>
        unsafe public abstract bool TryGetLocationSet(in FixedString512Bytes setName, out RemoteContentId* idPtr, out int count);
        /// <summary>
        /// Method called during the main process loop from the delivery service.
        /// </summary>
        public virtual void Process() { }
        ///<inheritdoc/>
        public virtual void Dispose() { }
    }


    [Serializable]
    internal struct RemoteContentCatalogData
    {
        [Serializable]
        public struct RemoteContentLocationData
        {
            public RemoteContentId identifier;
            public RemoteContentLocation location;
        }

        [Serializable]
        public struct RemoteContentSetData
        {
            public FixedString512Bytes Name;
            public BlobArray<int> Ids;
        }

        public BlobArray<RemoteContentLocationData> RemoteContentLocations;
        public BlobArray<RemoteContentSetData> ContentSets;
    }

}
