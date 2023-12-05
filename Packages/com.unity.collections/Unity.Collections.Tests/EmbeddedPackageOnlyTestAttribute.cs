using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace Unity.Collections.Tests
{
#if UNITY_DOTSRUNTIME
    // Always ignore these tests
    internal class EmbeddedPackageOnlyTestAttribute : IgnoreAttribute {
        public EmbeddedPackageOnlyTestAttribute() : base("Only runs in the editor when this package is embedded or referenced locally.")
        {
        }
    }
#else
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    internal class EmbeddedPackageOnlyTestAttribute : NUnitAttribute, IApplyToTest
    {
        public void ApplyToTest(Test test)
        {
#if UNITY_EDITOR
            var assembly = test.Method?.TypeInfo?.Assembly ?? test.TypeInfo?.Assembly;
            if (assembly == null)
            {
                UnityEngine.Debug.LogError($"The {nameof(EmbeddedPackageOnlyTestAttribute)} attribute can only be applied to tests in an assembly.");
                return;
            }

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
            if (package == null)
            {
                UnityEngine.Debug.LogError(
                    $"The {nameof(EmbeddedPackageOnlyTestAttribute)} attribute can only be applied to tests in a package.");
                return;
            }

            if (package.source == UnityEditor.PackageManager.PackageSource.Embedded ||
                package.source == UnityEditor.PackageManager.PackageSource.Local)
                return;
#endif

            test.RunState = RunState.Ignored;
            test.Properties.Add(PropertyNames.SkipReason, "Only runs in the editor when this package is embedded or referenced locally.");
        }
    }
#endif
}
