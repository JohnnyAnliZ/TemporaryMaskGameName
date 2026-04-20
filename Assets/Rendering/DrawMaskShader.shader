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
            int _PassIndex;    // 0, 1, 2, ... NumPasses-1
            int _NumPasses;    // total number of passes to fill the mask
            float2 _CameraPos;   // world-space camera position for stable hashing

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

            // 2D integer hash — distributes pixels pseudo-randomly but deterministically
            uint Hash2D(uint2 p) {
                uint x = p.x * 1664525u + p.y * 22695477u + 2891336453u;
                x ^= x >> 16;
                x *= 0x45d9f3bu;
                x ^= x >> 16;
                return x;
            }

            half4 Frag(Varyings i) : SV_Target {
                float2 pixelStep = _PixelSize / _Resolution;
                float2 snappedUV = floor(i.uv / pixelStep) * pixelStep + pixelStep * 0.5;

                // Which "macro pixel" are we in? Use integer coords for stable hashing
                uint2 pixelCoord = (uint2)(snappedUV * _Resolution / _PixelSize);
                // Offset by camera position so the pattern is world-stable
                //map _cameraPos to the background image

                uint2 worldPixelCoord = pixelCoord + (int2)(_CameraPos * 47 / _PixelSize);//hacky part, try to match the background movement to the mask movement

                float u = (float)Hash2D(worldPixelCoord) / 4294967296.0;//uniform 0 to 1

                // Bias toward higher indices
                float biased = 1 - pow(u, 3); // tweak 0.5

                uint assignedPass = (uint)(biased * _NumPasses);
                assignedPass = min(assignedPass, _NumPasses - 1);

                // Only draw if this pixel belongs to the current pass
                if ((int)assignedPass != _PassIndex)
                    discard;

                return half4(1.0, 1.0, 1.0, 1.0);
            }
            ENDHLSL
        }
    }
}