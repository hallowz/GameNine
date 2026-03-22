// Voidborne/TreeInstance
// GPU-instanced tree shader for URP.
// Reads per-tree data from a StructuredBuffer populated by TreeRenderer.
// Uses Unity's procedural instancing path (#pragma instancing_options procedural:SetupProcedural)
// so each DrawMeshInstancedIndirect call can render all trees of a variant in one draw call.
//
// Features:
//  · Position + quaternion rotation + uniform scale from StructuredBuffer
//  · Albedo texture + tint colour
//  · Receives and casts shadows
//  · Standard diffuse lighting via URP main light
//  · Hard LOD cutoff at far edge (billboard takes over beyond maxDrawDistance)

Shader "Voidborne/TreeInstance"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color   ("Tint",   Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── Forward Lit Pass ─────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma instancing_options procedural:SetupProcedural
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Per-tree instance data (must match TreeRenderer.TreeInstanceData) ──
            struct TreeRenderInstance
            {
                float3 position;    // 12 bytes
                float4 rotation;    // 16 bytes (quaternion x,y,z,w)
                float  scale;       //  4 bytes
            };

            StructuredBuffer<TreeRenderInstance> _TreeBuffer;

            // ── Procedural instancing callback ───────────────────────────
            void SetupProcedural()
            {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                TreeRenderInstance t = _TreeBuffer[unity_InstanceID];

                float4 q = t.rotation;
                float  s = t.scale;
                float3 p = t.position;

                // Quaternion to rotation matrix elements
                float x = q.x, y = q.y, z = q.z, w = q.w;
                float x2 = x + x, y2 = y + y, z2 = z + z;
                float xx = x * x2, xy = x * y2, xz = x * z2;
                float yy = y * y2, yz = y * z2, zz = z * z2;
                float wx = w * x2, wy = w * y2, wz = w * z2;

                float r00 = 1 - (yy + zz), r01 = xy - wz,       r02 = xz + wy;
                float r10 = xy + wz,        r11 = 1 - (xx + zz), r12 = yz - wx;
                float r20 = xz - wy,        r21 = yz + wx,        r22 = 1 - (xx + yy);

                // Object-to-world (scaled rotation + translation)
                unity_ObjectToWorld = float4x4(
                    s * r00, s * r01, s * r02, p.x,
                    s * r10, s * r11, s * r12, p.y,
                    s * r20, s * r21, s * r22, p.z,
                    0, 0, 0, 1
                );

                // World-to-object (inverse for correct normal transformation)
                float invS = 1.0 / max(s, 0.0001);
                float3 invP = -float3(
                    invS * (r00 * p.x + r10 * p.y + r20 * p.z),
                    invS * (r01 * p.x + r11 * p.y + r21 * p.z),
                    invS * (r02 * p.x + r12 * p.y + r22 * p.z)
                );

                unity_WorldToObject = float4x4(
                    invS * r00, invS * r10, invS * r20, invP.x,
                    invS * r01, invS * r11, invS * r21, invP.y,
                    invS * r02, invS * r12, invS * r22, invP.z,
                    0, 0, 0, 1
                );
            #endif
            }

            // ── Per-material uniforms ────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
            CBUFFER_END

            sampler2D _MainTex;

            // ── Crossfade globals (set per-frame by TreeRenderer) ──────
            float _TreeNearDistSq;
            float _TreeCrossfadeBandSq;

            // ── Vertex I/O ───────────────────────────────────────────────
            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 worldNormal  : TEXCOORD1;
                float3 worldPos     : TEXCOORD2;
                float  fogFactor    : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ── Vertex shader ────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 worldPos = TransformObjectToWorld(IN.positionOS);
                OUT.positionCS  = TransformWorldToHClip(worldPos);
                OUT.fogFactor   = ComputeFogFactor(OUT.positionCS.z);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                OUT.worldPos    = worldPos;

                return OUT;
            }

            // ── Fragment shader ──────────────────────────────────────────
            float4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // Dithered crossfade: fade out 3D trees in the overlap band
                float3 dv = IN.worldPos - _WorldSpaceCameraPos;
                float distSq = dot(dv, dv);
                if (distSq > _TreeNearDistSq)
                {
                    // 0 at _NearDistSq, 1 at _CrossfadeBandSq
                    float fade = saturate((distSq - _TreeNearDistSq)
                                        / max(_TreeCrossfadeBandSq - _TreeNearDistSq, 1.0));
                    // 4x4 ordered dither
                    int2 dp = int2(IN.positionCS.xy) % 4;
                    const float dither4x4[16] = {
                         0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                        12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                         3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                        15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
                    };
                    clip(dither4x4[dp.x + dp.y * 4] - fade);
                }

                float4 texColor = tex2D(_MainTex, IN.uv) * _Color;

                // Diffuse lighting from URP main light
                float4 shadowCoord = TransformWorldToShadowCoord(IN.worldPos);
                Light mainLight = GetMainLight(shadowCoord);
                float3 normal = normalize(IN.worldNormal);
                float NdotL = saturate(dot(normal, mainLight.direction));
                float3 lit = texColor.rgb * mainLight.color * (NdotL * 0.6 + 0.4) * mainLight.shadowAttenuation;

                // Additional lights (Forward+ cluster loop compatible)
                #ifdef _ADDITIONAL_LIGHTS
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS = IN.worldPos;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light addLight = GetAdditionalLight(lightIndex, IN.worldPos);
                        half addNdotL = saturate(dot(normal, addLight.direction));
                        lit += texColor.rgb * addLight.color * addNdotL * addLight.shadowAttenuation * addLight.distanceAttenuation;
                    LIGHT_LOOP_END
                }
                #endif

                // Add ambient
                float3 ambient = SampleSH(normal) * texColor.rgb;
                lit += ambient * 0.3;

                lit = MixFog(lit, IN.fogFactor);
                return float4(lit, 1.0);
            }
            ENDHLSL
        }

        // ── Shadow Caster Pass ───────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   vertShadow
            #pragma fragment fragShadow

            #pragma instancing_options procedural:SetupProcedural
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct TreeRenderInstance
            {
                float3 position;
                float4 rotation;
                float  scale;
            };

            StructuredBuffer<TreeRenderInstance> _TreeBuffer;

            // Shadow distance culling — set per frame from TreeRenderer
            float3 _ShadowCamPos;
            float  _ShadowMaxDistSq;

            void SetupProcedural()
            {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                TreeRenderInstance t = _TreeBuffer[unity_InstanceID];

                float4 q = t.rotation;
                float  s = t.scale;
                float3 p = t.position;

                // Cull shadows beyond shadow distance — collapse to degenerate point
                float3 sd = p - _ShadowCamPos;
                if (dot(sd, sd) > _ShadowMaxDistSq)
                {
                    unity_ObjectToWorld = float4x4(
                        0, 0, 0, p.x,
                        0, 0, 0, p.y,
                        0, 0, 0, p.z,
                        0, 0, 0, 1
                    );
                    unity_WorldToObject = float4x4(
                        0, 0, 0, 0,
                        0, 0, 0, 0,
                        0, 0, 0, 0,
                        0, 0, 0, 1
                    );
                    return;
                }

                float x = q.x, y = q.y, z = q.z, w = q.w;
                float x2 = x + x, y2 = y + y, z2 = z + z;
                float xx = x * x2, xy = x * y2, xz = x * z2;
                float yy = y * y2, yz = y * z2, zz = z * z2;
                float wx = w * x2, wy = w * y2, wz = w * z2;

                float r00 = 1 - (yy + zz), r01 = xy - wz,       r02 = xz + wy;
                float r10 = xy + wz,        r11 = 1 - (xx + zz), r12 = yz - wx;
                float r20 = xz - wy,        r21 = yz + wx,        r22 = 1 - (xx + yy);

                unity_ObjectToWorld = float4x4(
                    s * r00, s * r01, s * r02, p.x,
                    s * r10, s * r11, s * r12, p.y,
                    s * r20, s * r21, s * r22, p.z,
                    0, 0, 0, 1
                );

                float invS = 1.0 / max(s, 0.0001);
                float3 invP = -float3(
                    invS * (r00 * p.x + r10 * p.y + r20 * p.z),
                    invS * (r01 * p.x + r11 * p.y + r21 * p.z),
                    invS * (r02 * p.x + r12 * p.y + r22 * p.z)
                );

                unity_WorldToObject = float4x4(
                    invS * r00, invS * r10, invS * r20, invP.x,
                    invS * r01, invS * r11, invS * r21, invP.y,
                    invS * r02, invS * r12, invS * r22, invP.z,
                    0, 0, 0, 1
                );
            #endif
            }

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 _LightDirection;

            Varyings vertShadow(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 worldPos    = TransformObjectToWorld(IN.positionOS);
                float3 worldNormal = TransformObjectToWorldNormal(IN.normalOS);
                float4 clipPos     = TransformWorldToHClip(ApplyShadowBias(worldPos, worldNormal, _LightDirection));

                #if UNITY_REVERSED_Z
                    clipPos.z = min(clipPos.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    clipPos.z = max(clipPos.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = clipPos;

                return OUT;
            }

            float4 fragShadow(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ── Depth Only Pass (for depth prepass) ──────────────────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex   vertDepth
            #pragma fragment fragDepth

            #pragma instancing_options procedural:SetupProcedural
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct TreeRenderInstance
            {
                float3 position;
                float4 rotation;
                float  scale;
            };

            StructuredBuffer<TreeRenderInstance> _TreeBuffer;

            void SetupProcedural()
            {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                TreeRenderInstance t = _TreeBuffer[unity_InstanceID];

                float4 q = t.rotation;
                float  s = t.scale;
                float3 p = t.position;

                float x = q.x, y = q.y, z = q.z, w = q.w;
                float x2 = x + x, y2 = y + y, z2 = z + z;
                float xx = x * x2, xy = x * y2, xz = x * z2;
                float yy = y * y2, yz = y * z2, zz = z * z2;
                float wx = w * x2, wy = w * y2, wz = w * z2;

                float r00 = 1 - (yy + zz), r01 = xy - wz,       r02 = xz + wy;
                float r10 = xy + wz,        r11 = 1 - (xx + zz), r12 = yz - wx;
                float r20 = xz - wy,        r21 = yz + wx,        r22 = 1 - (xx + yy);

                unity_ObjectToWorld = float4x4(
                    s * r00, s * r01, s * r02, p.x,
                    s * r10, s * r11, s * r12, p.y,
                    s * r20, s * r21, s * r22, p.z,
                    0, 0, 0, 1
                );

                float invS = 1.0 / max(s, 0.0001);
                float3 invP = -float3(
                    invS * (r00 * p.x + r10 * p.y + r20 * p.z),
                    invS * (r01 * p.x + r11 * p.y + r21 * p.z),
                    invS * (r02 * p.x + r12 * p.y + r22 * p.z)
                );

                unity_WorldToObject = float4x4(
                    invS * r00, invS * r10, invS * r20, invP.x,
                    invS * r01, invS * r11, invS * r21, invP.y,
                    invS * r02, invS * r12, invS * r22, invP.z,
                    0, 0, 0, 1
                );
            #endif
            }

            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vertDepth(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float3 worldPos = TransformObjectToWorld(IN.positionOS);
                OUT.positionCS  = TransformWorldToHClip(worldPos);

                return OUT;
            }

            float4 fragDepth(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
