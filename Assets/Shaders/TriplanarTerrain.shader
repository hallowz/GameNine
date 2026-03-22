Shader "Voidborne/TriplanarTerrain"
{
    Properties
    {
        [Header(Biome 0 Plains)]
        _Tex0 ("Biome 0 Albedo", 2D) = "white" {}
        _Normal0 ("Biome 0 Normal", 2D) = "bump" {}
        _Color0 ("Biome 0 Tint", Color) = (0.5, 0.75, 0.25, 1)

        [Header(Biome 1 Volcanic Wastes)]
        _Tex1 ("Biome 1 Albedo", 2D) = "white" {}
        _Normal1 ("Biome 1 Normal", 2D) = "bump" {}
        _Color1 ("Biome 1 Tint", Color) = (0.25, 0.1, 0.05, 1)

        [Header(Biome 2 Tundra)]
        _Tex2 ("Biome 2 Albedo", 2D) = "white" {}
        _Normal2 ("Biome 2 Normal", 2D) = "bump" {}
        _Color2 ("Biome 2 Tint", Color) = (0.85, 0.9, 0.95, 1)

        [Header(Biome 3 Savanna)]
        _Tex3 ("Biome 3 Albedo", 2D) = "white" {}
        _Normal3 ("Biome 3 Normal", 2D) = "bump" {}
        _Color3 ("Biome 3 Tint", Color) = (0.75, 0.65, 0.3, 1)

        [Header(Triplanar Settings)]
        _TexScale ("Texture Scale", Float) = 0.25
        _BlendSharpness ("Blend Sharpness", Range(1, 16)) = 4.0
        _BiomeBlendSharpness ("Biome Blend Sharpness", Range(1, 16)) = 4.0

        // Ore colors are set globally via Shader.SetGlobalVectorArray("_OreColors")
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // ---- Forward Lit Pass ----
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP keywords for lighting and shadows
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;     // texture group blend weights in RGBA
                float4 uv2        : TEXCOORD2; // ore data: x=oreTypeId/8, y=oreBlend, z=skyExposure
                float4 uv3        : TEXCOORD3; // per-vertex biome tint (blended from all biomes)
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 biomeWeights : TEXCOORD2;
                float  fogFactor    : TEXCOORD3;
                float4 shadowCoord  : TEXCOORD4;
                nointerpolation float2 oreData : TEXCOORD5; // ore data — flat to prevent phantom ore colors
                float4 biomeTint    : TEXCOORD6; // per-vertex biome tint color
                float4 screenUV     : TEXCOORD7; // for Forward+ cluster light loop
                float  skyExposure  : TEXCOORD8; // 0 = underground, 1 = open sky
            };

            // Textures
            TEXTURE2D(_Tex0);   SAMPLER(sampler_Tex0);
            TEXTURE2D(_Tex1);   SAMPLER(sampler_Tex1);
            TEXTURE2D(_Tex2);   SAMPLER(sampler_Tex2);
            TEXTURE2D(_Tex3);   SAMPLER(sampler_Tex3);
            TEXTURE2D(_Normal0); SAMPLER(sampler_Normal0);
            TEXTURE2D(_Normal1); SAMPLER(sampler_Normal1);
            TEXTURE2D(_Normal2); SAMPLER(sampler_Normal2);
            TEXTURE2D(_Normal3); SAMPLER(sampler_Normal3);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color0;
                float4 _Color1;
                float4 _Color2;
                float4 _Color3;
                float  _TexScale;
                float  _BlendSharpness;
                float  _BiomeBlendSharpness;
            CBUFFER_END

            // Global ore color array — set via Shader.SetGlobalVectorArray from C#.
            // Index 0 is unused (air). Indices 1-13 are ores/terrain, 14-21 are per-biome grass.
            CBUFFER_START(OreColors)
                float4 _OreColors[32];
            CBUFFER_END

            // ---- Procedural noise helpers for ore textures ----
            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            // Value noise 3D
            float vnoise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float n000 = hash31(i);
                float n100 = hash31(i + float3(1, 0, 0));
                float n010 = hash31(i + float3(0, 1, 0));
                float n110 = hash31(i + float3(1, 1, 0));
                float n001 = hash31(i + float3(0, 0, 1));
                float n101 = hash31(i + float3(1, 0, 1));
                float n011 = hash31(i + float3(0, 1, 1));
                float n111 = hash31(i + float3(1, 1, 1));

                float n00 = lerp(n000, n100, f.x);
                float n10 = lerp(n010, n110, f.x);
                float n01 = lerp(n001, n101, f.x);
                float n11 = lerp(n011, n111, f.x);

                float n0 = lerp(n00, n10, f.y);
                float n1 = lerp(n01, n11, f.y);

                return lerp(n0, n1, f.z);
            }

            // FBM (fractal Brownian motion) 3 octaves
            float fbm3(float3 p)
            {
                float v = 0.0;
                v += 0.5    * vnoise3(p);
                v += 0.25   * vnoise3(p * 2.0);
                v += 0.125  * vnoise3(p * 4.0);
                return v / 0.875;
            }

            // Compute procedural ore texture pattern. Returns a multiplier (0-1 range) to modulate the ore color.
            float3 GetOreTexture(int oreTypeId, float3 posWS, float3 oreColorBase)
            {
                float3 p = posWS;

                if (oreTypeId == 1) // Iron — rusty streaks with pitting
                {
                    float veins = vnoise3(p * 3.0 + float3(0, p.y * 2.0, 0));
                    float rust = fbm3(p * 5.0 + 17.0);
                    float pitting = smoothstep(0.6, 0.75, vnoise3(p * 20.0 + 30.0));
                    float micro = vnoise3(p * 25.0 + 45.0);
                    float pattern = lerp(0.88, 1.06, veins) * lerp(0.90, 1.06, rust);
                    float3 tint = lerp(float3(0.94, 0.92, 0.90), float3(1.04, 1.0, 0.95), rust);
                    float3 col = oreColorBase * pattern * tint;
                    col *= lerp(0.90, 1.0, pitting * 0.4); // surface pitting darkens
                    col *= lerp(0.97, 1.03, micro);
                    return col;
                }
                else if (oreTypeId == 2) // Copper — patchy oxidation with verdigris detail
                {
                    float base = fbm3(p * 4.0 + 42.0);
                    float oxide = smoothstep(0.45, 0.65, vnoise3(p * 6.0 + 99.0));
                    float fineOxide = vnoise3(p * 16.0 + 110.0);
                    float micro = vnoise3(p * 30.0 + 120.0);
                    float3 copperClean = oreColorBase * lerp(0.90, 1.07, base);
                    float3 copperOxide = lerp(oreColorBase, float3(0.35, 0.6, 0.4), 0.35) * lerp(0.90, 1.07, base);
                    float3 col = lerp(copperClean, copperOxide, oxide * 0.35);
                    col *= lerp(0.95, 1.05, fineOxide); // fine oxidation detail
                    col *= lerp(0.97, 1.03, micro);
                    return col;
                }
                else if (oreTypeId == 3) // Coal — sedimentary layers with glassy seams
                {
                    float layers = sin(p.y * 12.0 + vnoise3(p * 3.0) * 4.0) * 0.5 + 0.5;
                    float grain = fbm3(p * 8.0 + 7.0);
                    float sheen = smoothstep(0.75, 0.9, vnoise3(p * 15.0 + 55.0));
                    float fracture = smoothstep(0.47, 0.53, vnoise3(p * 12.0 + 65.0));
                    float micro = vnoise3(p * 22.0 + 75.0);
                    float pattern = lerp(0.86, 1.06, layers * 0.6 + grain * 0.3 + fracture * 0.1);
                    float3 col = oreColorBase * pattern + float3(0.04, 0.04, 0.05) * sheen;
                    col *= lerp(0.97, 1.03, micro);
                    return col;
                }
                else if (oreTypeId == 4) // Gold — luster with sparkle and surface texture
                {
                    float base = fbm3(p * 3.0 + 123.0);
                    float sparkle = smoothstep(0.86, 0.92, vnoise3(p * 20.0 + 77.0));
                    float luster = vnoise3(p * 6.0 + 33.0);
                    float hammered = vnoise3(p * 14.0 + 140.0); // hammered surface detail
                    float micro = vnoise3(p * 28.0 + 155.0);
                    float3 col = oreColorBase * lerp(0.90, 1.08, base);
                    col += float3(1.0, 0.95, 0.5) * sparkle * 0.28;
                    col *= lerp(0.92, 1.07, luster);
                    col *= lerp(0.96, 1.04, hammered); // subtle hammered texture
                    col *= lerp(0.98, 1.02, micro);
                    return col;
                }
                else if (oreTypeId == 5) // Titanium — brushed metallic with anisotropic grain
                {
                    float grain = vnoise3(p * 8.0 + float3(p.x * 5.0, 0, 0) + 200.0);
                    float subtle = fbm3(p * 4.0 + 150.0);
                    float aniso = vnoise3(float3(p.x * 12.0, p.y * 3.0, p.z * 3.0) + 220.0); // directional brushing
                    float micro = vnoise3(p * 20.0 + 240.0);
                    float3 col = oreColorBase * lerp(0.91, 1.08, grain);
                    float sheen = smoothstep(0.65, 0.85, vnoise3(p * 12.0 + 300.0));
                    col += float3(0.05, 0.06, 0.1) * sheen * 0.22;
                    col *= lerp(0.94, 1.06, subtle);
                    col *= lerp(0.96, 1.04, aniso); // anisotropic brushed look
                    col *= lerp(0.98, 1.02, micro);
                    return col;
                }
                else if (oreTypeId == 6) // Diamond — crystalline facets with prism sparkles
                {
                    float facet = abs(vnoise3(p * 10.0 + 500.0) - 0.5) * 2.0;
                    float sparkle = smoothstep(0.88, 0.95, vnoise3(p * 25.0 + 888.0));
                    float prism = vnoise3(p * 7.0 + 600.0);
                    float subFacet = abs(vnoise3(p * 18.0 + 520.0) - 0.5) * 2.0; // smaller facet detail
                    float micro = vnoise3(p * 35.0 + 540.0);
                    float3 col = oreColorBase * lerp(0.88, 1.12, facet * 0.7 + subFacet * 0.3);
                    float3 rainbow = float3(
                        sin(prism * 6.28) * 0.5 + 0.5,
                        sin(prism * 6.28 + 2.09) * 0.5 + 0.5,
                        sin(prism * 6.28 + 4.18) * 0.5 + 0.5
                    );
                    col += rainbow * sparkle * 0.18;
                    col += float3(1, 1, 1) * sparkle * 0.14;
                    col *= lerp(0.97, 1.03, micro);
                    return col;
                }
                else if (oreTypeId == 7) // Sand — fine grain with ripples
                {
                    float grain = vnoise3(p * 15.0 + 700.0);
                    float coarse = vnoise3(p * 5.0 + 750.0);
                    float speck = smoothstep(0.78, 0.88, vnoise3(p * 30.0 + 770.0));
                    float ripple = sin(p.x * 8.0 + p.z * 6.0 + vnoise3(p * 2.0 + 720.0) * 5.0) * 0.5 + 0.5;
                    float micro = vnoise3(p * 40.0 + 790.0);
                    float3 col = oreColorBase * lerp(0.93, 1.05, grain * 0.5 + coarse * 0.3 + ripple * 0.2);
                    col = lerp(col, col * float3(1.04, 1.02, 0.97), speck);
                    col *= lerp(0.97, 1.03, micro); // micro grain detail
                    return col;
                }
                else if (oreTypeId == 8) // Dirt — earthy lumpy texture with roots and crevices
                {
                    float lumps = fbm3(p * 4.0 + 800.0);
                    float detail = vnoise3(p * 10.0 + 850.0);
                    float pebble = smoothstep(0.72, 0.82, vnoise3(p * 18.0 + 870.0));
                    float roots = smoothstep(0.46, 0.54, vnoise3(float3(p.x * 6.0, p.y * 2.0, p.z * 6.0) + 890.0));
                    float micro = vnoise3(p * 25.0 + 830.0);
                    float3 col = oreColorBase * lerp(0.88, 1.08, lumps);
                    col = lerp(col, col * float3(0.92, 0.94, 0.96), pebble * 0.25);
                    col *= lerp(0.85, 1.0, roots); // dark root/crevice lines
                    col *= lerp(0.95, 1.05, detail);
                    col *= lerp(0.97, 1.03, micro); // fine grain
                    return col;
                }
                else if (oreTypeId == 10) // Rock — rough stony with cracks and lichen
                {
                    float rough = fbm3(p * 3.0 + 900.0);
                    float crack = smoothstep(0.48, 0.52, vnoise3(p * 8.0 + 950.0));
                    float detail = vnoise3(p * 12.0 + 980.0);
                    float lichen = smoothstep(0.6, 0.75, vnoise3(p * 4.5 + 930.0)) * smoothstep(0.5, 0.7, vnoise3(p * 2.0 + 960.0));
                    float micro = vnoise3(p * 20.0 + 990.0);
                    float3 col = oreColorBase * lerp(0.88, 1.08, rough);
                    col *= lerp(0.80, 1.0, crack);
                    col = lerp(col, col * float3(0.85, 1.05, 0.82), lichen * 0.2); // subtle lichen tint
                    col *= lerp(0.95, 1.05, detail);
                    col *= lerp(0.97, 1.03, micro); // surface micro-texture
                    return col;
                }
                else if (oreTypeId == 9 || (oreTypeId >= 14 && oreTypeId <= 21)) // Grass — detailed clumpy variation with blade-level detail
                {
                    float patches = fbm3(p * 1.5 + 1100.0);
                    float blades = vnoise3(float3(p.x * 3.0, p.y * 12.0, p.z * 3.0) + 1150.0);
                    float clumps = vnoise3(p * 2.5 + 1200.0);
                    float dry = smoothstep(0.68, 0.82, vnoise3(p * 1.8 + 1250.0));
                    // Extra detail layers for LOD0
                    float fineBlades = vnoise3(float3(p.x * 8.0, p.y * 20.0, p.z * 8.0) + 1300.0);
                    float microVar = vnoise3(p * 12.0 + 1350.0);
                    float edgeWear = smoothstep(0.55, 0.7, vnoise3(p * 3.5 + 1400.0));

                    float brightness = lerp(0.88, 1.10, patches * 0.4 + blades * 0.3 + fineBlades * 0.3);
                    float3 hueShift = lerp(float3(0.95, 1.05, 0.94), float3(1.05, 0.98, 0.90), clumps);
                    float3 dryTint = lerp(float3(1, 1, 1), float3(1.08, 1.05, 0.82), dry * 0.3);
                    float3 edgeTint = lerp(float3(1, 1, 1), float3(0.95, 1.02, 0.88), edgeWear * 0.15);

                    return oreColorBase * brightness * hueShift * dryTint * edgeTint * lerp(0.97, 1.03, microVar);
                }

                // Default — return base color unmodified
                return oreColorBase;
            }

            // Sample a texture triplanarly given world position and absolute normal weights
            float4 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 posWS, float3 blendWeights)
            {
                float2 uvX = posWS.zy * _TexScale;  // YZ plane
                float2 uvY = posWS.xz * _TexScale;  // XZ plane
                float2 uvZ = posWS.xy * _TexScale;  // XY plane

                float4 colX = SAMPLE_TEXTURE2D(tex, samp, uvX);
                float4 colY = SAMPLE_TEXTURE2D(tex, samp, uvY);
                float4 colZ = SAMPLE_TEXTURE2D(tex, samp, uvZ);

                return colX * blendWeights.x + colY * blendWeights.y + colZ * blendWeights.z;
            }

            // Sample a normal map triplanarly, returning world-space normal perturbation
            float3 SampleTriplanarNormal(TEXTURE2D_PARAM(tex, samp), float3 posWS, float3 blendWeights, float3 normalWS)
            {
                float2 uvX = posWS.zy * _TexScale;
                float2 uvY = posWS.xz * _TexScale;
                float2 uvZ = posWS.xy * _TexScale;

                // Unpack normal maps (stored as DXT5nm or BC5 in Unity)
                float3 nX = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uvX));
                float3 nY = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uvY));
                float3 nZ = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, uvZ));

                // Swizzle tangent-space normals to align with world projection axes
                // and blend using the surface normal sign for correct orientation
                float3 axisSign = sign(normalWS);
                nX = float3(nX.xy * float2(axisSign.x, 1), abs(nX.z));
                nY = float3(nY.xy * float2(axisSign.y, 1), abs(nY.z));
                nZ = float3(nZ.xy * float2(axisSign.z, 1), abs(nZ.z));

                // Swizzle to world axes: X-projection → zy, Y-projection → xz, Z-projection → xy
                float3 worldNX = float3(0, nX.y, nX.x);    // tangent YZ
                float3 worldNY = float3(nY.x, 0, nY.y);    // tangent XZ
                float3 worldNZ = float3(nZ.x, nZ.y, 0);    // tangent XY

                // Blend and add geometry normal
                float3 result = worldNX * blendWeights.x + worldNY * blendWeights.y + worldNZ * blendWeights.z + normalWS;
                return normalize(result);
            }

            // Compute procedural normal perturbation from noise for ore/terrain types
            float3 GetProceduralNormal(int oreTypeId, float3 posWS, float3 normalWS)
            {
                float eps = 0.15; // offset for finite-difference gradient
                float3 bump = float3(0, 0, 0);

                if (oreTypeId == 1) // Iron — rusty ridges
                {
                    float c  = fbm3(posWS * 5.0 + 17.0);
                    float cx = fbm3((posWS + float3(eps, 0, 0)) * 5.0 + 17.0);
                    float cy = fbm3((posWS + float3(0, eps, 0)) * 5.0 + 17.0);
                    float cz = fbm3((posWS + float3(0, 0, eps)) * 5.0 + 17.0);
                    bump = float3(c - cx, c - cy, c - cz) / eps * 0.15;
                }
                else if (oreTypeId == 2) // Copper — oxidation bumps
                {
                    float c  = vnoise3(posWS * 6.0 + 99.0);
                    float cx = vnoise3((posWS + float3(eps, 0, 0)) * 6.0 + 99.0);
                    float cy = vnoise3((posWS + float3(0, eps, 0)) * 6.0 + 99.0);
                    float cz = vnoise3((posWS + float3(0, 0, eps)) * 6.0 + 99.0);
                    bump = float3(c - cx, c - cy, c - cz) / eps * 0.12;
                }
                else if (oreTypeId == 3) // Coal — layered sediment
                {
                    float3 p = posWS;
                    float c  = sin(p.y * 12.0 + vnoise3(p * 3.0) * 4.0);
                    float cy = sin((p.y + eps) * 12.0 + vnoise3((p + float3(0, eps, 0)) * 3.0) * 4.0);
                    bump = float3(0, (c - cy) / eps * 0.18, 0);
                }
                else if (oreTypeId == 4) // Gold — luster waves
                {
                    float c  = vnoise3(posWS * 6.0 + 33.0);
                    float cx = vnoise3((posWS + float3(eps, 0, 0)) * 6.0 + 33.0);
                    float cy = vnoise3((posWS + float3(0, eps, 0)) * 6.0 + 33.0);
                    float cz = vnoise3((posWS + float3(0, 0, eps)) * 6.0 + 33.0);
                    bump = float3(c - cx, c - cy, c - cz) / eps * 0.10;
                }
                else if (oreTypeId == 5) // Titanium — brushed grain
                {
                    float c  = vnoise3(posWS * 8.0 + float3(posWS.x * 5.0, 0, 0) + 200.0);
                    float cx = vnoise3((posWS + float3(eps, 0, 0)) * 8.0 + float3((posWS.x + eps) * 5.0, 0, 0) + 200.0);
                    float cy = vnoise3((posWS + float3(0, eps, 0)) * 8.0 + float3(posWS.x * 5.0, 0, 0) + 200.0);
                    float cz = vnoise3((posWS + float3(0, 0, eps)) * 8.0 + float3(posWS.x * 5.0, 0, 0) + 200.0);
                    bump = float3(c - cx, c - cy, c - cz) / eps * 0.14;
                }
                else if (oreTypeId == 6) // Diamond — crystalline facets
                {
                    float c  = abs(vnoise3(posWS * 10.0 + 500.0) - 0.5);
                    float cx = abs(vnoise3((posWS + float3(eps, 0, 0)) * 10.0 + 500.0) - 0.5);
                    float cy = abs(vnoise3((posWS + float3(0, eps, 0)) * 10.0 + 500.0) - 0.5);
                    float cz = abs(vnoise3((posWS + float3(0, 0, eps)) * 10.0 + 500.0) - 0.5);
                    bump = float3(c - cx, c - cy, c - cz) / eps * 0.20;
                }
                else if (oreTypeId == 7) // Sand — fine ripples
                {
                    float c  = vnoise3(posWS * 15.0 + 700.0);
                    float cx = vnoise3((posWS + float3(eps, 0, 0)) * 15.0 + 700.0);
                    float cz = vnoise3((posWS + float3(0, 0, eps)) * 15.0 + 700.0);
                    bump = float3(c - cx, 0, c - cz) / eps * 0.08;
                }
                else if (oreTypeId == 8) // Dirt — lumpy bumps
                {
                    float c  = fbm3(posWS * 4.0 + 800.0);
                    float cx = fbm3((posWS + float3(eps, 0, 0)) * 4.0 + 800.0);
                    float cy = fbm3((posWS + float3(0, eps, 0)) * 4.0 + 800.0);
                    float cz = fbm3((posWS + float3(0, 0, eps)) * 4.0 + 800.0);
                    bump = float3(c - cx, c - cy, c - cz) / eps * 0.18;
                }
                else if (oreTypeId == 9 || (oreTypeId >= 14 && oreTypeId <= 21)) // Grass — clumpy undulation
                {
                    float c  = fbm3(posWS * 1.5 + 1100.0) + vnoise3(float3(posWS.x * 3.0, posWS.y * 12.0, posWS.z * 3.0) + 1150.0) * 0.5;
                    float cx = fbm3((posWS + float3(eps, 0, 0)) * 1.5 + 1100.0) + vnoise3(float3((posWS.x + eps) * 3.0, posWS.y * 12.0, posWS.z * 3.0) + 1150.0) * 0.5;
                    float cy = fbm3((posWS + float3(0, eps, 0)) * 1.5 + 1100.0) + vnoise3(float3(posWS.x * 3.0, (posWS.y + eps) * 12.0, posWS.z * 3.0) + 1150.0) * 0.5;
                    float cz = fbm3((posWS + float3(0, 0, eps)) * 1.5 + 1100.0) + vnoise3(float3(posWS.x * 3.0, posWS.y * 12.0, (posWS.z + eps) * 3.0) + 1150.0) * 0.5;
                    bump = float3(c - cx, c - cy, c - cz) / eps * 0.12;
                }
                else if (oreTypeId == 10) // Rock — craggy surface
                {
                    float c  = fbm3(posWS * 3.0 + 900.0) + vnoise3(posWS * 8.0 + 950.0) * 0.4;
                    float cx = fbm3((posWS + float3(eps, 0, 0)) * 3.0 + 900.0) + vnoise3((posWS + float3(eps, 0, 0)) * 8.0 + 950.0) * 0.4;
                    float cy = fbm3((posWS + float3(0, eps, 0)) * 3.0 + 900.0) + vnoise3((posWS + float3(0, eps, 0)) * 8.0 + 950.0) * 0.4;
                    float cz = fbm3((posWS + float3(0, 0, eps)) * 3.0 + 900.0) + vnoise3((posWS + float3(0, 0, eps)) * 8.0 + 950.0) * 0.4;
                    bump = float3(c - cx, c - cy, c - cz) / eps * 0.22;
                }

                return normalize(normalWS + bump);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS   = posInputs.positionCS;
                output.positionWS   = posInputs.positionWS;
                output.normalWS     = normInputs.normalWS;
                output.biomeWeights = input.color;
                output.fogFactor    = ComputeFogFactor(posInputs.positionCS.z);
                output.shadowCoord  = float4(0, 0, 0, 0); // computed per-fragment below
                output.oreData      = input.uv2.xy;
                output.biomeTint    = input.uv3;
                output.screenUV     = ComputeScreenPos(posInputs.positionCS);
                output.skyExposure  = input.uv2.z;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Triplanar blend weights from world-space normal
                float3 absNormal = abs(input.normalWS);
                float3 blendWeights = pow(absNormal, _BlendSharpness);
                blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z + 0.0001);

                // Texture group weights from vertex color (R=group0, G=group1, B=group2, A=group3)
                float4 bw = input.biomeWeights;
                // Normalize texture group weights in case they don't sum to 1
                float bwSum = bw.x + bw.y + bw.z + bw.w;
                bw = bwSum > 0.001 ? bw / bwSum : float4(1, 0, 0, 0);

                // Sample each texture group triplanarly
                float4 tex0 = SampleTriplanar(TEXTURE2D_ARGS(_Tex0, sampler_Tex0), input.positionWS, blendWeights);
                float4 tex1 = SampleTriplanar(TEXTURE2D_ARGS(_Tex1, sampler_Tex1), input.positionWS, blendWeights);
                float4 tex2 = SampleTriplanar(TEXTURE2D_ARGS(_Tex2, sampler_Tex2), input.positionWS, blendWeights);
                float4 tex3 = SampleTriplanar(TEXTURE2D_ARGS(_Tex3, sampler_Tex3), input.positionWS, blendWeights);

                // Blend textures by group weights, then apply per-vertex biome tint
                float4 albedo = (tex0 * bw.x + tex1 * bw.y + tex2 * bw.z + tex3 * bw.w) * input.biomeTint;

                // Ore overlay — decode ore type from UV2.
                // oreData.y is 1.0 when ore is present, 0.0 otherwise.
                float oreBlend  = step(0.01, input.oreData.y);
                float oreTypeF  = round(input.oreData.x * 32.0);  // recover oreTypeId
                int   oreTypeId = clamp((int)oreTypeF, 0, 31);

                // Look up ore color from global array
                float4 oreColor = _OreColors[oreTypeId];

                // Terrain types (sand=7, dirt=8, grass=9, rock=10, biome grass=14-21)
                // fully replace the biome albedo. Actual ores (1-6, 11-13) blend at 80%.
                float isTerrain = (oreTypeId >= 7 && oreTypeId <= 10) || (oreTypeId >= 14 && oreTypeId <= 21) ? 1.0 : 0.0;

                // Apply procedural texture to ore color
                float3 texturedOre = GetOreTexture(oreTypeId, input.positionWS, oreColor.rgb);

                // Terrain types: full replacement. Mining ores: 80% blend over biome texture.
                albedo.rgb = lerp(
                    lerp(albedo.rgb, texturedOre, oreBlend * 0.8),  // ore blend path
                    texturedOre,                                     // terrain full replace
                    isTerrain * oreBlend
                );

                // ---- Normal mapping ----
                float3 geomNormal = normalize(input.normalWS);

                // Sample biome normal maps triplanarly and blend by texture group weights
                float3 norm0 = SampleTriplanarNormal(TEXTURE2D_ARGS(_Normal0, sampler_Normal0), input.positionWS, blendWeights, geomNormal);
                float3 norm1 = SampleTriplanarNormal(TEXTURE2D_ARGS(_Normal1, sampler_Normal1), input.positionWS, blendWeights, geomNormal);
                float3 norm2 = SampleTriplanarNormal(TEXTURE2D_ARGS(_Normal2, sampler_Normal2), input.positionWS, blendWeights, geomNormal);
                float3 norm3 = SampleTriplanarNormal(TEXTURE2D_ARGS(_Normal3, sampler_Normal3), input.positionWS, blendWeights, geomNormal);
                float3 biomeNormal = normalize(norm0 * bw.x + norm1 * bw.y + norm2 * bw.z + norm3 * bw.w);

                // Apply procedural normal perturbation for ore/terrain types
                float3 normalWS = biomeNormal;
                if (oreBlend > 0.01)
                {
                    float3 procNormal = GetProceduralNormal(oreTypeId, input.positionWS, biomeNormal);
                    // Blend procedural normal: full strength for ores, softer for terrain types
                    float procStrength = (oreTypeId >= 7) ? 0.6 : 0.8;
                    normalWS = normalize(lerp(biomeNormal, procNormal, oreBlend * procStrength));
                }

                // Lighting — manual shadow sampling to bypass screen-space shadow path.
                // In deferred rendering, screen-space shadows are generated before this
                // forward-only shader writes depth, so we sample the cascade shadow map directly.

                // Main light
                Light mainLight = GetMainLight();

                // Direct cascade shadow map sampling (works regardless of rendering mode)
                half shadow = 1.0;
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                {
                    half cascadeIndex = ComputeCascadeIndex(input.positionWS);
                    float4 shadowCoord = mul(_MainLightWorldToShadow[cascadeIndex], float4(input.positionWS, 1.0));
                    ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();
                    half4 shadowParams = GetMainLightShadowParams();
                    shadow = SampleShadowmap(TEXTURE2D_ARGS(_MainLightShadowmapTexture, sampler_LinearClampCompare),
                        shadowCoord, shadowSamplingData, shadowParams, false);
                }
                #endif

                // Diffuse lighting with shadow
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = mainLight.color * NdotL * shadow;

                // Additional lights (Forward+ cluster loop compatible)
                #ifdef _ADDITIONAL_LIGHTS
                {
                    // Build minimal InputData for LIGHT_LOOP_BEGIN (Forward+ needs screen UV)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                        half addNdotL = saturate(dot(normalWS, addLight.direction));
                        lighting += addLight.color * addNdotL * addLight.shadowAttenuation * addLight.distanceAttenuation;
                    LIGHT_LOOP_END
                }
                #endif

                // Ambient — modulated by per-vertex sky exposure so caves receive
                // no ambient light while surface terrain stays naturally lit.
                half3 ambient = SampleSH(normalWS) * input.skyExposure;

                // Combine
                half4 color;
                color.rgb = albedo.rgb * (lighting + ambient);
                color.a = 1.0;
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }

        // ---- Shadow Caster Pass ----
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off      // Marching cubes terrain needs both faces to cast shadows

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 GetShadowPositionHClip(ShadowAttributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ---- Depth Only Pass ----
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
