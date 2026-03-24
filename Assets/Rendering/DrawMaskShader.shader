Shader "Custom/CircleMask"
{
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" }
		Blend One One
		ZWrite Off
		ZTest Always
		Cull Off

		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

			float4 _Center;
			float _Radius;
			float2 _Resolution;
			float _PixelSize;

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

			half4 Frag(Varyings i) : SV_Target {
				float2 pixelStep = _PixelSize / _Resolution;
				float2 snappedUV = floor(i.uv / pixelStep) * pixelStep + pixelStep * 0.5;
				float dist = length(snappedUV - _Center.xy);
				half mask = dist < _Radius ? 1.0 : 0.0;
				return half4(mask, mask, mask, mask);
			}
			ENDHLSL
		}
	}
}
