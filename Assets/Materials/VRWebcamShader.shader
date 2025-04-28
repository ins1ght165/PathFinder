Shader "Custom/VRWebcamShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
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

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Simulate lens distortion (basic VR effect)
                float2 uv = i.uv - 0.5;
                uv *= 1.5; // Expand the field of view
                float r = length(uv);
                uv *= (1.0 + 0.2 * r * r);  // Adjust distortion strength
                uv += 0.5;

                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1)
                    return fixed4(0,0,0,1); // Black outside valid area

                return tex2D(_MainTex, uv);
            }
            ENDCG
        }
    }
}
