Shader "Homecoming/Projector light volume"
{
    Properties { _Clock("Clock",Float)=0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off Cull Off Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Clock;
            fixed4 frag(v2f_img i):SV_Target
            {
                float rays=.7+.3*sin(i.uv.x*94+_Clock*.25);
                float fade=(1-smoothstep(.12,1,i.uv.y))*smoothstep(0,.12,i.uv.y);
                return fixed4(.1,.57,.65,fade*rays*.065);
            }
            ENDCG
        }
    }
}
