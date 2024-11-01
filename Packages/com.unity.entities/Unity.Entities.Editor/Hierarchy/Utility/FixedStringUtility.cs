using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Editor
{
    static class FixedStringUtility
    {
        public static unsafe FixedString64Bytes ToLower(in FixedString64Bytes name)
        {
            var str = name;
            Utf8ToLower(str.GetUnsafePtr(), str.Length);
            return str;
        }

        public static unsafe void Utf8ToLower(byte* ptr, int length)
        {
            var readIndex = 0;
            
            while (readIndex < length)
            {
                var writeIndex = readIndex;
                Unicode.Utf8ToUcs(out var rune, ptr, ref readIndex, length);
                        
                var c = rune.value;

                if (c >= 'A' && c <= 'Z')
                {
                    // ascii upper range.
                    rune.value |= 32;
                    Unicode.UcsToUtf8(ptr, ref writeIndex, length, rune);
                }
                else if (c >= 'À' && c <= 'Ý')
                {
                    // extended ascii safe upper range based on looking at the table.
                    rune.value |= 32;
                    Unicode.UcsToUtf8(ptr, ref writeIndex, length, rune);
                }
            }
        }
    }
}