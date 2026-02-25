Shader "Hidden/ScriptableDataUI"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Blend SrcAlpha OneMinusSrcAlpha
		
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float2 clipUV : TEXCOORD1;
			};

			sampler2D _MainTex;

			sampler2D _GUIClipTexture;
    		float4x4 unity_GUIClipTextureMatrix;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				float3 eyePos = UnityObjectToViewPos(v.vertex);
				o.clipUV = mul(unity_GUIClipTextureMatrix, float4(eyePos.xy, 0, 1.0));
				return o;
			}

			float4 frag (v2f i) : SV_Target
			{
				clip(tex2D(_GUIClipTexture, i.clipUV).a - 0.5);
				float4 color = tex2D(_MainTex, i.uv);
				color.rgb = pow(color.rgb, 1.0 / 2.2);
				return color;
			}
			ENDCG
		}
	}
}
