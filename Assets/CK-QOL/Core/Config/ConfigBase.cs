using CK_QOL.Core.Features;
using CoreLib.Data.Configuration;

namespace CK_QOL.Core.Config
{
	internal abstract class ConfigBase
	{
		protected static ConfigFile Config { get; private set; }
        
		internal static ConfigFile Create(IFeature feature) => Config = new ConfigFile($"{ModSettings.ShortName}/{feature.Name}.cfg", true, Entry.ModInfo);
	}
}