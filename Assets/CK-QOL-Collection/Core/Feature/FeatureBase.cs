using System.Collections.Generic;
using CK_QOL_Collection.Core.Feature.Configuration;

namespace CK_QOL_Collection.Core.Feature
{
    /// <summary>
    ///     Provides a base implementation for features within the CK_QOL_Collection mod.
    ///     Implements the <see cref="IFeature" /> interface and provides default behavior for feature execution and updating.
    /// </summary>
    internal abstract class FeatureBase : IFeature
    {
        /// <summary>
        ///     Gets the configuration object for this feature.
        /// </summary>
        protected IFeatureConfiguration Configuration { get; }

        /// <summary>
        ///     Gets or sets the list of key bindings for this feature.
        /// </summary>
        protected List<IFeatureKeyBind> KeyBinds { get; private set; } = new();

        /// <summary>
        ///     Initializes a new instance of the <see cref="FeatureBase" /> class.
        /// </summary>
        /// <param name="name">The name of the feature.</param>
        protected FeatureBase(string name)
        {
            Name = name;
            Configuration = ConfigurationManager.GetFeatureConfiguration(name);
        }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public bool IsEnabled => Configuration is { Enabled: true };

        /// <inheritdoc />
        public virtual bool CanExecute() => IsEnabled;

        /// <inheritdoc />
        public virtual void Execute()
        {
        }

        /// <inheritdoc />
        public virtual void Update()
        {
        }
    }
}