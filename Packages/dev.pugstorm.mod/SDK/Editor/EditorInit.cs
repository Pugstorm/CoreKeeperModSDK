#if PUG_MOD_SDK && USE_PUG_OTHER
using UnityEditor;

[InitializeOnLoad]
public static class EditorInit
{
    static EditorInit()
    {
        Manager.projectIsModSDK = true;
    }
}
#endif