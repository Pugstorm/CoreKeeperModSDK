using System;
using System.Collections.Generic;
using UnityEditor.Presets;
using UnityEngine;
using UnityEngine.Assertions;

namespace UnityEditor.AddressableAssets.Settings
{
    /// <summary>
    /// Used to create template groups to make it easier for the user to create new groups.
    /// </summary>
    [CreateAssetMenu(fileName = "AddressableAssetGroupTemplate.asset", menuName = "Addressables/Group Templates/Blank Group Template")]
    public class AddressableAssetGroupTemplate : ScriptableObject, IGroupTemplate, ISerializationCallbackReceiver
    {
        [SerializeField]
        private List<AddressableAssetGroupSchema> m_SchemaObjects = new List<AddressableAssetGroupSchema>();

        [SerializeField]
        private string m_Description;

        [SerializeField]
        private AddressableAssetSettings m_Settings;

        internal AddressableAssetSettings Settings
        {
            get
            {
                if (m_Settings == null)
                    m_Settings = AddressableAssetSettingsDefaultObject.Settings;

                return m_Settings;
            }
            set { m_Settings = value; }
        }

        /// <summary>
        /// Returns a list of Preset objects for AddressableAssetGroupSchema associated with this template
        /// </summary>
        internal List<Preset> SchemaPresetObjects
        {
            get
            {
                List<Preset> m_SchemaPresetObjects = new List<Preset>(m_SchemaObjects.Count);
                foreach (AddressableAssetGroupSchema schemaObject in m_SchemaObjects)
                    m_SchemaPresetObjects.Add(new Preset(schemaObject));
                return m_SchemaPresetObjects;
            }
        }

        /// <summary>
        /// Returns the list of Preset objects of AddressableAssetGroupSchema associated with this template
        /// </summary>
        public List<AddressableAssetGroupSchema> SchemaObjects
        {
            get { return m_SchemaObjects; }
        }

        /// <summary>
        /// The name of the AddressableAssetGroupTemplate
        /// </summary>
        public string Name
        {
            get { return name; }
        }

        /// <summary>
        /// The description of the AddressableAssetGroupTemplate
        /// </summary>
        public string Description
        {
            get { return m_Description; }
            set { m_Description = value; }
        }

        /// <summary>
        /// Gets the types of the AddressableAssetGroupSchema associated with this template
        /// </summary>
        /// <returns>AddressableAssetGroupSchema types for schema on this template</returns>
        public Type[] GetTypes()
        {
            var types = new Type[m_SchemaObjects.Count];
            for (int i = 0; i < types.Length; i++)
                types[i] = m_SchemaObjects[i].GetType();
            return types;
        }

        /// <summary>
        /// Applies schema values for the group to the schema values found in the template
        /// </summary>
        /// <param name="group">The AddressableAssetGroup to apply the schema settings to</param>
        public void ApplyToAddressableAssetGroup(AddressableAssetGroup group)
        {
            foreach (AddressableAssetGroupSchema schema in group.Schemas)
            {
                List<Preset> presets = SchemaPresetObjects;
                foreach (Preset p in presets)
                {
                    Assert.IsNotNull(p);
                    if (p.CanBeAppliedTo(schema))
                    {
                        p.ApplyTo(schema);
                        schema.Group = group;
                    }
                }
            }
        }

        /// <summary>
        /// Adds the AddressableAssetGroupSchema of type to the template.
        /// </summary>
        /// <param name="type">The Type for the AddressableAssetGroupSchema to add to this template.</param>
        /// <param name="postEvent">If true, the event is propagated to callbacks.</param>
        /// <returns>If true, the type was added successfully.</returns>
        public bool AddSchema(Type type, bool postEvent = true)
        {
            if (type == null)
            {
                Debug.LogWarning("Cannot remove schema with null type.");
                return false;
            }

            if (!typeof(AddressableAssetGroupSchema).IsAssignableFrom(type))
            {
                Debug.LogWarningFormat("Invalid Schema type {0}. Schemas must inherit from AddressableAssetGroupSchema.", type.FullName);
                return false;
            }

            foreach (AddressableAssetGroupSchema schemaObject in m_SchemaObjects)
            {
                if (schemaObject.GetType() == type)
                {
                    Debug.LogError("Scheme of type " + type + " already exists");
                    return false;
                }
            }

            AddressableAssetGroupSchema schemaInstance = (AddressableAssetGroupSchema)CreateInstance(type);
            if (schemaInstance != null)
            {
                schemaInstance.name = type.Name;
                try
                {
                    schemaInstance.hideFlags |= HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(schemaInstance, this);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }

                m_SchemaObjects.Add(schemaInstance);

                SetDirty(AddressableAssetSettings.ModificationEvent.GroupTemplateSchemaAdded, this, postEvent);
                AssetDatabase.SaveAssets();
            }

            return schemaInstance != null;
        }

