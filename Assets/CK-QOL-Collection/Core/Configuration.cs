using CoreLib.Data.Configuration;
using CoreLib.RewiredExtension;
using PugMod;
using Rewired;

namespace CK_QOL_Collection.Core
{
    /// <summary>
    /// Handles the configuration settings for the CK_QOL_Collection mod.
    /// Provides functionality to initialize and manage settings for various features such as Crafting Range and Quick Stash.
    /// </summary>
    internal static class Configuration
    {
        /// <summary>
        /// The prefix used for all keybinds associated with this mod.
        /// </summary>
        internal const string KeybindPrefix = "CK_QOL";

        /// <summary>
        /// Gets the configuration file used to store and manage settings for the mod.
        /// </summary>
        internal static ConfigFile ConfigFile { get; private set; }

        /// <summary>
        /// Initializes the configuration settings for the mod.
        /// </summary>
        /// <param name="modInfo">The loaded mod information.</param>
        /// <returns>The initialized <see cref="ConfigFile"/> instance containing the mod's configuration.</returns>
        internal static ConfigFile Initialize(LoadedMod modInfo)
        {
            ConfigFile = new ConfigFile($"{Entry.Name}/{Entry.Name}.cfg", true, modInfo);

            // General settings
            ConfigFile.Bind(Sections.General.Options.Enabled.Definition, Sections.General.Options.Enabled.Default, Sections.General.Options.Enabled.Description);

            // CraftingRange settings
            ConfigFile.Bind(Sections.CraftingRange.Options.Enabled.Definition, Sections.CraftingRange.Options.Enabled.Default, Sections.CraftingRange.Options.Enabled.Description);
            ConfigFile.Bind(Sections.CraftingRange.Options.Distance.Definition, Sections.CraftingRange.Options.Distance.Default, Sections.CraftingRange.Options.Distance.Description);
            ConfigFile.Bind(Sections.CraftingRange.Options.ChestLimit.Definition, Sections.CraftingRange.Options.ChestLimit.Default, Sections.CraftingRange.Options.ChestLimit.Description);

            // QuickStash settings
            ConfigFile.Bind(Sections.QuickStash.Options.Enabled.Definition, Sections.QuickStash.Options.Enabled.Default, Sections.QuickStash.Options.Enabled.Description);
            ConfigFile.Bind(Sections.QuickStash.Options.Distance.Definition, Sections.QuickStash.Options.Distance.Default, Sections.QuickStash.Options.Distance.Description);
            
            // QuickStash keybinds
            RewiredExtensionModule.AddKeybind(Sections.QuickStash.KeyBinds.QuickStashKeyBindName, Sections.QuickStash.KeyBinds.QuickStashKeyBindingDescription, Sections.QuickStash.KeyBinds.DefaultQuickStashKey, Sections.QuickStash.KeyBinds.DefaultQuickStashKModifier);
            
            return ConfigFile;
        }

        /// <summary>
        /// Defines the various sections and settings for the mod configuration.
        /// </summary>
        internal static class Sections
        {
            /// <summary>
            /// Configuration settings for general mod behavior.
            /// </summary>
            internal static class General
            {
                /// <summary>
                /// The name of the general section.
                /// </summary>
                internal const string Name = nameof(General);
                /// <summary>
                /// Determines whether the mod is enabled.
                /// </summary>
                internal static bool IsEnabled => Options.Enabled.Value;

                /// <summary>
                /// Options for the General section.
                /// </summary>
                internal static class Options
                {
                    /// <summary>
                    /// Configuration setting that determines if the mod is enabled.
                    /// </summary>
                    internal static class Enabled
                    {
                        /// <summary>
                        /// The key for the enabled setting in the configuration file.
                        /// </summary>
                        internal const string Key = nameof(Enabled);
                        /// <summary>
                        /// The default value for the enabled setting.
                        /// </summary>
                        internal const bool Default = true;
                        private const string Text = "Should the Mod be enabled?";

                        /// <summary>
                        /// Gets the acceptable values for the enabled setting.
                        /// </summary>
                        private static AcceptableValueList<bool> AcceptableValues => new(true, false);
                        /// <summary>
                        /// Gets the configuration definition for the enabled setting.
                        /// </summary>
                        internal static ConfigDefinition Definition => new(Name, Key);
                        /// <summary>
                        /// Gets the configuration description for the enabled setting.
                        /// </summary>
                        internal static ConfigDescription Description => new(Text, AcceptableValues);

