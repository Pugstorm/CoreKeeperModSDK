using System;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.NetCode
{
    /// <summary>
    /// A collection of utility to assign constant colors for the NetworkId's. There are in total 13 unique colors,
    /// with 14+ mapping to the original.
    /// </summary>
    public static class NetworkIdDebugColorUtility
    {
        /// <summary>
        /// Get the constant color assigned to the given network id.
        /// </summary>
        /// <param name="networkId"></param>
        /// <returns>A constant debug color for NetworkId's to aid in debugging</returns>
        public static float4 Get(int networkId)
        {
            var colorIndex = networkId % 13;
            return colorIndex switch
            {
                1 => new float4(1f, 0.24f, 0.23f, 1f),
                2 => new float4(0.17f, 0.21f, 1f, 1f),
                3 => new float4(0.36f, 1f, 0.34f, 1f),
                4 => new float4(0.98f, 1f, 0f, 1f),
                5 => new float4(1f, 0.27f, 0.99f, 1f),
                6 => new float4(0.32f, 0.32f, 0.32f, 1f),
                7 => new float4(0.93f, 0.93f, 0.93f, 1f),
                8 => new float4(1f, 0.54f, 0f, 1f),
                9 => new float4(0.26f, 0.87f, 1f, 1f),
                10 => new float4(0.38f, 0.01f, 0.79f, 1f),
                11 => new float4(0.34f, 0.13f, 0.09f, 1f),
                12 => new float4(1f, 0.69f, 0.6f, 1f),
                _ => new float4(0.57f, 0.57f, 0.57f, 1f),
            };
        }

        /// <inheritdoc cref="NetworkIdDebugColorUtility.Get"/>
        public static Color GetColor(int networkId)
        {
            var color4 = Get(networkId);
            return new Color(color4.x, color4.y, color4.z);
        }
    }
}
