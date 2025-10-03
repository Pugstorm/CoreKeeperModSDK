using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.AddressableAssets.GUI;
using UnityEngine;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Serialization;

namespace UnityEditor.AddressableAssets.Settings
{
    /// <summary>
    /// Contains data for AddressableAssetGroups.
    /// </summary>
    public class AddressableAssetGroupSchema : ScriptableObject
    {
        [FormerlySerializedAs("m_group")]
        [AddressableReadOnly]
        [SerializeField]
        AddressableAssetGroup m_Group;

        SerializedObject m_SchemaSerializedObject = null;

        internal SerializedObject SchemaSerializedObject
        {
            get
            {
                if (m_SchemaSerializedObject == null)
                    m_SchemaSerializedObject = new SerializedObject(this);
                return m_SchemaSerializedObject;
            }
            set { m_SchemaSerializedObject = value; }
        }

        /// <summary>
        /// Get the group that the schema belongs to.
        /// </summary>
        public AddressableAssetGroup Group
        {
            get { return m_Group; }
            internal set
            {
                m_Group = value;
                if (m_Group != null)
                {
                    OnSetGroup(m_Group);
                    Validate();
                }
            }
        }

        /// <summary>
        /// Override this method to perform post creation initialization.
        /// </summary>
        /// <param name="group">The group that the schema is added to.</param>
        protected virtual void OnSetGroup(AddressableAssetGroup group)
        {
        }

        internal virtual void Validate()
        {
        }

        /// <summary>
        /// Used to display the GUI of the schema.
        /// </summary>
        public virtual void OnGUI()
        {
            var type = GetType();
            var p = SchemaSerializedObject.GetIterator();
            p.Next(true);
            while (p.Next(false))
            {
                var prop = type.GetField(p.name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (prop != null)
                    EditorGUILayout.PropertyField(p, true);
            }

            SchemaSerializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Used to display the GUI of multiple selected groups.
        /// </summary>
        /// <param name="otherSchemas">Schema instances in the other selected groups</param>
        public virtual void OnGUIMultiple(List<AddressableAssetGroupSchema> otherSchemas)
        {
        }

        /// <summary>
        /// Used to notify the addressables settings that data has been modified.  This must be called by subclasses to ensure proper cache invalidation.
        /// </summary>
        /// <param name="postEvent">Determines if this method call will post an event to the internal addressables event system</param>
        protected void SetDirty(bool postEvent)
        {
            m_SchemaSerializedObject = null;
            if (m_Group != null)
            {
                if (m_Group.Settings != null && m_Group.Settings.IsPersisted)
                {
                    EditorUtility.SetDirty(this);
                    AddressableAssetUtility.OpenAssetIfUsingVCIntegration(this);
                }

                if (m_Group != null)
                    m_Group.SetDirty(AddressableAssetSettings.ModificationEvent.GroupSchemaModified, this, postEvent, false);
            }
        }

        /// <summary>
        /// Used for drawing properties in the inspector.
        /// </summary>
        public virtual void ShowAllProperties()
        {
        }

        /// <summary>
        /// Display mixed values for the specified property found in a list of schemas.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="otherSchemas">The list of schemas that may contain the property.</param>
        /// <param name="type">The property type.</param>
        /// <param name="propertyName">The property name.</param>
        protected void ShowMixedValue(SerializedProperty property, List<AddressableAssetGroupSchema> otherSchemas, Type type, string propertyName)
        {
            foreach (var schema in otherSchemas)
            {
                var s_prop = schema.SchemaSerializedObject.FindProperty(propertyName);
                if ((property.propertyType == SerializedPropertyType.Enum && (property.enumValueIndex != s_prop.enumValueIndex)) ||
                    (property.propertyType == SerializedPropertyType.String && (property.stringValue != s_prop.stringValue)) ||
                    (property.propertyType == SerializedPropertyType.Integer && (property.intValue != s_prop.intValue)) ||
                    (property.propertyType == SerializedPropertyType.Boolean && (property.boolValue != s_prop.boolValue)))
                {
                    EditorGUI.showMixedValue = true;
                    return;
                }

                if (type == typeof(ProfileValueReference))
                {
                    var field = property.serializedObject.targetObject.GetType().GetField(property.name,
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance |
                        BindingFlags.DeclaredOnly);

                    string lhsId = (field?.GetValue(property.serializedObject.targetObject) as ProfileValueReference)?.Id;
                    string rhsId = (field?.GetValue(s_prop.serializedObject.targetObject) as ProfileValueReference)?.Id;

                    if (lhsId != null && rhsId != null && lhsId != rhsId)
                    {
                        EditorGUI.showMixedValue = true;
                        return;
                    }
                }

                if (type == typeof(SerializedType))
                {
                    var field = property.serializedObject.targetObject.GetType().GetField(property.name,
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance |
                        BindingFlags.DeclaredOnly);

                    Type lhs = ((SerializedType)field?.GetValue(property.serializedObject.targetObject)).Value;
                    Type rhs = ((SerializedType)field?.GetValue(s_prop.serializedObject.targetObject)).Value;

                    if (lhs != null && rhs != null && lhs != rhs)
                    {
                        EditorGUI.showMixedValue = true;
                        return;
                    }
                }
            }
        }
        public static int Compare(AddressableAssetGroupSchema x, AddressableAssetGroupSchema y)
        {
            if (x == null && y == null)
                return 0;
            if (x == null || x.SchemaSerializedObject == null || x.SchemaSerializedObject.targetObject == null)
                return -1;
            if (y == null || y.SchemaSerializedObject == null || y.SchemaSerializedObject.targetObject == null)
                return 1;
            // you can only have one schema of a given type in a set so using the name should be ok.
            return string.CompareOrdinal(x.SchemaSerializedObject.targetObject.name, y.SchemaSerializedObject.targetObject.name);
        }
    }
}
