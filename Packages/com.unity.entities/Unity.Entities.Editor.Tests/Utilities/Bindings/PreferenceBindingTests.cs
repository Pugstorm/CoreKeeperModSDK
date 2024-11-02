using NUnit.Framework;
using Unity.Properties;
using Unity.Serialization.Editor;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    class PreferenceBindingTests
    {
        abstract class AdvancedSettingsBinding<TValue> : PreferenceBinding<AdvancedSettings, TValue>
        {
            protected sealed override string SettingsKey { get; } = Constants.Settings.Advanced;
        }

        class ShowAdvancedWorldBinding : AdvancedSettingsBinding<bool>
        {
            public ShowAdvancedWorldBinding()
            {
                PreferencePath = new PropertyPath(nameof(AdvancedSettings.ShowAdvancedWorlds));
            }

            protected override void OnUpdate(bool value)
            {
                Assert.That(value, Is.EqualTo(UserSettings<AdvancedSettings>.GetOrCreate(Constants.Settings.Advanced).ShowAdvancedWorlds));
            }
        }

        bool m_BackupValue;

        [SetUp]
        public void Setup()
        {
            m_BackupValue = UserSettings<AdvancedSettings>.GetOrCreate(Constants.Settings.Advanced).ShowAdvancedWorlds;
        }

        [TearDown]
        public void Teardown()
        {
            UserSettings<AdvancedSettings>.GetOrCreate(Constants.Settings.Advanced).ShowAdvancedWorlds = m_BackupValue;
        }

        [Test]
        public void Binding_WithPreferenceSetting_UpdatesCorrectly()
        {
            var binding = (IBinding)new ShowAdvancedWorldBinding();
            binding.Update();

            UserSettings<AdvancedSettings>.GetOrCreate(Constants.Settings.Advanced).ShowAdvancedWorlds = true;
            binding.Update();

            UserSettings<AdvancedSettings>.GetOrCreate(Constants.Settings.Advanced).ShowAdvancedWorlds = false;
            binding.Update();
        }

        [Test]
        public void AggregatedBindings_WithPreferenceSetting_UpdatesCorrectly()
        {
            var binding = (IBinding)new AggregateBinding(
                new ShowAdvancedWorldBinding(),
                new ShowAdvancedWorldBinding(),
                new ShowAdvancedWorldBinding(),
                new ShowAdvancedWorldBinding(),
                new ShowAdvancedWorldBinding(),
                new ShowAdvancedWorldBinding()
            );
            binding.Update();

            UserSettings<AdvancedSettings>.GetOrCreate(Constants.Settings.Advanced).ShowAdvancedWorlds = true;
            binding.Update();

            UserSettings<AdvancedSettings>.GetOrCreate(Constants.Settings.Advanced).ShowAdvancedWorlds = false;
            binding.Update();
        }
    }
}
