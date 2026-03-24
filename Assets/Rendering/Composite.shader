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

			half4 Frag(Varyings i) : SV_Target
			{
				half4 colA = SAMPLE_TEXTURE2D_X(_CameraA_Tex, sampler_CameraA_Tex, i.uv);
				half4 colB = SAMPLE_TEXTURE2D_X(_CameraB_Tex, sampler_CameraB_Tex, i.uv);
				half mask = SAMPLE_TEXTURE2D_X(_MaskTex, sampler_MaskTex, i.uv).r;
				return lerp(colA, colB, mask);
			}
			ENDHLSL
		}
	}
}
