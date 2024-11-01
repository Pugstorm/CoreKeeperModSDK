using System;
using UnityEngine;

namespace Unity.Entities
{
    /// <summary>
    /// Attribute that informs the baking system that bakers need to be run on the authoring component
    /// even when the authoring component is disabled.
    /// </summary>
    internal class ForceBakingOnDisabledComponentsAttribute : Attribute { }
}
