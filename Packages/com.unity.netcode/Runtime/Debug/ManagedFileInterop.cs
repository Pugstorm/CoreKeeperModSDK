#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif
#if NETCODE_DEBUG
using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.NetCode.LowLevel.Unsafe
{
    internal unsafe struct ManagedFileInterop
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void CloseDelegate(IntPtr fileStream);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void WriteDelegate(IntPtr fileStream, in UnsafeText text);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr OpenDelegate(in FixedString512Bytes filename);
        static ManagedFileInterop()
        {
            ManagedFunctionPtr<CloseDelegate, ManagedFileInterop>.Init(ManagedClose);
            ManagedFunctionPtr<WriteDelegate, ManagedFileInterop>.Init(ManagedWrite);
            ManagedFunctionPtr<OpenDelegate, ManagedFileInterop>.Init(ManagedOpen);
        }
        [AOT.MonoPInvokeCallback(typeof(CloseDelegate))]
        [BurstDiscard]
        static void ManagedClose(IntPtr fileStream)
        {
            var handle = GCHandle.FromIntPtr(fileStream);
            var stream = handle.Target as FileStream;
            stream?.Close();
            handle.Free();
        }
        [AOT.MonoPInvokeCallback(typeof(WriteDelegate))]
        [BurstDiscard]
        static void ManagedWrite(IntPtr fileStream, in UnsafeText text)
        {
            var stream = GCHandle.FromIntPtr(fileStream).Target as FileStream;
            stream?.Write(new ReadOnlySpan<byte>(text.GetUnsafePtr(), text.Length));
        }
        [AOT.MonoPInvokeCallback(typeof(OpenDelegate))]
        [BurstDiscard]
        public static IntPtr ManagedOpen(in FixedString512Bytes filename)
        {
            var fName = Path.GetFullPath(filename.ToString());
            var directory = Path.GetDirectoryName(fName);
            if(directory != null)
                Directory.CreateDirectory(directory);
            var stream = new System.IO.FileStream(fName, FileMode.Create, FileAccess.Write, FileShare.Read);
            var handle = GCHandle.Alloc(stream);
            return GCHandle.ToIntPtr(handle);
        }

        public static IntPtr Open(in FixedString512Bytes filename)
        {
            var ptr = ManagedFunctionPtr<ManagedFileInterop.OpenDelegate, ManagedFileInterop>.Ptr;
            return ((delegate *unmanaged[Cdecl]<in FixedString512Bytes, IntPtr>)ptr)(filename);
        }
        public static void Write(IntPtr fileStream, in UnsafeText text)
        {
            var ptr = ManagedFunctionPtr<ManagedFileInterop.WriteDelegate, ManagedFileInterop>.Ptr;
            ((delegate *unmanaged[Cdecl]<IntPtr, in UnsafeText, void>)ptr)(fileStream, text);
        }
        public static void Close(IntPtr fileStream)
        {
            var ptr = ManagedFunctionPtr<ManagedFileInterop.CloseDelegate, ManagedFileInterop>.Ptr;
            ((delegate *unmanaged[Cdecl]<IntPtr, void>)ptr)(fileStream);
        }
    }
}
#endif