                        /// <summary>
                        /// Gets the value of the enabled setting.
                        /// </summary>
                        internal static bool Value => ConfigFile.TryGetEntry<bool>(Definition, out var enabled) && enabled.Value;
                    }
                }
            }

            /// <summary>
            /// Configuration settings for the Crafting Range feature.
            /// </summary>
            internal static class CraftingRange
            {
                /// <summary>
                /// The name of the Crafting Range section.
                /// </summary>
                internal const string Name = nameof(CraftingRange);
                /// <summary>
                /// Determines whether the Crafting Range feature is enabled.
                /// </summary>
                internal static bool IsEnabled => Options.Enabled.Value;

                /// <summary>
                /// Options for the Crafting Range feature.
                /// </summary>
                internal static class Options
                {
                    /// <summary>
                    /// Configuration setting that determines if the Crafting Range feature is enabled.
                    /// </summary>
                    internal static class Enabled
                    {
                        /// <summary>
                        /// The key for the enabled setting in the configuration file.
                        /// </summary>
                        internal const string Key = nameof(Enabled);
                        /// <summary>
                        /// The default value for the enabled setting.
                        /// </summary>
                        internal const bool Default = true;
                        private const string Text = "Should the 'Crafting Range' feature be enabled?";

                        /// <summary>
                        /// Gets the acceptable values for the enabled setting.
                        /// </summary>
                        private static AcceptableValueList<bool> AcceptableValues => new(true, false);
                        /// <summary>
                        /// Gets the configuration definition for the enabled setting.
                        /// </summary>
                        internal static ConfigDefinition Definition => new(Name, Key);
                        /// <summary>
                        /// Gets the configuration description for the enabled setting.
                        /// </summary>
                        internal static ConfigDescription Description => new(Text, AcceptableValues);

                        /// <summary>
                        /// Gets the value of the enabled setting.
                        /// </summary>
                        internal static bool Value => General.Options.Enabled.Value && ConfigFile.TryGetEntry<bool>(Definition, out var enabled) && enabled.Value;
                    }

                    /// <summary>
                    /// Configuration setting that determines the crafting range distance.
                    /// </summary>
                    internal static class Distance
                    {
                        /// <summary>
                        /// The key for the distance setting in the configuration file.
                        /// </summary>
                        internal const string Key = nameof(Distance);
                        /// <summary>
                        /// The default value for the distance setting.
                        /// </summary>
                        internal const float Default = 20f;
                        private const string Text = "The range to determine chests in proximity.";

                        /// <summary>
                        /// Gets the acceptable values for the distance setting.
                        /// </summary>
                        private static AcceptableValueRange<float> AcceptableValues => new(5f, 50f);
                        /// <summary>
                        /// Gets the configuration definition for the distance setting.
                        /// </summary>
                        internal static ConfigDefinition Definition => new(Name, Key);
                        /// <summary>
                        /// Gets the configuration description for the distance setting.
                        /// </summary>
                        internal static ConfigDescription Description => new(Text, AcceptableValues);

                        /// <summary>
                        /// Gets the value of the distance setting.
                        /// </summary>
                        internal static float Value => !ConfigFile.TryGetEntry<float>(Definition, out var distance)
                            ? Default
                            : distance.Value;
                    }

                    /// <summary>
                    /// Configuration setting that determines the maximum number of chests allowed.
                    /// </summary>
                    internal static class ChestLimit
                    {
                        /// <summary>
                        /// The key for the chest limit setting in the configuration file.
                        /// </summary>
                        internal const string Key = nameof(ChestLimit);
                        /// <summary>
                        /// The default value for the chest limit setting.
                        /// </summary>
                        internal const int Default = 8;
                        private const string Text = "The maximum amount of chests to include. Currently more than 8 chests will break the game.";

                        /// <summary>
                        /// Gets the acceptable values for the chest limit setting.
                        /// </summary>
                        private static AcceptableValueRange<int> AcceptableValues => new(1, 8);
                        /// <summary>
                        /// Gets the configuration definition for the chest limit setting.
                        /// </summary>
                        internal static ConfigDefinition Definition => new(Name, Key);
                        /// <summary>
                        /// Gets the configuration description for the chest limit setting.
                        /// </summary>
                        internal static ConfigDescription Description => new(Text, AcceptableValues);

