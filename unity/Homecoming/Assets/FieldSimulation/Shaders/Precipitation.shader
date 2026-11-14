Shader "Homecoming/Field Precipitation"
{
    Properties { _Strength ("Density", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Strength;
            struct app { float4 vertex:POSITION; float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float alpha:TEXCOORD0; };
            v2f vert(app v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.alpha=v.color.a*_Strength;
                return o;
            }
            float4 frag(v2f i):SV_Target { return float4(.52,.72,.81,i.alpha); }
            ENDCG
        }
    }
}
