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
                float2 centered = i.uv - .5;
                float2 sampleUv = saturate(centered * (1 + .045 * dot(centered, centered)) + .5);
                float3 c = tex2D(_MainTex,sampleUv).rgb;
                float shade = .97 + .03*sin(i.uv.y*1063);
                float vignette = 1 - smoothstep(.495, .535, max(abs(centered.x), abs(centered.y)) +
                    .14 * pow(min(abs(centered.x), abs(centered.y)), 2));
                return fixed4(c*_Color.rgb*shade*vignette,1);
            }
            ENDCG
        }
    }
}
