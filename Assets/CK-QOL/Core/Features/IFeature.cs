namespace CK_QOL.Core.Features
{
	internal interface IFeature
	{
		string Name { get; }
		string DisplayName { get; }
		string Description { get; }
		FeatureType FeatureType { get; }
		bool IsEnabled { get; }

		bool CanExecute();
		void Execute();
		void Update();
	}
}