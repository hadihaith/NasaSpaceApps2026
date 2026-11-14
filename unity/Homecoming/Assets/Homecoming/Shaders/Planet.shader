Shader "Homecoming/Planet"
{
    Properties
    {
        _MainTex ("Surface", 2D) = "white" {}
        _NightTex ("Night lights", 2D) = "black" {}
        _Hologram ("Projection", Range(0,1)) = 0
        _IsMoon ("Moon", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _NightTex;
            float _Hologram, _IsMoon;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 normal : TEXCOORD1; float3 view : TEXCOORD2; };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.view = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.normal), view = normalize(i.view);
                float3 sun = normalize(float3(-.85,.4,.65));
                float light = dot(n,sun), day = smoothstep(-.08,.18,light);
                float3 surface = tex2D(_MainTex,i.uv).rgb;
                float3 night = tex2D(_NightTex,i.uv).rgb * .7 + surface * .025;
                float3 c = lerp(night,surface*(.17+max(0,light)*1.12),day);
                float edge = pow(1-saturate(dot(n,view)),4);
                c += (1-_IsMoon)*float3(.05,.17,.28)*edge;
                c = lerp(c,surface*(.045+max(0,light)*1.2),_IsMoon);
                float2 grid = abs(frac(i.uv*float2(24,12))-.5);
                float lines = 1-smoothstep(.012,.03,min(grid.x,grid.y));
                float3 projected = float3(.12,.6,.65)*(.12+dot(surface,float3(.3,.59,.11))*.8+edge*.4+lines*.12);
                return fixed4(lerp(c,projected,_Hologram),1);
            }
            ENDCG
        }
    }
}
