using CK_QOL_Collection.Core;
using CK_QOL_Collection.Core.Feature.Configuration;
using Rewired;

namespace CK_QOL_Collection.Features.HealableBinding.KeyBinds
{
	/// <summary>
	///     Represents the key bindings for the 'Healable Binding' feature.
	/// </summary>
	internal class HealableBindingKeyBind : IFeatureKeyBind
	{
		private const string FeatureName = nameof(HealableBinding);

		/// <summary>
		///     Gets the name of the key binding, prefixed by the mod's key binding prefix.
		/// </summary>
		public string KeyBindName => $"{ModSettings.KeyBindPrefix}-{FeatureName}";

		/// <summary>
		///     Gets the description of the key binding for display purposes.
		/// </summary>
		public string KeyBindDescription => "Healable Binding Items";

		/// <summary>
		///     Gets the default key for the Healable Binding action.
		/// </summary>
		public KeyboardKeyCode DefaultKey => KeyboardKeyCode.F;

		/// <summary>
		///     Gets the default modifier key for the Healable Binding action.
		/// </summary>
		public ModifierKey DefaultModifier => ModifierKey.None;
	}
}