Shader "Tonbi/VertexLit"
{
    Properties
    {
        _Smooth ("Smoothness", Range(0,1)) = 0.2
        _Tint ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        struct Input { float4 color : COLOR; };
        half _Smooth;
        fixed4 _Tint;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
#ifdef UNITY_COLORSPACE_GAMMA
            o.Albedo = IN.color.rgb * _Tint.rgb;
#else
            o.Albedo = GammaToLinearSpace(IN.color.rgb) * _Tint.rgb;
#endif
            o.Smoothness = _Smooth;
            o.Metallic = 0;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
