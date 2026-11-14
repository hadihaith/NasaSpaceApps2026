Shader "Homecoming/Field Land"
{
    Properties
    {
        _Wet ("Soil saturation", Range(0,1)) = 0
        _Wind ("Wind", Float) = 0
        _Stress ("Plant stress", Range(0,1)) = 0
        _Plant ("Foliage", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float _Wet, _Wind, _Stress, _Plant;
            struct input { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; float4 color : COLOR; UNITY_FOG_COORDS(2) };
            v2f vert(input v)
            {
                v2f o;
                float3 w = mul(unity_ObjectToWorld,v.vertex).xyz;
                float sway = _Plant * v.color.a * (sin(w.x*.38+w.z*.29+_Time.y*2.3)*.65+sin(w.z*.91+_Time.y*3.7)*.35);
                v.vertex.x += sway * (.045 + _Wind*.033);
                v.vertex.z += sway * (.012 + _Wind*.016);
                o.pos=UnityObjectToClipPos(v.vertex); o.world=w; o.normal=UnityObjectToWorldNormal(v.normal); o.color=v.color;
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float3 n=normalize(i.normal);
                float sun = saturate(dot(n,normalize(float3(-.58,.78,.25))))*.72 + .38;
                float grain = sin(i.world.x*9.3+i.world.z*7.7)*sin(i.world.z*13.1-i.world.x*12.9);
                float noise = grain * .029 * saturate(1 - length(fwidth(i.world.xz))*3.5);
                float3 soil = i.color.rgb * (1 + noise - _Wet * .32);
                float3 plant = lerp(i.color.rgb,i.color.rgb*float3(1.6,.8,.38)+float3(.054,.025,0),_Stress);
                float3 c = lerp(soil, plant, _Plant) * sun + pow(1-saturate(n.y),3)*float3(.004,.012,.019);
                UNITY_APPLY_FOG(i.fogCoord,c);
                return float4(c,1);
            }
            ENDCG
        }
    }
}
