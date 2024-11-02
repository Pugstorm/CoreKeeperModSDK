using System.Reflection;
using UnityEngine;

namespace Unity.Entities.Editor
{
    // This helper is here to encapsulate the access to `pixelsPerPoint` private property on Texture2D.
    // We need to access this property to enable bilinear filter mode on the texture when its pixel
    // per point is different from the editor ppp.
    // TODO: @antoineb remove this when ppp is public or thanks to a cleaner solution
    static class InternalsHelpers
    {
        static readonly PropertyInfo s_TexturePixelsPerPoint;

        static InternalsHelpers()
        {
            s_TexturePixelsPerPoint = typeof(Texture2D).GetProperty("pixelsPerPoint", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        }

        public static float GetPixelsPerPoint(this Texture2D @this)
        {
            var v = s_TexturePixelsPerPoint?.GetValue(@this);
            if (v == null)
                return 1.0f;

            return (float)v;
        }
    }
}
