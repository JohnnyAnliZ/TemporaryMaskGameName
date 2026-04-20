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
            float _CellSize;     // size of the Voronoi cells in pixels

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

            float2 CellOffset(uint2 cellCoord) {
                // random offset within cell so centers aren't on a perfect grid
                uint hx = Hash2D(cellCoord);
                uint hy = Hash2D(cellCoord + uint2(1337u, 7919u));
                return float2(hx, hy) / 4294967295.0; // 0..1
            }

            float GlassDist(float2 a, float2 b) {
                float2 d = a - b;
                // heavy x weight = tall thin shards, tweak ratio to taste
                const float biasWeightX = 3.0;
                const float biasWeightY = 30.0;

                //random biasDirection
                float2 biasDirection = float2(sin(b.x * 12.9898 + b.y * 78.233), cos(b.x * 12.9898 + b.y * 78.233));

                float2 bias;
                bias.x = biasDirection.x * biasWeightX;
                bias.y = biasDirection.y * biasWeightY;
                return length(d+bias);
            }

            half4 Frag(Varyings i) : SV_Target {
                float2 pixelStep = _PixelSize / _Resolution;
                float2 snappedUV = floor(i.uv / pixelStep) * pixelStep + pixelStep * 0.5;

                // Which "macro pixel" are we in? Use integer coords for stable hashing
                uint2 pixelCoord = (uint2)(snappedUV * _Resolution / _PixelSize);
                // Offset by camera position so the pattern is world-stable
                //map _cameraPos to the background image

                uint2 worldPixelCoord = pixelCoord + (int2)(_CameraPos * 47 / _PixelSize);//hacky part, try to match the background movement to the mask movement

                 // which cell grid square are we in
                float2 cellCoordF = worldPixelCoord / _CellSize;
                int2 cellBase = (int2)floor(cellCoordF);

                // find nearest voronoi cell center among 3x3 neighbours
                float minDist = 1e9;
                int2 nearestCell = cellBase;

                for (int cx = -1; cx <= 1; cx++) {
                    for (int cy = -1; cy <= 1; cy++) {
                        int2 neighbor = cellBase + int2(cx, cy);
                        float2 offset = CellOffset((uint2)neighbor);
                        //biase the x value of the offset to mimic glass shattering

                        float2 center = (float2(neighbor) + offset) * _CellSize;
                        float d = GlassDist(worldPixelCoord, center);
                        if (d < minDist) {
                            minDist = d;
                            nearestCell = neighbor;
                        }
                    }
                }

                // hash the cell, not the pixel
                float noise = Hash2D((uint2)nearestCell) / 4294967295.0;

                // Bias toward higher indices
                float biased = 1 - pow(noise, 1.5); // tweak 0.5

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