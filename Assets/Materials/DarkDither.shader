Shader "Custom/DarkDither"
{
	Properties
	{
		_Amount ("Dither Amount", Range(0, 1)) = 0.8
		_Levels ("Shade Levels", Range(2, 16)) = 5
		_DitherThreshold ("Applies Below Luminance", Range(0, 1)) = 0.35
		_ShadowLift ("Shadow Lift", Range(0, 0.5)) = 0.15
		_EdgeNoise ("Band Edge Noise", Range(0, 2)) = 0.6
		_DitherPixelSize ("Cell Size (screen pixels)", Range(1, 8)) = 2

		//Cells stay screen-aligned, but grow closer to the camera and shrink with distance, the way texture
		//on a real surface would. This is what stops it reading as a flat sheet over the lens.
		[Header(Perspective)]
		_PerspectiveScale ("Perspective Amount", Range(0, 1)) = 0.6
		_PerspectiveRef ("Reference Distance", Range(0.1, 20)) = 3

		[Header(Camera Drift)]
		_DriftFromMove ("Drift From Movement", Range(0, 20)) = 4
		_DriftFromLook ("Drift From Looking", Range(0, 20)) = 3

		//Keeps the grain alive when nothing is moving
		[Header(Temporal)]
		_TemporalRate ("Reshuffles Per Second", Range(0, 30)) = 8
		_TemporalAmount ("Temporal Amount", Range(0, 1)) = 0.4
	}

	SubShader
	{
		Tags
		{
			"RenderPipeline" = "UniversalPipeline"
			"RenderType" = "Opaque"
		}

		Cull Off
		ZWrite Off
		ZTest Always

		Pass
		{
			Name "DarkDither"

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

			float _Amount;
			float _Levels;
			float _DitherThreshold;
			float _ShadowLift;
			float _EdgeNoise;
			float _DitherPixelSize;
			float _PerspectiveScale;
			float _PerspectiveRef;
			float _DriftFromMove;
			float _DriftFromLook;
			float _TemporalRate;
			float _TemporalAmount;

			//Global set by Trans3DSubsection, not a material property - a material value would shadow the
			//global and the toggle would do nothing. Phrased as "suppress" so an unset global reads as 0,
			//which means the dither runs normally: the safe default everywhere else.
			float _DitherSuppress;

			//Also a global, for the same reason: Player3DController drives it over a respawn, and a material
			//property of the same name would shadow it.
			float _FadeToBlack;

			static const float kBayer[16] =
			{
				 0.0,  8.0,  2.0, 10.0,
				12.0,  4.0, 14.0,  6.0,
				 3.0, 11.0,  1.0,  9.0,
				15.0,  7.0, 13.0,  5.0
			};

			float Bayer4x4(float2 cell)
			{
				int2 c = int2(fmod(abs(cell), 4.0));
				return kBayer[c.y * 4 + c.x] * (1.0 / 16.0);
			}

			float Hash2(float2 p)
			{
				return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
			}

			float Hash(float n)
			{
				return frac(sin(n * 127.1) * 43758.5453123);
			}

			half4 Frag(Varyings input) : SV_Target
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				//1:1 with the source, so load rather than sample - no filtering, no sampler to redeclare
				int2 pixel = int2(input.positionCS.xy);
				half4 col = LOAD_TEXTURE2D_X(_BlitTexture, pixel);

				float lum = dot(col.rgb, float3(0.2126, 0.7152, 0.0722));
				float range = max(_DitherThreshold, 1e-4);
				float amount = _Amount * (1.0 - saturate(_DitherSuppress));

				//Guarded rather than returned early, because the respawn fade at the bottom has to run on
				//every pixel - including the bright ones the dither skips.
				if (lum < range && amount > 0.0) {

				//Fade out at the top of the range so dithered and undithered areas meet without a hard seam
				float darkness = 1.0 - smoothstep(range * 0.75, range, lum);

				//Cell size follows distance, so nearby surfaces carry a coarser pattern than far ones and the
				//grain reads as sitting on the geometry rather than on the screen
				float eyeDepth = LinearEyeDepth(SampleSceneDepth(input.texcoord), _ZBufferParams);
				float perspective = lerp(1.0, clamp(_PerspectiveRef / max(eyeDepth, 0.01), 0.35, 3.0), _PerspectiveScale);
				float cellSize = max(_DitherPixelSize * perspective, 1.0);

				//Camera forward straight out of the view matrix, turned into yaw/pitch so looking around
				//shifts the pattern as readily as walking does
				float3 camFwd = -float3(UNITY_MATRIX_V._m20, UNITY_MATRIX_V._m21, UNITY_MATRIX_V._m22);
				float yaw = atan2(camFwd.x, camFwd.z);
				float pitch = asin(clamp(camFwd.y, -1.0, 1.0));

				float2 drift = float2(yaw, pitch) * _DriftFromLook
							 + float2(_WorldSpaceCameraPos.x + _WorldSpaceCameraPos.z, _WorldSpaceCameraPos.y) * _DriftFromMove;

				//Stepped rather than continuous: the pattern jumps to a new arrangement N times a second
				//instead of crawling every frame, which reads as grain rather than as swimming
				float tick = floor(_Time.y * max(_TemporalRate, 0.0001));
				drift += float2(Hash(tick), Hash(tick + 37.0)) * 4.0 * _TemporalAmount;

				float2 cell = floor(input.positionCS.xy / cellSize + drift);
				float threshold = Bayer4x4(cell);

				//Quantise across the dark range only. Normalising first is what keeps _Levels meaningful:
				//against raw luminance a low threshold can be narrower than one level, leaving every pixel to
				//flip between the same two values - which is exactly what makes it look like a flat overlay.
				float steps = max(_Levels, 2.0) - 1.0;
				//Lift the floor so even the deepest black straddles a level boundary. Without this anything at
				//true black quantises to level 0 whatever the Bayer value is, so the darkest part of the frame -
				//exactly where the texture is wanted - comes out perfectly clean.
				float t = lerp(_ShadowLift, 1.0, saturate(lum / range));

				//Jitter the boundary by up to a level so the bands break into ragged edges rather than reading
				//as clean iso-luminance contours. Tied to the temporal tick so the edges crawl with the grain.
				t += (Hash2(input.positionCS.xy + tick * 17.0) - 0.5) * (_EdgeNoise / steps);
				float quantised = (floor(t * steps + threshold) / steps) * range;

				//Carried as a luminance delta rather than a ratio - hue is preserved and it cannot blow up
				//as lum approaches zero
				float3 dithered = col.rgb + (quantised - lum);

				col.rgb = saturate(lerp(col.rgb, dithered, darkness * amount));
				}

				//Respawn fade, driven by Player3DController. Folded into this pass rather than given its own
				//fullscreen blit: this pass already reads and writes every pixel, so riding along costs one
				//multiply instead of a second full-resolution read and write every frame.
				col.rgb *= 1.0 - saturate(_FadeToBlack);
				return col;
			}
			ENDHLSL
		}
	}
}
