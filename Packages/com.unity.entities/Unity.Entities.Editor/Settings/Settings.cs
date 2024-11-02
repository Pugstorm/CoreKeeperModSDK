using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities.UI;
using UnityEditor;
using UnityEngine.UIElements;

using SettingsProvider = UnityEditor.SettingsProvider;

namespace Unity.Entities.Editor
{
    abstract class Settings<T> : SettingsProvider
        where T : SettingsAttribute
    {
        readonly struct SettingWrapper
        {
            public readonly ISetting Setting;
            public readonly bool Internal;

            public SettingWrapper(ISetting setting, bool isInternal)
            {
                Setting = setting;
                Internal = isInternal;
            }
        }

        static readonly VisualElementTemplate s_WindowTemplate = PackageResources.LoadTemplate("Preferences/preferences-window");
        static readonly VisualElementTemplate s_SettingTemplate = PackageResources.LoadTemplate("Preferences/preferences-setting");
        static readonly VisualElementTemplate s_ValueTemplate = PackageResources.LoadTemplate("Preferences/preferences-value");
        static readonly Dictionary<string, List<SettingWrapper>> s_Settings = new Dictionary<string, List<SettingWrapper>>();
        static readonly List<string> s_Keywords = new List<string>();
        static readonly string k_Prefix = $"{typeof(Settings<T>).FullName}: ";

        protected static bool HasAnySettings
        {
            get
            {
                if (!Unsupported.IsDeveloperMode())
                    return s_Settings.Any(category => category.Value.Any(w => !w.Internal));
                return s_Settings.Count > 0;
            }
        }

        static Settings()
        {
            try
            {
                CacheSettings();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
        }

        static void CacheSettings()
        {
            if (typeof(T) == typeof(SettingsAttribute))
            {
                UnityEngine.Debug.LogError(
                    $"{k_Prefix} Constraint of type `{nameof(SettingsAttribute)}` is not allowed, you must use a derived type of type `{nameof(SettingsAttribute)}`.");
                return;
            }

            var userSettingsType = typeof(Unity.Serialization.Editor.UserSettings<>);

            foreach (var type in UnityEditor.TypeCache.GetTypesWithAttribute<T>())
            {
                if (type.IsAbstract || type.IsGenericType || !type.IsClass)
                    continue;

                if (!typeof(ISetting).IsAssignableFrom(type))
                {
                    Debug.LogError(
                        $"{k_Prefix} type `{type.FullName}` must implement `{typeof(ISetting)}` in order to be used as a setting.");
                    continue;
                }

                var typedUserSettings = userSettingsType.MakeGenericType(type);
                var getOrCreateMethod =
                    typedUserSettings.GetMethod("GetOrCreate", BindingFlags.Static | BindingFlags.Public);
                if (null == getOrCreateMethod)
                {
                    Debug.LogError(
                        $"{k_Prefix} Could not find the `GetOrCreate` method on `{userSettingsType.FullName}` class.");
                    continue;
                }

                var attributes = type.GetCustomAttributes<T>();
                foreach (var attribute in attributes)
                {
                    var setting = (ISetting)getOrCreateMethod.Invoke(null, new object[] { attribute.SectionName });
                    if (!s_Settings.TryGetValue(attribute.SectionName, out var list))
                    {
                        s_Settings[attribute.SectionName] = list = new List<SettingWrapper>();
                        s_Keywords.Add(attribute.SectionName);
                    }

                    var wrapper = new SettingWrapper(setting, type.GetCustomAttributes<InternalSettingAttribute>().Any());
                    list.Add(wrapper);
                    var keywords = wrapper.Setting.GetSearchKeywords();
                    if (keywords != null && keywords.Length > 0)
                        s_Keywords.AddRange(keywords);
                }
            }
        }

        static string PathForScope(SettingsScope scope)
        {
            switch (scope)
            {
                case SettingsScope.User:
                    return "Preferences/";
                case SettingsScope.Project:
                    return "Project/";
                default:
                    throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
            }
        }

        protected virtual string Title { get; }

        protected Settings(string path, SettingsScope scope, IEnumerable<string> keywords = null) : base(PathForScope(scope) + path, scope, s_Keywords.Concat(keywords ?? Array.Empty<string>()))
        {
            Title = path.Replace("/", " ");
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            var window = s_WindowTemplate.Clone(rootElement);
            window.Q<ScrollView>("scrollView").horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            window.Q<Label>("title").text = Title;

            var settings = window.Q("settings");
            foreach (var pair in s_Settings)
            {
                if (pair.Value.All(v => v.Internal) && !Unsupported.IsDeveloperMode())
                    continue;

                var setting = s_SettingTemplate.Clone();
                setting.Q<Label>("name").text = pair.Key;

                var content = setting.Q("content");
                foreach (var wrapper in pair.Value)
                {
                    if (wrapper.Internal && !Unsupported.IsDeveloperMode())
                        continue;

                    var value = s_ValueTemplate.Clone();
                    var property = value.Q<PropertyElement>("property");
                    var target = wrapper.Setting;
                    property.SetAttributeFilter(AttributeFilter);
                    property.SetTarget(target);
                    property.OnChanged += (element, path) => target.OnSettingChanged(path);
                    property.RegisterCallback<GeometryChangedEvent>(e => StylingUtility.AlignInspectorLabelWidth(property));
                    content.Add(value);
                }
                settings.Add(setting);
            }

            base.OnActivate(searchContext, rootElement);
        }

        bool AttributeFilter(IEnumerable<Attribute> attributes)
        {
            return !attributes.OfType<InternalSettingAttribute>().Any() || Unsupported.IsDeveloperMode();
        }
    }
}
