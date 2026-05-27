#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using Voidborne.ArtPipeline;

namespace Voidborne.Editor.ArtPipeline
{
    /// <summary>
    /// Volume 3.2 — Procedural Primitive Mesh Factory.
    ///
    /// Editor-only static class that produces a cached <see cref="Mesh"/>
    /// asset for every <see cref="PrimitiveShape"/> value. Meshes live at
    /// <c>Assets/Models/Generated/Primitives/{shape}.asset</c> and are the
    /// raw building blocks consumed by V3.3 (ItemVisualRecipe / ItemModelComposer).
    ///
    /// Construction strategy by shape:
    ///   Cube/Cylinder/Capsule/Sphere — clone Unity's built-in primitives.
    ///   Slab/Panel/Door              — ProBuilder GenerateCube with custom size.
    ///   Stairs                       — ProBuilder GenerateStair (4 steps).
    ///   Torus                        — ProBuilder GenerateTorus.
    ///   Wedge                        — ProBuilder GeneratePrism (triangular prism).
    ///   Cone/Spike                   — ProBuilder GenerateCone (16-segment).
    ///   Disc                         — manual mesh: 16-segment flat fan.
    ///   Pyramid                      — manual mesh: 5 verts (4 base + apex).
    ///
    /// Idempotent: missing assets are created; existing assets are returned
    /// as-is. To force a rebuild, delete the asset on disk and re-run.
    /// </summary>
    public static class PrimitiveMeshFactory
    {
        public const string PrimitivesFolder = "Assets/Models/Generated/Primitives";

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Asset path for <paramref name="shape"/>'s cached mesh.
        /// </summary>
        public static string AssetPathFor(PrimitiveShape shape)
        {
            return $"{PrimitivesFolder}/{shape}.asset";
        }

