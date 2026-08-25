Shader "Custom/SpriteSway"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)

		[Header(Sway)]
		_SwayAmount ("Sway Amount (world units at top)", Float) = 0.08
		_SwaySpeed ("Sway Speed", Float) = 1
		_SwayCurve ("Bend Curve", Float) = 2

		[Header(Variation)]
		_WindWavelength ("Wind Wavelength (world units)", Float) = 14
		_AmountVariance ("Amount Variance", Range(0,1)) = 0.3
		_SwayPhase ("Global Phase Offset", Float) = 0
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
			"CanUseSpriteAtlas" = "True"
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
			#pragma multi_compile_instancing

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			//SpriteRenderer.color: under GPU Instancing, Unity can't bake per-instance tint into
			//vertex data (the mesh buffer is shared across the batch), so it feeds .color through
			//this specifically-named instanced buffer instead (see URP's Core2D.hlsl). Declared for
			//both paths so the shader is correct whether instancing is on or off.
			#ifdef UNITY_INSTANCING_ENABLED
				UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
					UNITY_DEFINE_INSTANCED_PROP(float4, unity_SpriteRendererColorArray)
				UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
				#define unity_SpriteColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
			#endif

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _Color;
				float _SwayAmount;
				float _SwaySpeed;
				float _SwayCurve;
				float _WindWavelength;
				float _AmountVariance;
				float _SwayPhase;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			//IMPORTANT: this shader deliberately uses NO per-object data (no unity_ObjectToWorld
			//components, no assumption that positionOS is local space). Unity always merge-batches
			//sprites, pre-transforming their vertices to world space on the CPU and leaving
			//unity_ObjectToWorld as identity -- so any per-object read silently collapses to the
			//same value for every sprite in a batch (that's what made every sprite share a phase),
			//and positionOS becomes a world coordinate (that's what made them slide instead of
			//bend). Both inputs used below survive batching intact: uv is untouched per-vertex
			//data, and TransformObjectToWorld is correct either way (identity is a no-op).
			Varyings vert(Attributes v)
			{
				Varyings o;
				UNITY_SETUP_INSTANCE_ID(v);

				//Bend weight from UV, not position: uv.y is 0 at the sprite's bottom edge and 1 at
				//its top, regardless of batching, transform scale, or where the pivot sits.
				//_SwayCurve eases the ramp so it bends like a stem rather than tilting rigidly.
				float heightWeight = pow(saturate(v.uv.y), max(_SwayCurve, 0.0001));

				float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);

				//Variation comes from WORLD POSITION rather than a per-instance random value: it's
				//the only per-sprite-varying input that batching can't destroy. Wind reads as a wave
				//travelling across the scene, so sprites at different x are naturally out of phase.
				//Both terms are smooth (not hashed/quantised), so all of a sprite's vertices stay
				//coherent -- a hash would put neighbouring vertices in unrelated states and tear the
				//quad apart.
				float phase = positionWS.x * (TWO_PI / max(_WindWavelength, 0.0001)) + _SwayPhase;

				//Amplitude varies spatially too, so neighbours differ in strength as well as timing.
				float amount = _SwayAmount * (1.0 + _AmountVariance * sin(positionWS.x * 0.37 + positionWS.y * 0.23));

				//Two summed waves at one shared speed give an irregular, gust-like rhythm. Speed
				//itself must stay uniform: _Time.y * (per-sprite speed) diverges without bound, so
				//any spatial variation in speed would shear a sprite further apart every second.
				float t = _Time.y * _SwaySpeed;
				float swing = sin(t + phase) * 0.75 + sin(t * 0.63 + phase * 1.7) * 0.25;

				positionWS.x += swing * amount * heightWeight;

				o.positionCS = TransformWorldToHClip(positionWS);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.color = v.color * _Color * unity_SpriteColor;
				return o;
			}

			half4 frag(Varyings i) : SV_Target
			{
				half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
				return tex * i.color;
			}
			ENDHLSL
		}
	}
}