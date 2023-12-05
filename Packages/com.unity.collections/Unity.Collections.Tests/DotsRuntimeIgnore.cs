using System;
using NUnit.Framework;

namespace Unity.Collections.Tests
{
#if UNITY_DOTSRUNTIME
    internal class DotsRuntimeIgnore : IgnoreAttribute
    {
        public DotsRuntimeIgnore(string msg="") : base("Need to fix for DotsRuntime.")
        {
        }
    }

#else
    internal class DotsRuntimeIgnoreAttribute : Attribute
    {
        public DotsRuntimeIgnoreAttribute(string msg="")
        {
        }
    }
#endif
}
