#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        /// <summary>
        /// Record view into journal buffer.
        /// </summary>
        [GenerateTestsForBurstCompatibility(RequiredUnityDefine = "(UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING")]
        [DebuggerDisplay("{Index} - {RecordType}, FrameIndex = {FrameIndex}")]
        [DebuggerTypeProxy(typeof(RecordViewDebugView))]
        [StructLayout(LayoutKind.Sequential)]
        public unsafe readonly struct RecordView : IEquatable<RecordView>
        {
            [NativeDisableUnsafePtrRestriction] internal readonly byte* m_BufferPtr;

            Header* _HeaderPtr => (Header*)m_BufferPtr;
            int _EntitiesOffset => UnsafeUtility.SizeOf<Header>();
            Entity* _EntitiesPtr => (Entity*)(m_BufferPtr + _EntitiesOffset);
            int _TypeIndexOffset => _EntitiesOffset + (_HeaderPtr->EntityCount * UnsafeUtility.SizeOf<Entity>());
            TypeIndex* _TypeIndexPtr => (TypeIndex*)(m_BufferPtr + _TypeIndexOffset);
            int _DataOffset => _TypeIndexOffset + (_HeaderPtr->TypeCount * UnsafeUtility.SizeOf<int>());
            byte* _DataPtr => m_BufferPtr + _DataOffset;

            /// <summary>
            /// The record unique index.
            /// </summary>
            public ulong Index => _HeaderPtr->Index;

            /// <summary>
            /// The record type.
            /// </summary>
            public RecordType RecordType => _HeaderPtr->RecordType;

            /// <summary>
            /// The record frame index.
            /// </summary>
            public int FrameIndex => _HeaderPtr->FrameIndex;

            /// <summary>
            /// The record world view.
            /// </summary>
            public WorldView World => new WorldView(&_HeaderPtr->WorldSequenceNumber);

            /// <summary>
            /// The record executing system view.
            /// </summary>
            public SystemView ExecutingSystem => new SystemView(&_HeaderPtr->ExecutingSystem);

            /// <summary>
            /// The record origin system view.
            /// In the case of deferred changes, this will tell where the deferred command originated from. Otherwise it will be a default value.
            /// </summary>
            public SystemView OriginSystem => new SystemView(&_HeaderPtr->OriginSystem);

            /// <summary>
            /// The record entities view.
            /// </summary>
            public EntityViewArray Entities => new EntityViewArray(_EntitiesPtr, _HeaderPtr->EntityCount, &_HeaderPtr->WorldSequenceNumber);

            /// <summary>
            /// The record component types view.
            /// </summary>
            public ComponentTypeViewArray ComponentTypes => new ComponentTypeViewArray(_TypeIndexPtr, _HeaderPtr->TypeCount);

            /// <summary>
            /// The record payload data pointer.
            /// The content of those bytes depends on the record type.
            /// </summary>
            public byte* DataPtr => _DataPtr;

            /// <summary>
            /// The record payload data length.
            /// </summary>
            public int DataLength => _HeaderPtr->DataLength;

            /// <summary>
            /// The record payload data.
            /// </summary>
            [ExcludeFromBurstCompatTesting("Returns managed object")]
            public object Data => GetRecordData(this);

            internal RecordView(byte* bufferPtr)
            {
                m_BufferPtr = bufferPtr;
            }

            internal byte* Ptr => m_BufferPtr;
            internal int Length => (int)(DataPtr + DataLength - m_BufferPtr);

            public bool Equals(RecordView other) => m_BufferPtr == other.m_BufferPtr;
            [ExcludeFromBurstCompatTesting("Takes managed object")] public override bool Equals(object obj) => obj is RecordView record ? Equals(record) : false;
            public override int GetHashCode() => new IntPtr(m_BufferPtr).GetHashCode();
            public static bool operator ==(RecordView lhs, RecordView rhs) => lhs.m_BufferPtr == rhs.m_BufferPtr;
            public static bool operator !=(RecordView lhs, RecordView rhs) => !(lhs == rhs);
            public static RecordView Null => new RecordView();
        }

        internal sealed class RecordViewDebugView
        {
            readonly RecordView m_RecordView;

            public RecordViewDebugView(RecordView recordView)
            {
                m_RecordView = recordView;
            }

            public ulong Index => m_RecordView.Index;
            public RecordType RecordType => m_RecordView.RecordType;
            public int FrameIndex => m_RecordView.FrameIndex;
            public WorldView World => m_RecordView.World;
            public SystemView ExecutingSystem => m_RecordView.ExecutingSystem;
            public SystemView OriginSystem => m_RecordView.OriginSystem;
            public EntityViewArray Entities => m_RecordView.Entities;
            public ComponentTypeViewArray ComponentTypes => m_RecordView.ComponentTypes;
            public object Data => m_RecordView.Data;
        }
    }
}
#endif
