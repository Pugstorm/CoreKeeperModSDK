using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Unity.Physics.Tests
{
    public class AErrorVerification_UnitTests
    {
        static StringBuilder s_ErrorLogBuilder = new StringBuilder();

        [InitializeOnLoadMethod]
        static void InitializeLogger()
        {
            Application.logMessageReceived += LogMessage;
        }

        static void LogMessage(string log, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                s_ErrorLogBuilder.AppendLine(log);
        }

        [Test]
        public void ErrorLog_OnEditorBoot_WillBeEmpty()
        {
            Assert.AreEqual(string.Empty, s_ErrorLogBuilder.ToString());
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= LogMessage;
            s_ErrorLogBuilder.Clear();
        }
    }
}
