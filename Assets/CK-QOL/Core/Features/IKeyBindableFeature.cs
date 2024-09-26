namespace CK_QOL.Core.Features
{
	/// <summary>
	///     Interface representing features that can be bound to a key for user interaction.
	/// </summary>
	public interface IKeyBindableFeature
	{
		/// <summary>
		///     Gets the key binding name for the feature.
		/// </summary>
		string KeyBindName { get; }

		/// <summary>
		///     Sets up the key binding for the feature.
		/// </summary>
		void SetupKeyBindings();
	}
}