                        /// <summary>
                        /// Gets the value of the chest limit setting.
                        /// </summary>
                        internal static int Value => !ConfigFile.TryGetEntry<int>(Definition, out var chestLimit)
                            ? Default
                            : chestLimit.Value;
                    }
                }
            }

            /// <summary>
            /// Configuration settings for the Quick Stash feature.
            /// </summary>
            internal static class QuickStash
            {
                /// <summary>
                /// The name of the Quick Stash section.
                /// </summary>
                internal const string Name = nameof(QuickStash);
                /// <summary>
                /// Determines whether the Quick Stash feature is enabled.
                /// </summary>
                internal static bool IsEnabled => Options.Enabled.Value;

                /// <summary>
                /// Options for the Quick Stash feature.
                /// </summary>
                internal static class Options
                {
                    /// <summary>
                    /// Configuration setting that determines if the Quick Stash feature is enabled.
                    /// </summary>
                    internal static class Enabled
                    {
                        /// <summary>
                        /// The key for the enabled setting in the configuration file.
                        /// </summary>
                        internal const string Key = nameof(Enabled);
                        /// <summary>
                        /// The default value for the enabled setting.
                        /// </summary>
                        internal const bool Default = true;
                        private const string Text = "Should the 'Quick Stash' feature be enabled?";

                        /// <summary>
                        /// Gets the acceptable values for the enabled setting.
                        /// </summary>
                        private static AcceptableValueList<bool> AcceptableValues => new(true, false);
                        /// <summary>
                        /// Gets the configuration definition for the enabled setting.
                        /// </summary>
                        internal static ConfigDefinition Definition => new(Name, Key);
                        /// <summary>
                        /// Gets the configuration description for the enabled setting.
                        /// </summary>
                        internal static ConfigDescription Description => new(Text, AcceptableValues);

                        /// <summary>
                        /// Gets the value of the enabled setting.
                        /// </summary>
                        internal static bool Value => General.Options.Enabled.Value && ConfigFile.TryGetEntry<bool>(Definition, out var enabled) && enabled.Value;
                    }

                    /// <summary>
                    /// Configuration setting that determines the range for the Quick Stash feature.
                    /// </summary>
                    internal static class Distance
                    {
                        /// <summary>
                        /// The key for the distance setting in the configuration file.
                        /// </summary>
                        internal const string Key = nameof(Distance);
                        /// <summary>
                        /// The default value for the distance setting.
                        /// </summary>
                        internal const float Default = 20f;
                        private const string Text = "The range to determine chests in proximity.";

                        /// <summary>
                        /// Gets the acceptable values for the distance setting.
                        /// </summary>
                        private static AcceptableValueRange<float> AcceptableValues => new(5f, 50f);
                        /// <summary>
                        /// Gets the configuration definition for the distance setting.
                        /// </summary>
                        internal static ConfigDefinition Definition => new(Name, Key);
                        /// <summary>
                        /// Gets the configuration description for the distance setting.
                        /// </summary>
                        internal static ConfigDescription Description => new(Text, AcceptableValues);

                        /// <summary>
                        /// Gets the value of the distance setting.
                        /// </summary>
                        internal static float Value => !ConfigFile.TryGetEntry<float>(Definition, out var distance)
                            ? Default
                            : distance.Value;
                    }
                }

                /// <summary>
                /// Keybindings for the Quick Stash feature.
                /// </summary>
                internal static class KeyBinds
                {
                    internal const string QuickStashKeyBindName = KeybindPrefix + "-" + Name;
                    internal const string QuickStashKeyBindingDescription = "Quick Stash Items";
                    /// <summary>
                    /// Gets the default key for the Quick Stash action.
                    /// </summary>
                    internal static KeyboardKeyCode DefaultQuickStashKey => KeyboardKeyCode.A;
                    /// <summary>
                    /// Gets the default modifier key for the Quick Stash action.
                    /// </summary>
                    internal static ModifierKey DefaultQuickStashKModifier => ModifierKey.Control;
                }
            }
        }
    }
}