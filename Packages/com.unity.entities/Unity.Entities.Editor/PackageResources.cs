using UnityEngine;

namespace Unity.Entities.Editor
{
    static class PackageResources
    {
        public static VisualElementTemplate LoadTemplate(string name) => new VisualElementTemplate(Resources.PackageId, name);
        public static StyleSheetWithVariant LoadStyleSheet(string name) => new StyleSheetWithVariant(Resources.PackageId, name);
        public static Texture2D LoadIcon(string path) => EditorResources.LoadTexture<Texture2D>(Resources.PackageId, path, true);
    }
}
