#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.Automation;
using Voidborne.Crafting;
using Voidborne.UI;
using Voidborne.UI.Style;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for Volume 4 Chunk 4.4 — Machine UI Frame.
    ///
    /// Validates the generic MachineUI panel:
    ///   - Open / Close lifecycle with a stub provider.
    ///   - howItWorks prose surface reads from MachineDefinition.howItWorks.
    ///   - processType badge label + colour follows the
    ///     Forgiving/Picky/Hybrid taxonomy.
    ///   - Recipe tabs appear when a machine has more than one recipe.
    ///   - Progress bar fillAmount tracks the provider's Progress value.
    /// </summary>
    public class MachineUITests
    {
        // ---------------------------------------------------------------
        //  Fixture
        // ---------------------------------------------------------------

        private GameObject _canvasGo;
        private Canvas     _canvas;
        private readonly List<Object> _disposables = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _canvasGo = new GameObject("MachineUITests_Canvas");
            _canvas   = _canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
            foreach (Object o in _disposables)
            {
                if (o != null) Object.DestroyImmediate(o);
            }
            _disposables.Clear();
        }

        // ---------------------------------------------------------------
        //  1. Open lifecycle
        // ---------------------------------------------------------------

        [Test]
        public void MachineUI_OpensWithMachineDefinition()
        {
            MachineUI ui = BuildPanel();
            MachineDefinition workbench = MakeMachine(
                id: "workbench",
                display: "Workbench",
                type: MachineProcessType.Hybrid_Crafting,
                gridW: 3, gridH: 3,
                howItWorks: "Crafts most early-game items.");

            StubMachineInputProvider stub = new StubMachineInputProvider(9, 1);

            Assert.IsFalse(ui.IsOpen, "Fresh MachineUI should be closed.");

            ui.Open(workbench, stub);

            Assert.IsTrue(ui.IsOpen,
                "MachineUI should report IsOpen after Open().");
            Assert.IsTrue(ui.gameObject.activeSelf,
                "MachineUI GameObject should be active after Open().");
            Assert.AreEqual(workbench, ui.CurrentMachine,
                "CurrentMachine should reflect the opened MachineDefinition.");
        }

        // ---------------------------------------------------------------
        //  2. howItWorks prose
        // ---------------------------------------------------------------

        [Test]
        public void MachineUI_DisplaysHowItWorksProse()
        {
            MachineUI ui = BuildPanel();
            // Steam Boiler — the canonical V4.4 design surface. The prose must
            // mention "milk" so the player learns the forgiving boiler accepts
            // it as a working fluid.
            MachineDefinition boiler = MakeMachine(
                id: "steam_boiler",
                display: "Steam Boiler",
                type: MachineProcessType.Forgiving_Thermal_Boil,
                gridW: 3, gridH: 3,
                howItWorks: "Heats a working fluid to produce steam. Accepts ANY Liquid_Aqueous as the medium and ANY Combustible_* as fuel. Pure water boils efficiently. Milk works — barely; the fats burn first, the water boils second, the curds clog the outlet.");

            ui.Open(boiler, new StubMachineInputProvider(9, 1));

            Assert.IsNotNull(ui.HowItWorksText,
                "MachineUI should expose the HowItWorks TMP component.");

            string text = ui.HowItWorksText.text ?? string.Empty;
            string lower = text.ToLowerInvariant();
            Assert.IsTrue(lower.Contains("milk"),
                "Steam Boiler howItWorks prose must surface 'milk' — the canonical " +
                "weird-path synergy. Got: " + text);
        }

        // ---------------------------------------------------------------
        //  3. processType badge — label
        // ---------------------------------------------------------------

        [Test]
        public void MachineUI_ShowsProcessTypeBadge()
        {
            MachineUI ui = BuildPanel();
            MachineDefinition forgiving = MakeMachine(
                id: "furnace",
                display: "Furnace",
                type: MachineProcessType.Forgiving_Thermal_DryBurn,
                gridW: 3, gridH: 3,
                howItWorks: "Smelts ore.");

            ui.Open(forgiving, new StubMachineInputProvider(9, 1));

            Assert.IsNotNull(ui.ProcessBadge,
                "MachineUI should expose the process-type badge TMP component.");

            string label = ui.ProcessBadge.text ?? string.Empty;
            Assert.IsTrue(
                label.Contains("Forgiving") || label.Contains("Thermal") || label.Contains("DryBurn"),
                "Process badge label should communicate the processType. Got: " + label);
        }

        // ---------------------------------------------------------------
        //  4. processType badge — colour by family
        // ---------------------------------------------------------------

        [Test]
        public void MachineUI_ProcessTypeBadgeColorMatches()
        {
            // Hybrid → UIStyle.Accent
            MachineUI hybridUi = BuildPanel();
            MachineDefinition workbench = MakeMachine(
                "workbench", "Workbench",
                MachineProcessType.Hybrid_Crafting, 3, 3, "");
            hybridUi.Open(workbench, new StubMachineInputProvider(9, 1));
            AssertColor(UIStyle.Accent, hybridUi.ProcessBadgeColor,
                "Hybrid_Crafting badge should be tinted UIStyle.Accent.");

            // Forgiving → UIStyle.TextSuccess
            MachineUI forgivingUi = BuildPanel();
            MachineDefinition boiler = MakeMachine(
                "steam_boiler", "Steam Boiler",
                MachineProcessType.Forgiving_Thermal_Boil, 3, 3, "");
            forgivingUi.Open(boiler, new StubMachineInputProvider(9, 1));
            AssertColor(UIStyle.TextSuccess, forgivingUi.ProcessBadgeColor,
                "Forgiving_* badge should be tinted UIStyle.TextSuccess.");

            // Picky → UIStyle.TextError
            MachineUI pickyUi = BuildPanel();
            MachineDefinition turret = MakeMachine(
                "auto_turret", "Auto Turret",
                MachineProcessType.Picky_Specialty, 3, 3, "");
            pickyUi.Open(turret, new StubMachineInputProvider(9, 1));
            AssertColor(UIStyle.TextError, pickyUi.ProcessBadgeColor,
                "Picky_* badge should be tinted UIStyle.TextError.");
        }

        // ---------------------------------------------------------------
        //  5. Recipe tabs for multi-recipe machines
        // ---------------------------------------------------------------

        [Test]
        public void MachineUI_RecipeTabsBuildForMultiRecipeMachines()
        {
            // Build a synthetic RecipeTabsUI fixture directly. RecipeRegistry.Instance
            // is a singleton ScriptableObject and resetting it across tests is
            // fragile, so this test exercises the tab strip's Build path with
            // a hand-rolled recipe list — which is what MachineUI's
            // RebuildRecipeTabs ultimately calls when the machine has > 1 recipe.

            GameObject host = new GameObject("RecipeTabsHost", typeof(RectTransform));
            host.transform.SetParent(_canvas.transform, false);
            RecipeTabsUI tabs = host.AddComponent<RecipeTabsUI>();
            _disposables.Add(host);

            RecipeDefinition r1 = MakeRecipe("plank", "wood_to_plank");
            RecipeDefinition r2 = MakeRecipe("nail",  "wood_to_nail");
            RecipeDefinition r3 = MakeRecipe("arrow_shaft", "wood_to_shaft");

            tabs.Build(new List<RecipeDefinition> { r1, r2, r3 });

            Assert.AreEqual(3, tabs.Tabs.Count,
                "RecipeTabsUI.Build should produce one button per recipe.");
            Assert.AreEqual(r1, tabs.Selected,
                "First recipe should be the default selection after Build.");
        }

        // ---------------------------------------------------------------
        //  6. Progress bar fillAmount tracks provider state
        // ---------------------------------------------------------------

        [Test]
        public void MachineUI_ProgressBarUpdatesFromProvider()
        {
            MachineUI ui = BuildPanel();
            MachineDefinition crusher = MakeMachine(
                "crusher", "Crusher",
                MachineProcessType.Forgiving_Mechanical_Crush,
                3, 3,
                "Grinds solids into powder.");

            StubMachineInputProvider stub = new StubMachineInputProvider(9, 1);
            ui.Open(crusher, stub);

            Assert.IsNotNull(ui.ProgressFill,
                "MachineUI should expose its progress fill Image.");

            // Default fillAmount must be ~0 before progress starts.
            Assert.AreEqual(0f, ui.ProgressFill.fillAmount, 0.001f,
                "Progress should default to 0 before any work.");

            stub.SetProgress(0.5f);

            Assert.AreEqual(0.5f, ui.ProgressFill.fillAmount, 0.001f,
                "Progress fillAmount should track IMachineInputProvider.Progress.");

            stub.SetProgress(1.0f);
            Assert.AreEqual(1.0f, ui.ProgressFill.fillAmount, 0.001f,
                "Progress fillAmount should clamp/track to 1.0 at completion.");
        }

        // ---------------------------------------------------------------
        //  7. Close
        // ---------------------------------------------------------------

        [Test]
        public void MachineUI_ClosesOnCloseCall()
        {
            MachineUI ui = BuildPanel();
            MachineDefinition workbench = MakeMachine(
                "workbench", "Workbench",
                MachineProcessType.Hybrid_Crafting, 3, 3,
                "Crafts most early-game items.");

            ui.Open(workbench, new StubMachineInputProvider(9, 1));
            Assert.IsTrue(ui.IsOpen, "Pre-condition: panel should be open.");

            ui.Close();
            Assert.IsFalse(ui.IsOpen,
                "MachineUI should report !IsOpen after Close().");
            Assert.IsFalse(ui.gameObject.activeSelf,
                "MachineUI GameObject should be inactive after Close().");
            Assert.IsNull(ui.CurrentMachine,
                "CurrentMachine should be null after Close().");
        }

        // ---------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------

        private MachineUI BuildPanel()
        {
            GameObject go = new GameObject("MachineUI_Test", typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            MachineUI ui = go.AddComponent<MachineUI>();
            // EditMode does not auto-invoke Awake on AddComponent'd
            // MonoBehaviours, so drive build setup directly.
            ui.EnsureBuilt();
            return ui;
        }

        private MachineDefinition MakeMachine(
            string id, string display, MachineProcessType type,
            int gridW, int gridH, string howItWorks)
        {
            MachineDefinition def = ScriptableObject.CreateInstance<MachineDefinition>();
            def.itemId       = id;
            def.displayName  = display;
            def.processType  = type;
            def.gridWidth    = gridW;
            def.gridHeight   = gridH;
            def.howItWorks   = howItWorks;
            _disposables.Add(def);
            return def;
        }

        private RecipeDefinition MakeRecipe(string outputId, string assetName)
        {
            RecipeDefinition r = ScriptableObject.CreateInstance<RecipeDefinition>();
            r.name = assetName;
            r.outputItemId = outputId;
            r.outputQty    = 1;
            r.efficiency   = 1f;
            r.outputModifier = 1f;
            _disposables.Add(r);
            return r;
        }

        private static void AssertColor(Color expected, Color actual, string message)
        {
            const float tol = 0.005f;
            Assert.AreEqual(expected.r, actual.r, tol, message + " (r channel)");
            Assert.AreEqual(expected.g, actual.g, tol, message + " (g channel)");
            Assert.AreEqual(expected.b, actual.b, tol, message + " (b channel)");
        }
    }
}
#endif
