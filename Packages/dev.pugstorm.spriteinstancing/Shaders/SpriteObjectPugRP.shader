Shader "SpriteObject/PugRP"
{
	Properties
	{
		_SurfaceMode ("Surface Mode", Float) = 0.0
		_ForwardOnly ("Forward Only", Float) = 0.0
		_TransparencyMode ("Transparency Mode", Float) = 0.0
		[ToggleOff(UNLIT_ON)] _LitOn ("Lit", Float) = 1.0
		[Toggle(ALPHATEST_ON)] _AlphaTestOn ("Alpha Test", Float) = 0.0
		[Toggle(PIVOT_DEPTH_PROJECTION)] _PivotDepthProjection ("Pivot Projection", Float) = 0.0
		[Toggle(THICK_OUTLINE)] _ThickOutline ("Thick Outline", Float) = 0.0
		[Toggle(TALL_SPRITE)] _TallSprite ("Tall Sprite", Float) = 0.0
		[Toggle(USE_NORMAL)] _UseNormal("Use Normal (Lambert shading, opaque only)", Float) = 0.0
		_ZWrite ("Write Depth", Float) = 0.0
		_ZTest ("Depth Test", Float) = 4.0
		_Cull ("Cull", Float) = 0.0
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue"="Geometry" }

		Cull [_Cull]
		ZTest [_ZTest]

		HLSLINCLUDE
		#define PPU 16
#if INSTANCING_ENABLED
		#define INSTANCING_ON
#endif

		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "SpriteObject.hlsl"
		#include "SpriteObjectInput.hlsl"

#if PIVOT_DEPTH_PROJECTION
		float DepthProjectPivot(v2f i)
		{
			#ifdef PUGRP_CORE_INCLUDED // Requires PugRP
			float2 screenUV = GetScreenUV(i.screenPos);
			float3 rayDir = GetCameraRay(screenUV);
			float3 positionWS = i.positionWS;

			float altitude = i.positionWS.y - i.worldSpacePivot.y;
			if (altitude < 0)
			{
				positionWS -= rayDir * (altitude - 1e-3) / rayDir.y;
			}

			return GetFragmentClipSpace(positionWS).z;
			#else
			return 0.0;
			#endif
		}
#endif
		ENDHLSL

		Pass
		{
			Blend One OneMinusSrcAlpha
			ZWrite [_ZWrite]

			Name "ForwardLit"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma require instancing
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma multi_compile _ SPRITE_INSTANCING_USE_COMPRESSED_ATLASES
			#pragma multi_compile _ SPRITE_INSTANCING_DISABLE_NORMAL_ATLAS
			#pragma multi_compile _ INSTANCING_ENABLED
			#pragma shader_feature PIVOT_DEPTH_PROJECTION
			#pragma shader_feature_fragment THICK_OUTLINE
			#pragma shader_feature_fragment UNLIT_ON
			#pragma shader_feature_fragment ALPHATEST_ON
			#pragma shader_feature_fragment ADDITIVE_ON

#ifdef PUGRP_CORE_INCLUDED 
			TEXTURE3D(_VolumetricLight);
			SAMPLER(sampler_VolumetricLight);
			float4x4 _WorldToVolumetric;

			float3 GetVolumetricLight(float3 positionWS)
			{
				float3 uvw = mul(_WorldToVolumetric, float4(positionWS, 1.0)).xyz;
				return SAMPLE_TEXTURE3D_LOD(_VolumetricLight, sampler_VolumetricLight, uvw, 0).rgb;
			}
#endif

#if PIVOT_DEPTH_PROJECTION
			float4 frag(v2f i, out float depthOut : SV_Depth) : SV_Target
#else
			float4 frag(v2f i) : SV_Target
#endif
			{
				float4 colorAlpha;
				float3 emission, normal;
				GetSpriteColor(i, colorAlpha, emission, normal);

#if ALPHATEST_ON
				clip(colorAlpha.a - 0.5);
#endif

#if !UNLIT_ON && defined(PUGRP_CORE_INCLUDED)
				colorAlpha.rgb *= GetVolumetricLight(i.positionWS);
#endif

				colorAlpha.rgb *= colorAlpha.a;

#if ADDITIVE_ON
				colorAlpha.a = 0.0;
#endif

				colorAlpha.rgb += emission;

#if PIVOT_DEPTH_PROJECTION
				depthOut = DepthProjectPivot(i);
#endif

				return colorAlpha;
			}
			
			ENDHLSL
		}

		Pass
		{
			Name "GBuffer"
			Tags { "LightMode" = "UniversalGBuffer" }

			HLSLPROGRAM
			#pragma require instancing
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#pragma multi_compile _ SPRITE_INSTANCING_USE_COMPRESSED_ATLASES
			#pragma multi_compile _ SPRITE_INSTANCING_DISABLE_NORMAL_ATLAS
			#pragma multi_compile _ INSTANCING_ENABLED
			#pragma shader_feature PIVOT_DEPTH_PROJECTION
			#pragma shader_feature_fragment THICK_OUTLINE
			#pragma shader_feature_fragment UNLIT_ON
			#pragma shader_feature_fragment ALPHATEST_ON
			#pragma shader_feature_fragment TALL_SPRITE
			#pragma shader_feature_fragment USE_NORMAL

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

#if PIVOT_DEPTH_PROJECTION
			FragmentOutput frag(v2f i, out float depthOut : SV_Depth)
#else
			FragmentOutput frag(v2f i)
#endif
			{
				float4 colorAlpha;
				float3 emission, normal;
				GetSpriteColor(i, colorAlpha, emission, normal);

#if ALPHATEST_ON
				clip(colorAlpha.a - 0.5);
#endif

				FragmentOutput o = (FragmentOutput)0;

#if UNLIT_ON
				emission += colorAlpha.rgb;
				colorAlpha.rgb = 0.0;
#endif

#if PIVOT_DEPTH_PROJECTION
				depthOut = DepthProjectPivot(i);
#endif

				// PugRP custom layout
				o.GBuffer0.xyz = colorAlpha.rgb;
				o.GBuffer1.xyz = normal * 0.5 + 0.5;
				o.GBuffer2.xyz = emission;

				/* // Reference
				output.GBuffer1.w = 1.0 / 3.0; // Tall sprite
				output.GBuffer1.w = 2.0 / 3.0; // Ground fade
				output.GBuffer1.w = 0.0 / 3.0; // No normals for lighting
				output.GBuffer1.w = 3.0 / 3.0; // Use normals for lighting
				*/

#if TALL_SPRITE
				o.GBuffer1.w = lerp(0.0, 1.0 / 3.0, s_tallMask);
#elif USE_NORMAL
				o.GBuffer1.w = 3.0 / 3.0;
#endif

				return o;
			}
			
			ENDHLSL
		}
	}
	CustomEditor "SpriteObjectStandardEditor"
}
