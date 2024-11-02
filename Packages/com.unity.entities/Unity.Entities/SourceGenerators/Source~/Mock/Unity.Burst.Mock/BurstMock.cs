using System.Runtime.InteropServices;

namespace Unity.Burst
{
    public class BurstMock { }

    namespace Intrinsics
    {
        [StructLayout(LayoutKind.Explicit)]
        public struct v128
        {
            [FieldOffset(0)] public ulong ULong0;
            [FieldOffset(8)] public ulong ULong1;
        }
    }

    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Struct | AttributeTargets.ReturnValue)]
    public class NoAliasAttribute : Attribute {}

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Assembly)]
    public class BurstCompileAttribute : System.Attribute
    {
        public FloatMode FloatMode { get; set; }
        public FloatPrecision FloatPrecision { get; set; }
        public bool CompileSynchronously { get; set; }
    }

    public enum FloatMode
    {
        Default,
        Strict,
        Deterministic,
        Fast,
    }

    public enum FloatPrecision
    {
        Standard,
        High,
        Medium,
        Low,
    }

    namespace CompilerServices
    {
        public static class Hint
        {
            public static bool Likely(bool condition) => condition;
            public static bool Unlikely(bool condition) => condition;
        }
    }
}

namespace AOT
{
    public class MonoPInvokeCallbackAttribute : Attribute
    {
        public MonoPInvokeCallbackAttribute(Type type) { }
    }
}
