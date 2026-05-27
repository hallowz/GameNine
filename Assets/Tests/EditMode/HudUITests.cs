#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.UI;
using Voidborne.UI.Style;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for Volume 4 Chunk 4.2 — HUD Layout.
    ///
    /// Validates the four-bar HUD (health/stamina/temperature/corruption)
    /// and the hotbar stack-count visibility rule. These tests construct a
    /// sterile Canvas + HudUI / SlotUI per case so there is no shared state.
    /// </summary>
    public class HudUITests
    {
        // ---------------------------------------------------------------
        //  Fixture
        // ---------------------------------------------------------------

        private GameObject _canvasGo;
        private Canvas _canvas;

        [SetUp]
        public void SetUp()
        {
            _canvasGo = new GameObject("HudUITests_Canvas");
            _canvas = _canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null)
            {
                Object.DestroyImmediate(_canvasGo);
            }
        }

        // ---------------------------------------------------------------
        //  HudUI tests
        // ---------------------------------------------------------------

        [Test]
        public void Hud_HealthBarUpdatesOnSetHealth()
        {
            HudUI hud = BuildHud();
            hud.SetHealth(50f, 100f);

            Image fill = FindFill(hud, "HealthBar");
            Assert.IsNotNull(fill, "HealthBar fill image not found.");
            Assert.AreEqual(0.5f, fill.fillAmount, 0.001f,
                "HealthBar fillAmount should be 0.5 after SetHealth(50, 100).");
            Assert.AreEqual(50f, hud.HealthCurrent, 0.001f);
            Assert.AreEqual(100f, hud.HealthMax, 0.001f);
        }

        [Test]
        public void Hud_StaminaBarUpdatesOnSetStamina()
        {
            HudUI hud = BuildHud();
            hud.SetStamina(25f, 100f);

            Image fill = FindFill(hud, "StaminaBar");
            Assert.IsNotNull(fill, "StaminaBar fill image not found.");
            Assert.AreEqual(0.25f, fill.fillAmount, 0.001f,
                "StaminaBar fillAmount should be 0.25 after SetStamina(25, 100).");
            Assert.AreEqual(25f, hud.StaminaCurrent, 0.001f);
        }

        [Test]
        public void Hud_TemperatureMarkerPositionsCorrectly()
        {
            HudUI hud = BuildHud();

            // Cold (-20°C): below minSafe -10 → pinned to left (normalized 0).
            hud.SetTemperature(-20f);
            Assert.AreEqual(0f, hud.TemperatureMarkerNormalized, 0.001f,
                "Cold temperature should pin the marker to the left.");

            // Comfortable (midpoint of -10..40 is 15°C): normalized 0.5.
            hud.SetTemperature(15f);
            Assert.AreEqual(0.5f, hud.TemperatureMarkerNormalized, 0.001f,
                "Midpoint temperature should put the marker at centre.");

            // Hot (50°C): above maxSafe 40 → pinned to right (normalized 1).
            hud.SetTemperature(50f);
            Assert.AreEqual(1f, hud.TemperatureMarkerNormalized, 0.001f,
                "Hot temperature should pin the marker to the right.");
        }

        [Test]
        public void Hud_CorruptionDefaultsToZero()
        {
            HudUI hud = BuildHud();

            Assert.AreEqual(0f, hud.CorruptionCurrent, 0.001f,
                "Fresh HudUI should report 0 corruption.");
            Assert.AreEqual(100f, hud.CorruptionMax, 0.001f,
                "Fresh HudUI should report 100 corruption max.");

            Image fill = FindFill(hud, "CorruptionBar");
            Assert.IsNotNull(fill, "CorruptionBar fill image not found.");
            Assert.AreEqual(0f, fill.fillAmount, 0.001f,
                "Corruption fill should be 0 at default.");
        }

        [Test]
        public void Hud_HealthAndStaminaDefaultToFull()
        {
            HudUI hud = BuildHud();

            Assert.AreEqual(100f, hud.HealthCurrent, 0.001f);
            Assert.AreEqual(100f, hud.StaminaCurrent, 0.001f);

            Image healthFill  = FindFill(hud, "HealthBar");
            Image staminaFill = FindFill(hud, "StaminaBar");
            Assert.AreEqual(1f, healthFill.fillAmount, 0.001f);
            Assert.AreEqual(1f, staminaFill.fillAmount, 0.001f);
        }

        // ---------------------------------------------------------------
        //  Hotbar / SlotUI stack-count test
        // ---------------------------------------------------------------

        [Test]
        public void Hotbar_StackCountHiddenWhenOne()
        {
            // Make sure ItemIconGenerator's static cache won't crash on a
            // brand-new item by giving it an icon-less ItemDefinition. SlotUI
            // calls ItemIconGenerator.GetIcon during Refresh; that path
            // tolerates a null icon and returns null.
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.itemId       = "test_item";
            item.displayName  = "Test Item";
            item.maxStackSize = 99;

            Inventory inv = new Inventory(1, 1);
            inv.SetSlot(0, new ItemStack(item, 1));

            GameObject slotGo = new GameObject("TestSlot", typeof(RectTransform), typeof(Image));
            slotGo.transform.SetParent(_canvas.transform, false);
            SlotUI slot = slotGo.AddComponent<SlotUI>();
            slot.Init(inv, 0);

            TMP_Text stack = FindStackLabel(slot);
            Assert.IsNotNull(stack, "SlotUI should have a StackCount label.");
            Assert.IsFalse(stack.enabled,
                "Stack count label should be disabled when quantity == 1.");
            Assert.AreEqual(string.Empty, stack.text,
                "Stack count text should be empty when quantity == 1.");

            // Sanity check: with quantity > 1 the label becomes visible.
            inv.SetSlot(0, new ItemStack(item, 5));
            slot.Refresh();
            Assert.IsTrue(stack.enabled, "Stack count should show when quantity > 1.");
            Assert.AreEqual("5", stack.text);

            Object.DestroyImmediate(item);
        }

        // ---------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------

        private HudUI BuildHud()
        {
            GameObject go = new GameObject("HudUI_Test", typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            HudUI hud = go.AddComponent<HudUI>();
            // EditMode tests do not automatically invoke MonoBehaviour.Awake on
            // freshly AddComponent'd components, so drive build setup directly.
            hud.EnsureBuilt();
            return hud;
        }

        private static Image FindFill(HudUI hud, string rowName)
        {
            Transform row = hud.transform.Find(rowName);
            if (row == null) return null;
            Transform bg = row.Find(rowName + "_Bg");
            if (bg == null) return null;
            Transform fill = bg.Find(rowName + "_Fill");
            return fill != null ? fill.GetComponent<Image>() : null;
        }

        private static TMP_Text FindStackLabel(SlotUI slot)
        {
            Transform label = slot.transform.Find("StackCount");
            return label != null ? label.GetComponent<TMP_Text>() : null;
        }
    }
}
#endif
