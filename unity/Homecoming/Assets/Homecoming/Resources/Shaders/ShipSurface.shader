// Explicitly included for the runtime-built bridge. Stock Standard variants can
// otherwise be stripped against the authored scene's original directional lights.
Shader "Homecoming/Ship surfaces"
{
    Properties
    {
        _Color("Enamel",Color)=(.2,.25,.28,1)
        _MainTex("Surface",2D)="white"{}
        _Metallic("Metal",Range(0,1))=.25
        _Glossiness("Polish",Range(0,1))=.3
        _EmissionColor("Practical emission",Color)=(0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color, _EmissionColor;
        half _Metallic, _Glossiness;
        struct Input {float2 uv_MainTex;};
        void surf(Input i,inout SurfaceOutputStandard o)
        {
            o.Albedo=tex2D(_MainTex,i.uv_MainTex).rgb*_Color.rgb;
            o.Metallic=_Metallic; o.Smoothness=_Glossiness;
            o.Emission=_EmissionColor.rgb; o.Alpha=1;
        }
        ENDCG
    }
    Fallback "Standard"
}
