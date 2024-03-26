using UnityEngine;
using UnityEngine.Playables;

using Unity.Entities;
using Unity.Collections;
using System;
using System.Collections.Generic;

namespace Unity.NetCode.Hybrid
{
    /// <summary>
    /// This class is an extension of regular PlayableBehaviour which can be used
    /// to implement graph assets for GhostAnimationController.
    /// It adds a new method for receiving update calls in the netcode prediction loop.
    /// When evaluating the graph in prediction PrepareFrame should set the time on all
    /// clips it is using since time will roll back.
    /// When using root motion PreparePredictedData should also set the time of all
    /// clips to the current time at the beginning of the call, not doing so will break
    /// root motion. You only need to set the time in PreparePredictedData if 'isRollback' is true,
    /// </summary>
    public abstract class GhostPlayableBehaviour : PlayableBehaviour
    {
        /// <summary>
        /// This method is called as part of the prediction loop if this behaviour is registered
        /// with a GhostAnimationController. This function is where all computation of animation
        /// data should happen, unless you use a system to compute the animation data instead.
        /// </summary>
        /// <param name="serverTick">Server tick.</param>
        /// <param name="deltaTime">You only need to set the time if <paramref name="isRollback"/> is true.</param>
        /// <param name="isRollback">Is rollback.</param>
        public abstract void PreparePredictedData(NetworkTick serverTick, float deltaTime, bool isRollback);
    }

    /// <summary>
    /// Interface used by GhostAnimationGraphAssets to communicate which components they are using
    /// to store animation data which should be ghosted.
    /// </summary>
    public interface IRegisterPlayableData
    {
        /// <summary>
        /// Register a new component type with playable data. It is ok to call this
        /// multiple times, and several assets on the same controller can register the
        /// same data, but it is up to the user to make sure the logic to update the data
        /// can handle that case.
        /// </summary>
        /// <typeparam name="T">Unmanaged of type <see cref="IComponentData"/></typeparam>
        void RegisterPlayableData<T>() where T: unmanaged, IComponentData;
    }
    /// <summary>
    /// The main graph asset for a GhostAnimationController. All animation logic which
    /// needs to be synchronized should be expressed as an assets of this type. The asset
    /// can reference other assets to build a full graph.
    /// </summary>
    public abstract class GhostAnimationGraphAsset : ScriptableObject
    {
        /// <summary>
        /// Create a playable for this node. The behaviours List must be populated with all GhostPlayableBehaviour
        /// which require a call to PreparePredictedData. If a GhostPlayableBehaviour is not added to that list
        /// the prediction update will not be called.
        /// This can create a GhostPlayableBehaviour which contains mixers, clips, references to other assets etc.
        /// </summary>
        /// <param name="controller"><see cref="GhostAnimationController"/> to construct playable from.</param>
        /// <param name="graph"><see cref="PlayableGraph"/> used to manage creation and destruction of playables.</param>
        /// <param name="behaviours">Populated list to call <see cref="GhostPlayableBehaviour.PreparePredictedData"/> on.</param>
        /// <returns><see cref="Playable"/> constructed for this node.</returns>
        public abstract Playable CreatePlayable(GhostAnimationController controller, PlayableGraph graph, List<GhostPlayableBehaviour> behaviours);
        /// <summary>
        /// Register playable data for this asset. Only data registered here can be accessed during PrepareFrame,
        /// no other entity data can be accessed.
        /// </summary>
        /// <param name="register">Communicate which components they are using.</param>
        public abstract void RegisterPlayableData(IRegisterPlayableData register);

        private class PlayableDataHashCollector : IRegisterPlayableData
        {
            public ulong Hash;
            public void RegisterPlayableData<T>() where T: unmanaged, IComponentData
            {
                // Combine hash with the hash of the new component
                var ctype = ComponentType.ReadWrite<T>();
                var typeHash = TypeManager.GetTypeInfo(ctype.TypeIndex).StableTypeHash;
                Hash = TypeHash.CombineFNV1A64(Hash, typeHash);
            }
        }
        private void OnValidate()
        {
            var hash = new PlayableDataHashCollector();
            RegisterPlayableData(hash);
            m_PlayableDataHash = hash.Hash;
        }
        [SerializeField] private ulong m_PlayableDataHash;
    }
}
