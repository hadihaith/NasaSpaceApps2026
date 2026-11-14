Shader "Homecoming/Orbital atmosphere"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Front ZWrite Off Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos:SV_POSITION;float3 n:TEXCOORD0;float3 v:TEXCOORD1; };
            v2f vert(appdata_base v)
            {
                v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.n=UnityObjectToWorldNormal(v.normal);
                o.v=_WorldSpaceCameraPos-mul(unity_ObjectToWorld,v.vertex).xyz;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float angle=abs(dot(normalize(i.n),normalize(i.v)));
                float rim=pow(1-angle,5)*smoothstep(0,.16,angle);
                float light=saturate(dot(normalize(i.n),normalize(float3(-.5,.32,.8)))+.4);
                return fixed4(.13,.39,.8,rim*light*.3);
            }
            ENDCG
        }
    }
}
