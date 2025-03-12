using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Entities;
using Unity.Entities.UI;
using Unity.Entities.Editor;
using Unity.Mathematics;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Physics.Editor
{
    [UsedImplicitly]
    class ColliderBlobInspector : PropertyInspector<BlobAssetReference<Collider>>
    {
        VisualElement m_Root;
        int _lastBlobHash;

        class FieldUpdateBinding<TValueType> : IBinding
        {
            public Action<INotifyValueChanged<TValueType>> UpdateFunc;
            public INotifyValueChanged<TValueType> Field;

            void IBinding.PreUpdate() {}
            void IBinding.Update() => UpdateFunc.Invoke(Field);
            void IBinding.Release() {}
        }

        public override VisualElement Build()
        {
            _lastBlobHash = Target.GetHashCode();

            m_Root = new VisualElement();

            DoBuild(m_Root);

            return m_Root;
        }

        void DoBuild(VisualElement parent)
        {
            var colliderBlob = Target;
            if (!colliderBlob.IsCreated)
            {
                var defaultGui = DoDefaultGui();
                defaultGui.SetEnabled(false);
                parent.Add(defaultGui);
            }
            else
            {
                var colliderRoot = new VisualElement();
                parent.Add(colliderRoot);

                AddField<IntegerField, int>(colliderRoot, "Hash", Target.GetHashCode());

                unsafe
                {
                    Collider* collider = (Collider*)colliderBlob.GetUnsafePtr();
                    AddColliderSection(colliderRoot, collider);
                }
            }
        }

        TFieldType AddField<TFieldType, TValueType>(VisualElement parent, string propertyName, TValueType propertyValue, Action<INotifyValueChanged<TValueType>> valueUpdateFunc = null, bool editable = false)
            where TFieldType : BaseField<TValueType>, new()
        {
            var label = ContentUtilities.NicifyTypeName(propertyName);
            bool enumField = (typeof(TFieldType) == typeof(EnumField) && typeof(TValueType) == typeof(Enum));
            var field = enumField ? new EnumField(label, propertyValue as Enum) as TFieldType : new TFieldType
            {
                label = label,
                value = propertyValue
            };

            if (field == null)
            {
                throw new NotImplementedException();
            }

            if (valueUpdateFunc != null)
            {
                field.binding = new FieldUpdateBinding<TValueType>
                {
                    Field = field,
                    UpdateFunc = valueUpdateFunc
                };
            }

            // ensure proper alignment when the UI needs to be updated (see Update() function)
            field.AddToClassList(BaseField<TValueType>.alignedFieldUssClassName);

            // need to descent into child FloatFields for Vector3Field for proper alignment
            if (typeof(TFieldType) == typeof(Vector3Field))
            {
                field.Query<FloatField>().ForEach(f => f.AddToClassList(FloatField.alignedFieldUssClassName));
            }

            field.SetEnabled(editable);
            InspectorUtility.AddRuntimeBar(field);

            parent.Add(field);
            return field;
        }

        BaseMaskField<int> AddMaskField<TValueType>(VisualElement parent, string propertyName, int propertyValue,
            Action<INotifyValueChanged<int>> valueUpdateFunc)
            where TValueType : unmanaged
        {
            // always use editable mask field so that user can inspect the mask value using the field's popup menu
            var field = AddField<MaskField, int>(parent, propertyName, propertyValue, valueUpdateFunc,
                editable: true);

            // set up mask field with the correct number of choices (bits)
            if (field is BaseMaskField<int> maskField)
            {
                var choices = new List<string>();
                unsafe
                {
                    var bitCount = sizeof(TValueType) * 8;
                    for (int i = 0; i < bitCount; ++i)
                    {
                        choices.Add($"{i}");
                    }
                }

                maskField.choices = choices;
            }

            return field;
        }

        unsafe void AddColliderSection(VisualElement parent, Collider* collider, bool preventEdits = false)
        {
            // Add common collider properties:

            AddField<EnumField, Enum>(parent, nameof(ColliderType), collider->Type);
            AddField<EnumField, Enum>(parent, nameof(CollisionType), collider->CollisionType);
            AddField<Toggle, bool>(parent, nameof(Collider.IsUnique), collider->IsUnique, field =>
            {
                field.SetValueWithoutNotify(collider->IsUnique);
            });

            AddField<Toggle, bool>(parent, nameof(Collider.RespondsToCollision), collider->RespondsToCollision, field =>
            {
                field.SetValueWithoutNotify(collider->RespondsToCollision);
            });

            AddField<IntegerField, int>(parent, nameof(Collider.MemorySize), collider->MemorySize);

            AddMassPropertiesSection(parent, collider);

            // Add material and geometry sections for the collider unless it is a compound
            if (collider->Type != ColliderType.Compound)
            {
                AddMaterialSection(parent, collider, preventEdits);
                AddCollisionFilterSection(parent, collider, preventEdits);
                AddGeometrySection(parent, collider, preventEdits);
            }
            else
            {
                CompoundCollider* compound = (CompoundCollider*)collider;
                var compoundFoldout = new Foldout
                {
                    text = "Geometry",
                    value = false
                };
                parent.Add(compoundFoldout);

                preventEdits = preventEdits || !collider->IsUnique;

                for (int childIndex = 0; childIndex < compound->Children.Length; childIndex++)
                {
                    var childFoldout = new Foldout
                    {
                        text = $"Child {childIndex}",
                        value = false
                    };
                    compoundFoldout.Add(childFoldout);

                    ref var child = ref compound->Children[childIndex];
                    AddColliderSection(childFoldout, child.Collider, preventEdits);
                }
            }
        }

        unsafe void AddMassPropertiesSection(VisualElement parent, Collider* collider)
        {
            var massPropertiesFoldout = new Foldout
            {
                text = ContentUtilities.NicifyTypeName(nameof(MassProperties)),
                value = false
            };
            parent.Add(massPropertiesFoldout);

            var massProperties = collider->MassProperties;
            ref var massDistribution = ref massProperties.MassDistribution;

            var massDistributionFoldout = new Foldout
            {
                text = ContentUtilities.NicifyTypeName(nameof(MassProperties.MassDistribution)),
                value = true
            };
            massPropertiesFoldout.Add(massDistributionFoldout);

            var transformFoldout = new Foldout
            {
                text = ContentUtilities.NicifyTypeName(nameof(MassProperties.MassDistribution.Transform)),
            };
            massDistributionFoldout.Add(transformFoldout);

            AddField<Vector3Field, Vector3>(transformFoldout, nameof(MassProperties.MassDistribution.Transform.pos), massDistribution.Transform.pos, field =>
            {
                field.SetValueWithoutNotify(collider->MassProperties.MassDistribution.Transform.pos);
            });

            AddField<Vector3Field, Vector3>(transformFoldout, nameof(MassProperties.MassDistribution.Transform.rot), Mathf.Rad2Deg * massDistribution.Transform.rot.ToEulerAngles(), field =>
            {
                field.SetValueWithoutNotify(Mathf.Rad2Deg * collider->MassProperties.MassDistribution.Transform.rot.ToEulerAngles());
            });

            AddField<Vector3Field, Vector3>(massDistributionFoldout, nameof(MassProperties.MassDistribution.InertiaTensor), massDistribution.InertiaTensor, field =>
            {
                field.SetValueWithoutNotify(collider->MassProperties.MassDistribution.InertiaTensor);
            });

            AddField<FloatField, float>(massPropertiesFoldout, nameof(MassProperties.Volume), massProperties.Volume, field =>
            {
                field.SetValueWithoutNotify(collider->MassProperties.Volume);
            });

            AddField<FloatField, float>(massPropertiesFoldout, nameof(MassProperties.AngularExpansionFactor), massProperties.AngularExpansionFactor, field =>
            {
                field.SetValueWithoutNotify(collider->MassProperties.AngularExpansionFactor);
            });
        }

        unsafe void AddGeometrySection(VisualElement parent, Collider* collider, bool preventEdits)
        {
            var geometryFoldout = new Foldout
            {
                text = "Geometry",
                value = true
            };

            var editable = !preventEdits && collider->IsUnique;

            // For each type of collider, add fields representing its geometry to the parent element
            switch (collider->Type)
            {
                // compound:
                case ColliderType.Compound:
                {
                    // does nothing; handled in AddColliderSection
                    return;
                }
                // non-primitive types:
                case ColliderType.Convex:
                case ColliderType.Triangle:
                case ColliderType.Quad:
                case ColliderType.Mesh:
                case ColliderType.Terrain:
                {
                    // not implemented
                    return;
                }
                // primitive types:
                case ColliderType.Sphere:
                {
                    var sphere = (SphereCollider*)collider;

                    var radiusField = AddField<FloatField, float>(geometryFoldout, nameof(SphereGeometry.Radius), sphere->Radius, field =>
                    {
                        field.SetValueWithoutNotify(sphere->Radius);
                    }, editable);

                    var centerField = AddField<Vector3Field, Vector3>(geometryFoldout, nameof(SphereGeometry.Center), sphere->Center, field =>
                    {
                        field.SetValueWithoutNotify(sphere->Center);
                    }, editable);

                    if (editable)
                    {
                        radiusField.RegisterValueChangedCallback(evt =>
                        {
                            var geometry = sphere->Geometry;
                            geometry.Radius = evt.newValue;
                            sphere->Geometry = geometry;
                        });

                        centerField.RegisterValueChangedCallback(evt =>
                        {
                            var geometry = sphere->Geometry;
                            geometry.Center = evt.newValue;
                            sphere->Geometry = geometry;
                        });
                    }

                    break;
                }
                case ColliderType.Capsule:
                {
                    var capsule = (CapsuleCollider*)collider;
                    var radiusField = AddField<FloatField, float>(geometryFoldout, nameof(CapsuleGeometry.Radius), capsule->Radius, field =>
                    {
                        field.SetValueWithoutNotify(capsule->Radius);
                    }, editable);

                    var vertex0Field = AddField<Vector3Field, Vector3>(geometryFoldout, nameof(CapsuleGeometry.Vertex0), capsule->Vertex0, field =>
                    {
                        field.SetValueWithoutNotify(capsule->Vertex0);
                    }, editable);

                    var vertex1Field = AddField<Vector3Field, Vector3>(geometryFoldout, nameof(CapsuleGeometry.Vertex1), capsule->Vertex1, field =>
                    {
                        field.SetValueWithoutNotify(capsule->Vertex1);
                    }, editable);

                    if (editable)
                    {
                        radiusField.RegisterValueChangedCallback(evt =>
                        {
                            var geometry = capsule->Geometry;
                            geometry.Radius = evt.newValue;
                            capsule->Geometry = geometry;
                        });

                        vertex0Field.RegisterValueChangedCallback(evt =>
                        {
                            var geometry = capsule->Geometry;
                            geometry.Vertex0 = evt.newValue;
                            capsule->Geometry = geometry;
                        });

                        vertex1Field.RegisterValueChangedCallback(evt =>
                        {
                            var geometry = capsule->Geometry;
                            geometry.Vertex1 = evt.newValue;
                            capsule->Geometry = geometry;
                        });
                    }

                    break;
                }
                case ColliderType.Box:
                {
                    var box = (BoxCollider*)collider;
                    var sizeField = AddField<Vector3Field, Vector3>(geometryFoldout, nameof(BoxGeometry.Size), box->Size, field =>
                    {
                        field.SetValueWithoutNotify(box->Size);
                    }, editable);

                    var centerField = AddField<Vector3Field, Vector3>(geometryFoldout, nameof(BoxGeometry.Center), box->Center, field =>
                    {
                        field.SetValueWithoutNotify(box->Center);
                    }, editable);

                    var orientationField = AddField<Vector3Field, Vector3>(geometryFoldout, nameof(BoxGeometry.Orientation), Mathf.Rad2Deg * box->Orientation.ToEulerAngles(), field =>
                    {
                        field.SetValueWithoutNotify(Mathf.Rad2Deg * box->Orientation.ToEulerAngles());
                    }, editable);

                    if (editable)
                    {
                        sizeField.RegisterValueChangedCallback(evt =>
                        {
                            var geometry = box->Geometry;
                            geometry.Size = evt.newValue;
                            box->Geometry = geometry;
                        });

                        centerField.RegisterValueChangedCallback(evt =>
                        {
                            var geometry = box->Geometry;
                            geometry.Center = evt.newValue;
                            box->Geometry = geometry;
                        });

                        orientationField.RegisterValueChangedCallback(evt =>
                        {
                            var geometry = box->Geometry;
                            geometry.Orientation = quaternion.EulerXYZ(Mathf.Deg2Rad * evt.newValue);
                            box->Geometry = geometry;
                        });
                    }

                    break;
                }
                case ColliderType.Cylinder:
                {
                    var cylinder = (CylinderCollider*)collider;

                    var radiusField = AddField<FloatField, float>(geometryFoldout, nameof(CylinderGeometry.Radius), cylinder->Radius, field =>
                    {
                        field.SetValueWithoutNotify(cylinder->Radius);
                    }, editable);

                    var heightField = AddField<FloatField, float>(geometryFoldout, nameof(CylinderGeometry.Height), cylinder->Height, field =>
                    {
                        field.SetValueWithoutNotify(cylinder->Height);
                    }, editable);

                    var centerField = AddField<Vector3Field, Vector3>(geometryFoldout, nameof(CylinderGeometry.Center), cylinder->Center, field =>
                    {
                        field.SetValueWithoutNotify(cylinder->Center);
                    }, editable);

                    var orientationField = AddField<Vector3Field, Vector3>(geometryFoldout, nameof(CylinderGeometry.Orientation), Mathf.Rad2Deg * cylinder->Orientation.ToEulerAngles(), field =>
                    {
                        field.SetValueWithoutNotify(Mathf.Rad2Deg * cylinder->Orientation.ToEulerAngles());
                    }, editable);

                    if (editable)
                    {
                        radiusField.RegisterValueChangedCallback(evt =>
                        {
                            var geometry = cylinder->Geometry;
                            geometry.Radius = evt.newValue;
                            cylinder->Geometry = geometry;
                        });

                        heightField.RegisterValueChangedCallback(evt =>
                        {
                            var geometry = cylinder->Geometry;
                            geometry.Height = evt.newValue;
                            cylinder->Geometry = geometry;
                        });

                        centerField.RegisterValueChangedCallback(evt =>
                        {
                            var geometry = cylinder->Geometry;
                            geometry.Center = evt.newValue;
                            cylinder->Geometry = geometry;
                        });

                        orientationField.RegisterValueChangedCallback(evt =>
                        {
                            var geometry = cylinder->Geometry;

                            geometry.Orientation = quaternion.EulerXYZ(Mathf.Deg2Rad * evt.newValue);
                            cylinder->Geometry = geometry;
                        });
                    }
                    break;
                }
                default:
                {
                    Debug.LogWarning($"Collider type {collider->Type} is not supported.");
                    break;
                }
            }

            parent.Add(geometryFoldout);
        }

        unsafe void AddMaterialSection(VisualElement parent, Collider* collider, bool preventEdits)
        {
            var materialFoldout = new Foldout
            {
                text = "Material",
                value = false
            };
            parent.Add(materialFoldout);

            var editable = !preventEdits && collider->IsUnique;

            var material = collider->GetMaterial(ColliderKey.Empty);
            var frictionField = AddField<FloatField, float>(materialFoldout, nameof(material.Friction), material.Friction, field =>
            {
                field.SetValueWithoutNotify(collider->GetFriction());
            }, editable);

            var restitutionField = AddField<FloatField, float>(materialFoldout, nameof(material.Restitution), material.Restitution, field =>
            {
                field.SetValueWithoutNotify(collider->GetRestitution());
            }, editable);

            var frictionPolicyField = AddField<EnumField, Enum>(materialFoldout, nameof(material.FrictionCombinePolicy), material.FrictionCombinePolicy, field =>
            {
                field.SetValueWithoutNotify(collider->GetMaterial(ColliderKey.Empty).FrictionCombinePolicy);
            }, editable);

            var restitutionPolicyField = AddField<EnumField, Enum>(materialFoldout, nameof(material.RestitutionCombinePolicy), material.RestitutionCombinePolicy, field =>
            {
                field.SetValueWithoutNotify(collider->GetMaterial(ColliderKey.Empty).RestitutionCombinePolicy);
            }, editable);

            var collisionResponseField = AddField<EnumField, Enum>(materialFoldout, nameof(material.CollisionResponse), material.CollisionResponse, field =>
            {
                field.SetValueWithoutNotify(collider->GetMaterial(ColliderKey.Empty).CollisionResponse);
            }, editable);

            var customTagField = AddMaskField<byte>(materialFoldout, nameof(material.CustomTags), material.CustomTags, field =>
            {
                field.SetValueWithoutNotify(collider->GetMaterial(ColliderKey.Empty).CustomTags);
            });

            if (editable)
            {
                frictionField.RegisterValueChangedCallback(
                    changeEvent =>
                    {
                        collider->SetFriction(changeEvent.newValue);
                    });

                restitutionField.RegisterValueChangedCallback(
                    changeEvent =>
                    {
                        collider->SetRestitution(changeEvent.newValue);
                    });

                frictionPolicyField.RegisterValueChangedCallback(
                    changeEvent =>
                    {
                        var newMaterial = collider->GetMaterial(ColliderKey.Empty);
                        newMaterial.FrictionCombinePolicy = (Material.CombinePolicy)changeEvent.newValue;
                        collider->SetMaterialField(newMaterial, ColliderKey.Empty, Material.MaterialField.FrictionCombinePolicy);
                    });

                restitutionPolicyField.RegisterValueChangedCallback(
                    changeEvent =>
                    {
                        var newMaterial = collider->GetMaterial(ColliderKey.Empty);
                        newMaterial.RestitutionCombinePolicy = (Material.CombinePolicy)changeEvent.newValue;
                        collider->SetMaterialField(newMaterial, ColliderKey.Empty, Material.MaterialField.RestitutionCombinePolicy);
                    });

                collisionResponseField.RegisterValueChangedCallback(
                    changeEvent =>
                    {
                        var newMaterial = collider->GetMaterial(ColliderKey.Empty);
                        newMaterial.CollisionResponse = (CollisionResponsePolicy)changeEvent.newValue;
                        collider->SetMaterialField(newMaterial, ColliderKey.Empty, Material.MaterialField.CollisionResponsePolicy);
                    });

                customTagField.RegisterValueChangedCallback(
                    changeEvent =>
                    {
                        var newMaterial = collider->GetMaterial(ColliderKey.Empty);
                        newMaterial.CustomTags = unchecked((byte)changeEvent.newValue);
                        collider->SetMaterialField(newMaterial, ColliderKey.Empty, Material.MaterialField.CustomTags);
                    });
            }
        }

        unsafe void AddCollisionFilterSection(VisualElement parent, Collider* collider, bool preventEdits)
        {
            var filterFoldout = new Foldout
            {
                text = "Collision Filter",
                value = false
            };
            parent.Add(filterFoldout);

            var editable = !preventEdits && collider->IsUnique;

            var filter = collider->GetCollisionFilter(ColliderKey.Empty);
            var belongsToField = AddMaskField<uint>(filterFoldout, nameof(filter.BelongsTo), unchecked((int)filter.BelongsTo), field =>
            {
                field.SetValueWithoutNotify(unchecked((int)collider->GetCollisionFilter().BelongsTo));
            });

            var collidesWithField = AddMaskField<uint>(filterFoldout, nameof(filter.CollidesWith), unchecked((int)filter.CollidesWith), field =>
            {
                field.SetValueWithoutNotify(unchecked((int)collider->GetCollisionFilter().CollidesWith));
            });

            var groupIndexField = AddField<IntegerField, int>(filterFoldout, nameof(filter.GroupIndex), filter.GroupIndex, field =>
            {
                field.SetValueWithoutNotify(collider->GetCollisionFilter().GroupIndex);
            }, editable);

            if (editable)
            {
                belongsToField.RegisterValueChangedCallback(
                    changeEvent =>
                    {
                        var newFilter = collider->GetCollisionFilter(ColliderKey.Empty);
                        newFilter.BelongsTo = unchecked((uint)changeEvent.newValue);
                        collider->SetCollisionFilter(newFilter, ColliderKey.Empty);
                    });

                collidesWithField.RegisterValueChangedCallback(
                    changeEvent =>
                    {
                        var newFilter = collider->GetCollisionFilter(ColliderKey.Empty);
                        newFilter.CollidesWith = unchecked((uint)changeEvent.newValue);
                        collider->SetCollisionFilter(newFilter, ColliderKey.Empty);
                    });

                groupIndexField.RegisterValueChangedCallback(
                    changeEvent =>
                    {
                        var newFilter = collider->GetCollisionFilter(ColliderKey.Empty);
                        newFilter.GroupIndex = changeEvent.newValue;
                        collider->SetCollisionFilter(newFilter, ColliderKey.Empty);
                    });
            }
        }

        public override void Update()
        {
            if (_lastBlobHash != Target.GetHashCode())
            {
                m_Root.Clear();
                _lastBlobHash = Target.GetHashCode();
                DoBuild(m_Root);
            }
        }
    }
}
