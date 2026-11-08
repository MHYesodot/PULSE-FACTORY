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
        private TMP_Text beat;
        private TMP_Text progress;
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
            title = AddText("Level title", 0.89f, 0.96f, 27, UIFactory.White,
                TextAlignmentOptions.Center, true, 0.04f, 0.75f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 18;
            title.fontSizeMax = 27;
            levelNumber = AddText("Level number", 0.855f, 0.89f, 12, UIFactory.Muted,
                TextAlignmentOptions.Center, true);
            var metrics = UIFactory.Image("Gameplay counters panel", root,
                new Color(0.1f, 0.15f, 0.22f, 0.88f), true);
            SetAnchors((RectTransform)metrics.transform, 0.795f, 0.845f, 0.055f, 0.945f);
            beat = AddText("Beat counter", 0.795f, 0.845f, 15, UIFactory.White,
                TextAlignmentOptions.MidlineLeft, true, 0.085f, 0.5f);
            progress = AddText("Delivery counter", 0.795f, 0.845f, 15, UIFactory.Mint,
                TextAlignmentOptions.MidlineRight, true, 0.5f, 0.915f);
            var switchPanel = UIFactory.Image("Switch status panel", root,
                new Color(0.17f, 0.14f, 0.27f, 0.82f), true);
            SetAnchors((RectTransform)switchPanel.transform, 0.75f, 0.79f, 0.17f, 0.83f);
            switches = AddText("Switch status", 0.75f, 0.79f, 13, UIFactory.Purple,
                TextAlignmentOptions.Center, true, 0.18f, 0.82f);
            instructions = AddText("Level instructions", 0.695f, 0.75f, 14, UIFactory.White,
                TextAlignmentOptions.Center, true, 0.05f, 0.95f);
            instructions.enableAutoSizing = true;
            instructions.fontSizeMin = 11;
            instructions.fontSizeMax = 14;
            var statusBackground = UIFactory.Image("Command feedback panel", root,
                new Color(0.1f, 0.16f, 0.22f), true);
            var statusRect = (RectTransform)statusBackground.transform;
            SetAnchors(statusRect, 0.265f, 0.295f, 0.1f, 0.9f);
            this.statusBackground = statusBackground.gameObject;
            status = UIFactory.Text("Command feedback", root, font, "", 13, UIFactory.Muted,
                TextAlignmentOptions.Center, false);
            SetAnchors(status.rectTransform, 0.265f, 0.295f, 0.1f, 0.9f);
        }

        public void Refresh(LevelCatalogEntry entry, LevelDefinition level, SimulationState state,
            LocalizationCatalog text, string feedbackKey, object[] feedbackArgs, bool animating)
        {
            title.text = text.Text(entry.TitleKey);
            title.isRightToLeftText = text.RightToLeft;
            levelNumber.text = text.Text("playtestLevel", entry.Number);
            levelNumber.isRightToLeftText = text.RightToLeft;
            beat.text = text.Text("beat", state.Beat, level.MaxBeats);
            progress.text = text.Text("progress", state.Deliveries.Count, level.Spawns.Count);
            switches.text = text.Text("switches", state.Switches.A ? text.Text("switchOn") : text.Text("switchOff"),
                state.Switches.B ? text.Text("switchOn") : text.Text("switchOff"));
            instructions.text = text.Text(entry.InstructionKey);
            status.text = text.Text(feedbackKey, feedbackArgs);
            status.isRightToLeftText = text.RightToLeft;
            instructions.isRightToLeftText = text.RightToLeft;
            beat.isRightToLeftText = text.RightToLeft;
            progress.isRightToLeftText = text.RightToLeft;
            switches.isRightToLeftText = text.RightToLeft;
            status.color = feedbackKey.StartsWith("failure", System.StringComparison.Ordinal)
                ? UIFactory.Amber : animating ? UIFactory.Mint : UIFactory.Muted;
            bool showStatus = animating || feedbackKey.StartsWith("failure", System.StringComparison.Ordinal);
            status.gameObject.SetActive(showStatus);
            statusBackground.SetActive(showStatus);
        }

        public void SetLocalization(LocalizationCatalog text)
        {
            foreach (var label in GetComponentsInChildren<TMP_Text>(true))
                label.isRightToLeftText = text.RightToLeft;
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
