Shader "Custom/CircleMask"
{
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" }
		ZWrite Off
		ZTest Always
		Cull Off
		Pass
		{
			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

			int _PassIndex;      // 0, 1, 2, ... NumPasses-1
			int _Num2DTo3DPasses;      // total number of passes to fill the mask for 2D to 3D conversion
			int _Num3DToBlackPasses;   // total number of steps for the sequence from 3D to black
			float _CellSize;     // size of the Voronoi cells in pixels
			float _ShatterBias;  // >1 skews toward later passes revealing more cells
			float4 _CameraPos;   // world-space camera position for stable hashing

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
			uint Hash2D(int2 p) {
				uint x = p.x * 1664525u + p.y * 22695477u + 2891336453u;
				x ^= x >> 16;
				x *= 0x45d9f3bu;
				x ^= x >> 16;
				return x;
			}

			float2 CellOffset(int2 cellCoord) {
				// random offset within cell so centers aren't on a perfect grid
				uint hx = Hash2D(cellCoord);
				uint hy = Hash2D(cellCoord + uint2(1337u, 7919u));
				return float2(hx, hy) / 4294967295.0; // 0..1
			}

			float GlassDist(float2 a, float2 b) {
				float2 d = a - b;
				// heavy x weight = tall thin shards, tweak ratio to taste
				float biasWeightX = _CellSize * 0.05;
				float biasWeightY = _CellSize * 0.5;

				//random biasDirection
				float2 biasDirection = float2(sin(b.x * 12.9898 + b.y * 78.233), cos(b.x * 12.9898 + b.y * 78.233));
				float2 bias;
				bias.x = biasDirection.x * biasWeightX;
				bias.y = biasDirection.y * biasWeightY;
				return length(d+bias);
			}

			half4 Frag(Varyings i) : SV_Target {
				//no more magic 47, and no more pixelation cuz shard generation would be much more complicated...
				float2 ndc = i.uv * 2.0 - 1.0;
				float2 worldPixelCoord = float2(ndc.x * _CameraPos.z + _CameraPos.x, ndc.y * _CameraPos.w + _CameraPos.y);

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
				float noise = Hash2D(nearestCell) / 4294967295.0;

				// Bias toward higher indices
				float biased = 1.0 - pow(noise, _ShatterBias);

				uint assignedPass = (int)(biased * _Num2DTo3DPasses);
				assignedPass = min(assignedPass, _Num2DTo3DPasses - 1);

				if (assignedPass >= _PassIndex) discard;

				half4 ret = half4(1.0, 0.0, 0.0, 0.0);//set the first channel(2D to 3D)


				int toBlack_index = _PassIndex - _Num2DTo3DPasses - 1;
				float video_width_uv = 0.3; // the width of the video in UV space, tweak to taste
				if (toBlack_index >= 0) {
					float progress = (toBlack_index + 1.0) / _Num3DToBlackPasses;
					progress = saturate(progress);

					// half-width of the visible middle strip:
					// starts at 0.5 (whole screen visible), ends at video_width_uv/2
					float visibleHalfWidth = lerp(0.5, video_width_uv * 0.5, progress);

					float distFromCenter = abs(i.uv.x - 0.5);

					if (distFromCenter > visibleHalfWidth) {
						ret = half4(0.0, 1.0, 0.0, 0.0); // 3D -> black
					}
				}

				return ret;
			}
			ENDHLSL
		}
	}
}
