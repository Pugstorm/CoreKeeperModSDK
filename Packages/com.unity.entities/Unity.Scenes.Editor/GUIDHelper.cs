using UnityEditor;

namespace Unity.Scenes.Editor
{
    static class GUIDHelper
    {
        public static GUID UnityEditorResources = new GUID("0000000000000000d000000000000000");
        public static GUID UnityBuiltinResources = new GUID("0000000000000000e000000000000000");
        public static GUID UnityBuiltinExtraResources = new GUID("0000000000000000f000000000000000");

        public static bool IsBuiltin(in GUID g) =>
            g == UnityEditorResources ||
            g == UnityBuiltinResources ||
            g == UnityBuiltinExtraResources;

        public static bool IsBuiltinResources(in GUID g) =>
            g == UnityBuiltinResources;

        public static bool IsBuiltinExtraResources(in GUID g) =>
            g == UnityBuiltinExtraResources;

        public static unsafe void PackBuiltinExtraWithFileIdent(ref GUID guid, long fileIdent)
        {
            fixed(void* ptr = &guid)
            {
                var asHash = (Entities.Hash128*)ptr;
                asHash->Value.w = (uint)fileIdent;
            }
        }

    }
}
