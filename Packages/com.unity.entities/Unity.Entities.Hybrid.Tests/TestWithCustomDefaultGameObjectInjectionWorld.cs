using System;
using Unity.Entities;
using UnityEngine.LowLevel;

namespace Unity.Entities.Hybrid.Tests
{
    [Serializable]
    public struct TestWithCustomDefaultGameObjectInjectionWorld
    {
        public World PreviousGameObjectInjectionWorld;
        private bool _wasInPlayerLoop;

        public World World => World.DefaultGameObjectInjectionWorld;
        public EntityManager EntityManager => World.DefaultGameObjectInjectionWorld.EntityManager;

        public void Setup(bool createWorld = false, bool isEditorWorld = false)
        {
            PreviousGameObjectInjectionWorld = World.DefaultGameObjectInjectionWorld;
            if (PreviousGameObjectInjectionWorld != null)
            {
                _wasInPlayerLoop = ScriptBehaviourUpdateOrder.IsWorldInCurrentPlayerLoop(PreviousGameObjectInjectionWorld);
                if (_wasInPlayerLoop)
                    ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(PreviousGameObjectInjectionWorld);
            }
            else
                _wasInPlayerLoop = false;

            World.DefaultGameObjectInjectionWorld = null;

            if (createWorld)
                DefaultWorldInitialization.Initialize("TestCustomDefaultWorld", isEditorWorld);
        }

        public void TearDown()
        {
            if (World.DefaultGameObjectInjectionWorld != null)
                World.DefaultGameObjectInjectionWorld.Dispose();
            if (PreviousGameObjectInjectionWorld != null && !PreviousGameObjectInjectionWorld.IsCreated)
                PreviousGameObjectInjectionWorld = null;
            World.DefaultGameObjectInjectionWorld = PreviousGameObjectInjectionWorld;
            if (_wasInPlayerLoop)
            {
                ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(PreviousGameObjectInjectionWorld);
                _wasInPlayerLoop = false;
            }
        }
    }
}
