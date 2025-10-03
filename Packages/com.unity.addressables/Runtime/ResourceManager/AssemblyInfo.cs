using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyCompany("Unity Technologies")]
[assembly: InternalsVisibleTo("Unity.ResourceManager.Tests")]
[assembly: InternalsVisibleTo("Unity.Addressables.Editor.Tests")]
[assembly: InternalsVisibleTo("Unity.Addressables.Tests")]
[assembly: InternalsVisibleTo("Unity.Addressables")]
[assembly: InternalsVisibleTo("Unity.Addressables.Android")]
#if UNITY_EDITOR
[assembly: InternalsVisibleTo("Unity.Addressables.Editor")]
#endif
