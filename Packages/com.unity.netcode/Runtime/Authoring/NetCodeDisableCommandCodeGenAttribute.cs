using System;

namespace Unity.NetCode
{
    /// <summary>
    /// This attribute is used to disable code generation for a struct implementing ICommandData or IRpcCommand
    /// </summary>
    [AttributeUsage(AttributeTargets.Class|AttributeTargets.Struct)]
    public class NetCodeDisableCommandCodeGenAttribute : Attribute
    {
    }
}