Shader "Custom/CompositeAlpha"
{
    SubShader
    {
        Tags { 
            "RenderPipeline"="UniversalPipeline"
            "LightMode"="SRPDefaultUnlit"
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

            TEXTURE2D_X(_CameraA_Tex);
            SAMPLER(sampler_CameraA_Tex);

            TEXTURE2D_X(_CameraB_Tex);
            SAMPLER(sampler_CameraB_Tex);   

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 Frag (Varyings i) : SV_Target
            {
                half4 baseCol = SAMPLE_TEXTURE2D_X(_CameraA_Tex, sampler_CameraA_Tex, i.uv);
                half4 overCol = SAMPLE_TEXTURE2D_X(_CameraB_Tex, sampler_CameraB_Tex, i.uv);
                return half4(1, 0, 1, 1);
                //return lerp(overCol,baseCol, overCol.a);
            }
            ENDHLSL
        }
    }
}