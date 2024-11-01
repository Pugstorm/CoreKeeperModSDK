using System;
using JetBrains.Annotations;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
#if UNITY_2023_3_OR_NEWER
    [UxmlElement]
#endif
    partial class SpinnerElement : VisualElement
    {
#if !UNITY_2023_3_OR_NEWER
        [UsedImplicitly]
        class SpinnerElementFactory : UxmlFactory<SpinnerElement, SpinnerElementTraits> { }

        [UsedImplicitly]
        class SpinnerElementTraits : UxmlTraits
        {
        }
#endif

        static readonly VisualElementTemplate k_Template = new(Resources.PackageId, "Spinner/spinner");

        readonly IVisualElementScheduledItem m_ScheduledItem;

        int m_Index;

        public SpinnerElement()
        {
            k_Template.AddStyles(this);

            AddToClassList("spinner");
            AddToClassList("spinner-icons");
            AddToClassList(GetSpinnerClass(m_Index));
            m_ScheduledItem = schedule.Execute(UpdateSpinner).Every(Convert.ToInt64(1000.0f / 12.0f));
        }

        static string GetSpinnerClass(int index)
            => $"spinner-background-image-{index}";

        void UpdateSpinner(TimerState obj)
        {
            RemoveFromClassList(GetSpinnerClass(m_Index));
            m_Index = (m_Index + 1) % 12;
            AddToClassList(GetSpinnerClass(m_Index));
        }

        public void Resume()
        {
            m_ScheduledItem.Resume();
        }

        public void Pause()
        {
            m_ScheduledItem.Pause();
        }
    }
}
