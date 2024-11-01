using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Entities.Serialization;
using UnityEditor;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;

namespace Unity.Entities.Content
{
    /// <summary>
    /// Utility functions for building archives.
    /// </summary>
    public static class ContentArchivesBuildUtility
    {
        static UnityEditor.Build.Player.TypeDB CachedTypeDB;

        /// <summary>
        ///  Build the set of Content Archives from a set of WeakObjectReferenceIds.
        /// </summary>
        /// <param name="buildTarget">The target platform for the built data.</param>
        /// <param name="objReferences">Set of object references to build.</param>
        /// <param name="sceneReferences">Set of scene references to build.</param>
        /// <param name="outputPath">The output path to build archives into.</param>
        /// <param name="results">The results data from the build.  This is returned in order to create the catalog data.</param>
        /// <param name="customContent">Custom content to include in the build.</param>
        /// <param name="taskList">The task list to use when building the content archives.</param>
        /// <param name="contextObjects">Additional content objects to pass into the build pipeline.</param>
        /// <returns>The result of the archive build process.</returns>
        public static ReturnCode BuildContentArchives(BuildTarget buildTarget,
            HashSet<UntypedWeakReferenceId> objReferences,
            HashSet<UntypedWeakReferenceId> sceneReferences,
            IEnumerable<CustomContent> customContent,
            string outputPath,
            out IBundleBuildResults results,
            IList<IBuildTask> taskList,
            params IContextObject[] contextObjects)
        {
            var abbs = new List<AssetBundleBuild>();
            var referencedAssets = new HashSet<string>(objReferences.Count);
            foreach (var o in objReferences)
                referencedAssets.Add(o.GlobalId.AssetGUID.ToString());

            var count = referencedAssets.Count;
            if (count > 0)
            {
                var abb = new AssetBundleBuild();
                abb.assetBundleName = "assets";
                abb.assetNames = new string[count];
                abb.addressableNames = new string[count];
                var index = 0;
                foreach (var a in referencedAssets)
                {
                    abb.assetNames[index] = AssetDatabase.GUIDToAssetPath(a);
                    abb.addressableNames[index] = a;
                    index++;
                }
                abbs.Add(abb);
            }
            foreach (var s in sceneReferences)
            {
                var abb = new AssetBundleBuild
                {
                    assetBundleName = $"scene_{s.GlobalId.AssetGUID}",
                    assetNames = new string[] { AssetDatabase.GUIDToAssetPath(s.GlobalId.AssetGUID.ToString()) },
                    addressableNames = new string[] { s.GlobalId.AssetGUID.ToString() }
                };
                abbs.Add(abb);
            }

            if (abbs.Count == 0 && customContent.Count() == 0)
            {
                results = default;
                return ReturnCode.SuccessNotRun;
            }

            var content = new BundleBuildContent(abbs);
            content.CustomAssets.AddRange(customContent);
            var group = BuildPipeline.GetBuildTargetGroup(buildTarget);
            Directory.CreateDirectory(outputPath);
            var arParams = new BundleBuildParameters(buildTarget, group, outputPath)
            {
                NonRecursiveDependencies = false,
                UseCache = true,
                BundleCompression = UnityEngine.BuildCompression.LZ4Runtime
            };
            if (CachedTypeDB != null)
                arParams.ScriptInfo = CachedTypeDB;
            var returnCode = ContentPipeline.BuildAssetBundles(arParams, content, out results, taskList, contextObjects);

            if (CachedTypeDB == null && returnCode >= ReturnCode.Success)
                CachedTypeDB = results.ScriptResults.typeDB;
            return returnCode;
        }
    }
}
