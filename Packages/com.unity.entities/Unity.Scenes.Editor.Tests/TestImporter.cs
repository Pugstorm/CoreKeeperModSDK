using System.IO;
using UnityEditor.AssetImporters;

namespace Unity.Scenes.Tests
{
    [ScriptedImporter(2, "extDontMatter_TestImporter")]
    internal class TestImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var path = ctx.GetOutputArtifactFilePath("output");
            File.WriteAllBytes(path, File.ReadAllBytes(ctx.assetPath));

        }
    }
}
