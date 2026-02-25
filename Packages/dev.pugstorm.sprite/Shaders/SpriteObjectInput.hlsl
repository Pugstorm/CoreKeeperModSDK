#ifndef SPRITE_INSTANCING_STANDARD_INCLUDED
#define SPRITE_INSTANCING_STANDARD_INCLUDED

#if defined(TEXTURE_UPSCALING) && !defined(SHADER_STAGE_COMPUTE) && !defined(DISALLOW_POSITION_SNAPPING)
	#define SNAP_POSITION
#endif

struct appdata
{
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 uv : TEXCOORD0;

	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
	float4 vertex : SV_POSITION;
	float3 positionWS : TEXCOORD1;
	float3 normal : NORMAL;
	float3 tangent : TANGENT;
	float4 color : COLOR;
	float2 uv : TEXCOORD0;
	float4 rect : TEXCOORD2;
	float2 pivot : TEXCOORD3;
	float4 emissiveColor : TEXCOORD4;
	float4 flashColor : TEXCOORD5;
	float4 outlineColor : TEXCOORD6;
	float3 gradientIndices : TEXCOORD7;
	float4 screenPos : TEXCOORD8;
	float3 worldSpacePivot : TEXCOORD9;
	float maskParam : TEXCOORD10;
#if defined(USE_MOTION_VECTORS)
	float4 positionCS : TEXCOORD11;
	float4 prevPositionCS : TEXCOORD12;
#endif
};

void GetSpriteColor(v2f i, out float4 colorAlpha, out float3 emission, out float3 normal, out float outlineSum)
{
	float2 uv = GetUV(i.uv, i.rect);

	float4 uvRange = GetUVRange(i.rect);

	colorAlpha = GetColor(uv, uvRange);
	s_tallMask = step(0.95, colorAlpha.a);
	colorAlpha.rgb = SampleGradients(colorAlpha.rgb, i.gradientIndices);

	float insideRect = all(abs(i.uv - 0.5) < 0.5);

	colorAlpha *= insideRect; // Force 0 alpha for outline padding pixels

	float outline = GetOutline(i.uv, i.rect, outlineSum);
	outline = max(0, outline - colorAlpha.a);

	colorAlpha *= i.color;

	colorAlpha.a = max(colorAlpha.a, outline * i.outlineColor.a);

	colorAlpha.a *= GetMaskAlpha(i.positionWS, i.maskParam);

	colorAlpha.rgb *= 1.0 - max(i.flashColor.a, outline);

	float4 emissive = GetEmissive(uv, uvRange) * insideRect;
	emissive.rgb *= i.emissiveColor.rgb * emissive.a;

	colorAlpha.rgb = max(0.0, colorAlpha.rgb - emissive.rgb);

	emission = emissive.rgb;
	emission += i.flashColor.rgb * i.flashColor.a * (1.0 - outline * i.outlineColor.a);
	emission += i.outlineColor.rgb * outline * i.outlineColor.a;

	float3 normalTS = GetNormal(uv, uvRange);

	float3 binormal = normalize(cross(i.tangent, i.normal));
	normal = normalTS.x * i.tangent + normalTS.y * binormal + normalTS.z * i.normal;
}

void GetSpriteColor(v2f i, out float4 colorAlpha, out float3 emission, out float3 normal)
{
	float outlineSum;
	GetSpriteColor(i, colorAlpha, emission, normal, outlineSum);
}

float3 PixelSnapPosition(float3 positionWS)
{
	return round(positionWS * PPU) / PPU;
}

