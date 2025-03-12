using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Physics.Authoring;
using UnityEngine;

namespace Unity.Physics.Tests.Authoring
{
    class HelpURLs_UnitTests
    {
        static readonly IEnumerable<Type> AllAuthoringTypes = typeof(BaseShapeBakingSystem).Assembly.GetTypes().Where(
            t => t.IsPublic
            && !t.IsAbstract
            && !t.IsGenericType
            && (t.IsSubclassOf(typeof(ScriptableObject)) || t.IsSubclassOf(typeof(MonoBehaviour)))
            && t.GetCustomAttributes().Count(a => a is ObsoleteAttribute) == 0
        );

        [Test]
        public void AuthoringType_HasProperlyFormattedHelpURL([ValueSource(nameof(AllAuthoringTypes))] Type type)
        {
            var attr = type.GetCustomAttribute(typeof(HelpURLAttribute)) as HelpURLAttribute;
            Assume.That(attr, Is.Not.Null, "Public authoring type has no HelpURLAttribute");

            var url = attr.URL;
            Assert.That(url, Contains.Substring(type.FullName), "HelpURLAttribute does not reference proper type name");
        }
    }
}
