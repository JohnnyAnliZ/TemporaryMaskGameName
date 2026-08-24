Shader "Custom/Shard"
{
	Properties
	{
		_MainTex ("Captured 2D", 2D) = "white" {}
		_EmissionIntensity ("Emission Intensity", Range(1, 8)) = 2
	}

	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
			"RenderType" = "Opaque"
			"Queue" = "Geometry"
		}

		Cull Off
		ZWrite On

		Pass
		{
			Name "Forward"
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float _EmissionIntensity;
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
				//The captured face is emissive: pushed above 1.0 so it reads as a glowing fragment of the
				//2D world rather than a surface the volumetric fog can flatten into the fog colour.
				if (isFrontFace)
				{
					half3 captured = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
					return half4(captured * _EmissionIntensity, 1.0);
				}

				float3 viewDirWS = normalize(GetWorldSpaceViewDir(i.positionWS));
				float3 normalWS = normalize(i.normalWS);
				float3 reflectDirWS = reflect(-viewDirWS, normalWS);
				half3 env = GlossyEnvironmentReflection(reflectDirWS, i.positionWS, 0, 1.0h);
				return half4(env, 1.0);
			}
			ENDHLSL
		}

		//Without these the shards never enter the depth prepass, so the volumetric fog samples the wall
		//behind them, marches the full distance and buries them under far-distance fog.
		Pass
		{
			Name "DepthNormals"
			Tags { "LightMode" = "DepthNormals" }
			ZWrite On

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 normalWS : TEXCOORD0;
			};

			Varyings vert(Attributes v)
			{
				Varyings o;
				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
				o.normalWS = TransformObjectToWorldNormal(v.normalOS);
				return o;
			}

			half4 frag(Varyings i) : SV_Target
			{
				return half4(normalize(i.normalWS), 0.0);
			}
			ENDHLSL
		}

		Pass
		{
			Name "DepthOnly"
			Tags { "LightMode" = "DepthOnly" }
			ZWrite On
			ColorMask 0

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			struct Attributes { float4 positionOS : POSITION; };
			struct Varyings { float4 positionCS : SV_POSITION; };

			Varyings vert(Attributes v)
			{
				Varyings o;
				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
				return o;
			}

			half4 frag(Varyings i) : SV_Target
			{
				return 0;
			}
			ENDHLSL
		}
	}
}
