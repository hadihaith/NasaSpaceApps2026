Shader "Homecoming/Intro Screen"
{
    Properties
    {
        _MainTex ("Video frame", 2D) = "black" {}
        _Fit ("Fit inside", Vector) = (1, 1, 0, 0)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float2 _Fit;

            half4 frag(v2f_img input) : SV_Target
            {
                float2 uv = (input.uv - 0.5) * _Fit + 0.5;
                if (any(uv < 0) || any(uv > 1)) return half4(0, 0, 0, 1);
                return half4(tex2D(_MainTex, uv).rgb, 1);
            }
            ENDCG
        }
    }
}
