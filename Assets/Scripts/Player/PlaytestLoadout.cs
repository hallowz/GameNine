using System.Collections.Generic;
using UnityEngine;

namespace Voidborne.Player
{
    /// <summary>
    /// M2 Playtest — populates the player's inventory with the starter set
    /// for the canonical milk-in-boiler synergy test on Start. Re-callable
    /// at runtime via <see cref="Refresh"/> (PlaytestDevConsole F2).
    ///
    /// Hard-coded item ids only — no SO authoring. If an id isn't in
    /// items_core.json the entry is skipped with a console warning.
    ///
    /// Slot policy: inventory is 5 hotbar + 35 main = 40 slots. Most starter
    /// items stack to >=20, so the full list fits comfortably. Items are
    /// added in priority order; anything that doesn't fit is logged so
    /// the developer knows what got dropped.
    ///
    /// Coop note: owner-authoritative — each player runs their own copy.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlaytestLoadout : MonoBehaviour
    {
        /// <summary>One entry in the starter loadout — item id + quantity.</summary>
        private readonly struct Entry
        {
            public readonly string Id;
            public readonly int    Quantity;
            public Entry(string id, int qty) { Id = id; Quantity = qty; }
        }

        // ---------------------------------------------------------------
        //  Starter set — Core 8 + extras for the M2 playtest brief
        // ---------------------------------------------------------------
        //
        // Order matters: machines + power + a few sources come first so
        // that if the inventory fills up, the milk-in-boiler loop is
        // still reachable. Pure bulk (sources / components) comes last.
        //
        // SLOT BUDGET: Inventory = 5 hotbar + 35 main = 40 slots. Many items
        // (machines, building cubes, conveyor_belts, inserters, storage_chests)
        // have maxStackSize=1, so each "qty" consumes a full slot. The order
        // below is strict priority: the M2 milk-in-boiler loop MUST fit in the
        // first ~12 slots (boiler, generator, battery, sink, cable, hand-crank,
        // milk, water, coal, workbench, furnace, hand-crank). Everything past
        // that is nice-to-have and may legitimately spill / drop if the player
        // hasn't picked up the worn dev_backpack yet.
        private static readonly Entry[] StarterSet = new Entry[]
        {
            // ---- M2 HERO LOOP (must always fit) — priority block 0 ----
            new Entry("steam_boiler",         1),
            new Entry("steam_generator",      1),
            new Entry("battery_basic",        2),
            new Entry("power_sink",           1),
            new Entry("copper_cable_t1",      30),
            new Entry("hand_crank_generator", 1),
            new Entry("milk",                 10),  // <-- the M2 hero input
            new Entry("water",                10),
            new Entry("coal_ore",             30),
            new Entry("workbench",            1),
            new Entry("furnace",              1),

            // ---- Core 8 machines + extras — priority block 1 ----
            new Entry("campfire",             1),
            new Entry("composter",            1),
            new Entry("drying_rack",          1),
            new Entry("crusher",              1),
            new Entry("press",                1),
            new Entry("storage_chest",        2),
            new Entry("conveyor_belt",        4),
            new Entry("inserter",             2),
            new Entry("junction_box",         2),

            // ---- Sources (mining-related) — priority block 2 ----
            new Entry("iron_ore",             20),
            new Entry("copper_ore",           20),
            new Entry("wood",                 30),
            new Entry("stone",                30),
            new Entry("plant_fiber",          16),
            new Entry("clay",                 8),
            new Entry("sand",                 8),
            new Entry("raw_meat",             5),
            new Entry("egg",                  5),
            new Entry("wheat",                10),

            // ---- Refined components — priority block 3 ----
            new Entry("iron_ingot",           16),
            new Entry("copper_ingot",         16),
            new Entry("charcoal",             10),
            new Entry("plank",                20),
            new Entry("nail",                 30),
            new Entry("wire",                 16),

            // ---- Combat samples (V10) — priority block 4 ----
            new Entry("wooden_spear",         1),
            new Entry("iron_sword",           1),
            new Entry("pistol",               1),
            new Entry("hunting_bow",          1),
        };

        private PlayerInventory _inventory;
        private bool _granted;

        private void Awake()
        {
            _inventory = GetComponent<PlayerInventory>();
            if (_inventory == null)
                _inventory = FindFirstObjectByType<PlayerInventory>();
        }

        private void Start()
        {
            // Defer one frame: PlayerInventory.Start() also runs on the
            // first frame and seeds the dev backpack into the worn slot.
            // Letting that happen first means our AddItem calls go through
            // a fully-initialised inventory (worn backpack open for spillover).
            StartCoroutine(GrantNextFrame());
        }

        private System.Collections.IEnumerator GrantNextFrame()
        {
            yield return null;
            Refresh();
        }

        /// <summary>
        /// Repopulate the starter set into the live PlayerInventory.
        /// Safe to call repeatedly; existing stacks are topped up via
        /// the inventory's normal stacking rules.
        /// </summary>
        public void Refresh()
        {
            if (_inventory == null)
            {
                Debug.LogWarning("[PlaytestLoadout] PlayerInventory not found — starter loadout skipped.");
                return;
            }

            ItemDatabase db = ItemDatabase.GetOrLoad();
            if (db == null)
            {
                Debug.LogWarning("[PlaytestLoadout] ItemDatabase not loaded — starter loadout skipped.");
                return;
            }

            List<string> missingIds = new List<string>();
            List<string> droppedIds = new List<string>();
            int placedCount  = 0;
            int droppedCount = 0;

            for (int i = 0; i < StarterSet.Length; i++)
            {
                Entry e = StarterSet[i];
                ItemDefinition def = db.GetItem(e.Id);
                if (def == null)
                {
                    missingIds.Add(e.Id);
                    continue;
                }

                bool fullyPlaced = _inventory.AddItem(new ItemStack(def, e.Quantity));
                if (fullyPlaced)
                {
                    placedCount++;
                }
                else
                {
                    droppedIds.Add(e.Id);
                    droppedCount++;
                }
            }

            _granted = true;

            Debug.Log($"[PlaytestLoadout] Granted starter set — {placedCount}/{StarterSet.Length} entries fully placed.");

            if (missingIds.Count > 0)
                Debug.LogWarning($"[PlaytestLoadout] Missing item ids (not in ItemDatabase): {string.Join(", ", missingIds)}");

            if (droppedIds.Count > 0)
                Debug.LogWarning($"[PlaytestLoadout] Inventory full — partially dropped entries: {string.Join(", ", droppedIds)}");
        }

        /// <summary>True once <see cref="Refresh"/> has run at least once.</summary>
        public bool HasGranted => _granted;
    }
}
