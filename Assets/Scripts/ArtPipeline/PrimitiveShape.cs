namespace Voidborne.ArtPipeline
{
    /// <summary>
    /// Volume 3.2 — Procedural Primitive Shape catalogue.
    ///
    /// Runtime-accessible enum naming every primitive form the visual asset
    /// pipeline can compose (see <c>ItemVisualRecipe</c> in V3.3). Each value
    /// has a corresponding cached mesh asset under
    /// <c>Assets/Models/Generated/Primitives/{shape}.asset</c> produced by
    /// the editor-only <c>PrimitiveMeshFactory</c>.
    ///
    /// New shapes appended here will be picked up by the factory's
    /// <c>GenerateAll</c> pass on the next editor run.
    /// </summary>
    public enum PrimitiveShape
    {
        Cube,
        Slab,
        Panel,
        Stairs,
        Door,
        Cylinder,
        Capsule,
        Sphere,
        Cone,
        Disc,
        Torus,
        Spike,
        Pyramid,
        Wedge
    }
}
