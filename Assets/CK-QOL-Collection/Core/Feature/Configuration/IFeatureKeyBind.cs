using Rewired;

namespace CK_QOL_Collection.Core.Feature.Configuration
{
	/// <summary>
	///     Represents the interface for key bindings of a feature.
	/// </summary>
	internal interface IFeatureKeyBind
	{
		/// <summary>
		///     Gets the name of the key binding.
		/// </summary>
		string KeyBindName { get; }

		/// <summary>
		///     Gets the description of the key binding.
		/// </summary>
		string KeyBindDescription { get; }

		/// <summary>
		///     Gets the default key code for the key binding.
		/// </summary>
		KeyboardKeyCode DefaultKey { get; }

		/// <summary>
		///     Gets the default modifier key for the key binding.
		/// </summary>
		ModifierKey DefaultModifier { get; }
	}
}