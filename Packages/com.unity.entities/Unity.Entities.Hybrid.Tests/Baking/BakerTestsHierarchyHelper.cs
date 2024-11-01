using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities.Conversion;
using Unity.Entities.Tests;
using Unity.Scenes.Editor.Tests;
using UnityEngine;

namespace Unity.Entities.Hybrid.Tests.Baking
{
   public class BakerTestsHierarchyHelper
   {
        public static GameObject CreateParentHierarchyWithType<T>(BakerTestsHierarchyHelper.ParentHierarchyMaskTests maskEnum, GameObject current, out int added) where T : Component
        {
            uint mask = (uint)maskEnum;
            List<GameObject> objects = new List<GameObject>();
            objects.Add(current);
            for (uint maskIter = mask; maskIter > 1; maskIter >>= 1)
            {
                GameObject child = new GameObject();
                child.transform.SetParent(current.transform);
                current = child;
                objects.Add(current);
            }

            int index = objects.Count - 1;
            added = 0;
            for (uint maskIter = mask; maskIter > 0; maskIter >>= 1)
            {
                if ((maskIter & 1) != 0)
                {
                    objects[index].AddComponent<T>();
                    ++added;
                }
                --index;
            }
            return current;
        }

        public static GameObject CreateParentHierarchy(int gameObjectCount, GameObject current)
        {
            List<GameObject> objects = new List<GameObject>();
            objects.Add(current);
            for (int index = 1; index < gameObjectCount; ++index)
            {
                GameObject child = new GameObject();
                child.transform.SetParent(current.transform);
                current = child;
                objects.Add(current);
            }
            return current;
        }

        // This recursive function is used to create a hierarchy with complete tree and return all the gameobjects in a flatten list
        public static void CreateChildrenHierarchy(GameObject parent, uint currentLevel, int childrenCount, List<GameObject> created)
        {
            if (currentLevel > 0)
            {
                for (int index = 0; index < childrenCount; ++index)
                {
                    GameObject child = new GameObject();
                    child.transform.SetParent(parent.transform);
                    created.Add(child);

                    CreateChildrenHierarchy(child, currentLevel - 1, childrenCount, created);
                }
            }
        }

        // This function adds a BoxCollider to the objects in the list based in the bist of the mask
        // bit 0 = 1 -> AddComponent(objects[0])
        // bit 1 = 1 -> AddComponent(objects[1])
        // bit 2 = 1 -> AddComponent(objects[2])
        // ...
        public static void AddTypeBasedOnMask<T>(uint mask, List<GameObject> objects, out int added)  where T : Component
        {
            int index = 0;
            added = 0;
            for (uint maskIter = mask; maskIter > 0 && index < objects.Count; maskIter >>= 1)
            {
                if ((maskIter & 1) != 0)
                {
                    objects[index].AddComponent<T>();
                    ++added;
                }
                ++index;
            }
        }

        public static GameObject CreateChildrenHierarchyWithType<T>(uint depth, int childrenCount, uint mask, GameObject root, out int added) where T : Component
        {
            List<GameObject> objects = new List<GameObject>();
            objects.Add(root);
            CreateChildrenHierarchy(root, depth - 1, childrenCount, objects);
            AddTypeBasedOnMask<T>(mask, objects, out added);
            return root;
        }

        public static List<GameObject> CreateChildrenHierarchyWithTypeList<T>(uint depth, int childrenCount, uint mask, GameObject root, out int added) where T : Component
        {
            List<GameObject> objects = new List<GameObject>();
            objects.Add(root);
            CreateChildrenHierarchy(root, depth - 1, childrenCount, objects);
            AddTypeBasedOnMask<T>(mask, objects, out added);
            return objects;
        }

        public enum ParentHierarchyMask
        {
            NoComponent =                 0,
            ComponentInGameObject =       1 << 0,
            ComponentInParent =           1 << 1,
            ComponentInParentParent =     1 << 2
        }

