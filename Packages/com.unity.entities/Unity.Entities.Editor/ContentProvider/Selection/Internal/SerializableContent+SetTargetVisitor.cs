using System.Collections.Generic;
using Unity.Properties;

namespace Unity.Entities.UI
{
    partial class DisplayContent
    {
        class SetTargetVisitor : ConcreteTypeVisitor
        {
            public SerializableContent Content;
            public DynamicInspectionContext InspectionContext;
            public PropertyElement Inspector;

            protected override void VisitContainer<TContainer>(ref TContainer target)
            {
                if (Inspector.TryGetTarget(out TContainer currentTarget)
                    && TypeTraits<TContainer>.CanBeNull
                    && EqualityComparer<TContainer>.Default.Equals(currentTarget, target))
                    return;

                Inspector.SetTarget(target);

                // In the inspector, the size of the content will depend on the size of the children (there is
                // intentionally no "expand to take all available size"), while in an editor window, you can expand your
                // content to take the full size of the window. For this window, users can choose between the default
                // window behaviour and the inspector behaviour. To support this, we will force the root to fully expand.
                // When a custom inspector is provided, we need to adjust the CustomInspectorElement to also fully expand.
                if (Inspector.childCount == 1
                    && Inspector[0] is CustomInspectorElement customInspector)
                {
                    customInspector.style.flexGrow = 1;
                }

                if (InspectionContext.ApplyInspectorStyling)
                    StylingUtility.AlignInspectorLabelWidth(Inspector);

                Content.Save();
            }
        }
    }
}
