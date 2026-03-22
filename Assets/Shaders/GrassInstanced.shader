// Voidborne/GrassInstanced
// GPU-instanced grass WITHOUT geometry shader — uses vertex shader + pre-built blade meshes.
// Drop-in replacement for GrassGeometry: same visual output, better GPU utilisation.
//
// Each blade is a pre-built triangle strip mesh (11/7/3 verts per LOD).
// The vertex shader reads per-blade data from a StructuredBuffer and transforms
// each vertex using the same curvature, wind, and rotation as the old geometry shader.
//
// Why this is faster:
//   Geometry shaders have variable output, poor SIMD occupancy, and stall the pipeline.
//   Vertex shaders have fixed output and run fully parallel across all wavefronts.
//   In practice this gives 2-4x speedup for the same visual quality.

Shader "Voidborne/GrassInstanced"
{
    Properties
    {
        [Header(Shading)]
        _TranslucentGain("Translucent Gain",  Range(0, 1)) = 0.5

        [Header(Blades)]
        _BladeWidth        ("Blade Width",              Float) = 0.035
        _BladeWidthRandom  ("Blade Width Random",       Float) = 0.015
        _BladeHeight       ("Blade Height",             Float) = 0.45
        _BladeHeightRandom ("Blade Height Random",      Float) = 0.25
        _BladeForward      ("Blade Forward Amount",     Float) = 0.15
        _BladeCurve        ("Blade Curvature Amount",   Range(1, 4)) = 2.5
        _BendRotationRandom("Bend Rotation Random",     Range(0, 1)) = 0.15

        [Header(Wind)]
        _WindDistortionMap ("Wind Distortion Map", 2D)  = "white" {}
        _WindStrength      ("Wind Strength",       Float) = 0.3
        _WindFrequency     ("Wind Scroll Speed",   Vector) = (0.05, 0.05, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry+100"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   vert
            #pragma fragment frag

            #pragma instancing_options procedural:SetupProcedural
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _GRASS_LOD1 _GRASS_LOD2
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Instance data (matches GrassRenderer.GrassInstanceData) ──────
            struct GrassInstanceData
            {
                float3 position;
                float3 normal;
                float  scale;
                float  colorVariant;
            };

            StructuredBuffer<GrassInstanceData> _GrassBuffer;

            // ── Per-material uniforms ─────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float  _TranslucentGain;
                float  _BladeWidth;
                float  _BladeWidthRandom;
                float  _BladeHeight;
                float  _BladeHeightRandom;
                float  _BladeForward;
                float  _BladeCurve;
                float  _BendRotationRandom;
                float4 _WindDistortionMap_ST;
                float  _WindStrength;
                float2 _WindFrequency;
            CBUFFER_END

            TEXTURE2D(_WindDistortionMap);
            SAMPLER(sampler_WindDistortionMap);

            // ── Per-biome data (set as globals by GrassRenderer each frame) ──
            float4 _BiomeTop0, _BiomeTop1, _BiomeTop2, _BiomeTop3;
            float4 _BiomeTop4, _BiomeTop5, _BiomeTop6;
            float4 _BiomeBot0, _BiomeBot1, _BiomeBot2, _BiomeBot3;
            float4 _BiomeBot4, _BiomeBot5, _BiomeBot6;
            float4 _BiomeWindMult;
            float4 _BiomeWindMult2;

            float4 GetBiomeTop(int idx)
            {
                if (idx == 1) return _BiomeTop1;
                if (idx == 2) return _BiomeTop2;
                if (idx == 3) return _BiomeTop3;
                if (idx == 4) return _BiomeTop4;
                if (idx == 5) return _BiomeTop5;
                if (idx == 6) return _BiomeTop6;
                return _BiomeTop0;
            }
            float4 GetBiomeBot(int idx)
            {
                if (idx == 1) return _BiomeBot1;
                if (idx == 2) return _BiomeBot2;
                if (idx == 3) return _BiomeBot3;
                if (idx == 4) return _BiomeBot4;
                if (idx == 5) return _BiomeBot5;
                if (idx == 6) return _BiomeBot6;
                return _BiomeBot0;
            }

            // ── Helpers (same as GrassGeometry) ──────────────────────────────
            float rand(float3 co)
            {
                float3 p = frac(co * float3(443.8975, 397.2973, 491.1871));
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            float3x3 AngleAxis3x3(float angle, float3 axis)
            {
                float c, s;
                sincos(angle, s, c);
                float tt = 1.0 - c;
                float x = axis.x, y = axis.y, z = axis.z;
                return float3x3(
                    tt*x*x + c,     tt*x*y - s*z,   tt*x*z + s*y,
                    tt*x*y + s*z,   tt*y*y + c,      tt*y*z - s*x,
                    tt*x*z - s*y,   tt*y*z + s*x,    tt*z*z + c
                );
            }

            // ── Procedural instancing callback ───────────────────────────────
            void SetupProcedural()
            {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                unity_ObjectToWorld = float4x4(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1);
                unity_WorldToObject = unity_ObjectToWorld;
            #endif
            }

            // ── I/O ──────────────────────────────────────────────────────────
            struct Attributes
            {
                float3 positionOS : POSITION;  // x = side factor (-1/+1/0), y = height t (0-1)
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 worldNormal  : TEXCOORD0;
                float2 uv           : TEXCOORD1;
                float  colorVariant : TEXCOORD2;
                float  fogFactor    : TEXCOORD3;
                float3 worldPos     : TEXCOORD4;
            };

            // ── Vertex shader — replaces the geometry shader entirely ────────
            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT = (Varyings)0;

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                GrassInstanceData blade = _GrassBuffer[unity_InstanceID];
            #else
                GrassInstanceData blade;
                blade.position = float3(0,0,0);
                blade.normal = float3(0,1,0);
                blade.scale = 1;
                blade.colorVariant = 0.5;
            #endif

                float3 pos        = blade.position;
                float3 surfNormal = normalize(blade.normal);
                float  bladeScale = blade.scale;
                float  colorVar   = blade.colorVariant;

                // Parametric coords from mesh vertex
                float t    = IN.positionOS.y;  // 0 at base, 1 at tip
                float side = IN.positionOS.x;  // -1 or +1 (0 at tip)

                // Decode biome index for wind multiplier
                int biomeIdx = clamp((int)colorVar, 0, 6);
                float biomeWind;
                if      (biomeIdx == 0) biomeWind = _BiomeWindMult.x;
                else if (biomeIdx == 1) biomeWind = _BiomeWindMult.y;
                else if (biomeIdx == 2) biomeWind = _BiomeWindMult.z;
                else if (biomeIdx == 3) biomeWind = _BiomeWindMult.w;
                else if (biomeIdx == 4) biomeWind = _BiomeWindMult2.x;
                else if (biomeIdx == 5) biomeWind = _BiomeWindMult2.y;
                else                    biomeWind = _BiomeWindMult2.z;

                // ── Tangent-to-world basis from surface normal ────────────────
                float3 up      = abs(surfNormal.y) < 0.999 ? float3(0, 1, 0) : float3(1, 0, 0);
                float3 tangent  = normalize(cross(up, surfNormal));
                float3 binormal = cross(surfNormal, tangent);

                float3x3 tangentToWorld = float3x3(
                    tangent.x,  binormal.x, surfNormal.x,
                    tangent.y,  binormal.y, surfNormal.y,
                    tangent.z,  binormal.z, surfNormal.z
                );

                // ── Per-blade random rotations (same seed as geometry shader) ─
                float3x3 facingRot = AngleAxis3x3(
                    rand(pos) * TWO_PI, float3(0, 0, 1));

                float3x3 bendRot = AngleAxis3x3(
                    rand(pos.zzx) * _BendRotationRandom * HALF_PI, float3(-1, 0, 0));

                // ── Wind from distortion texture ─────────────────────────────
                float2 windUV = pos.xz * _WindDistortionMap_ST.xy
                              + _WindDistortionMap_ST.zw
                              + _WindFrequency * _Time.y;
                float2 windSample = (SAMPLE_TEXTURE2D_LOD(
                    _WindDistortionMap, sampler_WindDistortionMap, windUV, 0).xy
                    * 2.0 - 1.0) * _WindStrength * biomeWind;
                float windMag = length(windSample);
                float3 windAxis = windMag > 0.001
                    ? float3(windSample.x / windMag, windSample.y / windMag, 0)
                    : float3(1, 0, 0);
                float3x3 windRot = AngleAxis3x3(saturate(windMag) * HALF_PI * 0.2, windAxis);

                // ── Combined transforms ──────────────────────────────────────
                float3x3 xformFull = mul(mul(mul(tangentToWorld, windRot), facingRot), bendRot);
                float3x3 xformBase = mul(tangentToWorld, facingRot);

                // ── Randomised blade dimensions ──────────────────────────────
                float height  = ((rand(pos.zyx) * 2.0 - 1.0) * _BladeHeightRandom + _BladeHeight) * bladeScale;
                float width   =  (rand(pos.xzy) * 2.0 - 1.0) * _BladeWidthRandom  + _BladeWidth;
                float forward =   rand(pos.yyz) * _BladeForward * bladeScale;

                // ── Transform parametric vertex to world space ───────────────
                float segW = width * (1.0 - t) * side;
                float segH = height * t;
                float segF = pow(t, _BladeCurve) * forward;

                // Base segment uses xformBase (no wind/bend), rest uses xformFull
                float3x3 xform = t < 0.001 ? xformBase : xformFull;
                float3 localPos = float3(segW, segF, segH);
                float3 worldPos = pos + mul(xform, localPos);

                float3 tangentNormal = normalize(float3(0, -1, forward));
                float3 worldNormal = mul(xform, tangentNormal);

                OUT.positionCS  = TransformWorldToHClip(worldPos);
                OUT.fogFactor   = ComputeFogFactor(OUT.positionCS.z);
                OUT.worldNormal = worldNormal;
                OUT.uv          = IN.uv;
                OUT.colorVariant = colorVar;
                OUT.worldPos    = worldPos;
                return OUT;
            }

            // ── Fragment shader (identical to GrassGeometry) ─────────────────
            float4 frag(Varyings i, half facing : VFACE) : SV_Target
            {
                float3 normal = facing > 0 ? i.worldNormal : -i.worldNormal;

                int biomeIdx = clamp((int)i.colorVariant, 0, 6);
                float variation = frac(i.colorVariant);

                float4 topCol = GetBiomeTop(biomeIdx);
                float4 botCol = GetBiomeBot(biomeIdx);

                Light mainLight = GetMainLight();
                float NdotL = saturate(saturate(dot(normal, mainLight.direction)) + _TranslucentGain);
                float3 ambient = SampleSH(normal);
                float3 lightIntensity = min(NdotL * mainLight.color + ambient, 1.4);

                #ifdef _ADDITIONAL_LIGHTS
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.worldPos;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);

                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light addLight = GetAdditionalLight(lightIndex, inputData.positionWS);
                        half addNdotL = saturate(dot(normal, addLight.direction));
                        lightIntensity += addLight.color * addNdotL * addLight.distanceAttenuation;
                    LIGHT_LOOP_END
                }
                #endif

                float gradient = smoothstep(0.0, 0.5, i.uv.y);
                float4 col = lerp(botCol, topCol, gradient);
                col.rgb *= lightIntensity;
                col.rgb = lerp(col.rgb * 0.92, col.rgb * 1.08, variation);
                col.rgb = MixFog(col.rgb, i.fogFactor);
                return float4(col.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
