namespace Doc.CodeSamples.Tests
{
    #region example
    using UnityEngine;
    using UnityEditor;
    using Unity.Entities.Serialization;

    public static class ContentManagementEditorUtility
    {
        [MenuItem("Content Management/Log UntypedWeakReferenceId of Selected")]
        private static void LogWeakReferenceIDs()
        {
            Object[] selectedObjects = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);
            for (int i = 0; i < selectedObjects.Length; i++)
            {
                Debug.Log($"{selectedObjects[i].name}: {UntypedWeakReferenceId.CreateFromObjectInstance(selectedObjects[i])}");
            }
        }
    }
    #endregion
}
