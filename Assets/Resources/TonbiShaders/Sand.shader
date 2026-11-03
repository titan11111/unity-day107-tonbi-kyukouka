Shader "Tonbi/Sand"
{
    Properties
    {
        _Dry ("Dry sand", Color) = (0.86,0.78,0.62,1)
        _Wet ("Wet sand", Color) = (0.50,0.42,0.32,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        struct Input { float3 worldPos; };
        fixed4 _Dry;
        fixed4 _Wet;

        float tHash (float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }
        float tNoise (float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = tHash(i);
            float b = tHash(i + float2(1, 0));
            float c = tHash(i + float2(0, 1));
            float d = tHash(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }
        float tFbm (float2 p)
        {
            float s = 0.0;
            float a = 0.5;
            for (int k = 0; k < 4; k++) { s += a * tNoise(p); p *= 2.03; a *= 0.5; }
            return s;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 p = IN.worldPos.xz;
            float z = IN.worldPos.z;
            float big = tFbm(p * 0.03);
            float rip = sin(p.x * 0.35 + tFbm(p * 0.07) * 9.0 + p.y * 0.1) * 0.5 + 0.5;
            float grain = tNoise(p * 6.0) * 0.5 + tNoise(p * 19.0) * 0.5;
            float3 c = lerp(_Dry.rgb * 0.88, _Dry.rgb * 1.06, big);
            c *= 0.93 + 0.10 * rip + 0.10 * grain;

            float wetLine = 14.0 + 7.0 * sin(p.x * 0.045 + 1.2) + 5.0 * sin(p.x * 0.11) + (tFbm(p * 0.05) - 0.5) * 8.0;
            float w = saturate((wetLine - z) / 10.0);
            c = lerp(c, _Wet.rgb * (0.9 + 0.2 * grain), w);

            // tide line debris (dark seaweed band)
            float band = smoothstep(2.5, 0.0, abs(z - (wetLine + 1.5))) * step(0.62, tNoise(p * 1.7));
            c = lerp(c, float3(0.16, 0.15, 0.09), band * 0.7);

            o.Albedo = c;
            o.Smoothness = 0.05 + 0.5 * w;
            o.Metallic = 0;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
