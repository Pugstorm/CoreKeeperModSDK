using System;
using System.Collections.Generic;
using CK_QOL_Collection.Features.CraftingRange;
using CK_QOL_Collection.Features.ItemPickUpNotifier;
using CK_QOL_Collection.Features.NoDeathPenalty;
using CK_QOL_Collection.Features.QuickStash;

namespace CK_QOL_Collection.Core
{
    /// <summary>
    ///     Manages the various features within the CK QOL Collection.
    ///     This class follows the singleton design pattern to ensure only one instance is used throughout the application.
    /// </summary>
    internal class FeatureManager
	{
        /// <summary>
        ///     A dictionary to hold all features managed by the <see cref="FeatureManager" />.
        /// </summary>
        private readonly Dictionary<string, IFeature> _features = new();

		#region Singleton

        /// <summary>
        ///     Holds the singleton instance of <see cref="FeatureManager" />.
        /// </summary>
        // ReSharper disable once InconsistentNaming
		private static readonly Lazy<FeatureManager> _instance = new(() => new FeatureManager());

        /// <summary>
        ///     Initializes a new instance of the <see cref="FeatureManager" /> class.
        ///     The constructor is private to prevent instantiation outside of this class.
        /// </summary>
        private FeatureManager()
		{
			RegisterFeature(new CraftingRangeFeature());
			RegisterFeature(new QuickStashFeature());
			RegisterFeature(new NoDeathPenaltyFeature());
			RegisterFeature(new ItemPickUpNotifierFeature());
		}

        /// <summary>
        ///     Gets the singleton instance of the <see cref="FeatureManager" />.
        /// </summary>
        internal static FeatureManager Instance => _instance.Value;

		#endregion Singleton

		/// <summary>
		///     Initializes all features for the mod.
		/// </summary>
		public static FeatureManager Initialize()
		{
			return Instance;
		}
		
		/// <summary>
		///     Updates all managed features by calling their <see cref="IFeature.Update" /> method.
		/// </summary>
		public void Update()
		{
			foreach (var feature in _features.Values)
			{
				feature.Update();
			}
		}
		
		#region Feature Management

        /// <summary>
        ///     Registers a feature with the feature manager.
        /// </summary>
        /// <param name="feature">The feature to register.</param>
        private void RegisterFeature(IFeature feature)
		{
			if (feature != null)
			{
				_features.TryAdd(feature.Name, feature);
			}
		}

        /// <summary>
        ///     Gets a specific feature by its name.
        /// </summary>
        /// <param name="featureName">The name of the feature.</param>
        /// <returns>The corresponding <see cref="IFeature" /> instance.</returns>
        internal IFeature GetFeature(string featureName)
		{
			return _features.GetValueOrDefault(featureName);
		}

        /// <summary>
        ///     Gets a specific feature by its type.
        /// </summary>
        /// <typeparam name="T">The type of the feature.</typeparam>
        /// <returns>The corresponding feature instance of type <typeparamref name="T" />.</returns>
        internal T GetFeature<T>()
			where T : class, IFeature
		{
			foreach (var feature in _features.Values)
			{
				if (feature is T typedFeature)
				{
					return typedFeature;
				}
			}

			return null;
		}
        
		/// <summary>
		///     Retrieves all features managed by the <see cref="FeatureManager" />.
		/// </summary>
		/// <returns>An enumerable collection of all registered <see cref="IFeature" /> instances.</returns>
		internal IEnumerable<IFeature> GetAllFeatures() => _features.Values;

		#endregion Feature Management
	}
}