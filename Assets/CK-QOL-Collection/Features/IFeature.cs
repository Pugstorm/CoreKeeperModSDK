namespace CK_QOL_Collection.Features
{
    /// <summary>
    /// Represents a feature within the CK_QOL_Collection mod.
    /// Defines common properties and methods that all features should implement.
    /// </summary>
    internal interface IFeature
    {
        /// <summary>
        /// Gets the name of the feature.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets a value indicating whether the feature is enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Determines whether the feature can execute its main logic.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the feature can execute; otherwise, <see langword="false"/>.
        /// </returns>
        bool CanExecute();

        /// <summary>
        /// Executes the main logic of the feature.
        /// This method is intended to be called to perform the primary function of the feature.
        /// </summary>
        void Execute();

        /// <summary>
        /// Updates the state or data related to the feature.
        /// This method is intended to be called periodically to refresh or maintain the feature's state.
        /// </summary>
        void Update();
    }
}