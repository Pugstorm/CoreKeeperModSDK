using CK_QOL_Collection.Core;
using CK_QOL_Collection.Core.Configuration;
using Rewired;

namespace CK_QOL_Collection.Features.QuickStash.KeyBinds
{
	/// <summary>
	///     Represents the key bindings for the 'Quick Stash' feature.
	/// </summary>
	internal class QuickStashKeyBind : IFeatureKeyBind
	{
		private const string FeatureName = nameof(QuickStash);

		/// <summary>
		///     Gets the name of the key binding, prefixed by the mod's key binding prefix.
		/// </summary>
		public string KeyBindName => $"{ModSettings.KeyBindPrefix}-{FeatureName}";

		/// <summary>
		///     Gets the description of the key binding for display purposes.
		/// </summary>
		public string KeyBindDescription => "Quick Stash Items";

		/// <summary>
		///     Gets the default key for the Quick Stash action.
		/// </summary>
		public KeyboardKeyCode DefaultKey => KeyboardKeyCode.A;

		/// <summary>
		///     Gets the default modifier key for the Quick Stash action.
		/// </summary>
		public ModifierKey DefaultModifier => ModifierKey.Control;
	}
}