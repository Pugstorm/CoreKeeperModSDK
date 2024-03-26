using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace Unity.NetCode.Editor
{
    class CodeGenMenu
    {
        [MenuItem("Multiplayer/Force Code Generation", priority = 10)]
        static private void ForceRunCodeGen()
        {
            EditorApplication.delayCall += () =>
            {
                //Re-import the netcode package
                //Extra problem: how to force a re-compile for the DOTSRuntime?
                //since the templates are not dependencies, how can I force to recompile
                //dlls?
                var obj = AssetDatabase.LoadAssetAtPath<Object>("Packages/com.unity.netcode/Runtime");
                var oldSelection = Selection.activeObject;
                Selection.activeObject = obj;
                try
                {
                    EditorApplication.ExecuteMenuItem("Assets/Reimport");
                }
                finally
                {
                    Selection.activeObject = oldSelection;
                }
            };
        }

        [MenuItem("Multiplayer/Open Source Generated Folder", priority = 12)]
        static private void OpenSourceGeneratedFolder()
        {
            if (!System.IO.File.Exists("Temp/NetCodeGenerated"))
            {
                //Create a dummy one with an empty log
                System.IO.Directory.CreateDirectory("Temp/NetCodeGenerated");
                System.IO.File.CreateText("Temp/NetCodeGenerated/SourceGenerator.log").Close();
            }
            EditorUtility.RevealInFinder("Temp/NetCodeGenerated/SourceGenerator.log");
        }
    }
}
