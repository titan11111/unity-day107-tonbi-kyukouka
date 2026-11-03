Shader "Tonbi/VertexUnlit"
{
    Properties
    {
        _Base ("Base glow", Range(0,1)) = 0.2
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

            float _TonbiGlow;
            half _Base;

            struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; };

            v2f vert (appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.col = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float g = saturate(_Base + _TonbiGlow);
                float3 c = i.col.rgb * (0.3 + g * 1.5);
#ifndef UNITY_COLORSPACE_GAMMA
                c = GammaToLinearSpace(c);
#endif
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
