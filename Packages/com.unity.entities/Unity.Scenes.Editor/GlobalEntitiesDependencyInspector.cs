using UnityEditor;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes.Editor
{
    [CustomEditor(typeof(GlobalEntitiesDependency))]
    internal class GlobalEntitiesDependencyInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var globalEntitiesDependency = (GlobalEntitiesDependency)target;
            var cacheGUID = globalEntitiesDependency.cacheGUID;
            var cacheGUIDString = cacheGUID.ToString();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Global Dependency GUID", cacheGUIDString);

            //TODO: Do we want to expose this? Could be confusing to some users.
            //Right now they can edit this manually using the debug view
            /*
            GUILayout.Space(10);
            var guidString = EditorGUILayout.TextField("Global Dependency GUID", cacheGUIDString);
            if (guidString != cacheGUIDString)
            {
                var newGUID = new Hash128(guidString);
                if (newGUID.IsValid)
                    cacheGUID = newGUID;
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Regenerate GUID"))
            {
                cacheGUID = GUID.Generate();
            }

            if (cacheGUID != globalEntitiesDependency.cacheGUID)
            {
                globalEntitiesDependency.cacheGUID = cacheGUID;
                EditorUtility.SetDirty(globalEntitiesDependency);
            }
            */
        }
    }
}
