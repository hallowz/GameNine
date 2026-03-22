Shader "Voidborne/ProceduralSkybox"
{
    Properties
    {
        // Sky gradient
        _ZenithColor ("Zenith Color", Color) = (0.15, 0.4, 0.8, 1)
        _HorizonColor ("Horizon Color", Color) = (0.6, 0.8, 0.95, 1)
        _GroundColor ("Ground Color", Color) = (0.3, 0.25, 0.2, 1)
        _HorizonSharpness ("Horizon Sharpness", Range(1, 10)) = 3

        // Sun
        _SunDir ("Sun Direction", Vector) = (0, 1, 0, 0)
        _SunColor ("Sun Color", Color) = (1, 0.95, 0.8, 1)
        _SunSize ("Sun Disc Size", Range(0.0, 0.02)) = 0.004
        _SunGlowSize ("Sun Glow Size", Range(0.0, 0.5)) = 0.15
        _SunGlowIntensity ("Sun Glow Intensity", Range(0, 2)) = 0.6

        // Moon
        _MoonDir ("Moon Direction", Vector) = (0, -1, 0, 0)
        _MoonColor ("Moon Color", Color) = (0.85, 0.9, 1, 1)
        _MoonSize ("Moon Disc Size", Range(0.0, 0.02)) = 0.003
        _MoonBrightness ("Moon Brightness", Range(0, 2)) = 0.8
        _MoonPhase ("Moon Phase Offset", Range(-1, 1)) = 0.3

        // Stars
        _StarBrightness ("Star Brightness", Range(0, 2)) = 1.0
        _StarDensity ("Star Density", Range(5, 50)) = 20
        _StarTwinkleSpeed ("Star Twinkle Speed", Range(0, 5)) = 1.5
        _NightFactor ("Night Factor", Range(0, 1)) = 0

        // Sunrise/sunset glow band
        _GlowColor ("Horizon Glow Color", Color) = (1, 0.4, 0.1, 1)
        _GlowIntensity ("Horizon Glow Intensity", Range(0, 2)) = 0
        _GlowWidth ("Horizon Glow Width", Range(0.01, 0.5)) = 0.15

        // Underground
        _UndergroundFactor ("Underground Factor", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldDir : TEXCOORD0;
            };

            float4 _ZenithColor;
            float4 _HorizonColor;
            float4 _GroundColor;
            float _HorizonSharpness;

            float3 _SunDir;
            float4 _SunColor;
            float _SunSize;
            float _SunGlowSize;
            float _SunGlowIntensity;

            float3 _MoonDir;
            float4 _MoonColor;
            float _MoonSize;
            float _MoonBrightness;
            float _MoonPhase;

            float _StarBrightness;
            float _StarDensity;
            float _StarTwinkleSpeed;
            float _NightFactor;

            float4 _GlowColor;
            float _GlowIntensity;
            float _GlowWidth;

            float _UndergroundFactor;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldDir = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                return o;
            }

            // Hash functions for star generation
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float hash31(float3 p)
            {
                p = frac(p * float3(443.897, 441.423, 437.195));
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            // Procedural stars using cell-based approach
            float Stars(float3 dir, float density, float twinkleSpeed)
            {
                // Project direction onto a high-res grid on unit sphere
                // Use spherical coordinates mapped to a grid
                float3 n = normalize(dir);

                // Create a grid on the sphere
                float scale = density;
                float2 uv = float2(atan2(n.z, n.x) / 6.28318 + 0.5, asin(n.y) / 3.14159 + 0.5);
                float2 grid = floor(uv * scale * float2(2.0, 1.0));
                float2 f = frac(uv * scale * float2(2.0, 1.0));

                float star = 0;

                // Check surrounding cells for stars
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        float2 cell = grid + float2(x, y);
                        float r = hash21(cell);

                        // Only some cells have stars
                        if (r > 0.7)
                        {
                            float2 starPos = float2(hash21(cell * 1.3), hash21(cell * 2.7)) * 0.8 + 0.1;
                            float2 diff = f - starPos - float2(x, y);
                            float dist = length(diff);

                            // Star brightness varies
                            float brightness = hash21(cell * 3.1);
                            brightness = brightness * brightness; // emphasise bright stars

                            // Twinkle
                            float twinkle = sin(_Time.y * twinkleSpeed * (hash21(cell * 5.7) + 0.5)
                                                + hash21(cell * 7.3) * 6.28) * 0.3 + 0.7;

                            // Sharp point
                            float s = saturate(1.0 - dist * 40.0) * brightness * twinkle;
                            // Soft glow around brighter stars
                            s += saturate(1.0 - dist * 12.0) * brightness * twinkle * 0.3;

                            star = max(star, s);
                        }
                    }
                }

                return star;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.worldDir);
                float y = dir.y;

                // --- Sky gradient ---
                float horizonBlend = pow(saturate(y), 1.0 / _HorizonSharpness);
                float3 skyColor;
                if (y >= 0)
                {
                    skyColor = lerp(_HorizonColor.rgb, _ZenithColor.rgb, horizonBlend);
                }
                else
                {
                    float groundBlend = pow(saturate(-y), 0.5);
                    skyColor = lerp(_HorizonColor.rgb, _GroundColor.rgb, groundBlend);
                }

                // --- Horizon glow (sunrise/sunset) ---
                float horizonDist = abs(y);
                float glowMask = saturate(1.0 - horizonDist / _GlowWidth);
                glowMask = glowMask * glowMask;

                // Concentrate glow toward sun azimuth
                float3 sunDirFlat = normalize(float3(_SunDir.x, 0, _SunDir.z));
                float3 dirFlat = normalize(float3(dir.x, 0, dir.z));
                float azimuthDot = dot(sunDirFlat, dirFlat) * 0.5 + 0.5; // 0..1
                float azimuthMask = pow(azimuthDot, 2.0);

                // Also add a weaker wrap-around glow
                float wrapGlow = glowMask * 0.3;
                float directGlow = glowMask * azimuthMask;

                skyColor += _GlowColor.rgb * _GlowIntensity * (directGlow + wrapGlow);

                // --- Sun disc and glow ---
                float3 sunDir = normalize(_SunDir);
                float sunDot = dot(dir, sunDir);

                // Hard disc
                float sunDisc = saturate((sunDot - (1.0 - _SunSize)) / (_SunSize * 0.1));
                skyColor += _SunColor.rgb * sunDisc * 2.0;

                // Soft glow
                float sunGlow = pow(saturate(sunDot), 8.0 / max(_SunGlowSize, 0.001));
                skyColor += _SunColor.rgb * sunGlow * _SunGlowIntensity;

                // --- Moon (only when night and moon is above horizon) ---
                float3 moonDir = normalize(_MoonDir);
                float moonDot = dot(dir, moonDir);

                if (_NightFactor > 0.01 && moonDir.y > -0.1)
                {
                    float moonDisc = saturate((moonDot - (1.0 - _MoonSize)) / (_MoonSize * 0.1));
                    if (moonDisc > 0)
                    {
                        float3 moonRight = normalize(cross(moonDir, float3(0, 1, 0)));
                        float3 moonUp = normalize(cross(moonRight, moonDir));
                        float3 toPixel = normalize(dir - moonDir * moonDot);
                        float phaseX = dot(toPixel, moonRight);
                        float phaseShadow = saturate(phaseX * (1.0 / max(abs(_MoonPhase), 0.01)) * sign(_MoonPhase) + 0.5);

                        skyColor += _MoonColor.rgb * moonDisc * _MoonBrightness * _NightFactor * phaseShadow;
                    }

                    // Subtle moon glow
                    float moonGlow = pow(saturate(moonDot), 32.0);
                    skyColor += _MoonColor.rgb * moonGlow * 0.15 * _NightFactor;
                }

                // --- Stars ---
                if (_NightFactor > 0.01 && y > -0.05)
                {
                    float starField = Stars(dir, _StarDensity, _StarTwinkleSpeed);

                    // Fade stars near horizon (atmospheric extinction)
                    float altitudeFade = saturate(y * 5.0);
                    starField *= altitudeFade;

                    skyColor += starField * _StarBrightness * _NightFactor;
                }

                // Underground: lerp entire sky to near-black
                float3 caveColor = float3(0.003, 0.003, 0.005);
                skyColor = lerp(skyColor, caveColor, _UndergroundFactor);

                return fixed4(skyColor, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}
