#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.Crafting;
using Voidborne.Player;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for Volume 4 Chunk 4.3 — Inventory Panel.
    ///
    /// Validates the restyled InventoryUI: section composition, hotbar mirror
    /// reflecting the active hotbar, removal of any backpack section, and
    /// personal-craft output recomputing on input change.
    /// </summary>
    public class InventoryPanelTests
    {
        // ---------------------------------------------------------------
        //  Fixture
        // ---------------------------------------------------------------

        private GameObject _canvasGo;
        private Canvas     _canvas;

        // Shared test items / recipe (built once per test).
        // V6.2 rewires PersonalCraftingGrid to call the V6.1 CraftingMatchEngine
        // against RecipeRegistry, not the legacy CraftingManager. We therefore
        // install in-memory ItemDatabase + RecipeRegistry fixtures and feed the
        // grid a V3 RecipeDefinition (with ingredients[]), not the V1 grid
        // CraftingRecipe.
        private ItemDefinition _wood;
        private ItemDefinition _stone;
        private ItemDefinition _plank;
        private RecipeDefinition _woodToPlankRecipe;

        // Fixture singletons for V6.2 wiring (rebuilt per test to avoid leaks).
        private ItemDatabase _itemDbFixture;
        private RecipeRegistry _recipeRegFixture;

        [SetUp]
        public void SetUp()
        {
            // Sterile canvas
            _canvasGo = new GameObject("InventoryPanelTests_Canvas");
            _canvas   = _canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Build items used across tests
            _wood = MakeItem("wood",  "Wood",  64);
            _stone = MakeItem("stone", "Stone", 64);
            _plank = MakeItem("plank", "Plank", 64);

            // V6.2 personal-grid recipe: 4 wood -> 1 plank. Scope = personal
            // grid (viaMachineId == null). The V6.1 engine matches via
            // bag-of-ingredients (position-independent) so we don't need to
            // populate any explicit gridWidth/gridHeight.
            _woodToPlankRecipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            _woodToPlankRecipe.outputItemId   = "plank";
            _woodToPlankRecipe.outputQty      = 1;
            _woodToPlankRecipe.ingredients    = new[] { new Ingredient("wood", 4) };
            _woodToPlankRecipe.efficiency     = 1.0f;
            _woodToPlankRecipe.outputModifier = 1.0f;
            _woodToPlankRecipe.viaMachineId   = null;

            // Install fixture singletons for ItemDatabase + RecipeRegistry.
            // ScriptableObject.CreateInstance triggers OnEnable which sets
            // ItemDatabase.Instance / RecipeRegistry.Instance on each SO.
            _itemDbFixture = ScriptableObject.CreateInstance<ItemDatabase>();
            _itemDbFixture.items = new List<ItemDefinition> { _wood, _stone, _plank };
            _itemDbFixture.Reindex();

            _recipeRegFixture = ScriptableObject.CreateInstance<RecipeRegistry>();
            _recipeRegFixture.allRecipes = new List<RecipeDefinition> { _woodToPlankRecipe };
            _recipeRegFixture.Reindex();
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
            if (_wood  != null) Object.DestroyImmediate(_wood);
            if (_stone != null) Object.DestroyImmediate(_stone);
            if (_plank != null) Object.DestroyImmediate(_plank);
            if (_woodToPlankRecipe != null) Object.DestroyImmediate(_woodToPlankRecipe);
            if (_itemDbFixture != null) Object.DestroyImmediate(_itemDbFixture);
            if (_recipeRegFixture != null) Object.DestroyImmediate(_recipeRegFixture);
        }

        // ---------------------------------------------------------------
        //  Visibility
        // ---------------------------------------------------------------

        [Test]
        public void Inventory_TogglesVisibilityViaShowAndHide()
        {
            (InventoryUI inv, _, _) = BuildInventory();

            // After Init(), the panel starts hidden.
            Assert.IsFalse(inv.gameObject.activeSelf,
                "InventoryUI should start hidden after Init().");

            inv.Show();
            Assert.IsTrue(inv.gameObject.activeSelf,
                "Show() should activate the panel.");

            inv.Hide();
            Assert.IsFalse(inv.gameObject.activeSelf,
                "Hide() should deactivate the panel.");
        }

        // ---------------------------------------------------------------
        //  Section composition
        // ---------------------------------------------------------------

        [Test]
        public void Inventory_NoBackpackSection()
        {
            (InventoryUI inv, _, _) = BuildInventory();
            inv.Show();

            // Any child or descendant whose name contains "backpack" (any case)
            // would constitute an embedded backpack section. V4.3 removed all
            // such elements — backpacks are V11's side panel.
            foreach (Transform t in inv.GetComponentsInChildren<Transform>(true))
            {
                string lname = t.name.ToLowerInvariant();
                Assert.IsFalse(lname.Contains("backpack"),
                    $"InventoryUI must not contain a 'backpack' element. Found '{t.name}'.");
                Assert.IsFalse(lname.Contains("worn"),
                    $"InventoryUI must not contain a worn-slot element. Found '{t.name}'.");
            }
        }

        [Test]
        public void Inventory_HasHeaderAndCraftSections()
        {
            (InventoryUI inv, _, _) = BuildInventory();
            inv.Show();

            Transform header = FindDescendant(inv.transform, "Header");
            Assert.IsNotNull(header, "InventoryUI should have a Header strip.");

            Transform craftSection = FindDescendant(inv.transform, "PersonalCraftSection");
            Assert.IsNotNull(craftSection, "InventoryUI should have a PersonalCraftSection.");

            // 4 input slots
            Assert.AreEqual(4, inv.PersonalCraftInputs.Count,
                "Personal craft section should have 4 input slots (2x2).");

            // 1 output slot
            Assert.IsNotNull(inv.PersonalCraftOutput,
                "Personal craft section should have an output slot.");
        }

        [Test]
        public void Inventory_HotbarMirrorMatchesActiveHotbar()
        {
            (InventoryUI inv, PlayerInventory pInv, _) = BuildInventory();

            // Place known items in the hotbar
            pInv.Hotbar.SetSlot(0, new ItemStack(_wood, 5));
            pInv.Hotbar.SetSlot(2, new ItemStack(_stone, 1));

            inv.Show();

            Assert.AreEqual(pInv.Hotbar.SlotCount, inv.HotbarMirrorSlots.Count,
                "Hotbar mirror should have the same slot count as PlayerInventory.Hotbar.");

            // Slot 0 should hold wood with count = 5 (count visible)
            TMPro.TMP_Text label0 = FindStackLabel(inv.HotbarMirrorSlots[0]);
            Assert.IsNotNull(label0, "Slot 0 should have a stack label.");
            Assert.AreEqual("5", label0.text,
                "Slot 0 stack label should show 5 when hotbar holds 5 wood.");

            // Slot 2 should hold stone with count hidden (qty == 1)
            TMPro.TMP_Text label2 = FindStackLabel(inv.HotbarMirrorSlots[2]);
            Assert.IsNotNull(label2);
            Assert.IsFalse(label2.enabled,
                "Slot 2 stack label should be hidden when qty == 1 (strict rule).");
            Assert.AreEqual(string.Empty, label2.text,
                "Slot 2 stack text should be empty when qty == 1.");
        }

        [Test]
        public void Inventory_MainGridMatchesActiveMain()
        {
            (InventoryUI inv, PlayerInventory pInv, _) = BuildInventory();

            // Sanity: the main inventory exposes 35 slots (5 x 7) per V4.2 model.
            // Test does not hardcode dimensions — just asserts the UI mirrors
            // whatever PlayerInventory.Main exposes.
            int expected = pInv.Main.SlotCount;

            inv.Show();

            Assert.AreEqual(expected, inv.MainGridSlots.Count,
                $"Main grid should have {expected} slots to mirror PlayerInventory.Main.");
        }

        // ---------------------------------------------------------------
        //  Personal craft output reactivity
        // ---------------------------------------------------------------

        [Test]
        public void PersonalCraft_BuildsOutputWhenInputsMatchRecipe()
        {
            (InventoryUI inv, _, PersonalCraftingGrid pGrid) = BuildInventory();
            inv.Show();

            // Empty grid → no output.
            CraftingOutputSlot output = inv.PersonalCraftOutput;
            Assert.IsNotNull(output);
            Assert.IsTrue(GetStack(output).IsEmpty,
                "Output should be empty when craft inputs are empty.");

            // Fill the 2x2 grid with wood — should match wood_to_plank recipe.
            pGrid.SetSlot(0, 0, new ItemStack(_wood, 1));
            pGrid.SetSlot(1, 0, new ItemStack(_wood, 1));
            pGrid.SetSlot(0, 1, new ItemStack(_wood, 1));
            pGrid.SetSlot(1, 1, new ItemStack(_wood, 1));

            // PersonalCraftingGrid.SetSlot internally updates the cached
            // result. The InventoryUI subscribes to OnGridChanged and pushes
            // the new result into the output slot.
            ItemStack outStack = GetStack(output);
            Assert.IsFalse(outStack.IsEmpty,
                "Output should populate when inputs match a registered recipe.");
            Assert.AreEqual(_plank, outStack.item,
                "Output slot should show the recipe's result item (plank).");
        }

        [Test]
        public void PersonalCraft_OutputClearsWhenInputsBroken()
        {
            (InventoryUI inv, _, PersonalCraftingGrid pGrid) = BuildInventory();
            inv.Show();

            // Set up a valid match first.
            pGrid.SetSlot(0, 0, new ItemStack(_wood, 1));
            pGrid.SetSlot(1, 0, new ItemStack(_wood, 1));
            pGrid.SetSlot(0, 1, new ItemStack(_wood, 1));
            pGrid.SetSlot(1, 1, new ItemStack(_wood, 1));

            Assert.IsFalse(GetStack(inv.PersonalCraftOutput).IsEmpty,
                "Pre-condition: output should be populated when 4 woods match.");

            // Break the recipe by swapping one slot to stone.
            pGrid.SetSlot(1, 1, new ItemStack(_stone, 1));

            Assert.IsTrue(GetStack(inv.PersonalCraftOutput).IsEmpty,
                "Output should clear once the input pattern no longer matches.");
        }

        // ---------------------------------------------------------------
        //  Build helpers
        // ---------------------------------------------------------------

        private (InventoryUI inv, PlayerInventory pInv, PersonalCraftingGrid pGrid) BuildInventory()
        {
            // Player GameObject with PlayerInventory + PersonalCraftingGrid
            var playerGo = new GameObject("Player_Test");
            // Awake() of PlayerInventory builds the inventories and the worn
            // slot, then Start() adds the dev_backpack starter via the
            // ItemDatabase — in EditMode neither runs automatically, so we
            // invoke Awake by hand and skip Start() so the test does not
            // depend on Resources/ItemDatabase being present.
            PlayerInventory pInv = playerGo.AddComponent<PlayerInventory>();
            InvokeMethod(pInv, "Awake");

            PersonalCraftingGrid pGrid = playerGo.AddComponent<PersonalCraftingGrid>();
            InvokeMethod(pGrid, "Awake");

            // InventoryUI sits under the canvas
            var invGo = new GameObject("InventoryPanel_Test", typeof(RectTransform));
            invGo.transform.SetParent(_canvas.transform, false);
            InventoryUI inv = invGo.AddComponent<InventoryUI>();

            // Init() drives build setup. EditMode tests do not auto-invoke
            // Awake on freshly added components, so we go through Init
            // directly. Init internally calls EnsureBuilt() which reads
            // PlayerInventory.Hotbar/Main, so we must Init BEFORE EnsureBuilt
            // is otherwise touched.
            inv.Init(pInv);
            inv.BindPersonalCraftingGrid(pGrid);

            // BuildInventory ensures the canvas + UI scaffold is non-null at
            // the caller. Return tuple so tests can mutate the model.
            return (inv, pInv, pGrid);
        }

        private static ItemDefinition MakeItem(string id, string display, int stack)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.itemId       = id;
            item.displayName  = display;
            item.maxStackSize = stack;
            item.itemType     = ItemType.Resource;
            return item;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static TMPro.TMP_Text FindStackLabel(SlotUI slot)
        {
            Transform label = slot.transform.Find("StackCount");
            return label != null ? label.GetComponent<TMPro.TMP_Text>() : null;
        }

        private static ItemStack GetStack(CraftingOutputSlot output)
        {
            // CraftingOutputSlot stores the stack in a private field; the
            // simplest way to observe it without adding a public getter is
            // to read the icon's enabled state and the count label. But a
            // cleaner read uses reflection on the private _stack field.
            FieldInfo f = typeof(CraftingOutputSlot).GetField(
                "_stack",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, "CraftingOutputSlot._stack field not found via reflection.");
            return (ItemStack)f.GetValue(output);
        }

        private static void InvokeMethod(object target, string name)
        {
            MethodInfo m = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (m != null) m.Invoke(target, null);
        }
    }
}
#endif
