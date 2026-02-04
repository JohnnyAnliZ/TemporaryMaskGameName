Shader "Custom/CompositeMask"
{
    Properties
    {
        _GameA ("Game A (2D Scene)", 2D) = "white" {}
        _GameB ("Game B (3D Scene)", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "black" {}
    }
     
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _GameA;
            sampler2D _GameB;
            sampler2D _MaskTex;

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
                // Sample both scenes
                fixed4 scene2D = tex2D(_GameA, i.uv);
                fixed4 scene3D = tex2D(_GameB, i.uv);
                
                // Sample the mask (white = show 3D, black = show 2D)
                fixed maskValue = tex2D(_MaskTex, i.uv).r;
                
                // Blend between the two scenes based on mask
                return lerp(scene2D, scene3D, maskValue);
            }
            ENDCG
        }
    }
}