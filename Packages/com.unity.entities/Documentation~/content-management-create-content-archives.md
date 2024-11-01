# Create custom content archives

Unity automatically creates content archives to store every object referenced from the [subscenes](conversion-subscenes.md) included in the build. For more information, refer to [How Unity generates content archives](content-management-intro.md#how-unity-generates-content-archives). This covers the content management requirements of many applications, but you can also create additional content archives that you can load independently at runtime. This can be useful to structure downloadable content, reduce the initial install size of your application, or load assets optimized for the end user’s platform.

The process to create a custom content archive that you can load into Unity at runtime has the following steps: 

* Build: Unity finds all the objects that should go into the content archive and copies the object files into a target directory.
* Publish: Unity takes all the files in the directory and organizes them into a structure that allows Unity to directly install the files onto the target platform. To do this, Unity renames the files to be a hash of the contents of the file. It then builds a catalog alongside the content archive to map the files to their original object.

The results of this process are content archives to store the files, and a catalog to enable Unity to find the correct file for an object.

The Unity Editor contains menu items that you can use to build and publish your own content archives. They take a set of subscenes, extract the objects that the subscenes reference, and build the objects into a content archive. You can use the menu items on a specific list of subscenes to add additional content to your application, or on the subscenes specified in [Build Settings](xref:UnityEditor.EditorBuildSettings.scenes) to rebuild all the content for the application's Standalone Player without needing to rebuild the Player itself.

## Rebuild the content for your application's Player

During the Player build process, Unity automatically generates content archives to store all the objects referenced from the [subscenes](conversion-subscenes.md) included in the build. For more information, refer to [How Unity generates content archives](content-management-intro.md#how-unity-generates-content-archives). To improve iteration time, Unity can generate this same set of content archives without building a new Player. You can then update the content in your application with the new content archives. To do this:

1. Select **Assets** > **Publish** > **Content Update**. This begins the content archive build and publish process. After this finishes, the result content archive and catalog will be in your [Streaming Assets](xref:StreamingAssets) folder. 

## Build content archives from a C# script

If the menu items don't provide enough control over which objects to include in custom content archives, you can use the content archive build and publish APIs to create content archives from a C# script.

The following code example shows how to build and publish a content update.

```C#
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Build.Classic;
using Unity.Collections;
using Unity.Scenes.Editor;
using UnityEditor.Experimental;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Unity.Build;
using Unity.Build.Common;
using Unity.Entities.Build;
using Unity.Entities.Content;

static class BuildUtilities
{
    //prepares the content files for publish.  The original files can be deleted or retained during this process by changing the last parameter of the PublishContent call.
    static void PublishExistingBuild()
    {
        var buildFolder = EditorUtility.OpenFolderPanel("Select Build To Publish",
        Path.GetDirectoryName(Application.dataPath), "Builds");
        if (!string.IsNullOrEmpty(buildFolder))
        {
            var streamingAssetsPath = $"{buildFolder}/{PlayerSettings.productName}_Data/StreamingAssets";
            //the content sets are defined by the functor passed in here.  
            RemoteContentCatalogBuildUtility.PublishContent(streamingAssetsPath, 
                $"{buildFolder}-RemoteContent", 
                f => new string[] { "all" }, true);
        }
}
    //This method is somewhat complicated because it will build the scenes from a player build but without fully building the player.
    static void CreateContentUpdate()
    {
        var buildFolder = EditorUtility.OpenFolderPanel("Select Build To Publish",
        Path.GetDirectoryName(Application.dataPath), "Builds");
        if (!string.IsNullOrEmpty(buildFolder))
        {
            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var tmpBuildFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath),
                        $"/Library/ContentUpdateBuildDir/{PlayerSettings.productName}");

            var instance = DotsGlobalSettings.Instance;
            var playerGuid = instance.GetPlayerType() == DotsGlobalSettings.PlayerType.Client ? instance.GetClientGUID() : instance.GetServerGUID();
            if (!playerGuid.IsValid)
                throw new Exception("Invalid Player GUID");

            var subSceneGuids = new HashSet<Unity.Entities.Hash128>();
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                var ssGuids = EditorEntityScenes.GetSubScenes(EditorBuildSettings.scenes[i].guid);
                foreach (var ss in ssGuids)
                    subSceneGuids.Add(ss);
            }
            RemoteContentCatalogBuildUtility.BuildContent(subSceneGuids, playerGuid, buildTarget, tmpBuildFolder);

            var publishFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", $"{buildFolder}-RemoteContent");
            RemoteContentCatalogBuildUtility.PublishContent(tmpBuildFolder, publishFolder, f => new string[] { "all" });
        }
    }
}
```



## Deliver the content

After you create custom content archives, you can deliver them to the application at runtime. The content archive build and publish process structures the content so that the content mirrors the structure of the local device cache. This makes the content delivery process simpler because you can load content into your application directly from content archives on the local device, or from an online content delivery service. For information on how to do this, refer to [Deliver content to an application](content-management-delivery.md).

## Additional resources

* [Deliver content to an application](content-management-delivery.md)

 