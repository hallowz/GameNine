// Voidborne/GrassBlade
// W2.4 — GPU-instanced grass blade shader.
// Reads per-blade data from a StructuredBuffer populated by GrassRenderer.
// Uses Unity's procedural instancing path (#pragma instancing_options procedural:SetupProcedural)
// so each DrawMeshInstancedIndirect call can render thousands of blades in one draw call.
//
// Features:
//  · Position + scale from StructuredBuffer (no Transform overhead)
//  · Wind: two-layer sine offset at blade tip
//  · Colour gradient: dark root → bright tip, blended by colorVariant
//  · Alpha cutout at tip
//  · Receives shadows / does NOT cast shadows (performance)

Shader "Voidborne/GrassBlade"
{
    Properties
    {
        _GrassColorA   ("Grass Color A (dark)",  Color) = (0.08, 0.25, 0.04, 1)
        _GrassColorB   ("Grass Color B (light)", Color) = (0.35, 0.65, 0.12, 1)
        _AlphaCutoff   ("Alpha Cutoff",          Range(0, 1)) = 0.25
        _BladeHeight   ("Blade Height (m)",      Float) = 0.35

        _WindDir       ("Wind Direction XZ",     Vector) = (1, 0, 0, 0)
        _WindSpeed     ("Wind Speed",            Float) = 1.2
        _WindFrequency ("Wind Frequency",        Float) = 0.08
        _WindAmplitude ("Wind Amplitude",        Float) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "TransparentCutout"
            "Queue"          = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── Forward Lit Pass ───────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off            // Double-sided blades

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // Enable Unity's procedural instancing path
            #pragma instancing_options procedural:SetupProcedural
            #pragma multi_compile_instancing

            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            // URP includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Per-blade instance data (must match GrassRenderer.GrassInstanceData) ──
            struct GrassInstanceData
            {
                float3 position;     // blade root (world space)
                float3 normal;       // surface normal
                float  scale;        // height multiplier
                float  colorVariant; // 0–1, lerped between _GrassColorA / B
            };

            StructuredBuffer<GrassInstanceData> _GrassBuffer;

            // ── Procedural instancing callback ──────────────────────────────
            void SetupProcedural()
            {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                GrassInstanceData d = _GrassBuffer[unity_InstanceID];
                float s = d.scale;

                // Per-blade Y rotation from position hash — breaks repetition
                float rot = frac(d.position.x * 12.9898 + d.position.z * 78.233 + d.position.y * 43.758) * 6.28318;
                float c = cos(rot);
                float sn = sin(rot);

                float px = d.position.x;
                float py = d.position.y;
                float pz = d.position.z;

                // TRS: scale S, rotate θ around Y, translate
                unity_ObjectToWorld = float4x4(
                    s*c,   0,  s*sn,  px,
                    0,     s,  0,     py,
                   -s*sn,  0,  s*c,   pz,
                    0,     0,  0,     1
                );
                float invS = 1.0 / max(s, 0.0001);
                unity_WorldToObject = float4x4(
                    invS*c,   0,     -invS*sn,  invS*(-px*c  + pz*sn),
                    0,        invS,   0,        -py*invS,
                    invS*sn,  0,      invS*c,   invS*(-px*sn - pz*c),
                    0,        0,      0,        1
                );
            #endif
            }

            // ── Per-material uniforms ───────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _GrassColorA;
                float4 _GrassColorB;
                float  _AlphaCutoff;
                float  _BladeHeight;
                float4 _WindDir;
                float  _WindSpeed;
                float  _WindFrequency;
                float  _WindAmplitude;
            CBUFFER_END

            // ── Vertex I/O ──────────────────────────────────────────────────
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 worldNormal  : TEXCOORD1;
                float  colorVariant : TEXCOORD2;
                float  fogFactor    : TEXCOORD3;
                float3 worldPos     : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ── Vertex shader ───────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // After SetupProcedural(), unity_ObjectToWorld encodes blade position+scale
                float3 worldPos = TransformObjectToWorld(IN.positionOS);

                // ── Wind ───────────────────────────────────────────────────
                // Only the tip is displaced (uv.y = 0 at root, 1 at tip)
                float tipFactor = IN.uv.y;
                float windPhase = dot(worldPos.xz, _WindDir.xz) * _WindFrequency
                                  + _Time.y * _WindSpeed;
                float windOffset1 = sin(windPhase)           * _WindAmplitude;
                float windOffset2 = sin(windPhase * 2.3 + 1) * _WindAmplitude * 0.4;
                float totalWind   = (windOffset1 + windOffset2) * tipFactor;

                worldPos.x += _WindDir.x * totalWind;
                worldPos.z += _WindDir.z * totalWind;

                OUT.positionCS  = TransformWorldToHClip(worldPos);
                OUT.fogFactor   = ComputeFogFactor(OUT.positionCS.z);
                OUT.worldPos    = worldPos;
                OUT.uv          = IN.uv;
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);

                // Retrieve colorVariant for this instance
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                OUT.colorVariant = _GrassBuffer[unity_InstanceID].colorVariant;
            #else
                OUT.colorVariant = 0.5;
            #endif
                return OUT;
            }

            // ── Fragment shader ─────────────────────────────────────────────
            float4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // Vertical gradient: root = dark, tip = bright
                float gradient = IN.uv.y;

                // Colour from variant + gradient
                float3 colA = lerp(_GrassColorA.rgb, _GrassColorB.rgb, IN.colorVariant);
                float3 colB = lerp(_GrassColorA.rgb * 1.4, _GrassColorB.rgb * 1.2, IN.colorVariant);
                float3 col  = lerp(colA, colB, gradient);

                // Alpha: full at base, fade at tip
                float alpha = 1.0 - pow(max(0, IN.uv.y - 0.6) / 0.4, 2.0);
                clip(alpha - _AlphaCutoff);

                // Simple diffuse lighting
                Light mainLight = GetMainLight();
                float3 normal = normalize(IN.worldNormal);
                float NdotL = saturate(dot(normal, mainLight.direction));
                float3 lit  = col * (mainLight.color * (NdotL * 0.7 + 0.3));

                // Additional lights (Forward+ compatible)
                #ifdef _ADDITIONAL_LIGHTS
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS = IN.worldPos;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light addLight = GetAdditionalLight(lightIndex, inputData.positionWS);
                        half addNdotL = saturate(dot(normal, addLight.direction));
                        lit += col * addLight.color * addNdotL * addLight.distanceAttenuation;
                    LIGHT_LOOP_END
                }
                #endif

                lit = MixFog(lit, IN.fogFactor);
                return float4(lit, 1.0);
            }
            ENDHLSL
        }

        // ── Shadow caster pass (grass does NOT cast shadows — performance) ─
        // Omitted intentionally.
    }

    FallBack "Hidden/InternalErrorShader"
}
