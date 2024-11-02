using JetBrains.Annotations;
using UnityEngine.UIElements;

namespace Unity.Editor.Bridge
{
    class VisualElementBridge : VisualElement
    {
        [UsedImplicitly]
        internal override void OnViewDataReady() => base.OnViewDataReady();

        protected new string GetFullHierarchicalViewDataKey() => base.GetFullHierarchicalViewDataKey();
        protected new void OverwriteFromViewData(object obj, string key) => base.OverwriteFromViewData(obj, key);
        protected new void SaveViewData() => base.SaveViewData();
        
        protected new float scaledPixelsPerPoint => base.scaledPixelsPerPoint;
        
        protected new bool isCompositeRoot
        {
            get => base.isCompositeRoot;
            set => base.isCompositeRoot = value;
        }

        internal new int pseudoStates
        {
            get => (int) base.pseudoStates;
            set => base.pseudoStates = (PseudoStates) value;
        }
    }
}
