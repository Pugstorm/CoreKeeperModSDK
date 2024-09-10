using CK_QOL_Collection.Core;
using CK_QOL_Collection.Core.Feature.Configuration;
using Rewired;

namespace CK_QOL_Collection.Features.EatableBinding.KeyBinds
{
	/// <summary>
	///     Represents the key bindings for the 'Eatable Binding' feature.
	/// </summary>
	internal class EatableBindingKeyBind : IFeatureKeyBind
	{
		private const string FeatureName = nameof(EatableBinding);

		/// <summary>
		///     Gets the name of the key binding, prefixed by the mod's key binding prefix.
		/// </summary>
		public string KeyBindName => $"{ModSettings.KeyBindPrefix}-{FeatureName}";

		/// <summary>
		///     Gets the description of the key binding for display purposes.
		/// </summary>
		public string KeyBindDescription => "Eatable Binding Items";

		/// <summary>
		///     Gets the default key for the Eatable Binding action.
		/// </summary>
		public KeyboardKeyCode DefaultKey => KeyboardKeyCode.G;

		/// <summary>
		///     Gets the default modifier key for the Eatable Binding action.
		/// </summary>
		public ModifierKey DefaultModifier => ModifierKey.None;
	}
}