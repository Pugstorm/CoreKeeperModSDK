# Netcode for Entities Project Setup

To setup Netcode for Entities, you need to make sure you are on the correct version of the Editor.

## Unity Editor Version

Netcode for Entities requires you to have Unity version __2022.3.0f1__ or higher.

## IDE support
The Entities package uses [Roslyn Source Generators](https://docs.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview). For a better editing experience, we suggest to use an IDE that's compatible with source generators. 
The following IDEs are compatible with source generators:

* Visual Studio 2022+
* Rider 2021.3.3+

## Project Setup

1. Open the __Unity Hub__ and create a new __URP Project__.

2. Navigate to the __Package Manager__ (Window -> Package Manager). And add the following packages using __Add package from git URL...__ under the __+__ menu at the top left of the Package Manager.
    - com.unity.netcode
    - com.unity.entities.graphics

When the Package Manager is done, you can continue with the [next part](networked-cube.md).
