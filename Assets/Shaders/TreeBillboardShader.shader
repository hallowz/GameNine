// Voidborne/TreeBillboard
// Camera-facing billboard quad for distant trees.
// Reads per-tree data from a StructuredBuffer populated by TreeRenderer.
// Uses cylindrical billboarding (Y-axis rotation only) so trees stay upright.
//
// Features:
//  · Procedural instancing from StructuredBuffer
//  · Alpha-cutout billboard texture (pre-lit from bake)

Shader "Voidborne/TreeBillboard"
{
    Properties
    {
        _MainTex ("Billboard", 2D) = "white" {}
        _Color   ("Tint",   Color) = (1,1,1,1)
        _Cutoff  ("Alpha Cutoff", Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "TransparentCutout"
            "Queue"          = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── Forward Lit Pass ─────────────────────────────────────────────
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma instancing_options procedural:SetupProcedural
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Per-tree instance data ───────────────────────────────────
            struct TreeRenderInstance
            {
                float3 position;
                float4 rotation;
                float  scale;
            };

            StructuredBuffer<TreeRenderInstance> _TreeBuffer;

            // ── Billboard uniforms (set via MaterialPropertyBlock) ───────
            float _BillboardWidth;
            float _BillboardHeight;

            // ── Procedural instancing — identity matrix, billboard in vert ─
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

            // ── Per-material uniforms ────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _Cutoff;
            CBUFFER_END

            sampler2D _MainTex;

            // ── Crossfade globals (set per-frame by TreeRenderer) ──────
            float _TreeNearDistSq;
            float _TreeCrossfadeBandSq;
            float _TreeFarDistSq;
            float _TreeFarFadeStartSq;

            // ── Vertex I/O ───────────────────────────────────────────────
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 worldPos     : TEXCOORD1;
                float  fogFactor    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // ── Vertex shader ────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                TreeRenderInstance t = _TreeBuffer[unity_InstanceID];
                float3 treePos = t.position;
                float  s       = t.scale;

                // Cylindrical billboard — rotate around Y to face camera
                float3 camPos   = _WorldSpaceCameraPos;
                float3 toCamera = camPos - treePos;
                toCamera.y = 0;
                float  len     = length(toCamera);
                float3 forward = len > 0.001 ? toCamera / len : float3(0, 0, 1);
                float3 right   = float3(forward.z, 0, -forward.x);

                // Expand quad: x → right, y → up
                float3 worldPos = treePos
                    + right       * IN.positionOS.x * s * _BillboardWidth
                    + float3(0,1,0) * IN.positionOS.y * s * _BillboardHeight;

                OUT.positionCS = TransformWorldToHClip(worldPos);
                OUT.fogFactor  = ComputeFogFactor(OUT.positionCS.z);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.worldPos   = worldPos;
            #else
                float3 worldPos = TransformObjectToWorld(IN.positionOS);
                OUT.positionCS  = TransformWorldToHClip(worldPos);
                OUT.fogFactor   = ComputeFogFactor(OUT.positionCS.z);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.worldPos    = worldPos;
            #endif

                return OUT;
            }

            // ── Fragment shader ──────────────────────────────────────────
            float4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // 4x4 ordered dither (shared by near crossfade and far fade)
                int2 dp = int2(IN.positionCS.xy) % 4;
                const float dither4x4[16] = {
                     0.0/16.0,  8.0/16.0,  2.0/16.0, 10.0/16.0,
                    12.0/16.0,  4.0/16.0, 14.0/16.0,  6.0/16.0,
                     3.0/16.0, 11.0/16.0,  1.0/16.0,  9.0/16.0,
                    15.0/16.0,  7.0/16.0, 13.0/16.0,  5.0/16.0
                };
                float dval = dither4x4[dp.x + dp.y * 4];

                float3 dv = IN.worldPos - _WorldSpaceCameraPos;
                float distSq = dot(dv, dv);

                // Near crossfade: fade in billboards in the overlap band.
                // Use (1 - dither) so the pattern is exactly complementary to
                // the 3D tree shader's clip(dither - fade). This guarantees
                // every pixel is covered by exactly one LOD — no holes.
                if (distSq < _TreeCrossfadeBandSq)
                {
                    // 1 at _NearDistSq (fully clipped), 0 at _CrossfadeBandSq (fully visible)
                    float fade = saturate((_TreeCrossfadeBandSq - distSq)
                                        / max(_TreeCrossfadeBandSq - _TreeNearDistSq, 1.0));
                    clip((1.0 - dval) - fade);
                }

                // Far edge fade: smoothly fade out billboards approaching max draw distance
                // instead of a hard pop at the boundary.
                if (distSq > _TreeFarFadeStartSq)
                {
                    // 0 at _FarFadeStartSq (fully visible), 1 at _FarDistSq (fully clipped)
                    float farFade = saturate((distSq - _TreeFarFadeStartSq)
                                           / max(_TreeFarDistSq - _TreeFarFadeStartSq, 1.0));
                    clip(dval - farFade);
                }

                float4 texColor = tex2D(_MainTex, IN.uv) * _Color;

                // Alpha cutoff
                clip(texColor.a - _Cutoff);

                // Apply ambient lighting so billboards darken at night.
                // The baked texture already has diffuse shading, so we modulate
                // by ambient + a fraction of the main directional light.
                float3 ambient = unity_AmbientSky.rgb;
                // Main light contribution — approximate with a flat NdotL
                // since billboards have no meaningful normal.
                float3 mainLight = _MainLightColor.rgb * saturate(_MainLightPosition.y);
                float3 lightFactor = saturate(ambient + mainLight);
                texColor.rgb *= max(lightFactor, 0.02); // min floor so trees aren't invisible

                texColor.rgb = MixFog(texColor.rgb, IN.fogFactor);
                return float4(texColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