        public enum ParentHierarchyMaskTests
        {
            NoComponent                     = ParentHierarchyMask.NoComponent,                                                                                                      // 0u
            BaseObject                      = ParentHierarchyMask.ComponentInGameObject,                                                                                            // 1u
            FirstParent                     = ParentHierarchyMask.ComponentInParent,                                                                                                // 10u
            FirstParentAndBaseObject        = ParentHierarchyMask.ComponentInParent | ParentHierarchyMask.ComponentInGameObject,                                                    // 11u
            SecondParent                    = ParentHierarchyMask.ComponentInParentParent,                                                                                          // 100u
            SecondParentAndBaseObject       = ParentHierarchyMask.ComponentInParentParent | ParentHierarchyMask.ComponentInGameObject,                                              // 101u
            FirstAndSecondParent            = ParentHierarchyMask.ComponentInParent | ParentHierarchyMask.ComponentInParentParent,                                                  // 110u
            All                             = ParentHierarchyMask.ComponentInParent | ParentHierarchyMask.ComponentInParentParent | ParentHierarchyMask.ComponentInGameObject,      // 111u
        }

        public static List<GameObject> CreateHierarchyWithType<T>(ParentHierarchyMaskTests maskEnum, GameObject current, out int added)  where T : Component
        {
            List<GameObject> objects = new List<GameObject>();
            objects.Add(current);
            uint mask = (uint)maskEnum;
            for (uint maskIter = mask; maskIter > 1; maskIter >>= 1)
            {
                GameObject child = new GameObject();
                child.transform.SetParent(current.transform);
                current = child;
                objects.Add(current);
            }

            int index = objects.Count - 1;
            added = 0;
            for (uint maskIter = mask; maskIter > 0; maskIter >>= 1)
            {
                if ((maskIter & 1) != 0)
                {
                    objects[index].AddComponent<T>();
                    ++added;
                }
                --index;
            }
            return objects;
        }

        public enum HierarchyChildrenBits
        {
            Root    = 1,
            ChildA  = 2,
            ChildAA = 4,
            ChildAB = 8,
            ChildB  = 16,
            ChildBA = 32,
            ChildBB = 64,
        }

        public enum HierarchyChildrenTests
        {
            None                    = 0,
            RootOnly                = HierarchyChildrenBits.Root,
            FirstChild              = HierarchyChildrenBits.ChildA,
            SecondChild             = HierarchyChildrenBits.ChildB,
            RootAndFirstChild       = HierarchyChildrenBits.Root | HierarchyChildrenBits.ChildA,
            RootAndSecondChild      = HierarchyChildrenBits.Root | HierarchyChildrenBits.ChildB,
            RootAndChildren         = HierarchyChildrenBits.Root | HierarchyChildrenBits.ChildA | HierarchyChildrenBits.ChildB,
            OnlyChildren            = HierarchyChildrenBits.ChildA | HierarchyChildrenBits.ChildB,

            ChildAA                 = HierarchyChildrenBits.ChildAA,
            ChildAB                 = HierarchyChildrenBits.ChildAB,
            ChildBA                 = HierarchyChildrenBits.ChildBA,
            ChildBB                 = HierarchyChildrenBits.ChildBB,

            Root_ChildAA            = HierarchyChildrenBits.Root | HierarchyChildrenBits.ChildAA,
            Root_ChildAB            = HierarchyChildrenBits.Root | HierarchyChildrenBits.ChildAB,
            Root_ChildBA            = HierarchyChildrenBits.Root | HierarchyChildrenBits.ChildBA,
            Root_ChildBB            = HierarchyChildrenBits.Root | HierarchyChildrenBits.ChildBB,

            Children_AA_AB          = HierarchyChildrenBits.ChildAA | HierarchyChildrenBits.ChildAB,
            Children_A_AA_AB        = HierarchyChildrenBits.ChildA | HierarchyChildrenBits.ChildAA | HierarchyChildrenBits.ChildAB,
            Children_BA_BB          = HierarchyChildrenBits.ChildBA | HierarchyChildrenBits.ChildBB,
            Children_B_BA_BB        = HierarchyChildrenBits.ChildB | HierarchyChildrenBits.ChildBA | HierarchyChildrenBits.ChildBB,
            Children_B_AA_AB        = HierarchyChildrenBits.ChildB | HierarchyChildrenBits.ChildAA | HierarchyChildrenBits.ChildAB,
            Children_A_BA_BB        = HierarchyChildrenBits.ChildA | HierarchyChildrenBits.ChildBA | HierarchyChildrenBits.ChildBB,

            All                     = HierarchyChildrenBits.Root | HierarchyChildrenBits.ChildA | HierarchyChildrenBits.ChildAA | HierarchyChildrenBits.ChildAB | HierarchyChildrenBits.ChildB | HierarchyChildrenBits.ChildBA | HierarchyChildrenBits.ChildBB
        }
    }
}
