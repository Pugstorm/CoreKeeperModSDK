using System;
using System.Diagnostics;
using System.Text;
using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Entities
{
    sealed class EntityNameStorageDebugView
    {
        public FixedString64Bytes[] Table
        {
            get
            {
                var table = new FixedString64Bytes[EntityNameStorage.Entries];
                for (var i = 0; i < EntityNameStorage.Entries; ++i)
                    EntityNameStorage.GetFixedString(i, ref table[i]);
                return table;
            }
        }
    }

    /// <summary>
    /// Can hold <see cref="kMaxEntries"/> (up to <see cref="kMaxChars"/>) entity names.
    /// Duplicate entries (with identical hashes) share the same location (<see cref="GetIndexFromHashAndFixedString"/>).
    /// Once added, a name cannot be removed.
    /// Will throw if store is full.
    /// </summary>
    [DebuggerTypeProxy(typeof(EntityNameStorageDebugView))]
    [GenerateTestsForBurstCompatibility]
    internal struct EntityNameStorage
    {
        internal struct Entry
        {
            public int offset;
            public int length;
        }

        internal struct State
        {
            public byte initialized;
            internal byte hasLoggedError;
            public UnsafeList<byte> buffer; // all the UTF-8 encoded bytes in one place
            public UnsafeList<Entry> entry; // one offset for each text in "buffer"
            public UnsafeParallelMultiHashMap<int, int> hash; // from string hash to table entry
            public int chars; // bytes in buffer allocated so far
            public int entries; // number of strings allocated so far
            public FixedString512Bytes kMaxEntriesMsg;
        }
        internal static readonly SharedStatic<State> s_State = SharedStatic<State>.GetOrCreate<EntityNameStorage>();


        internal const int kMaxEntries = 16 << 10;
        internal const int kMaxChars = kMaxEntries * 64;
        internal const int kErrorExceedMaxEntryCapacity = -1;



        public const int kEntityNameMaxLengthBytes = FixedString64Bytes.utf8MaxLengthInBytes;

        /// <summary>
        /// Returns the number of stored entries (not characters). I.e. Length or Count.
        /// </summary>
        public static int Entries => s_State.Data.entries;

        public static void Initialize()
        {
            if (s_State.Data.initialized != 0)
                return;

            s_State.Data.buffer = new UnsafeList<byte>(kMaxChars, Allocator.Persistent);
            s_State.Data.buffer.Length = s_State.Data.buffer.Capacity;
            s_State.Data.entry = new UnsafeList<Entry>(kMaxEntries, Allocator.Persistent);
            s_State.Data.entry.Length = s_State.Data.entry.Capacity;
            s_State.Data.hash = new UnsafeParallelMultiHashMap<int, int>(kMaxEntries, Allocator.Persistent);
            Clear();
            s_State.Data.initialized = 1;
            s_State.Data.hasLoggedError = 0;
            s_State.Data.kMaxEntriesMsg = "Max unique Entity Name capacity exceeded. If you require more storage, edit EntityNameStorage.cs and change the value of kMaxEntries to pre-allocate more space.";
        }

        public static void Shutdown()
        {
            if (s_State.Data.initialized == 0)
                return;

            s_State.Data.buffer.Dispose();
            s_State.Data.entry.Dispose();
            s_State.Data.hash.Dispose();
            s_State.Data.initialized = 0;
            s_State.Data.hasLoggedError = 0;
        }

        /// <summary>
        /// Clears the store.
        /// </summary>
        public static void Clear()
        {
            s_State.Data.chars = 0;
            s_State.Data.entries = 0;
            s_State.Data.hash.Clear();
            var temp = new FixedString64Bytes();
            GetOrCreateIndex(in temp); // make sure that Index=0 means empty string
        }

        /// <summary>
        /// Copies the stored FixedString64Bytes (at this index) into the ref.
        /// Asserts if out of range.
        /// </summary>
        public static unsafe void GetFixedString(int index, ref FixedString64Bytes temp)
        {
            Assert.IsTrue(index < s_State.Data.entries);
            var e = s_State.Data.entry[index];
            Assert.IsTrue(e.length <= kEntityNameMaxLengthBytes);
            temp.Length = math.min(e.length, temp.Capacity);
            UnsafeUtility.MemCpy(temp.GetUnsafePtr(), s_State.Data.buffer.Ptr + e.offset, temp.Length);
        }

        /// <remarks>
        /// You must pass in both the hash of the FixedString, and the FixedString itself.
        /// </remarks>
        /// <returns>If found, returns the index of the name. Otherwise, -1 and default(FixedString64Bytes).</returns>
        public static int GetIndexFromHashAndFixedString(int hash, in FixedString64Bytes fixedString)
        {
            Assert.IsTrue(fixedString.Length <= kEntityNameMaxLengthBytes);
            Assert.AreEqual(hash, fixedString.GetHashCode()); // The inputted hash must be the hash of the FixedString.
            int itemIndex;
            NativeParallelMultiHashMapIterator<int> iter;
            if (s_State.Data.hash.TryGetFirstValue(hash, out itemIndex, out iter))
            {
                do
                {
                    var e = s_State.Data.entry[itemIndex];
                    Assert.IsTrue(e.length <= kEntityNameMaxLengthBytes);
                    if (e.length == fixedString.Length)
                    {
                        int matches;
                        for (matches = 0; matches < e.length; ++matches)
                            if (fixedString[matches] != s_State.Data.buffer[e.offset + matches])
                                break;
                        if (matches == fixedString.Length)
                            return itemIndex;
                    }
                } while (s_State.Data.hash.TryGetNextValue(out itemIndex, ref iter));
            }
            return -1;
        }

        /// <returns>Returns true if the Store contains this FixedString.</returns>
        /// <see cref="GetIndexFromHashAndFixedString"/>
        public static bool Contains(in FixedString64Bytes value)
        {
            int h = value.GetHashCode();
            return GetIndexFromHashAndFixedString(h, in value) != -1;
        }

        /// <returns>Returns true if the Store contains this name (in FixedString form).</returns>
        /// <see cref="GetIndexFromHashAndFixedString"/>
        [ExcludeFromBurstCompatTesting("Takes managed string")]
        public static unsafe bool Contains(string value)
        {
            FixedString64Bytes temp = value;
            return Contains(in temp);
        }

        /// <summary>
        /// If the store contains this name already, returns the index of it.
        /// Otherwise, assigns the next free index, and saves the name into the store.
        /// </summary>
        /// <returns>New or existing index of value (i.e. name).</returns>
        public static int GetOrCreateIndex(in FixedString64Bytes value)
        {
            int h = value.GetHashCode();
            var itemIndex = GetIndexFromHashAndFixedString(h, in value);

            if (itemIndex != kErrorExceedMaxEntryCapacity)
                return itemIndex;

            //return an invalid value when table entry is full
            if (s_State.Data.entries >= kMaxEntries)
                return kErrorExceedMaxEntryCapacity;

            Assert.IsTrue(s_State.Data.chars + value.Length <= kMaxChars);
            var o = s_State.Data.chars;
            var l = (ushort)value.Length;
            for (var i = 0; i < l; ++i)
                s_State.Data.buffer[s_State.Data.chars++] = value[i];
            s_State.Data.entry[s_State.Data.entries] = new Entry { offset = o, length = l };
            s_State.Data.hash.Add(h, s_State.Data.entries);
            return s_State.Data.entries++;
        }
    }

    /// <remarks>
    /// An "EntityName" is an integer that refers to 4,096 or fewer chars of UTF-16 text in a global storage blob.
    /// Each should refer to *at most* about one printed page of text.
    ///
    /// If you need more text, consider using one EntityName struct for each printed page's worth.
    ///
    /// Each EntityName instance that you create is stored in a single, internally-managed EntityNameStorage object,
    /// which can hold up to 16,384 EntityName entries. Once added, the entries in EntityNameStorage cannot be modified
    /// or removed.
    /// </remarks>
    [GenerateTestsForBurstCompatibility]
    internal struct EntityName
    {
        internal int Index;

        /// <summary>
        /// Writes the FixedString at this corresponding <see cref="Index"/> into value.
        /// </summary>
        public void ToFixedString(ref FixedString64Bytes value)
        {
            EntityNameStorage.GetFixedString(Index, ref value);
        }

        /// <summary>
        /// System.String equivalent of <see cref="ToFixedString"/>.
        /// </summary>
        [ExcludeFromBurstCompatTesting("Returns managed string")]
        public override string ToString()
        {
            FixedString64Bytes temp = default;
            ToFixedString(ref temp);
            return temp.ToString();
        }

        /// <summary>
        /// Writes the FixedString value into the store, cached as an <see cref="Index"/>.
        /// </summary>
        public void SetFixedString(in FixedString64Bytes value)
        {
            int tryIndex = EntityNameStorage.GetOrCreateIndex(in value);

            if(tryIndex >= 0)
                Index = tryIndex;
            else if(EntityNameStorage.s_State.Data.hasLoggedError == 0)
            {
                UnityEngine.Debug.LogError(EntityNameStorage.s_State.Data.kMaxEntriesMsg);
                EntityNameStorage.s_State.Data.hasLoggedError++;
            }
        }
    }
}
