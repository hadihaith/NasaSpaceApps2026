Shader "Homecoming/Worn ship deck"
{
    Properties { _Color("Deck enamel", Color) = (.19,.23,.25,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        fixed4 _Color;
        struct Input { float3 worldPos; };
        float Noise(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        void surf(Input i, inout SurfaceOutputStandard o)
        {
            float2 p=i.worldPos.xz;
            float2 grid=abs(frac(p*float2(1,.5)+.5)-.5);
            float seam=1-smoothstep(.002,.006+max(fwidth(p.x),fwidth(p.y))*.3,min(grid.x,grid.y));
            float brushing=.5+.5*sin(p.x*340+sin(p.y*21)*2);
            brushing=lerp(.5,brushing,saturate(1-fwidth(p.x)*170));
            float wear=Noise(floor(p*7))*.025;
            o.Albedo=_Color.rgb*(.94+wear+brushing*.05)*(1-seam*.3);
            o.Metallic=.42; o.Smoothness=.29+brushing*.06;
            o.Occlusion=1-seam*.2;
        }
        ENDCG
    }
    Fallback "Standard"
}
