using System;
using System.IO;
using Unity.Collections;
using Unity.Entities.Serialization;

namespace Unity.Entities.Content
{
    /// <summary>
    /// Utility functions for building content catalog data.
    /// </summary>
    internal static class ContentCatalogBuildUtility
    {
        /// <summary>
        /// Print the data provided by an ICatalogDataSource.
        /// </summary>
        /// <param name="src">The data source.</param>
        /// <param name="printFunc">The method to print each line.</param>
        /// <param name="idRemapFunc">Functor that remaps runtime ids.</param>
        public static void Print(IRuntimeCatalogDataSource src, Action<string> printFunc, Func<UntypedWeakReferenceId, UntypedWeakReferenceId> idRemapFunc)
        {
            foreach (var a in src.GetArchiveIds())
            {
                printFunc($"Archive: {a.Value}");
                foreach (var f in src.GetFileIds(a))
                {
                    printFunc($"\tFile: {f.Value}");
                    foreach (var o in src.GetObjects(f))
                        printFunc($"\t\tObject: {idRemapFunc(o.Item1)}");
                    foreach (var o in src.GetScenes(f))
                        printFunc($"\t\tScene: {idRemapFunc(o.Item1)}");
                    foreach (var o in src.GetDependencies(f))
                        printFunc($"\t\tDependency: {o.Value}");
                }
            }
        }

        /// <summary>
        /// Builds a text version of the catalog that is human readable for debug purposes.
        /// </summary>
        /// <param name="src">The catalog data source.</param>
        /// <param name="outputPath">The output path to save the data.</param>
        /// <param name="idRemapFunc">Functor that remaps runtime ids.</param>
        public static void BuildCatalogDataVerbose(IRuntimeCatalogDataSource src, string outputPath, Func<UntypedWeakReferenceId, UntypedWeakReferenceId> idRemapFunc)
        {
            var sb = new System.Text.StringBuilder();
            Print(src, l=>sb.AppendLine(l), idRemapFunc);
            File.WriteAllText(outputPath, sb.ToString());
        }

        /// <summary>
        /// Builds a binary version of the catalog that is consumable by the player.  
        /// </summary>
        /// <param name="results">The build results.</param>
        /// <param name="src">The catalog data source.</param>
        /// <param name="outputPath">The output path to save the data.</param>
        /// <param name="idRemapFunc">Functor that remaps runtime ids.</param>
        public static void BuildCatalogDataRuntime(IRuntimeCatalogDataSource src, string outputPath, Func<UntypedWeakReferenceId, UntypedWeakReferenceId> idRemapFunc)
        {
            using (var blobBuilder = new BlobBuilder(Allocator.Temp))
            {
                ref var blob = ref blobBuilder.ConstructRoot<RuntimeContentCatalogData>();
                RuntimeContentCatalogDataUtility.Create(src, blobBuilder, ref blob, idRemapFunc);
                using (var aref = blobBuilder.CreateBlobAssetReference<RuntimeContentCatalogData>(Allocator.Temp))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                    BlobAssetReference<RuntimeContentCatalogData>.Write(blobBuilder, outputPath, 1);
                }
            }
        }
    }
}
