Shader "Tonbi/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.20,0.48,0.88,1)
        _Hor ("Horizon", Color) = (0.78,0.90,0.97,1)
        _Bot ("Bottom", Color) = (0.55,0.72,0.80,1)
        _SunDir ("Sun dir", Vector) = (0.45,0.62,0.45,0)
        _SunCol ("Sun colour", Color) = (1,0.92,0.75,1)
        _Night ("Night", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Fog { Mode Off }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Top;
            fixed4 _Hor;
            fixed4 _Bot;
            fixed4 _SunCol;
            float4 _SunDir;
            float _Night;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                float3 col;
                if (h > 0.0) col = lerp(_Hor.rgb, _Top.rgb, pow(saturate(h), 0.45));
                else col = lerp(_Hor.rgb, _Bot.rgb, saturate(-h * 3.0));

                float s = saturate(dot(d, normalize(_SunDir.xyz)));
                col += _SunCol.rgb * (pow(s, 600.0) * 3.0 + pow(s, 12.0) * 0.35 + pow(s, 3.0) * 0.08);

                float3 q = floor(d * 220.0);
                float hh = frac(sin(dot(q, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float star = step(0.9965, hh) * saturate(d.y * 3.0) * _Night;
                col += star;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
