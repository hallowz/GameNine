using System;
using UnityEngine;

namespace Voidborne.ArtPipeline
{
    /// <summary>
    /// Volume 3.3 — Item Visual Recipe.
    ///
    /// Pure data: a tiny ordered list of <see cref="Layer"/> entries that
    /// compose into a single placeholder GameObject. Authoring lives entirely
    /// in editor code (<c>ItemVisualRecipeMapper</c>); this class is
    /// runtime-visible only so M2's prefab pipeline can reference it from
    /// non-editor code without conditional compilation.
    ///
    /// A recipe is intentionally cheap: 1–4 layers, each a primitive +
    /// material key. Composition is enough to give every Core 60 item a
    /// recognisable category-coloured silhouette ("stylized debug" visuals).
    /// True art passes belong to M8.
    ///
    /// Coop note: stateless data class. Recipes are produced by editor tools
    /// and never mutated at runtime.
    /// </summary>
    [Serializable]
    public class ItemVisualRecipe
    {
        /// <summary>
        /// Ordered set of primitive layers composed into one prefab. The
        /// composer instantiates one child GameObject per layer under the
        /// recipe's root.
        /// </summary>
        public Layer[] layers;

        public ItemVisualRecipe()
        {
            layers = Array.Empty<Layer>();
        }

        public ItemVisualRecipe(Layer[] layers)
        {
            this.layers = layers ?? Array.Empty<Layer>();
        }
    }

    /// <summary>
    /// A single primitive within an <see cref="ItemVisualRecipe"/>. Mirrors
    /// the spec's <c>Layer { PrimitiveShape, Vector3 localScale, Vector3
    /// localPos, Quaternion localRot, string materialKey }</c>.
    ///
    /// Default values keep authoring ergonomic — declaring
    /// <c>new Layer { shape = PrimitiveShape.Cube, materialKey = "machine" }</c>
    /// produces a unit-scale, identity-rotation, origin-centered cube.
    /// </summary>
    [Serializable]
    public struct Layer
    {
        /// <summary>Primitive mesh shape — resolves to a <see cref="Mesh"/> via the editor-only PrimitiveMeshFactory.</summary>
        public PrimitiveShape shape;

        /// <summary>Local scale of the layer relative to the recipe root.</summary>
        public Vector3 localScale;

        /// <summary>Local position of the layer relative to the recipe root.</summary>
        public Vector3 localPos;

        /// <summary>Local rotation of the layer relative to the recipe root.</summary>
        public Quaternion localRot;

        /// <summary>Palette key — resolves to a material via <c>PaletteRegistry.AssetPathFor(materialKey)</c>.</summary>
        public string materialKey;

        /// <summary>
        /// Authoring helper — creates a layer with sensible defaults:
        /// unit scale, identity rotation, zero offset. Caller supplies the
        /// shape and material key.
        /// </summary>
        public static Layer Make(PrimitiveShape shape, string materialKey)
        {
            return new Layer
            {
                shape = shape,
                localScale = Vector3.one,
                localPos = Vector3.zero,
                localRot = Quaternion.identity,
                materialKey = materialKey
            };
        }

        /// <summary>
        /// Authoring helper — creates a layer with explicit scale + position,
        /// identity rotation.
        /// </summary>
        public static Layer Make(PrimitiveShape shape, string materialKey, Vector3 localScale, Vector3 localPos)
        {
            return new Layer
            {
                shape = shape,
                localScale = localScale,
                localPos = localPos,
                localRot = Quaternion.identity,
                materialKey = materialKey
            };
        }
    }
}
