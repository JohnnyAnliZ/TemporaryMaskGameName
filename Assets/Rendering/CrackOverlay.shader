Shader "Custom/CrackOverlay"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_CrackColor ("Crack Color", Color) = (0.02, 0.02, 0.03, 1)
		_CrackHighlight ("Crack Highlight", Color) = (0.85, 0.9, 1.0, 1)
		_CrackCore ("Crack Core Width", Range(0.01, 1)) = 0.3
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
			//Set globally by CompositeManager: the live 3D view, for glass that has cracked but not yet gone.
			TEXTURE2D(_CameraB_Tex);
			SAMPLER(sampler_CameraB_Tex);

			CBUFFER_START(UnityPerMaterial)
				float4 _MainTex_ST;
				float4 _CrackColor;
				float4 _CrackHighlight;
				float _CrackCore;
			CBUFFER_END

			struct Attributes {
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
				//x = -1..1 across the crack's width, y = shading mode, z = how much this crack glints
				float3 crack : TEXCOORD1;
				float4 color : COLOR;
			};
			struct Varyings {
				float4 positionCS : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 crack : TEXCOORD1;
				float4 color : COLOR;
			};

			Varyings Vert(Attributes v)
			{
				Varyings o;
				float2 clip = v.positionOS.xy * 2.0 - 1.0;
				o.positionCS = float4(clip, 0, 1);
				o.uv = v.uv;
				o.crack = v.crack;
				o.color = v.color; //rgb tint; alpha carries the crack's fade in/out
				return o;
			}

			half4 Frag(Varyings i) : SV_Target
			{
				//This pass writes clip space directly instead of going through a fullscreen-triangle helper,
				//so it doesn't inherit the texcoord flip those helpers apply. Composite.shader pairs clip
				//y=+1 with v=0; matching that here is what stops a shard showing the mirrored patch of the
				//scene rather than the one actually behind it.
				float2 tuv = i.uv;
				#if UNITY_UV_STARTS_AT_TOP
					tuv.y = 1.0 - tuv.y;
				#endif

				//Cracked but still standing: the live view, sampled through the shard's own offset so the
				//piece sits slightly out of line with its neighbours the way loose glass does.
				if (i.crack.y > 1.5)
					return SAMPLE_TEXTURE2D(_CameraB_Tex, sampler_CameraB_Tex, tuv) * i.color;

				//Fallen shards (and the black holes they left) sample the frozen 3D frame.
				half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, tuv) * i.color;
				if (i.crack.y < 0.5) return tex;

				//A crack is a dark separation, and on a few of them the fracture catches the light.
				float t = abs(i.crack.x);

				//One pixel of edge, measured in screen space. The old falloff was a fixed fraction of the
				//crack's width, so it ate nearly half of an already thin line, still wasn't visible, and
				//changed as the crack tapered. fwidth gives the same ~1px regardless of width or taper --
				//which matters here because nothing on this path has MSAA.
				float aa = fwidth(t);
				float edge = 1.0 - smoothstep(1.0 - aa, 1.0, t);

				//crack.z gates the glint per crack, so most stay plain dark. A bright core on every one
				//reads as glowing veins rather than broken mirror.
				float core = (1.0 - smoothstep(0.0, _CrackCore, t)) * i.crack.z;
				half3 rgb = lerp(_CrackColor.rgb, _CrackHighlight.rgb, core);

				//color.a carries the tip taper from the mesh builder.
				return half4(rgb, edge * i.color.a);
			}
			ENDHLSL
		}
	}
}
