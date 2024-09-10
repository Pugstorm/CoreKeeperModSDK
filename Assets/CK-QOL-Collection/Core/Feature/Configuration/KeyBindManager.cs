using System;
using System.Collections.Generic;
using CK_QOL_Collection.Features.EatableBinding.KeyBinds;
using CK_QOL_Collection.Features.HealableBinding.KeyBinds;
using CK_QOL_Collection.Features.QuickStash.KeyBinds;
using CoreLib.RewiredExtension;

namespace CK_QOL_Collection.Core.Feature.Configuration
{
    /// <summary>
    ///     Manages all key bindings for the CK QOL Collection mod.
    /// </summary>
    internal class KeyBindManager
    {
        /// <summary>
        ///     A dictionary to store key bindings by their names.
        /// </summary>
        private readonly Dictionary<string, IFeatureKeyBind> _keyBinds = new();

        #region Singleton

        /// <summary>
        ///     Holds the singleton instance of <see cref="KeyBindManager" />.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private static readonly Lazy<KeyBindManager> _instance = new(() => new KeyBindManager());

        /// <summary>
        ///     Initializes a new instance of the <see cref="KeyBindManager" /> class.
        ///     The constructor is private to prevent instantiation outside of this class.
        /// </summary>
        private KeyBindManager()
        {
            RegisterKeyBind(new QuickStashKeyBind());
            RegisterKeyBind(new EatableBindingKeyBind());
            RegisterKeyBind(new HealableBindingKeyBind());
        }

        /// <summary>
        ///     Gets the singleton instance of the <see cref="KeyBindManager" />.
        /// </summary>
        internal static KeyBindManager Instance => _instance.Value;

        #endregion Singleton

        /// <summary>
        ///     Initializes all key bindings for the mod.
        /// </summary>
        public static KeyBindManager Initialize()
        {
            return Instance;
        }

        #region KeyBind Management
        
        /// <summary>
        ///     Registers a new key binding.
        /// </summary>
        /// <param name="keyBind">The key binding to register.</param>
        private void RegisterKeyBind(IFeatureKeyBind keyBind)
        {
            if (keyBind == null)
            {
                return;
            }
            
            _keyBinds.TryAdd(keyBind.KeyBindName, keyBind);
            RewiredExtensionModule.AddKeybind(keyBind.KeyBindName, keyBind.KeyBindDescription, keyBind.DefaultKey, keyBind.DefaultModifier);
        }

        /// <summary>
        ///     Retrieves a key binding by its name, automatically appending the prefix.
        /// </summary>
        /// <param name="featureName">The name of the feature associated with the key binding.</param>
        /// <returns>The <see cref="IFeatureKeyBind"/> instance if found; otherwise, null.</returns>
        internal IFeatureKeyBind GetKeyBind(string featureName)
        {
            _keyBinds.TryGetValue($"{ModSettings.KeyBindPrefix}-{featureName}", out var keyBind);
            
            return keyBind;
        }
        
        /// <summary>
        ///     Gets a specific key binding by its type.
        /// </summary>
        /// <typeparam name="T">The type of the key binding.</typeparam>
        /// <returns>The corresponding key binding instance of type <typeparamref name="T" />.</returns>
        internal T GetKeyBind<T>()
            where T : class, IFeatureKeyBind
        {
            foreach (var keyBind in _keyBinds.Values)
            {
                if (keyBind is T typedKeyBind)
                {
                    return typedKeyBind;
                }
            }

            return null;
        }

        /// <summary>
        ///     Retrieves all registered key bindings.
        /// </summary>
        /// <returns>A collection of all <see cref="IFeatureKeyBind"/> instances.</returns>
        internal IEnumerable<IFeatureKeyBind> GetAllKeyBinds() => _keyBinds.Values;
        
        #endregion KeyBind Management
    }
}