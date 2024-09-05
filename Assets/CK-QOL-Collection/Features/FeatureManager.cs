using System;
using System.Collections.Generic;

namespace CK_QOL_Collection.Features
{
    /// <summary>
    /// Manages the various features within the CK_QOL_Collection.
    /// This class follows the singleton design pattern to ensure only one instance is used throughout the application.
    /// </summary>
    internal class FeatureManager
    {
        #region Singleton

        // ReSharper disable once InconsistentNaming
        /// <summary>
        /// Holds the singleton instance of <see cref="FeatureManager"/>.
        /// </summary>
        private static readonly Lazy<FeatureManager> _instance = new(() => new FeatureManager());

        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureManager"/> class.
        /// The constructor is private to prevent instantiation outside of this class.
        /// Initializes the features using lazy loading and adds them to the feature list.
        /// </summary>
        private FeatureManager()
        {
            _craftingRange = new Lazy<CraftingRange.Feature>(() => new CraftingRange.Feature());
            _quickStash = new Lazy<QuickStash.Feature>(() => new QuickStash.Feature());
            
            _features.Add(CraftingRange);
            _features.Add(QuickStash);
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="FeatureManager"/>.
        /// </summary>
        internal static FeatureManager Instance => _instance.Value;

        #endregion Singleton
        
        #region Feature Instances
        
        /// <summary>
        /// A lazily initialized <see cref="Features.CraftingRange.Feature"/> instance representing the crafting range feature.
        /// </summary>
        private readonly Lazy<CraftingRange.Feature> _craftingRange;

        /// <summary>
        /// Gets the <see cref="Features.CraftingRange.Feature"/> instance that manages crafting range functionality.
        /// </summary>
        internal CraftingRange.Feature CraftingRange => _craftingRange.Value;
        
        /// <summary>
        /// A lazily initialized <see cref="Features.QuickStash.Feature"/> instance representing the quick stash feature.
        /// </summary>
        private readonly Lazy<QuickStash.Feature> _quickStash;

        /// <summary>
        /// Gets the <see cref="Features.QuickStash.Feature"/> instance that manages quick stash functionality.
        /// </summary>
        internal QuickStash.Feature QuickStash => _quickStash.Value;
        
        #endregion Feature Instances
        
        /// <summary>
        /// A list of all features managed by the <see cref="FeatureManager"/>.
        /// </summary>
        private readonly List<IFeature> _features = new();

        /// <summary>
        /// Updates all managed features by calling their <see cref="IFeature.Update"/> method.
        /// </summary>
        public void Update()
        {
            foreach (var feature in _features)
            {
                feature.Update();
            }
        }
    }
}