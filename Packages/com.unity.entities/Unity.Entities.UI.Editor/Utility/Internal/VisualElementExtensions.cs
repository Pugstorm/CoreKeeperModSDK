using System.Collections.Generic;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    static class VisualElementExtensions
    {
        public static void ForceUpdateBindings(this VisualElement element)
        {
            var list = ListPool<IBinding>.Get();
            try
            {
                PopulateBindings(element, list);

                foreach (var binding in list)
                {
                    binding.PreUpdate();
                }

                foreach (var binding in list)
                {
                    binding.Update();
                }
            }
            finally
            {
                ListPool<IBinding>.Release(list);
            }
        }

        static void PopulateBindings(this VisualElement element, List<IBinding> list)
        {
            if (element is IBindable bindable && null != bindable.binding)
                list.Add(bindable.binding);

            if (element is IBinding binding)
                list.Add(binding);

            foreach (var child in element.Children())
            {
                PopulateBindings(child, list);
            }
        }
    }
}
