using System.Globalization;
using PulseFactory.Core;
using PulseFactory.Localization;
using PulseFactory.UI;
using TMPro;
using UnityEngine;

namespace PulseFactory.Presentation
{
    public sealed class GameplayHudView : MonoBehaviour
    {
        private TMP_FontAsset font;
        private RectTransform root;
        private TMP_Text title;
        private TMP_Text levelNumber;
        private TMP_Text beatLabel;
        private TMP_Text beatValue;
        private TMP_Text progressLabel;
        private TMP_Text progressValue;
        private TMP_Text switches;
        private TMP_Text instructions;
        private TMP_Text status;
        private GameObject statusBackground;
        public void Build(RectTransform parent, TMP_FontAsset fontAsset)
        {
            font = fontAsset;
            root = UIFactory.Rect("Gameplay HUD", parent, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFactory.Stretch(root);
            title = AddText("Level title", 0.94f, 0.98f, 27, UIFactory.White,
                TextAlignmentOptions.Center, true, 0.04f, 0.75f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 18;
            title.fontSizeMax = 27;
            levelNumber = AddText("Level number", 0.915f, 0.94f, 12, UIFactory.Muted,
                TextAlignmentOptions.Center, true);
            var metrics = UIFactory.Image("Gameplay counters panel", root,
                new Color(0.1f, 0.15f, 0.22f, 0.88f), true);
            SetAnchors((RectTransform)metrics.transform, 0.86f, 0.905f, 0.055f, 0.945f);
            beatLabel = AddText("Beat label", 0.86f, 0.905f, 12, UIFactory.Muted,
                TextAlignmentOptions.MidlineLeft, true, 0.05f, 0.22f);
            beatValue = AddText("Beat count", 0.86f, 0.905f, 15, UIFactory.White,
                TextAlignmentOptions.MidlineRight, false, 0.22f, 0.48f);
            progressLabel = AddText("Delivery label", 0.86f, 0.905f, 12, UIFactory.Muted,
                TextAlignmentOptions.MidlineLeft, true, 0.52f, 0.72f);
            progressValue = AddText("Delivery count", 0.86f, 0.905f, 15, UIFactory.Mint,
                TextAlignmentOptions.MidlineRight, false, 0.72f, 0.95f);
            var switchPanel = UIFactory.Image("Switch status panel", root,
                new Color(0.17f, 0.14f, 0.27f, 0.82f), true);
            SetAnchors((RectTransform)switchPanel.transform, 0.825f, 0.85f, 0.17f, 0.83f);
            switches = AddText("Switch status", 0.825f, 0.85f, 13, UIFactory.Purple,
                TextAlignmentOptions.Center, true, 0.18f, 0.82f);
            instructions = AddText("Level instructions", 0.78f, 0.82f, 12, UIFactory.White,
                TextAlignmentOptions.Center, true, 0.05f, 0.95f);
            instructions.enableAutoSizing = true;
            instructions.fontSizeMin = 9;
            instructions.fontSizeMax = 12;
            var statusBackground = UIFactory.Image("Command feedback panel", root,
                new Color(0.1f, 0.16f, 0.22f), true);
            var statusRect = (RectTransform)statusBackground.transform;
            SetAnchors(statusRect, 0.185f, 0.21f, 0.1f, 0.9f);
            this.statusBackground = statusBackground.gameObject;
            status = UIFactory.Text("Command feedback", root, font, "", 13, UIFactory.Muted,
                TextAlignmentOptions.Center, false);
            SetAnchors(status.rectTransform, 0.185f, 0.21f, 0.1f, 0.9f);
        }

        public void Refresh(LevelCatalogEntry entry, LevelDefinition level, SimulationState state,
            LocalizationCatalog text, string feedbackKey, object[] feedbackArgs, bool animating,
            int? displayedDeliveries = null)
        {
            title.text = text.Text(entry.TitleKey);
            UIFactory.SetTextDirection(title, text.RightToLeft);
            levelNumber.text = text.Text("playtestLevel", entry.Number);
            UIFactory.SetTextDirection(levelNumber, text.RightToLeft);
            beatLabel.text = text.Text("beatLabel");
            beatValue.text = string.Format(CultureInfo.InvariantCulture, "{0} / {1}", state.Beat, level.MaxBeats);
            progressLabel.text = text.Text("progressLabel");
            progressValue.text = string.Format(CultureInfo.InvariantCulture, "{0} / {1}",
                displayedDeliveries ?? state.Deliveries.Count, level.Spawns.Count);
            switches.text = text.Text("switches", state.Switches.A ? text.Text("switchOn") : text.Text("switchOff"),
                state.Switches.B ? text.Text("switchOn") : text.Text("switchOff"));
            instructions.text = text.Text(entry.InstructionKey);
            status.text = text.Text(feedbackKey, feedbackArgs);
            foreach (var label in new[] { status, instructions, beatLabel, progressLabel, switches })
                UIFactory.SetTextDirection(label, text.RightToLeft);
            UIFactory.SetTextDirection(beatValue, false);
            UIFactory.SetTextDirection(progressValue, false);
            LayoutMetrics(text.RightToLeft);
            status.color = feedbackKey.StartsWith("failure", System.StringComparison.Ordinal)
                ? UIFactory.Amber : animating ? UIFactory.Mint : UIFactory.Muted;
            bool showStatus = animating || feedbackKey.StartsWith("failure", System.StringComparison.Ordinal);
            status.gameObject.SetActive(showStatus);
            statusBackground.SetActive(showStatus);
        }

        public void SetLocalization(LocalizationCatalog text)
        {
            foreach (var label in GetComponentsInChildren<TMP_Text>(true))
                UIFactory.SetTextDirection(label, text.RightToLeft);
            UIFactory.SetTextDirection(beatValue, false);
            UIFactory.SetTextDirection(progressValue, false);
            LayoutMetrics(text.RightToLeft);
        }

        private void LayoutMetrics(bool rtl)
        {
            SetMetric(beatLabel, rtl ? 0.27f : 0.05f, rtl ? 0.48f : 0.22f,
                rtl ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
            SetMetric(beatValue, rtl ? 0.05f : 0.22f, rtl ? 0.25f : 0.48f,
                TextAlignmentOptions.Midline);
            SetMetric(progressLabel, rtl ? 0.74f : 0.52f, rtl ? 0.95f : 0.72f,
                rtl ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
            SetMetric(progressValue, rtl ? 0.52f : 0.72f, rtl ? 0.72f : 0.95f,
                TextAlignmentOptions.Midline);
        }

        private static void SetMetric(TMP_Text label, float left, float right, TextAlignmentOptions alignment)
        {
            label.rectTransform.anchorMin = new Vector2(left, 0.86f);
            label.rectTransform.anchorMax = new Vector2(right, 0.905f);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.alignment = alignment;
        }

        private TMP_Text AddText(string name, float bottom, float top, float size, Color color,
            TextAlignmentOptions alignment, bool rtl, float left = 0.05f, float right = 0.95f)
        {
            var value = UIFactory.Text(name, root, font, "", size, color, alignment, rtl, true);
            SetAnchors(value.rectTransform, bottom, top, left, right);
            return value;
        }

        private static void SetAnchors(RectTransform rect, float bottom, float top, float left, float right)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
