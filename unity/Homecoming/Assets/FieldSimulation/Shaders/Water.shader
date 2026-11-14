Shader "Homecoming/Field Water"
{
    Properties
    {
        _Weather ("Storm", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+15" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float _Weather;
            struct app { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; float depth : TEXCOORD1; UNITY_FOG_COORDS(2) };
            v2f vert(app v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz; o.depth=o.world.y-v.uv.x;
                UNITY_TRANSFER_FOG(o,o.pos); return o;
            }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float4 frag(v2f i) : SV_Target
            {
                float2 p=i.world.xz;
                float time=_Time.y;
                float distortion=sin(p.y*.87+sin(p.x*.69)+time*.42)*1.34 + sin(p.x*1.7+p.y*.64-time*.8)*.65;
                float a=p.x*1.8+p.y*2.7+time*1.8+distortion;
                float b=p.x*5.5-p.y*2.2+time*2.3+distortion;
                float calm=saturate(1-length(fwidth(p))*.69);
                float3 n=normalize(float3((sin(a)*.035+sin(b)*.014)*calm,1,(cos(b)*.035-cos(a)*.026)*calm));
                float3 view=normalize(_WorldSpaceCameraPos-i.world);
                float fresnel=pow(1-saturate(dot(n,view)),3);
                float ripple=0;
                float2 cell=p*1.25;
                for(int x=-1;x<=1;x++)
                for(int y=-1;y<=1;y++)
                {
                    float2 id=floor(cell)+float2(x,y);
                    float h=hash(id);
                    float t=frac(time*(.74+h*.5)+h*9);
                    float d=length(cell-id-float2(h,hash(id+6)))-t*.52;
                    ripple+=exp(-abs(d)*80)*sin(t*3.141593)*(1-t);
                }
                float3 deep=lerp(float3(.018,.135,.15),float3(.028,.095,.11),_Weather);
                float3 shallow=lerp(float3(.13,.21,.16),deep,saturate(i.depth*.64));
                float3 sky=lerp(float3(.20,.45,.49),float3(.13,.24,.29),_Weather);
                float3 c=lerp(shallow,sky,fresnel*.83);
                float glint=pow(saturate(dot(reflect(-normalize(float3(-.52,.56,.5)),n),view)),85);
                c+=glint*float3(.24,.25,.21)*(1-_Weather*.67);
                c+=ripple*_Weather*saturate(i.depth*5)*float3(.17,.31,.32);
                c+=exp(-max(i.depth,0)*16)*(.025+.021*sin(a))*float3(1,1.4,1.2);
                UNITY_APPLY_FOG(i.fogCoord,c);
                return float4(c,1);
            }
            ENDCG
        }
    }
}
