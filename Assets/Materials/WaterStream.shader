Shader "Custom/WaterStream"
{
	Properties
	{
		_Color ("Water Tint", Color) = (0.62, 0.82, 0.92, 0.95)
		_EdgeColor ("Edge Highlight", Color) = (1, 1, 1, 1)
		_EdgeWidth ("Edge Highlight Width", Range(0, 1)) = 0.45

		[Header(Shape)]
		_TopWidth ("Top Width", Range(0.05, 1)) = 0.45
		_BottomWidth ("Bottom Width", Range(0.05, 1)) = 0.8
		_Softness ("Edge Softness", Range(0.01, 0.6)) = 0.25
		_TopFade ("Top Fade", Range(0, 0.5)) = 0.03
		_BottomFade ("Bottom Fade", Range(0, 0.5)) = 0.15
		_Cutoff ("Alpha Cutoff", Range(0.01, 1)) = 0.09

		[Header(Flow)]
		_FlowSpeed ("Flow Speed", Float) = 1.2
		_StrandCount ("Strand Count", Range(1, 24)) = 8
		_StrandStrength ("Strand Contrast", Range(0, 1)) = 0.45
		_Wobble ("Horizontal Wobble", Range(0, 0.2)) = 0.015
		_WobbleSpeed ("Wobble Speed", Float) = 2.5
	}

	//AlphaTest (2450) is inside URP's opaque queue range, and the 3D renderer copies depth after opaques,
	//so ZWrite here is what puts the stream into _CameraDepthTexture. On the Transparent queue it is not,
	//and the volumetric fog then samples whatever is behind the stream and buries it.
	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
			"RenderType" = "TransparentCutout"
			"Queue" = "AlphaTest"
			"IgnoreProjector" = "True"
		}

		Pass
		{
			Name "Forward"
			Tags { "LightMode" = "UniversalForward" }

			Cull Off
			ZTest LEqual
			ZWrite On
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float4 _Color;
				float4 _EdgeColor;
				float _EdgeWidth;
				float _TopWidth, _BottomWidth, _Softness;
				float _TopFade, _BottomFade, _Cutoff;
				float _FlowSpeed, _StrandCount, _StrandStrength;
				float _Wobble, _WobbleSpeed;
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
				o.uv = v.uv;
				return o;
			}

			float Hash(float n)
			{
				return frac(sin(n) * 43758.5453123);
			}

			half4 frag(Varyings i) : SV_Target
			{
				//uv.y is 1 at the faucet, 0 where the water lands
				float2 uv = i.uv;

				//The falling column drifts sideways a little so it never reads as a static decal
				float wobble = sin(uv.y * 6.0 + _Time.y * _WobbleSpeed) * _Wobble;
				float cx = uv.x - 0.5 + wobble;

				//Narrow at the spout, spreading slightly as it falls
				float halfWidth = lerp(_BottomWidth, _TopWidth, uv.y) * 0.5;
				float d = abs(cx) / max(halfWidth, 1e-4);

				float body = 1.0 - smoothstep(1.0 - _Softness, 1.0, d);
				float edge = smoothstep(1.0 - _EdgeWidth, 1.0, d) * body;

				//Fade where it leaves the spout and where it meets the water, so both ends blend instead of cutting off
				float topFade = _TopFade > 0.0 ? smoothstep(0.0, _TopFade, 1.0 - uv.y) : 1.0;
				float bottomFade = _BottomFade > 0.0 ? smoothstep(0.0, _BottomFade, uv.y) : 1.0;
				float shape = body * topFade * bottomFade;

				//Clipped on the silhouette alone, never the shimmer below - a time-varying clip would make
				//the outline and the depth it writes crawl frame to frame
				clip(shape - _Cutoff);

				//Vertical strands, each scrolling downward from its own offset so the column has internal motion
				float strandId = floor((cx / max(halfWidth, 1e-4)) * _StrandCount * 0.5);
				float flow = frac(uv.y - _Time.y * _FlowSpeed + Hash(strandId));
				float shimmer = lerp(1.0, 0.55 + 0.45 * sin(flow * 6.2831853 * 2.0), _StrandStrength);

				float3 color = lerp(_Color.rgb, _EdgeColor.rgb, edge);
				return half4(color, saturate(shape * shimmer * _Color.a));
			}
			ENDHLSL
		}
	}
}
