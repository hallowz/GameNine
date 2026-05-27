#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Voidborne.ArtPipeline;
using Voidborne.Editor.ArtPipeline;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for Volume 3.2 — Procedural Primitive Mesh Factory.
    ///
    /// Drives <see cref="PrimitiveMeshFactory.GenerateAll"/> and then walks
    /// every <see cref="PrimitiveShape"/> value to assert that:
    ///   1. An asset file exists at the canonical path.
    ///   2. The loaded mesh has vertices and triangles.
    ///   3. The mesh's bounds are roughly unit-size (catches degenerate or
    ///      runaway-scaled meshes).
    /// </summary>
    public class PrimitiveMeshFactoryTests
    {
        // Acceptance band for "approx unit size".
        // Floor 0.05 instead of 0.5 because Door/Panel are intentionally 0.1m
        // thin and Slab is 0.5m tall — those are legal "small" extents.
        // Ceiling 3 catches runaway-scale meshes (Capsule is 2m tall, Door 2m).
        // A flat primitive (Disc) is legal — at most ONE axis may be near-zero
        // (< MinExtent), and at least two axes must be inside [MinExtent, MaxExtent].
        private const float MinExtent = 0.05f;
        private const float MaxExtent = 3.0f;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Run once for the whole fixture so the disk-touching cost is paid
            // a single time. GenerateAll is idempotent — repeat calls in other
            // tests are cheap (just load-and-return).
            PrimitiveMeshFactory.GenerateAll();
        }

        [Test]
        public void GenerateAll_ProducesAllShapes()
        {
            foreach (PrimitiveShape shape in Enum.GetValues(typeof(PrimitiveShape)))
            {
                string path = PrimitiveMeshFactory.AssetPathFor(shape);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.IsNotNull(mesh,
                    $"Expected a Mesh asset at '{path}' for shape {shape}.");
            }
        }

        [Test]
        public void EachMesh_HasVerticesAndTriangles()
        {
            foreach (PrimitiveShape shape in Enum.GetValues(typeof(PrimitiveShape)))
            {
                string path = PrimitiveMeshFactory.AssetPathFor(shape);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.IsNotNull(mesh, $"Missing mesh for {shape} at {path}.");

                Assert.Greater(mesh.vertexCount, 0,
                    $"Mesh {shape} has zero vertices.");
                Assert.Greater(mesh.triangles.Length, 0,
                    $"Mesh {shape} has zero triangles.");
                Assert.AreEqual(0, mesh.triangles.Length % 3,
                    $"Mesh {shape} triangle index count is not a multiple of 3.");
            }
        }

        [Test]
        public void EachMesh_BoundsApproxUnitSize()
        {
            foreach (PrimitiveShape shape in Enum.GetValues(typeof(PrimitiveShape)))
            {
                string path = PrimitiveMeshFactory.AssetPathFor(shape);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.IsNotNull(mesh, $"Missing mesh for {shape}.");

                Vector3 size = mesh.bounds.size;

                // Ceiling applies to every axis — no primitive should be huge.
                Assert.LessOrEqual(size.x, MaxExtent,
                    $"Mesh {shape} bounds.size.x ({size.x}) exceeds ceiling {MaxExtent}.");
                Assert.LessOrEqual(size.y, MaxExtent,
                    $"Mesh {shape} bounds.size.y ({size.y}) exceeds ceiling {MaxExtent}.");
                Assert.LessOrEqual(size.z, MaxExtent,
                    $"Mesh {shape} bounds.size.z ({size.z}) exceeds ceiling {MaxExtent}.");

                // Floor allows ONE axis to be near-zero (flat primitives like Disc).
                int inRangeAxes =
                    (size.x >= MinExtent ? 1 : 0) +
                    (size.y >= MinExtent ? 1 : 0) +
                    (size.z >= MinExtent ? 1 : 0);

                Assert.GreaterOrEqual(inRangeAxes, 2,
                    $"Mesh {shape} bounds.size {size} has fewer than 2 axes >= {MinExtent} — degenerate.");
            }
        }
    }
}
#endif
