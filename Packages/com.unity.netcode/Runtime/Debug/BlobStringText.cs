using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Unity.NetCode.LowLevel
{
    /// <summary>
    /// Simple <see cref="BlobString"/> wrapper that can be embedded into components and allow
    /// to access the blob text as <see cref="IUTF8Bytes"/> and <see cref="INativeList{T}"/>.
    /// The text is considered readonly. All methods that change or affect the string will throw <see cref="NotImplementedException"/>.
    /// </summary>
    public struct BlobStringText: INativeList<byte>, IUTF8Bytes
    {
        [NativeDisableUnsafePtrRestriction] private IntPtr m_Text;
        private int m_Length;

        /// <summary>
        /// Construct the text from a <see cref="BlobString"/> reference. The string pointer
        /// is cached internally by this wrapper and if the original blob is detroyed, the memory content
        /// may point to something that it is not a string.
        /// </summary>
        /// <param name="blob"></param>
        public BlobStringText(ref BlobString blob)
        {
            unsafe
            {
                m_Text = (IntPtr)UnsafeUtility.As<BlobString, BlobArray<byte>>(ref blob).GetUnsafePtr();
            }
            m_Length = blob.Length;
        }
        
        /// <inheritdoc cref="IUTF8Bytes.IsEmpty"/>
        public bool IsEmpty => m_Length == 0;

        /// <inheritdoc cref="IUTF8Bytes.GetUnsafePtr"/>
        public unsafe byte* GetUnsafePtr()
        {
            return (byte*)m_Text;
        }

        /// <inheritdoc cref="IUTF8Bytes.TryResize"/>
        /// <remarks>Always throw NotImplementedException</remarks>
        /// <exception cref="NotImplementedException"></exception>
        public bool TryResize(int newLength, NativeArrayOptions clearOptions = NativeArrayOptions.ClearMemory)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc cref="INativeList{T}.Length"/>
        /// <remarks>Always throw NotImplementedException</remarks>
        /// <exception cref="NotImplementedException"></exception>
        public int Length
        {
            get => m_Length;
            set => throw new NotImplementedException();
        }
        /// <inheritdoc cref="INativeList{T}.ElementAt"/>
        /// <remarks>Always throw NotImplementedException</remarks>
        public ref byte ElementAt(int index)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc cref="INativeList{T}.Capacity"/>
        /// <remarks>Always throw NotImplementedException</remarks>
        /// <exception cref="NotImplementedException"></exception>
        public int Capacity {
            get => m_Length;
            set => throw new NotImplementedException();
        }
        /// <inheritdoc cref="INativeList{T}.this[int]"/>
        /// <remarks>Always throw NotImplementedException</remarks>
        /// <exception cref="NotImplementedException"></exception>
        public byte this[int index]
        {
            get
            {
                unsafe { return *((byte*)m_Text); }
            }
            set => throw new NotImplementedException();
        }
        /// <inheritdoc cref="INativeList{T}.Clear"/>
        /// <remarks>Always throw NotImplementedException</remarks>
        /// <exception cref="NotImplementedException"></exception>
        public void Clear()
        {
            throw new NotImplementedException();
        }
    }
}
