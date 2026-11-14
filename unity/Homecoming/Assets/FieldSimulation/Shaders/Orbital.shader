Shader "Homecoming/Field Orbital Earth"
{
    Properties { _MainTex ("Earth texture", 2D) = "white" {} }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 view : TEXCOORD2; };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord;
                o.n=normalize(UnityObjectToWorldNormal(v.normal));
                o.view=_WorldSpaceCameraPos-mul(unity_ObjectToWorld,v.vertex).xyz;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float3 n=normalize(i.n);
                float sun = smoothstep(-.12,.76,dot(n,normalize(float3(-.55,.64,-.64))));
                float edge = pow(1-saturate(dot(n,normalize(i.view))),4);
                float3 c = tex2D(_MainTex,i.uv).rgb * (.17 + sun * 1.33);
                return float4(c + float3(.044,.24,.48)*edge*(.34+sun*.66),1);
            }
            ENDCG
        }
    }
}
