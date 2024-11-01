using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Internal;
using UnityEngine;
using Unity.Properties;

namespace Unity.Collections
{
	[Serializable]
    [StructLayout(LayoutKind.Sequential, Size=32)]
    [GenerateTestsForBurstCompatibility]
    [Obsolete("Just for old serialization support, use FixedString32Bytes instead")]
    public partial struct FixedString32
    {
        internal const ushort utf8MaxLengthInBytes = 29;
        [SerializeField] internal ushort utf8LengthInBytes;
        [SerializeField] internal FixedBytes30 bytes;
    }

	[Serializable]
    [StructLayout(LayoutKind.Sequential, Size=64)]
    [GenerateTestsForBurstCompatibility]
    [Obsolete("Just for old serialization support, use FixedString64Bytes instead")]
    public partial struct FixedString64
    {
        internal const ushort utf8MaxLengthInBytes = 61;
        [SerializeField] internal ushort utf8LengthInBytes;
        [SerializeField] internal FixedBytes62 bytes;
    }

	[Obsolete("Renamed to FixedString128Bytes (UnityUpgradable) -> FixedString128Bytes", true)]
    public partial struct FixedString128 {}

	[Obsolete("Renamed to FixedString512Bytes (UnityUpgradable) -> FixedString512Bytes", true)]
    public partial struct FixedString512 {}

	[Obsolete("Renamed to FixedString4096Bytes (UnityUpgradable) -> FixedString4096Bytes", true)]
    public partial struct FixedString4096 {}
}
