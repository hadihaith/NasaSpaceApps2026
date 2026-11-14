Shader "Homecoming/Observation glass"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 view:TEXCOORD1; };
            v2f vert(appdata_base v)
            {
                v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);
                o.view=_WorldSpaceCameraPos-mul(unity_ObjectToWorld,v.vertex).xyz;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float3 n=normalize(i.normal),v=normalize(i.view);
                float f=.022+.12*pow(1-saturate(dot(n,v)),5);
                half4 probe=UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0,reflect(-v,n),3);
                float3 reflected=DecodeHDR(probe,unity_SpecCube0_HDR);
                return fixed4(reflected*.65+float3(.025,.055,.07),f);
            }
            ENDCG
        }
    }
}
