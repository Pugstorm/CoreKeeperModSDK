using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Entities.Editor.Tests
{
    [Serializable]
    sealed class DataModeControllerTests
    {
        IDataModeController m_DataModeController;
        [SerializeField] bool m_DataModeChangedEventFired;
        [SerializeField] DataMode m_LastDataModeInEvent;

        HierarchyWindow m_HierarchyWindow;

        [SetUp]
        public void SetUp()
        {
            m_HierarchyWindow = EditorWindow.GetWindow<HierarchyWindow>();
            m_HierarchyWindow.Show();


            m_DataModeController = m_HierarchyWindow.dataModeController;
            m_DataModeController.dataModeChanged += OnDataModeChanged;
        }

        void OnDataModeChanged(DataModeChangeEventArgs arg)
        {
            m_DataModeChangedEventFired = true;
            m_LastDataModeInEvent = arg.nextDataMode;
        }

        [TearDown]
        public void TearDown()
        {
            m_DataModeController.dataModeChanged -= OnDataModeChanged;
            m_HierarchyWindow.Close();
            m_DataModeController = null;
            m_DataModeChangedEventFired = false;
            m_LastDataModeInEvent = default;
        }

        void AssertThatEventHasNotBeenFired()
        {
            Assert.That(m_DataModeChangedEventFired, Is.False);
            Assert.That(m_LastDataModeInEvent, Is.EqualTo((DataMode)default));
        }

        void AssertThatEventHasBeenFiredWithArgument(DataMode expectedDataModeInEvent)
        {
            Assert.That(m_DataModeChangedEventFired, Is.True);
            Assert.That(m_LastDataModeInEvent, Is.EqualTo(expectedDataModeInEvent));

            m_DataModeChangedEventFired = false;
            m_LastDataModeInEvent = default;
        }

        [Test]
        public void EditMode_ModeSwitching()
        {
            Assert.That(EditorApplication.isPlaying, Is.False);
            Assert.That(m_DataModeController.dataMode, Is.EqualTo(DataMode.Authoring), $"Initial data mode in edit mode must be {DataMode.Authoring}");
            AssertThatEventHasNotBeenFired();

            m_DataModeController.TryChangeDataMode(DataMode.Runtime);
            Assert.That(m_DataModeController.dataMode, Is.EqualTo(DataMode.Runtime), $"Now data mode is switched to {DataMode.Runtime}");
            AssertThatEventHasBeenFiredWithArgument(DataMode.Runtime);

            var supportedDataModes = HierarchyWindow.GetSupportedDataModes();
            Assert.That(supportedDataModes.Contains(DataMode.Authoring), Is.True);
            Assert.That(supportedDataModes.Contains(DataMode.Runtime), Is.True);
            Assert.That(supportedDataModes.Contains(DataMode.Mixed), Is.True);
        }

        [UnityTest]
        public IEnumerator PlayMode_ModeSwitching()
        {
            Assert.That(EditorApplication.isPlaying, Is.False);

            yield return new EnterPlayMode();

            Assert.That(EditorApplication.isPlaying, Is.True);
            Assert.That(m_DataModeController.dataMode, Is.EqualTo(DataMode.Mixed), $"Initial data mode in play mode must be {DataMode.Mixed}");
            // We can only assert this when enter playmode doesn't trigger a domain reload because
            // the enter play mode change event is triggered before we get the chance to register to the event.
            if (EditorSettings.enterPlayModeOptionsEnabled && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0)
                AssertThatEventHasBeenFiredWithArgument(DataMode.Mixed);

            m_DataModeController.TryChangeDataMode(DataMode.Runtime);
            Assert.That(m_DataModeController.dataMode, Is.EqualTo(DataMode.Runtime), $"Now data mode is switched to {DataMode.Runtime}");
            AssertThatEventHasBeenFiredWithArgument(DataMode.Runtime);

            m_DataModeController.TryChangeDataMode(DataMode.Authoring);
            Assert.That(m_DataModeController.dataMode, Is.EqualTo(DataMode.Authoring), $"Now data mode is switched to {DataMode.Authoring}");
            AssertThatEventHasBeenFiredWithArgument(DataMode.Authoring);

            m_DataModeController.TryChangeDataMode(DataMode.Mixed);
            Assert.That(m_DataModeController.dataMode, Is.EqualTo(DataMode.Mixed), $"Now data mode is switched to {DataMode.Mixed}");
            AssertThatEventHasBeenFiredWithArgument(DataMode.Mixed);

            var supportedDataModes = HierarchyWindow.GetSupportedDataModes();
            Assert.That(supportedDataModes.Contains(DataMode.Authoring), Is.True);
            Assert.That(supportedDataModes.Contains(DataMode.Runtime), Is.True);
            Assert.That(supportedDataModes.Contains(DataMode.Mixed), Is.True);

            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AlternateBetweenPlayModeEditMode_ReuseLastSelectedMode()
        {
            m_DataModeController.TryChangeDataMode(DataMode.Runtime);
            yield return new EnterPlayMode();
            m_DataModeController.TryChangeDataMode(DataMode.Authoring);
            yield return new ExitPlayMode();

            Assert.That(EditorApplication.isPlaying, Is.False);
            AssertThatEventHasBeenFiredWithArgument(DataMode.Authoring);
            Assert.That(m_DataModeController.dataMode, Is.EqualTo(DataMode.Authoring));

            yield return new EnterPlayMode();

            Assert.That(EditorApplication.isPlaying, Is.True);
            // We can only assert this when enter playmode doesn't trigger a domain reload because
            // the enter play mode change event is triggered before we get the chance to register to the event.
            if (EditorSettings.enterPlayModeOptionsEnabled && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0)
                AssertThatEventHasBeenFiredWithArgument(DataMode.Mixed);
            Assert.That(m_DataModeController.dataMode, Is.EqualTo(DataMode.Mixed));
        }
    }
}
