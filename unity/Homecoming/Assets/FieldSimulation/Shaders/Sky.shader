Shader "Homecoming/Field Sky"
{
    Properties
    {
        _Weather ("Storm", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Weather;
            struct v2f { float4 pos : SV_POSITION; float3 direction : TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.direction=v.vertex.xyz; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            float4 frag(v2f i) : SV_Target
            {
                float3 ray=normalize(i.direction);
                float haze=exp(-max(0,ray.y)*5.8);
                float3 clear=lerp(float3(.065,.275,.42),float3(.55,.71,.67),haze);
                float3 storm=lerp(float3(.025,.073,.117),float3(.26,.40,.41),haze);
                float3 c=lerp(clear,storm,_Weather);
                float2 coord=ray.xz/(.25+max(0,ray.y))*3.7+float2(_Time.y*.018,.7);
                float n=noise(coord)*.52+noise(coord*2.03+2)*.27+noise(coord*4.1-3)*.14+noise(coord*9.1)*.07;
                float cloud=smoothstep(.50-_Weather*.21,.76-_Weather*.17,n) * smoothstep(-.07,.23,ray.y);
                float3 cloudColor=lerp(float3(.72,.76,.684),lerp(float3(.068,.123,.157),float3(.32,.43,.45),n),_Weather);
                c=lerp(c,cloudColor,cloud);
                float dotSun=dot(ray,normalize(float3(-.62,.42,.67)));
                c+=float3(.55,.37,.19)*(pow(saturate(dotSun),19)*.38+smoothstep(.9982,.9994,dotSun)*.95)*(1-_Weather*.91);
                return float4(c,1);
            }
            ENDCG
        }
    }
}