        /// <summary>
        /// Returns the cached <see cref="Mesh"/> asset for <paramref name="shape"/>,
        /// constructing it (and the parent folder) if it does not exist.
        /// </summary>
        public static Mesh GetOrCreate(PrimitiveShape shape)
        {
            EnsureFolder(PrimitivesFolder);
            string path = AssetPathFor(shape);

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            var mesh = BuildMesh(shape);
            mesh.name = shape.ToString();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>
        /// Ensure every <see cref="PrimitiveShape"/> value has a mesh asset on
        /// disk. Logs created vs already-existing counts.
        /// </summary>
        public static void GenerateAll()
        {
            EnsureFolder(PrimitivesFolder);

            int created = 0;
            int existed = 0;
            var values = (PrimitiveShape[])Enum.GetValues(typeof(PrimitiveShape));

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var shape in values)
                {
                    string path = AssetPathFor(shape);
                    if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
                    {
                        existed++;
                        continue;
                    }

                    var mesh = BuildMesh(shape);
                    mesh.name = shape.ToString();
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();

                    AssetDatabase.CreateAsset(mesh, path);
                    created++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int total = created + existed;
            Debug.Log(
                $"[PrimitiveMeshFactory] Generated {total} primitive meshes " +
                $"({created} new, {existed} already existed) under {PrimitivesFolder}/.");
        }

        [MenuItem("Voidborne/Generate/Primitives")]
        private static void GenerateAllMenu()
        {
            GenerateAll();
        }

        // -------------------------------------------------------------------
        // Mesh construction
        // -------------------------------------------------------------------

        private static Mesh BuildMesh(PrimitiveShape shape)
        {
            switch (shape)
            {
                case PrimitiveShape.Cube:
                    return FromUnityPrimitive(PrimitiveType.Cube);
                case PrimitiveShape.Cylinder:
                    return FromUnityPrimitive(PrimitiveType.Cylinder);
                case PrimitiveShape.Capsule:
                    return FromUnityPrimitive(PrimitiveType.Capsule);
                case PrimitiveShape.Sphere:
                    return FromUnityPrimitive(PrimitiveType.Sphere);

                case PrimitiveShape.Slab:
                    // 1 × 0.5 × 1 — flat half-height cube.
                    return FromProBuilderCube(new Vector3(1f, 0.5f, 1f));
                case PrimitiveShape.Panel:
                    // 1 × 1 × 0.1 — thin wall.
                    return FromProBuilderCube(new Vector3(1f, 1f, 0.1f));
                case PrimitiveShape.Door:
                    // 1 × 2 × 0.1 — tall thin panel. ProBuilder's GenerateDoor
                    // produces a door-FRAME (legs + lintel); for our visual
                    // library a simple tall panel is the right primitive.
                    return FromProBuilderCube(new Vector3(1f, 2f, 0.1f));

                case PrimitiveShape.Stairs:
                    return FromProBuilderStair(new Vector3(1f, 1f, 1f), 4);

                case PrimitiveShape.Torus:
                    // outerRadius 0.5, tube radius 0.15  =>  innerRadius = 0.35.
                    return FromProBuilderTorus(rows: 8, columns: 16, innerRadius: 0.35f, outerRadius: 0.5f);

                case PrimitiveShape.Wedge:
                    // Triangular prism. ProBuilder GeneratePrism returns a wedge.
                    return FromProBuilderPrism(new Vector3(1f, 1f, 1f));

                case PrimitiveShape.Cone:
                    // Standard cone: radius 0.5, height 1, 16 segments.
                    return FromProBuilderCone(radius: 0.5f, height: 1f, subdiv: 16);

                case PrimitiveShape.Spike:
                    // Narrow tall cone.
                    return FromProBuilderCone(radius: 0.2f, height: 1f, subdiv: 12);

                case PrimitiveShape.Disc:
                    return BuildDiscMesh(radius: 0.5f, segments: 16);

                case PrimitiveShape.Pyramid:
                    return BuildPyramidMesh(baseSize: 1f, height: 1f);

                default:
                    throw new InvalidOperationException($"Unhandled PrimitiveShape: {shape}");
            }
        }

        // -------------------------------------------------------------------
        // Unity built-in primitives.
        // -------------------------------------------------------------------

        private static Mesh FromUnityPrimitive(PrimitiveType prim)
        {
            // We instantiate a temporary GO so we own a Mesh we can save.
            // Unity owns the runtime primitive's shared mesh; we duplicate it
            // before destroying the GO.
            var go = GameObject.CreatePrimitive(prim);
            try
            {
                var mf = go.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                {
                    throw new InvalidOperationException(
                        $"Unity primitive {prim} had no MeshFilter/sharedMesh.");
                }

                return UnityEngine.Object.Instantiate(mf.sharedMesh);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // -------------------------------------------------------------------
        // ProBuilder-backed shapes.
        // -------------------------------------------------------------------

        private static Mesh ExtractAndDispose(ProBuilderMesh pb)
        {
            try
            {
                pb.ToMesh();
                pb.Refresh();
                var mf = pb.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                {
                    throw new InvalidOperationException(
                        "ProBuilderMesh did not produce a MeshFilter.sharedMesh after ToMesh/Refresh.");
                }
                return UnityEngine.Object.Instantiate(mf.sharedMesh);
            }
            finally
            {
                if (pb != null && pb.gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(pb.gameObject);
                }
            }
        }

        private static Mesh FromProBuilderCube(Vector3 size)
        {
            var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            return ExtractAndDispose(pb);
        }

        private static Mesh FromProBuilderStair(Vector3 size, int steps)
        {
            var pb = ShapeGenerator.GenerateStair(PivotLocation.Center, size, steps, true);
            return ExtractAndDispose(pb);
        }

        private static Mesh FromProBuilderTorus(int rows, int columns, float innerRadius, float outerRadius)
        {
            // ProBuilder 6.x signature:
            //   GenerateTorus(pivot, rows, columns, innerRadius, outerRadius,
            //                 smooth, horizontalCircumference, verticalCircumference, manualUvs=false)
            var pb = ShapeGenerator.GenerateTorus(
                PivotLocation.Center,
                rows,
                columns,
                innerRadius,
                outerRadius,
                smooth: true,
                horizontalCircumference: 360f,
                verticalCircumference: 360f);
            return ExtractAndDispose(pb);
        }

        private static Mesh FromProBuilderPrism(Vector3 size)
        {
            var pb = ShapeGenerator.GeneratePrism(PivotLocation.Center, size);
            return ExtractAndDispose(pb);
        }

        private static Mesh FromProBuilderCone(float radius, float height, int subdiv)
        {
            var pb = ShapeGenerator.GenerateCone(PivotLocation.Center, radius, height, subdiv);
            return ExtractAndDispose(pb);
        }

        // -------------------------------------------------------------------
        // Manually built meshes.
        // -------------------------------------------------------------------

        /// <summary>
        /// Flat disc on the XZ plane, centered on the origin, radius
        /// <paramref name="radius"/>, <paramref name="segments"/> radial wedges.
        /// </summary>
        private static Mesh BuildDiscMesh(float radius, int segments)
        {
            if (segments < 3) segments = 3;

            var vertices = new List<Vector3>(segments + 1);
            var triangles = new List<int>(segments * 3);

            // Centre vertex.
            vertices.Add(Vector3.zero);

            for (int i = 0; i < segments; i++)
            {
                float t = (float)i / segments * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius));
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i + 1;
                int b = (i + 1) % segments + 1;
                // Wind so the disc faces +Y (counter-clockwise looking down -Y in Unity's LH coords).
                triangles.Add(0);
                triangles.Add(b);
                triangles.Add(a);
            }

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        /// <summary>
        /// 4-sided pyramid: <paramref name="baseSize"/>×<paramref name="baseSize"/>
        /// square base on the XZ plane, apex at +Y * <paramref name="height"/>.
        /// 5 vertices, 6 triangles (4 sides + 2-tri base).
        /// </summary>
        private static Mesh BuildPyramidMesh(float baseSize, float height)
        {
            float h = baseSize * 0.5f;
            // Sit the base on y=0 with apex at +Y so bounds.size.y == height.
            var v0 = new Vector3(-h, 0f, -h); // back-left
            var v1 = new Vector3(h, 0f, -h);  // back-right
            var v2 = new Vector3(h, 0f, h);   // front-right
            var v3 = new Vector3(-h, 0f, h);  // front-left
            var apex = new Vector3(0f, height, 0f);

            var vertices = new[] { v0, v1, v2, v3, apex };
            // Triangles: 4 side faces + 2 base triangles.
            // Side winding: outward-facing.
            //   back   side: v0, v1, apex  (normal -Z-ish)
            //   right  side: v1, v2, apex
            //   front  side: v2, v3, apex
            //   left   side: v3, v0, apex
            // Base faces -Y so we wind v0->v3->v2->v1.
            var triangles = new[]
            {
                0, 1, 4, // back
                1, 2, 4, // right
                2, 3, 4, // front
                3, 0, 4, // left
                0, 3, 2, // base tri 1 (facing -Y)
                0, 2, 1  // base tri 2
            };

            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            return mesh;
        }

        // -------------------------------------------------------------------
        // Folder helpers.
        // -------------------------------------------------------------------

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;

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
