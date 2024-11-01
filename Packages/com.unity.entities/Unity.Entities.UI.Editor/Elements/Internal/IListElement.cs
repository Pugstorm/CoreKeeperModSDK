using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    class IListElement<TList, TElement> : NullableFoldout<TList>, ICustomStyleApplier
        where TList : IList<TElement>
    {
        readonly IntegerField m_Size;
        readonly Button m_AddItemButton;
        internal readonly VisualElement m_ContentRoot;
        readonly PaginationElement m_PaginationElement;

        bool UsesPagination { get; set; }

        public IListElement()
        {
            Resources.Templates.ListElement.Clone(this);
            Resources.Templates.ListElementDefaultStyling.AddStyles(this);
            binding = this;

            m_Size = new IntegerField();
            m_Size.AddToClassList(UssClasses.ListElement.Size);
            m_Size.RegisterValueChangedCallback(CountChanged);
            m_Size.RegisterCallback<KeyDownEvent>(TrapKeys);
            m_Size.isDelayed = true;

            var toggle = this.Q<Toggle>();
            var toggleInput = toggle.Q(className: UssClasses.Unity.ToggleInput);
            toggleInput.AddToClassList(UssClasses.ListElement.ToggleInput);
            toggle.Add(m_Size);

            m_AddItemButton = new Button(OnAddItem)
            {
                text = "+ Add Element"
            };
            m_AddItemButton.AddToClassList(UssClasses.ListElement.AddItemButton);

            m_ContentRoot = new VisualElement();
            m_ContentRoot.name = "platforms-list-content";
            m_PaginationElement = new PaginationElement();
            Add(m_PaginationElement);
            Add(m_ContentRoot);
            Add(m_AddItemButton);
        }

        static void TrapKeys(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
#if UNITY_2023_2_OR_NEWER
                evt.StopPropagation();
                (evt.target as VisualElement)?.focusController?.IgnoreEvent(evt);
#else
                    evt.PreventDefault();
#endif
            }
        }

        public override void OnContextReady()
        {
            var list = GetValue();
            var property = GetProperty();
            if ((property?.IsReadOnly ?? true) && list.IsReadOnly)
            {
                m_Size.SetEnabled(false);
                m_AddItemButton.SetEnabled(false);
            }

            UsesPagination = HasAttribute<PaginationAttribute>();
            if (!UsesPagination)
            {
                m_PaginationElement.Enabled = false;
            }

            var pagination = GetAttribute<PaginationAttribute>();
            if (null == pagination)
                return;

            m_PaginationElement.OnChanged += () =>
            {
                UiPersistentState.SetPaginationState(Root.GetTargetType(), Path, m_PaginationElement.PaginationSize,
                    m_PaginationElement.CurrentPage);
                Reload();
            };

            m_PaginationElement.SetPaginationSizes(pagination.Sizes);
            m_PaginationElement.AutoHide = pagination.AutoHide;
            var paginationData = UiPersistentState.GetPaginationState(Root.GetTargetType(), Path);
            if (!EqualityComparer<UiPersistentState.PaginationData>.Default.Equals(paginationData, default))
            {
                m_PaginationElement.TotalCount = GetValue()?.Count ?? 0;
                m_PaginationElement.SetPaginationSize(paginationData.PaginationSize);
                m_PaginationElement.GoToPage(paginationData.CurrentPage);
            }
        }

        public override void Reload(IProperty property)
        {
            m_ContentRoot.Clear();

            var list = GetValue();
            if (EqualityComparer<TList>.Default.Equals(list, default))
                return;

            m_PaginationElement.Update(list.Count);

            if (m_Size.focusController?.focusedElement != m_Size)
            {
                m_Size.isDelayed = false;
                m_Size.SetValueWithoutNotify(list.Count);
                m_Size.isDelayed = true;
            }

            var startIndex = 0;
            var endIndex = list.Count;

            if (UsesPagination)
            {
                startIndex = m_PaginationElement.StartIndex;
                endIndex = m_PaginationElement.EndIndex;
            }

            for (var i = startIndex; i < endIndex; ++i)
            {
                var index = i;

                var atIndexPath = PropertyPath.AppendIndex(Path, index);
                var root = new VisualElement();
                Root.VisitAtPath(atIndexPath, root);
                MakeListElement(root, index);
                m_ContentRoot.Add(root);
            }
        }

        void CountChanged(ChangeEvent<int> evt)
        {
            evt.StopImmediatePropagation();
#if UNITY_2023_2_OR_NEWER
            (evt.target as VisualElement)?.focusController?.IgnoreEvent(evt);
#else
            evt.PreventDefault();
#endif
            var count = evt.newValue;
            if (count < 0)
            {
                m_Size.SetValueWithoutNotify(0);
                count = 0;
            }

            var iList = GetValue();
            if (EqualityComparer<TList>.Default.Equals(iList, default))
                return;

            var constructContext = GetAttribute<CreateElementOnAddAttribute>();

            switch (iList)
            {
                case TElement[] array:
                    var newArray = new TElement[count];
                    for (var i = 0; i < Math.Min(array.Length, count); ++i)
                    {
                        newArray[i] = array[i];
                    }

                    for (var i = array.Length; i < newArray.Length; ++i)
                    {
                        newArray[i] = CreateInstance(constructContext);
                    }

                    Root.SetValue(Path, newArray);
                    break;
                default:
                    while (iList.Count > count)
                    {
                        iList.RemoveAt(iList.Count - 1);
                    }

                    while (iList.Count < count)
                    {
                        iList.Add(CreateInstance(constructContext));
                    }

                    break;
            }

            Root.NotifyChanged(Path);
            Reload();
        }

        static TElement CreateInstance(CreateElementOnAddAttribute context)
        {
            if (null == context)
                return default;

            var type = context.Type;
            return null == type
                ? TypeUtility.Instantiate<TElement>()
                : TypeUtility.Instantiate<TElement>(type);
        }

        protected override void OnUpdate()
        {
            var list = GetValue();
            if (EqualityComparer<TList>.Default.Equals(list, default))
                return;

            if (!UsesPagination)
            {
                if (list.Count != m_ContentRoot.childCount)
                {
                    Reload();
                }
            }
            else
            {
                m_PaginationElement.Update(list.Count);

                var startIndex = m_PaginationElement.StartIndex;
                var endIndex = m_PaginationElement.EndIndex;

                if (m_PaginationElement.PaginationSize != m_ContentRoot.childCount)
                {
                    if (list.Count > 0 && m_PaginationElement.CurrentPage != m_PaginationElement.LastPage)
                    {
                        Reload();
                    }
                }

                for (var i = startIndex; i < endIndex; ++i)
                {
                    if (m_ContentRoot[i - startIndex].ClassListContains(UssClasses.ListElement.MakeListItem(i)))
                    {
                        continue;
                    }

                    Reload();
                    break;
                }
            }
        }

        void OnAddItem()
        {
            var iList = GetValue();
            if (EqualityComparer<TList>.Default.Equals(iList, default))
                return;

            var item = CreateInstance(GetAttribute<CreateElementOnAddAttribute>());

            switch (iList)
            {
                case TElement[] array:
                    Root.SetValue(Path, ArrayUtility.InsertAt(array, array.Length, item));
                    break;
                default:
                    iList.Add(item);
                    break;
            }

            Root.NotifyChanged(Path);
            m_PaginationElement.TotalCount = iList.Count;
            m_PaginationElement.GoToLastPage();
            Reload();
        }

        void OnRemoveItem(int index)
        {
            var typedIList = GetValue();
            if (EqualityComparer<TList>.Default.Equals(typedIList, default))
                return;

            switch (typedIList)
            {
                case TElement[] array:
                    Root.SetValue(Path, ArrayUtility.RemoveAt(array, index));
                    break;
                default:
                    typedIList.RemoveAt(index);
                    break;
            }

            Root.NotifyChanged(Path);

            m_PaginationElement.TotalCount = typedIList.Count;
            Reload();
        }

        void Swap(int index, int newIndex)
        {
            var iList = GetValue();
            if (null == iList)
                return;

            var temp = iList[index];
            iList[index] = iList[newIndex];
            iList[newIndex] = temp;
            Root.NotifyChanged(Path);
            Reload();
        }

        public void ApplyStyleAtPath(PropertyPath propertyPath)
        {
            var index = 0;
            for (; index < Path.Length; ++index)
            {
                if (propertyPath.Length == index)
                {
                    return;
                }

                if (!Path[index].Equals(propertyPath[index]))
                {
                    return;
                }
            }

            if (!propertyPath[index].IsIndex)
            {
                return;
            }

            var itemIndex = propertyPath[index].Index;
            if (UsesPagination)
            {
                itemIndex -= m_PaginationElement.StartIndex;
            }

            var current = m_ContentRoot[itemIndex];
            current.Q<Button>(className: UssClasses.ListElement.RemoveItemButton)?.RemoveFromHierarchy();

            MakeListElement(current, itemIndex);
        }

        void MakeListElement(VisualElement root, int index)
        {
            root.AddToClassList(UssClasses.ListElement.ItemContainer);
            root.AddToClassList(UssClasses.Variables);
            root.AddToClassList(UssClasses.ListElement.MakeListItem(index));
            var element = root[0];

            if (null == element)
                return;

            VisualElement toRemoveParent;
            VisualElement contextMenuParent;

            if (element is Foldout foldout)
            {
                foldout.AddToClassList(UssClasses.ListElement.Item);
                var toggle = foldout.Q<Toggle>();
                toggle.AddToClassList(UssClasses.ListElement.ItemFoldout);
                contextMenuParent = foldout.Q<VisualElement>(className: UssClasses.Unity.ToggleInput);

                toRemoveParent = toggle;
                foldout.contentContainer.AddToClassList(UssClasses.ListElement.ItemContent);
                root.style.flexDirection = new StyleEnum<FlexDirection>(StyleKeyword.Auto);
            }
            else
            {
                toRemoveParent = root;
                contextMenuParent = root.Q<Label>();
                element.AddToClassList(UssClasses.ListElement.ItemNoFoldout);
                root.style.flexDirection = FlexDirection.Row;
            }

            var list = GetValue();
            var property = GetProperty();
            var disableRemove = property?.IsReadOnly ?? true && list.IsReadOnly;

            contextMenuParent.AddManipulator(
                new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("Delete", action => { OnRemoveItem(index); },
                        disableRemove ? DropdownMenuAction.Status.Disabled : DropdownMenuAction.Status.Normal);
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction("Move Up", action => { Swap(index, index - 1); },
                        list.Count > 1 && index - 1 >= 0
                            ? DropdownMenuAction.Status.Normal
                            : DropdownMenuAction.Status.Disabled);
                    evt.menu.AppendAction("Move Down", action => { Swap(index, index + 1); },
                        list.Count > 1 && index + 1 < list.Count
                            ? DropdownMenuAction.Status.Normal
                            : DropdownMenuAction.Status.Disabled);
                }));


            var button = new Button();
            button.AddToClassList(UssClasses.ListElement.RemoveItemButton);
            button.clickable.clicked += () => { OnRemoveItem(index); };
            button.SetEnabled(!disableRemove);
            toRemoveParent.Add(button);
        }
    }
}
