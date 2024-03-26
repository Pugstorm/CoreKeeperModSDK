using System;
using System.IO;
using NUnit.Framework;

namespace Unity.NetCode.GeneratorTests
{
    class BaseTest
    {
        string? m_OriginalDirectory;

        [SetUp]
        public void SetupCommon()
        {
            m_OriginalDirectory = Environment.CurrentDirectory;
            //This will point to the com.unity.netcode directory
            string? currentDir = m_OriginalDirectory;
            while (currentDir?.Length > 0 && !currentDir.EndsWith("com.unity.netcode", StringComparison.Ordinal))
                currentDir = Path.GetDirectoryName(currentDir);

            if (currentDir == null || !currentDir.EndsWith("com.unity.netcode", StringComparison.Ordinal))
            {
                Assert.Fail("Cannot find com.unity.netcode folder");
                return;
            }

            //Execute in Runtime/SourceGenerators/Source~/Temp
            Environment.CurrentDirectory = Path.Combine(currentDir, "Runtime", "SourceGenerators", "Source~");
            Generators.Profiler.Initialize();
        }

        [TearDown]
        public void TearDownCommon()
        {
            Environment.CurrentDirectory = m_OriginalDirectory ?? string.Empty;
        }
    }
}
