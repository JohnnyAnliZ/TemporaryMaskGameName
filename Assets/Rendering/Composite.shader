Shader "Custom/Composite"
{
	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
		}

		Pass
		{
			ZTest Always
			ZWrite Off
			Cull Off

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

			TEXTURE2D_X(_CameraA_Tex);
			SAMPLER(sampler_CameraA_Tex);

			TEXTURE2D_X(_CameraB_Tex);
			SAMPLER(sampler_CameraB_Tex);

			TEXTURE2D_X(_MaskTex);
			SAMPLER(sampler_MaskTex);

			float _BlurStrength;
			float _StreakLength;
			float _StreakBands;

			#define STREAK_SAMPLES 24

			struct Varyings
			{
				float4 positionHCS : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			Varyings Vert(uint vertexID : SV_VertexID)
			{
				Varyings o;
				o.positionHCS = GetFullScreenTriangleVertexPosition(vertexID);
				o.uv = GetFullScreenTriangleTexCoord(vertexID);
				return o;
			}

			half4 SampleAStreaked(float2 uv)
			{
				if (_BlurStrength <= 0.001) {
					return SAMPLE_TEXTURE2D_X(_CameraA_Tex, sampler_CameraA_Tex, uv);
				}

				float sampleY = uv.y;
				if (_StreakBands > 1.0) {
					sampleY = (floor(uv.y * _StreakBands) + 0.5) / _StreakBands;
				}

				float len = _BlurStrength * _StreakLength;
				half4 sum = 0;
				float wsum = 0;
				[unroll]
				for (int s = 0; s < STREAK_SAMPLES; s++) {
					float t = (float)s / (STREAK_SAMPLES - 1);
					float offset = (t - 0.5) * len;
					float sx = uv.x + offset;
					float inBounds = step(0.0, sx) * step(sx, 1.0);
					float w = 1.0 - abs(t - 0.5) * 2.0;
					w *= w;
					w *= inBounds;
					float2 su = float2(saturate(sx), sampleY);
					sum += SAMPLE_TEXTURE2D_X(_CameraA_Tex, sampler_CameraA_Tex, su) * w;
					wsum += w;
				}
				if (wsum < 0.0001) return SAMPLE_TEXTURE2D_X(_CameraA_Tex, sampler_CameraA_Tex, uv);
				return sum / wsum;
			}

			half4 Frag(Varyings i) : SV_Target
			{
				half4 colA = SampleAStreaked(i.uv);
				half4 colB = SAMPLE_TEXTURE2D_X(_CameraB_Tex, sampler_CameraB_Tex, i.uv);
				half mask = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_MaskTex, i.uv).r;
				half blackness = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_MaskTex, i.uv).g;

				if(blackness == 1.0) {
					return half4(0.0,0.0,0.0,1.0);
				}				
				return lerp(colA, colB, mask);
			}
			ENDHLSL
		}
	}
}
