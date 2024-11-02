using System;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    [UsedImplicitly]
    sealed class SystemVersionPropertyInspector : PropertyInspector<Version>
    {
        public override VisualElement Build()
        {
            var root = new Foldout {text = DisplayName};
            var majorField = new IntegerField
            {
                bindingPath = nameof(Version.Major),
                label = ObjectNames.NicifyVariableName(nameof(Version.Major))
            };
            majorField.RegisterCallback<ChangeEvent<int>, IntegerField>(ClampEditingValue, majorField);
            majorField.RegisterValueChangedCallback(OnMajorChanged);
            root.Add(majorField);

            var minorField = new IntegerField
            {
                bindingPath = nameof(Version.Minor),
                label = ObjectNames.NicifyVariableName(nameof(Version.Minor))
            };
            minorField.RegisterCallback<ChangeEvent<int>, IntegerField>(ClampEditingValue, minorField);
            minorField.RegisterValueChangedCallback(OnMinorChanged);
            root.Add(minorField);

            var usage = GetAttribute<SystemVersionUsageAttribute>();
            if (usage?.IncludeBuild ?? true)
            {
                var buildField = new IntegerField
                {
                    bindingPath = nameof(Version.Build),
                    label = ObjectNames.NicifyVariableName(nameof(Version.Build))
                };
                buildField.RegisterCallback<ChangeEvent<int>, IntegerField>(ClampEditingValue, buildField);
                buildField.RegisterValueChangedCallback(OnBuildChanged);
                root.Add(buildField);
            }

            if (usage?.IncludeRevision ?? true)
            {
                var revisionField = new IntegerField
                {
                    bindingPath = nameof(Version.Revision),
                    label = ObjectNames.NicifyVariableName(nameof(Version.Revision))
                };
                revisionField.RegisterCallback<ChangeEvent<int>, IntegerField>(ClampEditingValue, revisionField);
                revisionField.RegisterValueChangedCallback(OnRevisionChanged);
                root.Add(revisionField);
            }

            if (IsReadOnly)
                root.contentContainer.SetEnabled(false);

            return root;
        }

        void OnMajorChanged(ChangeEvent<int> evt)
        {
            var version = Target;
            SetTarget(evt.newValue, version.Minor, version.Build, version.Revision);
        }

        void OnMinorChanged(ChangeEvent<int> evt)
        {
            var version = Target;
            SetTarget(version.Major, evt.newValue, version.Build, version.Revision);
        }

        void OnBuildChanged(ChangeEvent<int> evt)
        {
            var version = Target;
            SetTarget(version.Major, version.Minor, evt.newValue, version.Revision);
        }

        void OnRevisionChanged(ChangeEvent<int> evt)
        {
            var version = Target;
            SetTarget(version.Major, version.Minor, version.Build, evt.newValue);
        }

        void SetTarget(int major, int minor, int build, int revision)
        {
            var usage = GetAttribute<SystemVersionUsageAttribute>();
            if (null != usage)
            {
                switch (usage.Usage)
                {
                    case SystemVersionUsage.MajorMinor:
                        Target = new Version(Clamp(major), Clamp(minor));
                        break;
                    case SystemVersionUsage.MajorMinorBuild:
                        Target = new Version(Clamp(major), Clamp(minor), Clamp(build));
                        break;
                    case SystemVersionUsage.MajorMinorBuildRevision:
                        Target = new Version(Clamp(major), Clamp(minor), Clamp(build), Clamp(revision));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            else
                Target = new Version(Clamp(major), Clamp(minor), Clamp(build), Clamp(revision));
        }

        static void ClampEditingValue(ChangeEvent<int> evt, IntegerField field)
        {
            if (evt.newValue < 0)
                field.SetValueWithoutNotify(0);
        }

        static int Clamp(int value)
        {
            return Mathf.Clamp(value, 0, int.MaxValue);
        }
    }
}
