// Voidborne/GrassGeometry
// Geometry-shader grass with curved multi-segment blades.
// Based on IronWarrior/UnityGrassGeometryShader, adapted for URP + compute-culled instancing.
//
// Features:
//   Per-biome colors and wind strength
//   Soft alpha blend at blade base (terrain transition)
//   LOD variants via multi_compile (5/3/1 segments)

Shader "Voidborne/GrassGeometry"
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

        // ── Forward Lit Pass ─────────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma target 4.6
            #pragma vertex   vert
            #pragma geometry geo
            #pragma fragment frag

            #pragma instancing_options procedural:SetupProcedural
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _GRASS_LOD1 _GRASS_LOD2
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── LOD segment counts ───────────────────────────────────────────
            #if defined(_GRASS_LOD2)
                #define BLADE_SEGMENTS 1
            #elif defined(_GRASS_LOD1)
                #define BLADE_SEGMENTS 3
            #else
                #define BLADE_SEGMENTS 5
            #endif

            // ── Instance data (matches GrassRenderer.GrassInstanceData) ──────
            struct GrassInstanceData
            {
                float3 position;
                float3 normal;
                float  scale;
                float  colorVariant; // floor = biomeIndex, frac = random variation
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
            float4 _BiomeWindMult;       // x=Plains, y=Forest, z=River, w=Savanna
            float4 _BiomeWindMult2;      // x=Highlands, y=GrandHills, z=ToweringBluffs

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

            // ── Helpers ──────────────────────────────────────────────────────
            // Improved hash — less prone to visible patterns than sin-based rand
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
                float t = 1.0 - c;
                float x = axis.x, y = axis.y, z = axis.z;
                return float3x3(
                    t*x*x + c,     t*x*y - s*z,   t*x*z + s*y,
                    t*x*y + s*z,   t*y*y + c,     t*y*z - s*x,
                    t*x*z - s*y,   t*y*z + s*x,   t*z*z + c
                );
            }

            // ── Procedural instancing callback ───────────────────────────────
            void SetupProcedural()
            {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                unity_ObjectToWorld = float4x4(
                    1, 0, 0, 0,
                    0, 1, 0, 0,
                    0, 0, 1, 0,
                    0, 0, 0, 1
                );
                unity_WorldToObject = unity_ObjectToWorld;
            #endif
            }

            // ── Vertex I/O ───────────────────────────────────────────────────
            struct VertexInput
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VertexOutput
            {
                float4 vertex         : SV_POSITION;
                float3 instancePos    : TEXCOORD0;
                float3 instanceNormal : TEXCOORD1;
                float2 instanceData   : TEXCOORD2; // x = scale, y = colorVariant (packed biome+random)
            };

            struct GeomOutput
            {
                float4 pos          : SV_POSITION;
                float3 worldNormal  : TEXCOORD0;
                float2 uv           : TEXCOORD1;
                float  colorVariant : TEXCOORD2;
                float  fogFactor    : TEXCOORD3;
                float3 worldPos     : TEXCOORD4;
            };

            // ── Vertex shader ────────────────────────────────────────────────
            VertexOutput vert(VertexInput v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                VertexOutput o;
                o.vertex = float4(0, 0, 0, 1);

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                GrassInstanceData d = _GrassBuffer[unity_InstanceID];
                o.instancePos    = d.position;
                o.instanceNormal = d.normal;
                o.instanceData   = float2(d.scale, d.colorVariant);
            #else
                o.instancePos    = float3(0, 0, 0);
                o.instanceNormal = float3(0, 1, 0);
                o.instanceData   = float2(1, 0.5);
            #endif
                return o;
            }

            // ── Geometry shader helper ───────────────────────────────────────
            GeomOutput MakeVertex(float3 worldPos, float3 normal, float2 uv, float colorVar)
            {
                GeomOutput o;
                o.pos          = TransformWorldToHClip(worldPos);
                o.fogFactor    = ComputeFogFactor(o.pos.z);
                o.worldNormal  = normal;
                o.uv           = uv;
                o.colorVariant = colorVar;
                o.worldPos     = worldPos;
                return o;
            }

            // ── Geometry shader ──────────────────────────────────────────────
            [maxvertexcount(BLADE_SEGMENTS * 2 + 1)]
            void geo(point VertexOutput IN[1], inout TriangleStream<GeomOutput> triStream)
            {
                float3 pos        = IN[0].instancePos;
                float3 surfNormal = normalize(IN[0].instanceNormal);
                float  bladeScale = IN[0].instanceData.x;
                float  colorVar   = IN[0].instanceData.y;

                // Decode biome index for wind multiplier
                int biomeIdx = clamp((int)colorVar, 0, 6);
                float biomeWind = biomeIdx < 4 ? _BiomeWindMult[biomeIdx] : _BiomeWindMult2[biomeIdx - 4];

                // ── Build tangent-to-world basis from surface normal ─────────
                float3 up      = abs(surfNormal.y) < 0.999 ? float3(0, 1, 0) : float3(1, 0, 0);
                float3 tangent  = normalize(cross(up, surfNormal));
                float3 binormal = cross(surfNormal, tangent);

                float3x3 tangentToWorld = float3x3(
                    tangent.x,  binormal.x, surfNormal.x,
                    tangent.y,  binormal.y, surfNormal.y,
                    tangent.z,  binormal.z, surfNormal.z
                );

                // ── Per-blade random rotations ───────────────────────────────
                float3x3 facingRot = AngleAxis3x3(
                    rand(pos) * TWO_PI, float3(0, 0, 1));

                float3x3 bendRot = AngleAxis3x3(
                    rand(pos.zzx) * _BendRotationRandom * HALF_PI, float3(-1, 0, 0));

                // ── Wind from distortion texture (scaled by biome) ───────────
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
                // Max ~18° rotation, scaled by biome wind
                float3x3 windRot = AngleAxis3x3(saturate(windMag) * HALF_PI * 0.2, windAxis);

                // ── Combined transforms ──────────────────────────────────────
                float3x3 xformFull = mul(mul(mul(tangentToWorld, windRot), facingRot), bendRot);
                float3x3 xformBase = mul(tangentToWorld, facingRot);

                // ── Randomised blade dimensions ──────────────────────────────
                float height  = ((rand(pos.zyx) * 2.0 - 1.0) * _BladeHeightRandom + _BladeHeight) * bladeScale;
                float width   =  (rand(pos.xzy) * 2.0 - 1.0) * _BladeWidthRandom  + _BladeWidth;
                float forward =   rand(pos.yyz) * _BladeForward * bladeScale;

                float3 tangentNormal = normalize(float3(0, -1, forward));

                // ── Emit blade segments ──────────────────────────────────────
                for (int i = 0; i < BLADE_SEGMENTS; i++)
                {
                    float t   = i / (float)BLADE_SEGMENTS;
                    float segH = height * t;
                    float segW = width  * (1.0 - t);
                    float segF = pow(t, _BladeCurve) * forward;

                    float3x3 xform = i == 0 ? xformBase : xformFull;

                    float3 worldL = pos + mul(xform, float3(-segW, segF, segH));
                    float3 worldR = pos + mul(xform, float3( segW, segF, segH));
                    float3 worldN = mul(xform, tangentNormal);

                    triStream.Append(MakeVertex(worldL, worldN, float2(0, t), colorVar));
                    triStream.Append(MakeVertex(worldR, worldN, float2(1, t), colorVar));
                }

                // ── Tip vertex ───────────────────────────────────────────────
                float3 tipWorld = pos + mul(xformFull, float3(0, forward, height));
                float3 tipN     = mul(xformFull, tangentNormal);
                triStream.Append(MakeVertex(tipWorld, tipN, float2(0.5, 1), colorVar));
            }

            // ── Fragment shader ──────────────────────────────────────────────
            float4 frag(GeomOutput i, half facing : VFACE) : SV_Target
            {
                float3 normal = facing > 0 ? i.worldNormal : -i.worldNormal;

                // Decode biome index and per-blade variation
                int biomeIdx = clamp((int)i.colorVariant, 0, 6);
                float variation = frac(i.colorVariant);

                float4 topCol = GetBiomeTop(biomeIdx);
                float4 botCol = GetBiomeBot(biomeIdx);

                // URP main light + translucency
                Light mainLight = GetMainLight();
                float NdotL = saturate(saturate(dot(normal, mainLight.direction)) + _TranslucentGain);
                float3 ambient = SampleSH(normal);
                // Clamp light intensity to prevent washing out colors
                float3 lightIntensity = min(NdotL * mainLight.color + ambient, 1.4);

                // Additional lights (Forward+ compatible)
                #ifdef _ADDITIONAL_LIGHTS
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.worldPos;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.pos);

                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light addLight = GetAdditionalLight(lightIndex, inputData.positionWS);
                        half addNdotL = saturate(dot(normal, addLight.direction));
                        lightIntensity += addLight.color * addNdotL * addLight.distanceAttenuation;
                    LIGHT_LOOP_END
                }
                #endif

                // Dark base blends into terrain, lit tip shows biome color
                // Extend bottom color influence higher (bottom 40%) for better ground blend
                float gradient = smoothstep(0.0, 0.5, i.uv.y);
                float4 col = lerp(botCol, topCol, gradient);
                col.rgb *= lightIntensity;
                // Per-blade color variation
                col.rgb = lerp(col.rgb * 0.92, col.rgb * 1.08, variation);

                col.rgb = MixFog(col.rgb, i.fogFactor);
                return float4(col.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
