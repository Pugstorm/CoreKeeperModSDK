using System;
using System.Diagnostics.CodeAnalysis;

namespace CK_QOL.Core.Features
{
	/// <summary>
	///		A base class for all features, implementing the IFeature interface and providing thread-safe singleton behavior.
	/// </summary>
	/// <typeparam name="TFeature">The type of the feature inheriting from FeatureBase.</typeparam>
	internal abstract class FeatureBase<TFeature>: IFeature
		where TFeature : FeatureBase<TFeature>, new()
	{
		#region Singleton

		[SuppressMessage("ReSharper", "InconsistentNaming")]
		private static readonly Lazy<TFeature> _instance = new(() => new TFeature());

		public static TFeature Instance => _instance.Value;

		[SuppressMessage("ReSharper", "EmptyConstructor")]
		protected FeatureBase()
		{
		}

		#endregion Singleton

		#region IFeature

		public abstract string Name { get; }
		public abstract string DisplayName { get; }
		public abstract string Description { get; }
		public abstract FeatureType FeatureType { get; }
		public bool IsEnabled { get; protected set; }

		#endregion IFeature
		
		/// <summary>
		///		Determines whether the feature can be executed.
		///		Override this method in derived classes to provide additional checks or conditions.
		/// </summary>
		/// <remarks>		
		///		When overriding, the base method <see cref="CanExecute"/> should be called to ensure the base conditions are respected.
		/// </remarks>
		public virtual bool CanExecute() => IsEnabled;

		/// <summary>
		///		Executes the feature's logic.
		///		Override this method in derived classes to implement specific execution logic for the feature.
		/// </summary>
		/// <remarks>
		///		When overriding, <see cref="CanExecute"/> should be called to ensure the base conditions are respected.
		/// </remarks>
		public virtual void Execute()
		{
		}

		/// <summary>
		///		Executes the feature's update logic.
		///		Override this method in derived classes to implement specific update logic for the feature.
		/// </summary>
		/// <remarks>
		///		When overriding, <see cref="CanExecute"/> should be called to ensure the base conditions are respected.
		/// </remarks>
		public virtual void Update()
		{
		}
	}
}