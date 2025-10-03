using NUnit.Framework;
using UnityEditor;
using UnityEngine.AddressableAssets;

public class EditorPlatformMappingServiceTests
{
    [TestCase(BuildTarget.XboxOne, AddressablesPlatform.XboxOne)]
    [TestCase(BuildTarget.Switch, AddressablesPlatform.Switch)]
    [TestCase(BuildTarget.PS4, AddressablesPlatform.PS4)]
    [TestCase(BuildTarget.iOS, AddressablesPlatform.iOS)]
    [TestCase(BuildTarget.Android, AddressablesPlatform.Android)]
    [TestCase(BuildTarget.WebGL, AddressablesPlatform.WebGL)]
    [TestCase(BuildTarget.StandaloneWindows64, AddressablesPlatform.Windows)]
    [TestCase(BuildTarget.StandaloneWindows, AddressablesPlatform.Windows)]
    [TestCase(BuildTarget.StandaloneOSX, AddressablesPlatform.OSX)]
    [TestCase(BuildTarget.StandaloneLinux64, AddressablesPlatform.Linux)]
    public void EditorPlatformMappingService_EqualsDesiredAddressablesPlatform(BuildTarget platform, AddressablesPlatform desiredPlatform)
    {
        Assert.AreEqual(PlatformMappingService.GetAddressablesPlatformInternal(platform), desiredPlatform);
    }
}
