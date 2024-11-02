using Unity.Properties;

namespace Unity.Entities.UI
{
    interface IContextElement
    {
        PropertyPath Path { get; }

        void SetContext(BindingContextElement root, PropertyPath path);
        void OnContextReady();
    }
}
