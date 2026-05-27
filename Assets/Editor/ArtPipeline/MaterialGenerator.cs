#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Voidborne.ArtPipeline;

namespace Voidborne.Editor.ArtPipeline
{
    /// <summary>
    /// Volume 3.1 — Material Generator.
    ///
    /// Generates one URP/Lit material per <see cref="PaletteRegistry"/> entry
    /// under <c>Assets/Materials/Generated/</c>. Build-material variants
    /// (stone / wood / iron / ...) get bespoke PBR config (smoothness,
    /// metallic, transparency) so they read as the right surface family.
    ///
    /// Ores deliberately do NOT use emission — see
    /// <c>feedback_ore_visuals.md</c>.
    ///
    /// Idempotent: re-running updates the colour / PBR fields on existing
    /// materials in place; new keys are created; assets removed from the
    /// palette are NOT auto-deleted (manual cleanup or "Regenerate All").
    /// </summary>
    public static class MaterialGenerator
    {
        private const string GeneratedFolder = "Assets/Materials/Generated";
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";

        // URP/Lit shader properties.
        private const string PropBaseColor = "_BaseColor";
        private const string PropMetallic = "_Metallic";
        private const string PropSmoothness = "_Smoothness";
        private const string PropSurface = "_Surface";     // 0 opaque, 1 transparent
        private const string PropBlend = "_Blend";         // 0 alpha, 1 premul, 2 additive, 3 multiply
        private const string PropAlphaClip = "_AlphaClip";

        [MenuItem("Voidborne/Generate/Materials")]
        public static void Generate()
        {
            var shader = Shader.Find(UrpLitShaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"[MaterialGenerator] Could not find shader '{UrpLitShaderName}'. " +
                    "Make sure URP is installed (it should be on this project).");
                return;
            }

            EnsureFolder(GeneratedFolder);

            int created = 0;
            int updated = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var entry in PaletteRegistry.AllEntries)
                {
                    string key = entry.Key;
                    Color color = entry.Value;
                    string path = $"{GeneratedFolder}/Mat_{key}.mat";

                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    bool isNew = mat == null;
                    if (isNew)
                    {
                        mat = new Material(shader);
                        AssetDatabase.CreateAsset(mat, path);
                        created++;
                    }
                    else
                    {
                        // If somebody swapped the shader, force it back to URP/Lit so
                        // _BaseColor / _Metallic / _Smoothness behave as expected.
                        if (mat.shader != shader) mat.shader = shader;
                        updated++;
                    }

                    ApplyMaterialConfig(mat, key, color);
                    EditorUtility.SetDirty(mat);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int total = created + updated;
            Debug.Log($"[MaterialGenerator] Generated {total} materials ({created} new, {updated} updated) under {GeneratedFolder}/.");
        }

        // -------------------------------------------------------------------
        // PBR config per palette key.
        // -------------------------------------------------------------------

        private static void ApplyMaterialConfig(Material mat, string paletteKey, Color color)
        {
            // Default opaque, slightly diffuse, non-metallic.
            float smoothness = 0.4f;
            float metallic = 0f;
            bool transparent = false;
            Color baseColor = color;
            baseColor.a = 1f;

            if (PaletteRegistry.IsBuildMaterialKey(paletteKey))
            {
                string mat_id = PaletteRegistry.StripBuildPrefix(paletteKey);
                switch (mat_id)
                {
                    case "stone":
                    case "brick":
                    case "obsidian":
                    case "ash":
                    case "cinder":
                    case "marsh":
                    case "neutral":
                        smoothness = 0.3f;
                        metallic = 0f;
                        break;

                    case "iron":
                    case "copper":
                    case "titanium":
                    case "gold":
                    case "silver":
                        smoothness = 0.6f;
                        metallic = 0.9f;
                        break;

                    case "glass":
                        smoothness = 0.9f;
                        metallic = 0f;
                        transparent = true;
                        baseColor.a = 0.6f;
                        break;

                    case "wood":
                    case "fabric":
                        smoothness = 0.2f;
                        metallic = 0f;
                        break;

                    case "bone":
                        smoothness = 0.4f;
                        metallic = 0f;
                        break;

                    case "chitin":
                        smoothness = 0.5f;
                        metallic = 0f;
                        break;

                    case "frost":
                        // Slightly translucent, glossy, but not metallic.
                        smoothness = 0.7f;
                        metallic = 0f;
                        break;

                    case "vord":
                    case "void":
                    case "kin":
                        smoothness = 0.5f;
                        metallic = 0.4f;
                        break;

                    default:
                        smoothness = 0.4f;
                        metallic = 0f;
                        break;
                }
            }
            else
            {
                // Non-build palette keys.
                switch (paletteKey)
                {
                    case "ore":
                        // Ores: shiny but NOT emissive (see feedback_ore_visuals.md).
                        smoothness = 0.5f;
                        metallic = 0.3f;
                        break;

                    case "machine":
                    case "automation":
                        smoothness = 0.55f;
                        metallic = 0.5f;
                        break;

                    case "weapon":
                        smoothness = 0.5f;
                        metallic = 0.3f;
                        break;

                    case "armor":
                        smoothness = 0.45f;
                        metallic = 0.3f;
                        break;

                    case "power":
                    case "gadget":
                        smoothness = 0.6f;
                        metallic = 0.2f;
                        break;

                    case "fauna":
                    case "flora":
                    case "food":
                    case "soil":
                    case "exotic":
                    case "vehicle":
                    case "deco":
                        smoothness = 0.3f;
                        metallic = 0f;
                        break;

                    default:
                        smoothness = 0.4f;
                        metallic = 0f;
                        break;
                }
            }

            if (mat.HasProperty(PropBaseColor)) mat.SetColor(PropBaseColor, baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor); // legacy alias
            if (mat.HasProperty(PropMetallic)) mat.SetFloat(PropMetallic, metallic);
            if (mat.HasProperty(PropSmoothness)) mat.SetFloat(PropSmoothness, smoothness);

            ConfigureSurfaceMode(mat, transparent);

            // Always ensure no emission — ores in particular must not glow.
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            mat.DisableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
        }

        private static void ConfigureSurfaceMode(Material mat, bool transparent)
        {
            // URP/Lit surface mode plumbing: _Surface + _Blend + render queue +
            // keywords + render type tag. Mirror what the Material inspector does
            // so the material renders correctly even outside the inspector.
            if (transparent)
            {
                if (mat.HasProperty(PropSurface)) mat.SetFloat(PropSurface, 1f); // Transparent
                if (mat.HasProperty(PropBlend)) mat.SetFloat(PropBlend, 0f);     // Alpha
                if (mat.HasProperty(PropAlphaClip)) mat.SetFloat(PropAlphaClip, 0f);
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.SetOverrideTag("RenderType", "Transparent");
            }
            else
            {
                if (mat.HasProperty(PropSurface)) mat.SetFloat(PropSurface, 0f); // Opaque
                if (mat.HasProperty(PropAlphaClip)) mat.SetFloat(PropAlphaClip, 0f);
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.One);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 1f);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = -1; // From shader
                mat.SetOverrideTag("RenderType", "Opaque");
            }
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;

            // Recursive parent creation — AssetDatabase.CreateFolder requires
            // the parent to already exist.
            var parts = assetPath.Split('/');
            string acc = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{acc}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(acc, parts[i]);
                }
                acc = next;
            }
        }
    }
}
#endif
