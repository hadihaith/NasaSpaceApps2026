// Film finishing pass for the story camera (Built-in Render Pipeline):
// soft-threshold bloom, exposure, filmic (ACES fitted) tone mapping, grade, vignette and grain.
// Lives in Resources so player builds always include it.
Shader "Hidden/Homecoming/FilmGrade"
{
    Properties { _MainTex ("Source", 2D) = "black" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _BloomTex, _FocusTex;
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    float4 _MainTex_TexelSize;
    float _Threshold, _Knee, _BloomIntensity, _Exposure, _Saturation, _Contrast, _Vignette, _Grain, _Seed;
    float4 _Tint, _ShadowTint;
    float4 _Focus;

    half3 Box4(float2 uv, float2 texel)
    {
        float4 o = texel.xyxy * float4(-1, -1, 1, 1);
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * .25;
    }
    half3 Tent9(float2 uv, float2 texel)
    {
        float4 o = texel.xyxy * float4(1, 1, -1, 0);
        half3 s = tex2D(_MainTex, uv - o.xy).rgb;
        s += tex2D(_MainTex, uv - o.wy).rgb * 2;
        s += tex2D(_MainTex, uv - o.zy).rgb;
        s += tex2D(_MainTex, uv + o.zw).rgb * 2;
        s += tex2D(_MainTex, uv).rgb * 4;
        s += tex2D(_MainTex, uv + o.xw).rgb * 2;
        s += tex2D(_MainTex, uv + o.zy).rgb;
        s += tex2D(_MainTex, uv + o.wy).rgb * 2;
        s += tex2D(_MainTex, uv + o.xy).rgb;
        return s / 16;
    }
    half3 Aces(half3 x) { return saturate(x * (2.51 * x + .03) / (x * (2.43 * x + .59) + .14)); }
    float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

    half4 Prefilter(v2f_img i) : SV_Target
    {
        half3 c = Box4(i.uv, _MainTex_TexelSize.xy);
        half brightness = max(c.r, max(c.g, c.b));
        half soft = clamp(brightness - _Threshold + _Knee, 0, 2 * _Knee);
        soft = soft * soft / (4 * _Knee + 1e-4);
        half contribution = max(soft, brightness - _Threshold) / max(brightness, 1e-4);
        return half4(c * contribution, 1);
    }
    half4 Down(v2f_img i) : SV_Target { return half4(Box4(i.uv, _MainTex_TexelSize.xy), 1); }
    half4 Up(v2f_img i) : SV_Target { return half4(Tent9(i.uv, _MainTex_TexelSize.xy), 1); }
    half4 Composite(v2f_img i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        if (_Focus.z > .001)
        {
            float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv));
            // Far-background defocus only; no blurred foreground bleeding across faces.
            float coc = smoothstep(_Focus.y,_Focus.y*2.1,max(0,depth-_Focus.x))*_Focus.z;
            c = lerp(c,tex2D(_FocusTex,i.uv).rgb,coc);
        }
        c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity;
        c *= _Exposure * _Tint.rgb;
        c = Aces(c);
        half luma = dot(c, half3(.2126, .7152, .0722));
        c = lerp(c, c * _ShadowTint.rgb, saturate(1 - luma * 2.2) * _ShadowTint.a);
        c = lerp(luma.xxx, c, _Saturation);
        float2 d = (i.uv - .5) * float2(_MainTex_TexelSize.y * _MainTex_TexelSize.z, 1);
        c *= 1 - _Vignette * smoothstep(.3, .95, length(d) * 1.35);
        // Grain belongs in display space; adding it to linear black amplified it
        // into bright speckles when the framebuffer converted to sRGB.
        half3 display = LinearToGammaSpace(max(c,0));
        // Contrast also belongs in display space. A small linear-space contrast
        // boost was clipping nearly every dim bulkhead pixel to absolute black.
        display = saturate((display - .5) * _Contrast + .5);
        display += (Hash(i.uv * _MainTex_TexelSize.zw + _Seed) - .5) * _Grain;
        c = GammaToLinearSpace(saturate(display));
        return half4(c, 1);
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Prefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Down
            ENDCG }
        Pass { Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Up
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Composite
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FocusBlur
            half4 FocusBlur(v2f_img i):SV_Target {return half4(Tent9(i.uv,_MainTex_TexelSize.xy*2.3),1);}
            ENDCG }
    }
    Fallback Off
}