        /// <summary>
        /// Removes the AddressableAssetGroupSchema of the type from the template.
        /// </summary>
        /// <param name="type">The type of AddressableAssetGroupSchema to be removed.</param>
        /// <param name="postEvent">If true, the event is propagated to callbacks.</param>
        /// <returns>If true, the type was removed successfully.</returns>
        public bool RemoveSchema(Type type, bool postEvent = true)
        {
            if (type == null)
            {
                Debug.LogWarning("Cannot remove schema with null type.");
                return false;
            }

            if (!typeof(AddressableAssetGroupSchema).IsAssignableFrom(type))
            {
                Debug.LogWarningFormat("Invalid Schema type {0}. Schemas must inherit from AddressableAssetGroupSchema.", type.FullName);
                return false;
            }

            for (int i = 0; i < m_SchemaObjects.Count; ++i)
            {
                if (m_SchemaObjects[i].GetType() == type)
                    return RemoveSchema(i, postEvent);
            }

            return false;
        }

        /// <summary>
        /// Removes the Schema at the given index.
        /// </summary>
        /// <param name="index">The index of the object to be removed.</param>
        /// <param name="postEvent">If true, the event is propagated to callbacks.</param>
        /// <returns>If true, the type was removed successfully.</returns>
        internal bool RemoveSchema(int index, bool postEvent = true)
        {
            if (index == -1)
                return false;

            AssetDatabase.RemoveObjectFromAsset(m_SchemaObjects[index]);
            DestroyImmediate(m_SchemaObjects[index]);
            m_SchemaObjects.RemoveAt(index);

            SetDirty(AddressableAssetSettings.ModificationEvent.GroupTemplateSchemaRemoved, this, postEvent);
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>
        /// Marks the object as modified.
        /// </summary>
        /// <param name="modificationEvent">The event type that is changed.</param>
        /// <param name="eventData">The object data that corresponds to the event.</param>
        /// <param name="postEvent">If true, the event is propagated to callbacks.</param>
        public void SetDirty(AddressableAssetSettings.ModificationEvent modificationEvent, object eventData, bool postEvent)
        {
            if (Settings != null)
            {
                if (Settings.IsPersisted && this != null)
                {
                    EditorUtility.SetDirty(this);
                    AddressableAssetUtility.OpenAssetIfUsingVCIntegration(this);
                }

                Settings.SetDirty(modificationEvent, eventData, postEvent, false);
            }
        }

        /// <summary>
        /// Checks if the group contains a schema of a given type.
        /// </summary>
        /// <param name="type">The schema type.</param>
        /// <returns>True if the schema type or subclass has been added to this group.</returns>
        public bool HasSchema(Type type)
        {
            return GetSchemaByType(type) != null;
        }

        /// <summary>
        /// Gets an added schema of the specified type.
        /// </summary>
        /// <param name="type">The schema type.</param>
        /// <returns>The schema if found, otherwise null.</returns>
        public AddressableAssetGroupSchema GetSchemaByType(Type type)
        {
            foreach (var schema in m_SchemaObjects)
            {
                if (schema.GetType() == type)
                {
                    return schema;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the index of a schema based on its specified type.
        /// </summary>
        /// <param name="type">The schema type.</param>
        /// <returns>Valid index if found, otherwise returns -1.</returns>
        public int FindSchema(Type type)
        {
            for (int i = 0; i < m_SchemaObjects.Count; i++)
            {
                if (m_SchemaObjects[i].GetType() == type)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Implementation of ISerializationCallbackReceiver. Sorts collections for deterministic ordering.
        /// </summary>
        public void OnBeforeSerialize()
        {
            m_SchemaObjects.Sort(AddressableAssetGroupSchema.Compare);
        }

        /// <summary>
        /// Implementation of ISerializationCallbackReceiver. Does nothing.
        /// </summary>
        public void OnAfterDeserialize()
        {
        }
    }
}
