using System;
using System.Collections.Generic;
using Unity.Properties;
using Unity.Serialization.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor
{
    static class InspectorUtility
    {
        public static InspectorSettings Settings => UserSettings<InspectorSettings>.GetOrCreate(Constants.Settings.Inspector);
        static readonly Dictionary<string, Texture2D> k_AspectIconsDict = new Dictionary<string, Texture2D>();

        static InspectorUtility()
        {
            k_AspectIconsDict.Add("RigidbodyAspect", EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_Rigidbody Icon" : "Rigidbody Icon").image as Texture2D);
            k_AspectIconsDict.Add("CameraAspect", EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_Camera Icon" : "Camera Icon").image as Texture2D);
            k_AspectIconsDict.Add("RendererAspect", EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "d_MeshRenderer Icon" : "MeshRenderer Icon").image as Texture2D);
        }

        public static void CreateComponentHeader(VisualElement parent, ComponentPropertyType type, string displayName)
        {
            Resources.Templates.Inspector.ComponentHeader.Clone(parent);
            var foldout = parent.Q<Foldout>(className: UssClasses.Inspector.Component.Header);
            foldout.text = displayName;

            var label = foldout.Q<Label>(className: UssClasses.UIToolkit.Toggle.Text);
            label.name = "ComponentName";
            label.AddToClassList(UssClasses.Inspector.Component.Name);

            var input = foldout.Q<VisualElement>(className: UssClasses.UIToolkit.Toggle.Input);
            input.AddToClassList("shrink");

            if (!type.HasFlag(ComponentPropertyType.Tag))
            {
                var icon = new BindableElement();
                icon.name = "ComponentIcon";
                icon.AddToClassList(UssClasses.Inspector.Component.Icon);
                icon.AddToClassList(UssClasses.Inspector.Icons.Small);
                icon.AddToClassList(GetComponentClass(type));
                input.Insert(1, icon);

                var enabled = new Toggle();
                enabled.name = "ComponentEnabled";
                enabled.AddToClassList(UssClasses.Inspector.Component.Enabled);
                input.Insert(2, enabled);
            }

            var categoryLabel = new Label(GetComponentCategoryPostfix(type));
            categoryLabel.name = "ComponentCategory";
            categoryLabel.AddToClassList(UssClasses.Inspector.Component.Category);
            input.Add(categoryLabel);
            categoryLabel.binding = new BooleanVisibilityPreferenceBinding
            {
                Target = categoryLabel,
                PreferencePath = new PropertyPath(nameof(InspectorSettings.DisplayComponentType))
            };
            categoryLabel.binding.Update();
        }

        static string GetComponentCategoryPostfix(ComponentPropertyType type)
        {
            switch (type)
            {
                case ComponentPropertyType.Component: return "(Component)";
                case ComponentPropertyType.Tag: return "(Tag)";
                case ComponentPropertyType.SharedComponent: return "(Shared)";
                case ComponentPropertyType.ChunkComponent: return "(Chunk)";
                case ComponentPropertyType.CompanionComponent: return "(Companion)";
                case ComponentPropertyType.Buffer: return "(Buffer)";
                case ComponentPropertyType.None:
                case ComponentPropertyType.All:
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        static string GetComponentClass(ComponentPropertyType type)
        {
            switch (type)
            {
                case ComponentPropertyType.Component: return UssClasses.Inspector.ComponentTypes.Component;
                case ComponentPropertyType.Tag: return UssClasses.Inspector.ComponentTypes.Tag;
                case ComponentPropertyType.SharedComponent: return UssClasses.Inspector.ComponentTypes.SharedComponent;
                case ComponentPropertyType.ChunkComponent: return UssClasses.Inspector.ComponentTypes.ChunkComponent;
                case ComponentPropertyType.CompanionComponent: return UssClasses.Inspector.ComponentTypes.ManagedComponent;
                case ComponentPropertyType.Buffer: return UssClasses.Inspector.ComponentTypes.BufferComponent;
                case ComponentPropertyType.None:
                case ComponentPropertyType.All:
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static void CreateAspectHeader(VisualElement parent, Type type, string displayName)
        {
            Resources.Templates.Inspector.ComponentHeader.Clone(parent);
            var foldout = parent.Q<Foldout>(className: UssClasses.Inspector.Component.Header);
            foldout.text = displayName;
            foldout.Q<Label>(className: UssClasses.UIToolkit.Toggle.Text).AddToClassList(UssClasses.Inspector.Component.Name);

            var icon = new BindableElement();
            icon.AddToClassList(UssClasses.Inspector.Icons.Small);
            if (k_AspectIconsDict.TryGetValue(type.Name, out var texture))
                icon.style.backgroundImage = texture;

            icon.AddToClassList(UssClasses.Inspector.Component.AspectIcon);

            var input = foldout.Q<VisualElement>(className: UssClasses.UIToolkit.Toggle.Input);
            input.AddToClassList(UssClasses.Inspector.Component.Shrink);
            input.Insert(1, icon);

            var menu = CreateDropdownSettings(UssClasses.Inspector.Component.Menu);
            input.Add(menu);
        }

        public static ToolbarMenu CreateDropdownSettings(string ussClass)
        {
            var dropdownSettings = new ToolbarMenu
            {
                name = "dropdownSettings",
                variant = ToolbarMenu.Variant.Popup
            };

            Resources.Templates.DotsEditorCommon.AddStyles(dropdownSettings);
            dropdownSettings.AddToClassList(UssClasses.DotsEditorCommon.CommonResources);
            dropdownSettings.AddToClassList(ussClass);

            var arrow = dropdownSettings.Q(className: UssClasses.DotsEditorCommon.UnityToolbarMenuArrow);
            arrow.style.backgroundImage = null;

            return dropdownSettings;
        }

        public static void SetUnityBaseFieldInputsEnabled(VisualElement root, bool enabled)
        {
            root.Query(className: UssClasses.DotsEditorCommon.UnityBaseField).ForEach(e =>
            {
                if (e is {parent: not Toggle} && e.parent?.parent is not Foldout)
                    e.SetEnabled(enabled);
            });
        }

        public static void Synchronize(List<string> existingOrder, List<string> targetOrder, IComparer<string> comparer, VisualElement root, Func<string, VisualElement> Factory)
        {
            // optimizations for simple cases
            if (targetOrder.Count == 0)
            {
                root.Clear();
                return;
            }

            if (existingOrder.Count == 0)
            {
                foreach (var path in targetOrder)
                {
                    root.Add(Factory(path));
                }

                return;
            }

            for (int x = 0, y = 0; x < existingOrder.Count || y < targetOrder.Count;)
            {
                // If the target list is exhausted,
                // delete the current element from the subject list
                if (y >= targetOrder.Count)
                {
                    existingOrder.RemoveAt(x);
                    root.RemoveAt(x);
                }

                // if the subject list is exhausted,
                // insert the current element from the target list
                else if (x >= existingOrder.Count)
                {
                    existingOrder.Insert(y, targetOrder[y]);
                    root.Insert(y, Factory(targetOrder[y]));
                }

                // if the current subject element precedes the current target element,
                // delete the current subject element.
                else
                {
                    var existingItem = existingOrder[x];
                    var targetItem = targetOrder[y];

                    switch (comparer.Compare(existingItem, targetItem))
                    {
                        case < 0:
                            existingOrder.RemoveAt(x);
                            root.RemoveAt(x);
                            break;

                        // O/w, if the current subject element follows the current target element,
                        // insert the current target element.
                        case > 0:
                            existingOrder.Insert(x, targetItem);
                            root.Insert(x, Factory(targetItem));
                            break;

                        // O/w the current elements match; consider the next pair
                        default:
                            x++;
                            y++;
                            break;
                    }
                }
            }
        }

        public static int Compare(string x, string y, string[] topElements)
        {
            var strCmp = StringComparer.OrdinalIgnoreCase.Compare(x, y);
            if (strCmp == 0)
                return strCmp;

            var xIndexTopComponent = Array.IndexOf(topElements, x);
            var yIndexTopComponent = Array.IndexOf(topElements, y);

            switch (xIndexTopComponent, yIndexTopComponent)
            {
                case (-1, >= 0):
                case (>= 0, >= 0) when xIndexTopComponent > yIndexTopComponent:
                    return 1;
                case (>= 0, -1):
                case (>= 0, >= 0) when xIndexTopComponent < yIndexTopComponent:
                    return -1;
                default:
                    return strCmp;
            }
        }

        public static void AddRuntimeBar(VisualElement parent)
        {
            if (!EditorApplication.isPlaying)
                return;

            Resources.Templates.Inspector.EntityInspector.AddStyles(parent);

            var runtimeBar = new VisualElement();
            runtimeBar.AddToClassList(UssClasses.Inspector.RuntimeBar.RuntimeYellowBar);
            parent.Add(runtimeBar);
            parent.AddToClassList(UssClasses.Inspector.RuntimeBar.RuntimeYellowBarAdded);
            parent.RegisterCallback<GeometryChangedEvent, VisualElement>(SetRuntimeBarPosition, runtimeBar);
        }

        static void SetRuntimeBarPosition(GeometryChangedEvent evt, VisualElement runtimeBar)
        {
            if (evt.target is not VisualElement element)
                return;

            runtimeBar.style.left = 2f - element.worldBound.x;
            element.UnregisterCallback<GeometryChangedEvent, VisualElement>(SetRuntimeBarPosition);
        }
    }
}
