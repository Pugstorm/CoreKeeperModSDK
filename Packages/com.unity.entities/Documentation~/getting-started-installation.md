# Entities project setup

When you set up an Entities project, there are additional steps you must follow.

## Recommended packages

Check the overview of the available [DOTS packages](https://unity.com/dots/packages).

You should add the following recommended set of core packages to your project:

* [com.unity.entities](https://docs.unity3d.com/Packages/com.unity.entities@latest)
* [com.unity.entities.graphics](https://docs.unity3d.com/Packages/com.unity.entities.graphics@latest)

## IDE support
The Entities package uses [Roslyn Source Generators](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview). Because of this, you should use an IDE that's compatible with source generators. Previous IDE versions might experience slow-downs or mark valid code as errors. The following IDEs are compatible with source generators:

* Visual Studio 2022+
* Rider 2021.3.3+

## Domain Reload setting

To get the best performance in your Entities project, you should disable Unity's [Domain Reload](https://docs.unity3d.com/Manual/ConfigurableEnterPlayMode.html) setting. To do this, go to **Edit &gt; Project Settings &gt; Editor** menu, and enable the **Enter Play Mode Options** setting, but leave the **Reload Domain** and **Reload Scene** boxes disabled.

> [!NOTE]
> If you disable **Domain Reloads** [be mindful of your use of static fields and static event handlers](https://docs.unity3d.com/Manual/DomainReloading.html).

