using NUnit.Framework.Constraints;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Tests
{
    static class UIToolkitTestHelper
    {
        internal class Is : NUnit.Framework.Is
        {
            public static DisplayConstraint Display(DisplayStyle expected)
            {
                return new DisplayConstraint(expected);
            }
        }

        internal class DisplayConstraint : Constraint
        {
            readonly DisplayStyle m_Expected;

            public DisplayConstraint(DisplayStyle expected)
            {
                m_Expected = expected;
                Description = expected.ToString();
            }

            public override ConstraintResult ApplyTo(object actual)
            {
                if (actual is VisualElement visual)
                    return new ConstraintResult(this, visual.resolvedStyle.display, visual.resolvedStyle.display == m_Expected);
                return new ConstraintResult(this, $"not a {nameof(VisualElement)}", false);
            }
        }
    }


}
