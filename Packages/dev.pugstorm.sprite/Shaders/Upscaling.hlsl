#ifndef SPRITE_UPSCALING
#define SPRITE_UPSCALING

#define SAMPLE_TEXTURE2D_UPSCALED SAMPLE_TEXTURE2D_UPSCALED_3X

#define BACKGROUND float4(0.0, 0.0, 0.0, 0.0)

float dist(float4 c1, float4 c2)
{
	return (c1 == c2) ? 0.0 : abs(c1.r - c2.r) + abs(c1.g - c2.g) + abs(c1.b - c2.b);
}

bool similar(float4 c1, float4 c2, float4 input)
{
	return (c1 == c2 || (dist(c1, c2) <= dist(input, c2) && dist(c1, c2) <= dist(input, c1)));
}

bool different(float4 c1, float4 c2, float4 input)
{
	return !similar(c1, c2, input);
}

float4 SAMPLE_TEXTURE2D_UPSCALED_3X(TEXTURE2D(tex), SAMPLER(smp), float2 uv, float4 texelSize, float4 uvRange = float4(0, 0, 1, 1))
{
	float2 pixel_size = texelSize.xy;
	float4 cE = SAMPLE_TEXTURE2D(tex, smp, uv);
	cE = cE.a == 0.0 ? BACKGROUND : cE;

	float2 uv0 = clamp(uv + pixel_size * float2(-1.0, 0.0), uvRange.xy, uvRange.zw);
	float2 uv1 = clamp(uv + pixel_size * float2( 1.0, 0.0), uvRange.xy, uvRange.zw);
	float2 uv2 = clamp(uv + pixel_size * float2( 0.0, 1.0), uvRange.xy, uvRange.zw);
	float2 uv3 = clamp(uv + pixel_size * float2( 0.0,-1.0), uvRange.xy, uvRange.zw);
	float2 uv4 = clamp(uv + pixel_size * float2(-1.0,-1.0), uvRange.xy, uvRange.zw);
	float2 uv5 = clamp(uv + pixel_size * float2( 1.0, 1.0), uvRange.xy, uvRange.zw);
	float2 uv6 = clamp(uv + pixel_size * float2(-1.0, 1.0), uvRange.xy, uvRange.zw);
	float2 uv7 = clamp(uv + pixel_size * float2( 1.0,-1.0), uvRange.xy, uvRange.zw);
	
	float4 cD = SAMPLE_TEXTURE2D(tex, smp, uv0);
	cD = cD.a == 0.0 ? BACKGROUND : cD;
	float4 cF = SAMPLE_TEXTURE2D(tex, smp, uv1);
	cF = cF.a == 0.0 ? BACKGROUND : cF;
	float4 cH = SAMPLE_TEXTURE2D(tex, smp, uv2);
	cH = cH.a == 0.0 ? BACKGROUND : cH;
	float4 cB = SAMPLE_TEXTURE2D(tex, smp, uv3);
	cB = cB.a == 0.0 ? BACKGROUND : cB;
	float4 cA = SAMPLE_TEXTURE2D(tex, smp, uv4);
	cA = cA.a == 0.0 ? BACKGROUND : cA;
	float4 cI = SAMPLE_TEXTURE2D(tex, smp, uv5);
	cI = cI.a == 0.0 ? BACKGROUND : cI;
	float4 cG = SAMPLE_TEXTURE2D(tex, smp, uv6);
	cG = cG.a == 0.0 ? BACKGROUND : cG;
	float4 cC = SAMPLE_TEXTURE2D(tex, smp, uv7);
	cC = cC.a == 0.0 ? BACKGROUND : cC;
	
	if (different(cD,cF, cE)
     && different(cH,cB, cE)
     && ((similar(cE, cD, cE) || similar(cE, cH, cE) || similar(cE, cF, cE) || similar(cE, cB, cE) ||
         ((different(cA, cI, cE) || similar(cE, cG, cE) || similar(cE, cC, cE)) &&
          (different(cG, cC, cE) || similar(cE, cA, cE) || similar(cE, cI, cE))))))
    {
		float2 unit = uv - (floor(uv / pixel_size) * pixel_size);
		float2 pixel_3_size = pixel_size / 3.0;
		
		// E0
		if (unit.x < pixel_3_size.x && unit.y < pixel_3_size.y) {
			return similar(cB, cD, cE) ? cB : cE;
		}
		
		
		// E1
		if (unit.x < pixel_3_size.x * 2.0 && unit.y < pixel_3_size.y) {
			return (similar(cB, cD, cE) && different(cE, cC, cE))
				|| (similar(cB, cF, cE) && different(cE, cA, cE)) ? cB : cE;
		}
		
		// E2
		if (unit.y < pixel_3_size.y) {
			return similar(cB, cF, cE) ? cB : cE;
		}
		
		// E3
		if (unit.x < pixel_3_size.x && unit.y < pixel_3_size.y * 2.0) {
			return (similar(cB, cD, cE) && different(cE, cG, cE)
				|| (similar(cH, cD, cE) && different(cE, cA, cE))) ? cD : cE;
		}
		
		// E5
		if (unit.x >= pixel_3_size.x * 2.0 && unit.x < pixel_3_size.x * 3.0 && unit.y < pixel_3_size.y * 2.0) {
			return (similar(cB, cF, cE) && different(cE, cI, cE))
				|| (similar(cH, cF, cE) && different(cE, cC, cE)) ? cF : cE;
		}
		
		// E6
		if (unit.x < pixel_3_size.x && unit.y >= pixel_3_size.y * 2.0) {
			return similar(cH, cD, cE) ? cH : cE;
		}
		
		// E7
		if (unit.x < pixel_3_size.x * 2.0 && unit.y >= pixel_3_size.y * 2.0) {
			return (similar(cH, cD, cE) && different(cE, cI, cE))
				|| (similar(cH, cF, cE) && different(cE, cG, cE)) ? cH : cE;
		}
		
		// E8
		if (unit.y >= pixel_3_size.y * 2.0) {
			return similar(cH, cF, cE) ? cH : cE;
		}
    }
	
	return cE;
}

#endif // SPRITE_UPSCALING