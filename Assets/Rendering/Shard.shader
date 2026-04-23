Shader "Custom/Shard"
{
	Properties
	{
		_MainTex ("Captured 2D", 2D) = "white" {}
	}

	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
		}

		Cull Off
		ZWrite On //I'm gonna kill myself
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 positionWS : TEXCOORD1;
				float3 normalWS : TEXCOORD2;
			};

			Varyings vert(Attributes v)
			{
				Varyings o;
				o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
				o.positionCS = TransformWorldToHClip(o.positionWS);
				o.normalWS = TransformObjectToWorldNormal(v.normalOS);
				o.uv = v.uv;
				return o;
			}

			half4 frag(Varyings i, bool isFrontFace : SV_IsFrontFace) : SV_Target
			{
				if (isFrontFace) return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

				float3 viewDirWS = normalize(GetWorldSpaceViewDir(i.positionWS));
				float3 normalWS = normalize(i.normalWS);
				float3 reflectDirWS = reflect(-viewDirWS, normalWS);
				half3 env = GlossyEnvironmentReflection(reflectDirWS, i.positionWS, 0, 1.0h);
				return half4(env, 1.0);
			}
			ENDHLSL
		}
	}
}
