using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace Unity.Scenes.Tests
{
    [ScriptedImporter(2, "extDontMatter_TestImporterWithSourceDependency")]
    internal class TestImporterWithSourceDependency : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var contents = File.ReadAllText(ctx.assetPath);
            var guid = contents.Split(" ")[0];
            ctx.DependsOnSourceAsset(new GUID(guid));
            Thread.Sleep(10 * 1000);
            var path = ctx.GetOutputArtifactFilePath("output");
            File.WriteAllText(path, guid, Encoding.ASCII);
        }
    }
}
