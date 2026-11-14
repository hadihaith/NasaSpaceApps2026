Shader "Homecoming/Live ship instruments"
{
    Properties { _Clock("Clock",Float)=0 _Power("Power",Float)=1 _Channel("Channel",Float)=0 }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float _Clock,_Power,_Channel;
            fixed4 frag(v2f_img i):SV_Target
            {
                float2 uv=i.uv;
                float2 grid=abs(frac(uv*float2(12,5)+.5)-.5);
                float marks=(1-smoothstep(.01,.035,min(grid.x,grid.y)))*.08;
                float phase=uv.x*12+_Clock*.8+_Channel;
                float trace=.5+sin(phase)*.13+sin(phase*2.7)*.035;
                float signal=exp(-abs(uv.y-trace)*110);
                float sweep=exp(-abs(uv.x-frac(_Clock*.2+_Channel*.3))*25);
                float bars=step(.82,uv.y)*step(uv.x,.13+frac(_Clock*.06)*.6)*.35;
                float3 phosphor=float3(.3,.74,.61)*(marks+signal*(.4+sweep)+bars);
                return fixed4(float3(.007,.024,.027)+phosphor*_Power,1);
            }
            ENDCG
        }
    }
}
