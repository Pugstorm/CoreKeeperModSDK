using JetBrains.Annotations;
using Unity.Entities.UI;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    readonly struct Header
    {
        public readonly string Name;
        public readonly string IconClass;

        public Header(string name, string iconClass)
        {
            Name = name;
            IconClass = iconClass;
        }
    }

    [UsedImplicitly]
    class HeaderInspector : PropertyInspector<Header>
    {
        public override VisualElement Build()
        {
            var root = new VisualElement();
            Resources.Templates.ContentProvider.Header.AddStyles(root);
            root.AddToClassList("header__container");

            var icon = new VisualElement();
            icon.AddToClassList("content__icons");
            icon.AddToClassList("header__icon");
            icon.AddToClassList(Target.IconClass);
            root.Add(icon);

            var name = new Label {text = Target.Name};
            name.AddToClassList("header__name");
            root.Add(name);

            return root;
        }
    }
}
