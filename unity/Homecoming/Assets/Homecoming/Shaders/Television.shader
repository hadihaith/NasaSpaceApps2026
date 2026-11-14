Shader "Homecoming/Television"
{
    Properties { _MainTex ("Broadcast", 2D) = "white" {} _Color ("Phosphor", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex,i.uv).rgb;
                float shade = .94 + .06*sin(i.uv.y*720*3.14159);
                float2 edge = i.uv*(1-i.uv);
                float vignette = saturate(pow(edge.x*edge.y*16,.18));
                return fixed4(c*_Color.rgb*shade*vignette,1);
            }
            ENDCG
        }
    }
}
