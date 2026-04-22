Shader "Custom/SpriteOutline"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Transparent"
			"RenderPipeline" = "UniversalPipeline"
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"PreviewType" = "Plane"
		}

		Cull Off
		Lighting Off
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);
			float4 _MainTex_TexelSize;

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _OutlineColor;
				float4 _OffsetDir;      //(x, y, 0, 0) — set per-renderer via MPB (x flipped when flipX is on)
				float4 _UvMin;          //(uMin, vMin, 0, 0) — sprite's atlas subregion lower corner
				float4 _UvMax;          //(uMax, vMax, 0, 0) — sprite's atlas subregion upper corner
				float _OutlineThickness;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
			};

			Varyings vert(Attributes v)
			{
				Varyings o;
				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}

			half4 frag(Varyings i) : SV_Target
			{
				//Child (offset copy) samples at its own UV.
				half aChild = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
				//Main's UV at same world pixel = childUV + offsetUV. Bounds-check against sprite's atlas rect so we
				//don't pick up the clamped edge (which would suppress the outline) or neighbor atlas sprites.
				float2 mainUV = i.uv + _OffsetDir.xy * _MainTex_TexelSize.xy * _OutlineThickness;
				bool inBounds = mainUV.x >= _UvMin.x && mainUV.x <= _UvMax.x
				             && mainUV.y >= _UvMin.y && mainUV.y <= _UvMax.y;
				half aMain = inBounds ? SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV).a : 0.0;
				//Outline only where the child is opaque AND main doesn't cover this pixel (empties the middle).
				half mask = step(0.001, aChild) * (1.0 - step(0.001, aMain));
				return half4(_OutlineColor.rgb, mask * _OutlineColor.a);
			}
			ENDHLSL
		}
	}
}
