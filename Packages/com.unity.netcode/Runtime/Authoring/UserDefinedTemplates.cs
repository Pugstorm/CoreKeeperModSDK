// IMPORTANT NOTE: This file is shared with NetCode source generators
// NO UnityEngine, UnityEditore or other packages dll references are allowed here.
using System.Collections.Generic;

namespace Unity.NetCode.Generators
{
    ///<summary>
    /// UserDefinedTemplates is used to add custom templates to the code-generation system.
    /// Add a partial class definition to an AssemblyDefinitionReference (.asmref) referencing Unity.NetCode,
    /// implement the <see cref="RegisterTemplates"/> method by adding your new typesto the templates list.
    /// </summary>
    public static partial class UserDefinedTemplates
    {
        internal static List<TypeRegistryEntry> Templates;

        static UserDefinedTemplates()
        {
            Templates = new List<TypeRegistryEntry>();
            RegisterTemplates(Templates, "Packages/com.unity.netcode/Editor/Templates");
        }
        static partial void RegisterTemplates(List<TypeRegistryEntry> templates, string defaultRootPath);
    }
}
