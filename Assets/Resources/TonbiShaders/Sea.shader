Shader "Tonbi/Sea"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.02,0.22,0.40,1)
        _Shallow ("Shallow", Color) = (0.12,0.68,0.72,1)
        _Foam ("Foam", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        struct Input { float3 worldPos; };
        fixed4 _Deep;
        fixed4 _Shallow;
        fixed4 _Foam;

        float sHash (float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }
        float sNoise (float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = sHash(i);
            float b = sHash(i + float2(1, 0));
            float c = sHash(i + float2(0, 1));
            float d = sHash(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }
        float sHeight (float2 p, float t)
        {
            float h = 0.0;
            h += sin(dot(p, float2(0.05, 0.12)) + t * 0.9) * 0.5;
            h += sin(dot(p, float2(-0.09, 0.07)) + t * 1.3) * 0.35;
            h += sin(dot(p, float2(0.17, 0.03)) + t * 1.9) * 0.18;
            h += (sNoise(p * 0.3 + t * 0.4) - 0.5) * 0.4;
            return h;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 p = IN.worldPos.xz;
            float t = _Time.y;
            float e = 0.4;
            float h0 = sHeight(p, t);
            float hx = sHeight(p + float2(e, 0), t);
            float hz = sHeight(p + float2(0, e), t);
            o.Normal = normalize(float3(-(hx - h0) / e * 0.9, -(hz - h0) / e * 0.9, 1.0));

            float dist = -p.y; // metres from the shoreline (sea is at -Z)
            float sh = saturate(1.0 - dist / 220.0);
            sh = sh * sh;
            float3 col = lerp(_Deep.rgb, _Shallow.rgb, sh);
            col *= 0.9 + 0.2 * (h0 + 0.5);

            float shoreMask = saturate(1.0 - dist / 70.0);
            float band = sin(dist * 0.22 + t * 0.8 + sin(p.x * 0.03) * 1.5);
            float f = smoothstep(0.75, 1.0, band) * shoreMask;
            f *= 0.5 + 0.9 * sNoise(p * 0.5 + t * 0.3);
            float edge = saturate(1.0 - (dist + 2.0 * sin(p.x * 0.05 + t * 0.4)) / 5.0);
            f += edge * (0.6 + 0.4 * sNoise(p * 0.8 + t * 0.2));
            f = saturate(f);

            o.Albedo = lerp(col, _Foam.rgb, f);
            o.Smoothness = lerp(0.88, 0.3, f);
            o.Metallic = 0;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
