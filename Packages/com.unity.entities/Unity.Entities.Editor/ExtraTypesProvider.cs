using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Player;

namespace Unity.Entities.Editor
{
    [InitializeOnLoad]
    sealed class ExtraTypesProvider
    {
        static ExtraTypesProvider()
        {
            PlayerBuildInterface.ExtraTypesProvider += () =>
            {
                var extraTypes = new HashSet<string>();

                TypeManager.Initialize();

                foreach (var typeInfo in TypeManager.AllTypes)
                {
                    Type type = TypeManager.GetType(typeInfo.TypeIndex);
                    if (type != null)
                    {
                        FastEquality.AddExtraAOTTypes(type, extraTypes);
                    }
                }

                return extraTypes;
            };
        }
    }
}
