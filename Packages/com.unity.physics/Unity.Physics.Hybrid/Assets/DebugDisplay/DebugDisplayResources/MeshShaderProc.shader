Shader "MeshShaderProc" {
	Properties
	{
    }
	SubShader
	{
		Pass
		{
            ZWrite on
		    ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha
            Offset -0.2, -1

            CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
			#pragma target 4.5

			#include "UnityCG.cginc"

			StructuredBuffer<float4> colorBuffer;
			float4 Palette(uint index)
			{
				return colorBuffer[index];
			}

			struct instanceData
			{
                float4 m_vertex0;
                float4 m_vertex1;
                float4 m_vertex2;
			    float3 m_normal;
			};

			StructuredBuffer<instanceData> meshBuffer;
			struct v2f
			{
				float4 pos : SV_POSITION;
			    half3 worldNormal : TEXCOORD0;
				float4 color : TEXCOORD1;
			};

			v2f vert(uint vid : SV_VertexID)
			{
			    float4 pos;
                const uint offset = vid / 3;
			    const uint vertex = vid % 3;

			    if (vertex == 0)
			        pos = meshBuffer[offset].m_vertex0;
			    else if (vertex==1)
			        pos = meshBuffer[offset].m_vertex1;
			    else //if (vertex==2)
			        pos = meshBuffer[offset].m_vertex2;

				const float4 worldPos = float4(pos.xyz, 1);
				v2f o;
				o.pos = UnityObjectToClipPos(worldPos);
				o.color = Palette(pos.w);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
			    return i.color;
			}

			ENDCG
		}
	}
}
