Shader "Custom/LightShaft"
{
	Properties
	{
		//Unused for shading - SpriteRenderer requires this property to exist to treat the material as a sprite shader
		_MainTex ("Sprite Texture", 2D) = "white" {}

		//Not shown in the Inspector - SpriteRenderer.color is delivered here via MaterialPropertyBlock, independent of GPU instancing
		[HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)

		_Intensity ("Intensity", Range(0, 4)) = 1

		[Header(Left Edge)]
		_LeftFade ("Left Fade Width", Range(0.001, 1)) = 0.15
		_LeftPower ("Left Falloff Power", Range(0.1, 8)) = 1

		[Header(Right Edge)]
		_RightFade ("Right Fade Width", Range(0.001, 1)) = 0.55
		_RightPower ("Right Falloff Power", Range(0.1, 8)) = 1

		//Bottom edge fades to nothing
		[Header(Bottom Edge)]
		_BottomFade ("Bottom Fade Height", Range(0.001, 1)) = 0.6
		_BottomPower ("Bottom Falloff Power", Range(0.1, 8)) = 1

		//Top Fade of 0 means a hard cutoff, no fade
		[Header(Top Edge)]
		_TopFade ("Top Fade Height", Range(0, 1)) = 0
		_TopPower ("Top Falloff Power", Range(0.1, 8)) = 1

		[Header(Highlight Bands)]
		_BandCount ("Band Count", Range(1, 16)) = 5
		_BandSharpness ("Band Sharpness", Range(1, 32)) = 6
		_BandIntensity ("Band Brightness", Range(0, 1)) = 0.6
		_BandNoise ("Band Position Irregularity", Range(0, 1)) = 0.3
		_BandScrollSpeed ("Band Scroll Speed", Float) = 0

		//Slow, irregular dips in overall opacity - a long, non-repeating flicker/breathe
		[Header(Flicker)]
		_FlickerAmount ("Flicker Amount", Range(0, 1)) = 0.35
		_FlickerSpeed ("Flicker Speed", Range(0.02, 2)) = 0.25
		_FlickerSharpness ("Flicker Dip Sharpness", Range(1, 8)) = 3
	}

	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
		}

		Cull Off
		ZWrite Off
		ZTest LEqual
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			Name "Forward"
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			//Deliberately outside UnityPerMaterial - Unity sets this per-renderer via MaterialPropertyBlock
			half4 _RendererColor;

			//Global set by CutscenePlayer: 1 while a cutscene is playing, 0 otherwise. The streak pass smears
			//whatever the flicker's per-frame opacity happens to be into a strobe, so flicker is held off for
			//the duration of a cutscene. Deliberately phrased as "suppress" rather than "enable": an unset
			//global reads as 0, which here means flicker runs normally - the safe default.
			float _FlickerSuppress;

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float _Intensity;
				float _LeftFade, _LeftPower;
				float _RightFade, _RightPower;
				float _BottomFade, _BottomPower;
				float _TopFade, _TopPower;
				float _BandCount, _BandSharpness, _BandIntensity, _BandNoise, _BandScrollSpeed;
				float _FlickerAmount, _FlickerSpeed, _FlickerSharpness;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
				float flickerSeed : TEXCOORD1;
			};

			Varyings vert(Attributes v)
			{
				UNITY_SETUP_INSTANCE_ID(v);

				Varyings o;
				o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
				o.uv = v.uv;

				//Nothing here may branch on UNITY_INSTANCING_ENABLED. Unity swaps between the instanced and
				//non-instanced variant as batching changes - which happens constantly while the camera pans -
				//so anything applied in only one variant makes the shaft visibly jump. _RendererColor carries
				//SpriteRenderer.color in both, so it is the single source. (SpriteRenderer FlipX/FlipY is not
				//honoured as a result; all the shaft instances are unflipped.)
				o.color = v.color * _RendererColor;

				//Desyncs the flicker between instances sharing this material. World position alone isn't
				//enough - layered siblings built from the same sprite can sit at the same spot - so this
				//also folds in the renderer's own alpha, which already differs between those stacked layers
				float3 objectWorldPos = GetObjectToWorldMatrix()._m03_m13_m23;
				float seedInput = dot(objectWorldPos.xy, float2(12.9898, 78.233)) + o.color.a * 43.758;
				o.flickerSeed = frac(sin(seedInput) * 43758.5453123);

				return o;
			}

			//Cheap hash for per-band position jitter, no texture lookup needed
			float Hash(float n)
			{
				return frac(sin(n) * 43758.5453123);
			}

			half4 frag(Varyings i) : SV_Target
			{
				float2 uv = i.uv;

				//Each side fades independently so the beam can be sharp on one edge and soft on another
				float left = pow(saturate(uv.x / max(_LeftFade, 1e-4)), _LeftPower);
				float right = pow(saturate((1.0 - uv.x) / max(_RightFade, 1e-4)), _RightPower);
				float bottom = pow(saturate(uv.y / max(_BottomFade, 1e-4)), _BottomPower);
				float top = _TopFade > 0.0 ? pow(saturate((1.0 - uv.y) / max(_TopFade, 1e-4)), _TopPower) : 1.0;
				float shape = left * right * bottom * top;

				//A handful of thin, irregularly-spaced bright streaks running the length of the beam
				float bandX = uv.x * _BandCount + _BandScrollSpeed * _Time.y;
				float cell = floor(bandX);
				float jitter = lerp(0.5, Hash(cell), _BandNoise);
				float dist = abs(frac(bandX) - jitter);
				float band = pow(saturate(1.0 - dist * 2.0), _BandSharpness);

				//Brighten toward white without ever clipping past it, so the tint stays visible instead of blowing out
				float3 tint = i.color.rgb;
				float3 color = lerp(tint, 1.0, saturate(band * _BandIntensity));

				//Sum of non-harmonic sine waves so the dips don't feel like a mechanical loop
				//Offset by a per-instance seed so sprites sharing this material don't flicker in lockstep
				float ft = _Time.y * _FlickerSpeed + i.flickerSeed * 100.0;
				float flickerNoise = sin(ft) + sin(ft * 1.618034 + 1.7) + sin(ft * 2.71828 + 4.1);
				flickerNoise = flickerNoise / 3.0 * 0.5 + 0.5;
				float flickerDip = 1.0 - pow(1.0 - flickerNoise, _FlickerSharpness);
				float flicker = lerp(1.0, flickerDip, _FlickerAmount * (1.0 - saturate(_FlickerSuppress)));

				float alpha = saturate(shape * _Intensity) * i.color.a * flicker;

				return half4(color, alpha);
			}
			ENDHLSL
		}
	}
}