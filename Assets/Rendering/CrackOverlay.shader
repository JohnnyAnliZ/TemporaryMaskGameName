Shader "Custom/CrackOverlay"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}

	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
		}

		Pass
		{
			ZTest Always
			ZWrite Off
			Cull Off
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
			CBUFFER_END

			struct Attributes {
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};
			struct Varyings {
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			Varyings Vert(Attributes v)
			{
				Varyings o;
				float2 clip = v.positionOS.xy * 2.0 - 1.0;
				o.positionCS = float4(clip, 0, 1);
				o.uv = v.uv;
				o.color = v.color; //vertex color to differentiate crack vs shard
				return o;
			}

			half4 Frag(Varyings i) : SV_Target
			{
				return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
			}
			ENDHLSL
		}
	}
}
