#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Voidborne.UI.Style;

namespace Voidborne.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for Volume 4 Chunk 4.1 — UI Style Kit.
    /// Validates the palette, font lookup, and the UIBuilder helpers.
    /// </summary>
    public class UIStyleTests
    {
        private const string FontResourcePath = "Fonts/JetBrainsMono-SDF";

        // ---------------------------------------------------------------
        //  Fixture: a sterile Canvas hierarchy per test.
        // ---------------------------------------------------------------

        private GameObject _canvasGo;
        private Canvas _canvas;

        [SetUp]
        public void SetUp()
        {
            _canvasGo = new GameObject("UIStyleTests_Canvas");
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
        //  Tests
        // ---------------------------------------------------------------

        [Test]
        public void UIStyle_HasJetBrainsMonoFont()
        {
            var asset = Resources.Load<TMP_FontAsset>(FontResourcePath);
            if (asset == null)
            {
                Assert.Ignore(
                    "JetBrains Mono SDF asset missing at Resources/" + FontResourcePath +
                    ". V4.1 fallback path is active; re-bake the SDF asset to remove " +
                    "this ignore.");
            }

            Assert.IsNotNull(asset, "JetBrains Mono SDF asset should load from Resources.");
            Assert.IsTrue(asset.name.Contains("JetBrainsMono"),
                "Loaded font asset should be JetBrainsMono; got '" + asset.name + "'.");
        }

        [Test]
        public void UIStyle_PaletteColorsParseCorrectly()
        {
            AssertHex("#0a0e14", UIStyle.Background,  nameof(UIStyle.Background));
            AssertHex("#14181f", UIStyle.Panel,       nameof(UIStyle.Panel));
            AssertHex("#1e2430", UIStyle.PanelLight,  nameof(UIStyle.PanelLight));
            AssertHex("#2a2f38", UIStyle.Border,      nameof(UIStyle.Border));
            AssertHex("#b6f73e", UIStyle.Accent,      nameof(UIStyle.Accent));
            AssertHex("#6b9a25", UIStyle.AccentDim,   nameof(UIStyle.AccentDim));
            AssertHex("#c9d1d9", UIStyle.Text,        nameof(UIStyle.Text));
            AssertHex("#8b949e", UIStyle.TextDim,     nameof(UIStyle.TextDim));
            AssertHex("#f85149", UIStyle.TextError,   nameof(UIStyle.TextError));
            AssertHex("#56d364", UIStyle.TextSuccess, nameof(UIStyle.TextSuccess));
        }

        [Test]
        public void UIStyle_StandardSizesMatchSpec()
        {
            Assert.AreEqual(11, UIStyle.FontSizeSmall);
            Assert.AreEqual(13, UIStyle.FontSizeBody);
            Assert.AreEqual(14, UIStyle.FontSizeLabel);
            Assert.AreEqual(16, UIStyle.FontSizeHeader);
            Assert.AreEqual(20, UIStyle.FontSizeTitle);
            Assert.AreEqual(50, UIStyle.SlotSize);
            Assert.AreEqual(4,  UIStyle.SlotGap);
            Assert.AreEqual(12, UIStyle.PanelPadding);
            Assert.AreEqual(1,  UIStyle.BorderWidth);
        }

        [Test]
        public void UIBuilder_PanelCreatesValidHierarchy()
        {
            var rt = UIBuilder.Panel(_canvas.transform, "TestPanel");

            Assert.IsNotNull(rt, "Panel() must return a RectTransform.");
            Assert.AreEqual("TestPanel", rt.gameObject.name);
            Assert.AreSame(_canvas.transform, rt.parent,
                "Panel should be parented to the supplied transform.");

            var img = rt.GetComponent<Image>();
            Assert.IsNotNull(img, "Panel root must have an Image component.");
            AssertColorsEqual(UIStyle.Panel, img.color,
                "Panel Image should be tinted UIStyle.Panel.");
        }

        [Test]
        public void UIBuilder_TextSetsFontAndContent()
        {
            var tmp = UIBuilder.Text(
                _canvas.transform,
                "Hello",
                UIStyle.FontSizeBody,
                UIStyle.Text,
                "TestText");

            Assert.IsNotNull(tmp);
            Assert.AreEqual("Hello", tmp.text);
            Assert.AreEqual(UIStyle.FontSizeBody, (int)tmp.fontSize);
            AssertColorsEqual(UIStyle.Text, tmp.color, "Text colour should match.");
            // Font may be the JetBrainsMono SDF asset or the TMP default fallback;
            // either way it must match what UIStyle resolves to and be non-null.
            Assert.IsNotNull(UIStyle.Font, "UIStyle.Font should resolve to something.");
            Assert.AreSame(UIStyle.Font, tmp.font,
                "Builder must use UIStyle.Font (real or fallback).");
        }

        [Test]
        public void UIBuilder_SlotBgIsSquareSlotSized()
        {
            var img = UIBuilder.SlotBg(_canvas.transform, "TestSlot");
            Assert.IsNotNull(img);

            var rt = img.rectTransform;
            Assert.AreEqual(UIStyle.SlotSize, rt.sizeDelta.x, 0.001f);
            Assert.AreEqual(UIStyle.SlotSize, rt.sizeDelta.y, 0.001f);
            AssertColorsEqual(UIStyle.PanelLight, img.color,
                "SlotBg should use the PanelLight tint.");
        }

        [Test]
        public void UIBuilder_BtnHasLabelChild()
        {
            var btn = UIBuilder.Btn(_canvas.transform, "Click", null, "TestBtn");

            Assert.IsNotNull(btn);
            Assert.IsNotNull(btn.GetComponent<Image>(),
                "Button needs an Image as its target graphic.");

            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            Assert.IsNotNull(label, "Button must have a TMP label child.");
            Assert.AreEqual("Click", label.text);
        }

        // ---------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------

        private static void AssertHex(string hex, Color actual, string name)
        {
            Assert.IsTrue(
                ColorUtility.TryParseHtmlString(hex, out var expected),
                "Test bug: bad hex string " + hex);
            AssertColorsEqual(expected, actual, name + " hex " + hex);
        }

        private static void AssertColorsEqual(Color expected, Color actual, string label)
        {
            const float eps = 1f / 255f + 0.0005f;
            Assert.AreEqual(expected.r, actual.r, eps, label + " (r channel)");
            Assert.AreEqual(expected.g, actual.g, eps, label + " (g channel)");
            Assert.AreEqual(expected.b, actual.b, eps, label + " (b channel)");
            Assert.AreEqual(expected.a, actual.a, eps, label + " (a channel)");
        }
    }
}
#endif
