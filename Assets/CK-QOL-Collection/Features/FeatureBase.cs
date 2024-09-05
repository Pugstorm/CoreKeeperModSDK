namespace CK_QOL_Collection.Features
{
    /// <summary>
    /// Provides a base implementation for features within the CK_QOL_Collection mod.
    /// Implements the <see cref="IFeature"/> interface and provides default behavior for feature execution and updating.
    /// </summary>
    internal abstract class FeatureBase : IFeature
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureBase"/> class.
        /// </summary>
        /// <param name="name">The name of the feature.</param>
        /// <param name="isEnabled">A value indicating whether the feature is enabled.</param>
        protected FeatureBase(string name, bool isEnabled)
        {
            Name = name;
            IsEnabled = isEnabled;
        }
        
        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public bool IsEnabled { get; }

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