// LEGACY — do not use. Asset wipe + replacement happens in Volume 2/5.
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Legacy v1 ItemDatabase preserved for reference only.
/// Renamed from <c>ItemDatabase</c> to <c>LegacyItemDatabase</c>; the new
/// V2.2 class (under <c>Assets/Scripts/Inventory/</c>) owns the identifier
/// project-wide.
/// </summary>
public class LegacyItemDatabase : ScriptableObject
{
    public List<LegacyItemDefinition> items = new List<LegacyItemDefinition>();
}