v2f vert(appdata v)
{
	v2f o;

#if INSTANCING_ENABLED && defined(UNITY_INSTANCING_ENABLED)
	InstanceData instanceData = _InstanceData[UNITY_GET_INSTANCE_ID(v)];
	float4x4 localToWorld = instanceData.localToWorld;
	o.pivot = instanceData.pivot;
	o.rect = instanceData.rect;
	float4 color = instanceData.color;
	float4 emissiveColor = instanceData.emissiveColor;
	o.flashColor = instanceData.flashColor;
	o.outlineColor = instanceData.outlineColor;
	o.gradientIndices = instanceData.gradientIndices;
	float3 transformAnimParams = instanceData.transformAnimParams;
	o.maskParam = instanceData.maskParam;
#else
	float4x4 localToWorld = UNITY_MATRIX_M;
	o.pivot = _Pivot;
	o.rect = _Rect;
	float4 color = _Color;
	float4 emissiveColor = _EmissiveColor;
	o.flashColor = _FlashColor;
	o.outlineColor = _OutlineColor;
	o.gradientIndices = _GradientIndices;
	float3 transformAnimParams = _TransformAnimParams;
	o.maskParam = _MaskParam;
#endif

#if defined(USE_MOTION_VECTORS) && defined(INSTANCING_ENABLED)
	float4x4 prevLocalToWorld = instanceData.prevLocalToWorld;
#endif

#ifdef SNAP_POSITION
	float3 majorAxis = abs(localToWorld._m02_m12_m22);
	float3 snappedPosition = PixelSnapPosition(localToWorld._m03_m13_m23);
	if (majorAxis.z > majorAxis.y)
	{
		localToWorld._m03_m13 = snappedPosition.xy;
	}
	else
	{
		localToWorld._m03_m23 = snappedPosition.xz;
	}
	#if defined(USE_MOTION_VECTORS) && defined(INSTANCING_ENABLED)
	snappedPosition = PixelSnapPosition(prevLocalToWorld._m03_m13_m23)y;
	if (majorAxis.z > majorAxis.y)
	{
		prevLocalToWorld._m03_m13 = snappedPosition.xy;
	}
	else
	{
		prevLocalToWorld._m03_m23 = snappedPosition.xz;
	}
	#endif
#endif
	
	// Snap pivot to rect pixels. This prevents aliasing if the fed pivot data was misaligned (such as when inheriting a pivot from a rect of a different size)
	o.pivot = round(o.pivot * o.rect.zw) / o.rect.zw;

#ifdef UV_BIAS
	float uvBias = saturate(1.0 - UV_BIAS);
#else
	float uvBias = 1.0;
#endif

	// 1px padding for outline support
	float2 paddedSize = o.rect.zw + 2;
	v.vertex.xy *= paddedSize / o.rect.zw;
	v.vertex.xy -= 1.0 / o.rect.zw;
	v.uv *= paddedSize / o.rect.zw * uvBias;
	v.uv -= 1.0 / o.rect.zw * uvBias;

	v.vertex.xy -= o.pivot;
	
	v.vertex.xy *= o.rect.zw / PPU;

	if (transformAnimParams.x > -1)
	{
#if INSTANCING_ENABLED && defined(UNITY_INSTANCING_ENABLED) || defined(SINGLE_RENDERER)
		float time = _TransformAnimationTime;
#else
		float time = _EditorTime;
#endif
		ApplyTransformAnimation(v.vertex, transformAnimParams, time);
	}

	o.positionWS = mul(localToWorld, v.vertex);
	o.normal = normalize(mul((float3x3)localToWorld, v.normal));
	o.tangent = normalize(mul((float3x3)localToWorld, v.tangent.xyz));

	o.vertex = mul(UNITY_MATRIX_VP, float4(o.positionWS, 1.0));
	o.uv = v.uv;

	o.color = color;
	o.emissiveColor = emissiveColor;

	o.screenPos = ComputeScreenPos(o.vertex);

	o.worldSpacePivot = mul(localToWorld, float4(0.0, 0.0, 0.0, 1.0)).xyz;
	
#if INSTANCING_ENABLED && defined(USE_MOTION_VECTORS)
	o.positionCS = mul(MATRIX_VP, float4(o.positionWS, 1.0));
	o.prevPositionCS = mul(MATRIX_VP_PREV, mul(prevLocalToWorld, v.vertex));
#endif

	return o;
}

int _ObjectId;
int _PassValue;

float4 fragSceneHighlightPass(v2f i) : SV_Target
{
	float4 colorAlpha;
	float3 emission, normal;
	GetSpriteColor(i, colorAlpha, emission, normal);
#if ALPHATEST_ON
	clip(colorAlpha.a - 0.5);
#endif
	return float4(_ObjectId, _PassValue, 1, 1);
}

float4 _SelectionID;

float4 fragScenePickingPass(v2f i) : SV_Target
{
	float4 colorAlpha;
	float3 emission, normal;
	GetSpriteColor(i, colorAlpha, emission, normal);
#if ALPHATEST_ON
	clip(colorAlpha.a - 0.5);
#endif
	return _SelectionID;
}

#endif // SPRITE_INSTANCING_STANDARD_INCLUDED