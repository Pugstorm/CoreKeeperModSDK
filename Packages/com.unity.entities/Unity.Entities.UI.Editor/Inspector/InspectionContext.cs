namespace Unity.Entities.UI
 {
     /// <summary>
     /// Base type to define an inspection context that can be passed to a <see cref="BindingContextElement"/>.
     /// </summary>
     internal abstract class InspectionContext
     {
         /// <summary>
         /// Returns the name of the context.
         /// </summary>
         public virtual string Name => GetType().Name;
     }
 }
