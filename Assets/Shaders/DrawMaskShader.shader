Shader "Custom/CircleMask"
{
    Properties
    {
        _Center ("Center", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Radius", Float) = 0.2
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" }
        Blend One One  // Additive blending to accumulate portals
        ZWrite Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Center;
            float _Radius;
            float _FadeEdge;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 toCenter = i.uv - _Center.xy;
                float dist = length(toCenter);
                
                // Smooth circle with fade edge
                float mask = dist > _Radius ? 0.0 : 1.0;
                
                return fixed4(mask, mask, mask, mask);
            }
            ENDCG
        }
    }
}