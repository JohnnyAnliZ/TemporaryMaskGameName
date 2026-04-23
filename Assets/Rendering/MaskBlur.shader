Shader "Custom/MaskBlur"
{
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" }
		ZWrite Off
		ZTest Always
		Cull Off

		HLSLINCLUDE
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

		Texture2D _MainTex;
		SamplerState sampler_MainTex;
		float4 _MainTex_TexelSize;
		float2 _BlurDir;
		float _BlurRadius;

		struct Varyings {
			float4 positionHCS : SV_POSITION;
			float2 uv : TEXCOORD0;
		};

		Varyings Vert(uint vertexID : SV_VertexID) {
			Varyings o;
			o.positionHCS = GetFullScreenTriangleVertexPosition(vertexID);
			o.uv = GetFullScreenTriangleTexCoord(vertexID);
			return o;
		}

		// 9-tap separable Gaussian with linear-sampling trick: 5 taps cover 9 pixels.
		// Precomputed offsets/weights for sigma = _BlurRadius/3 approximation.
		half4 Frag(Varyings i) : SV_Target {
			float2 texel = _MainTex_TexelSize.xy * _BlurDir * _BlurRadius;
			// Linear-sample offsets for a 9-tap Gaussian (symmetric)
			float2 off1 = texel * 1.3846153846;
			float2 off2 = texel * 3.2307692308;
			half4 c = _MainTex.Sample(sampler_MainTex, i.uv)	* 0.2270270270;
			c      += _MainTex.Sample(sampler_MainTex, i.uv + off1)	* 0.3162162162;
			c      += _MainTex.Sample(sampler_MainTex, i.uv - off1)	* 0.3162162162;
			c      += _MainTex.Sample(sampler_MainTex, i.uv + off2)	* 0.0702702703;
			c      += _MainTex.Sample(sampler_MainTex, i.uv - off2)	* 0.0702702703;
			return c;
		}
		ENDHLSL

		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			ENDHLSL
		}
	}
}
