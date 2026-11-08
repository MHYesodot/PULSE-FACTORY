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
        public void Build(RectTransform parent, TMP_FontAsset fontAsset)
        {
            font = fontAsset;
            root = UIFactory.Rect("Gameplay HUD", parent, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFactory.Stretch(root);
            title = AddText("Level title", 0.82f, 0.96f, 29, UIFactory.White,
                TextAlignmentOptions.Center, true, 0.04f, 0.72f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 18;
            title.fontSizeMax = 29;
            levelNumber = AddText("Level number", 0.77f, 0.82f, 12, UIFactory.Muted,
                TextAlignmentOptions.Center, true);
            beat = AddText("Beat counter", 0.72f, 0.77f, 15, UIFactory.White,
                TextAlignmentOptions.MidlineLeft, true, 0.06f, 0.5f);
            progress = AddText("Delivery counter", 0.72f, 0.77f, 15, UIFactory.Mint,
                TextAlignmentOptions.MidlineRight, true, 0.5f, 0.94f);
            switches = AddText("Switch status", 0.665f, 0.72f, 13, UIFactory.Purple,
                TextAlignmentOptions.Center, true);
            instructions = AddText("Level instructions", 0.29f, 0.405f, 15, UIFactory.White,
                TextAlignmentOptions.Center, true, 0.05f, 0.95f);
            instructions.enableAutoSizing = true;
            instructions.fontSizeMin = 12;
            instructions.fontSizeMax = 15;
            var statusBackground = UIFactory.Image("Command feedback panel", root,
                new Color(0.1f, 0.16f, 0.22f), true);
            var statusRect = (RectTransform)statusBackground.transform;
            statusRect.anchorMin = new Vector2(0.05f, 0.22f);
            statusRect.anchorMax = new Vector2(0.95f, 0.29f);
            statusRect.offsetMin = statusRect.offsetMax = Vector2.zero;
            status = UIFactory.Text("Command feedback", root, font, "", 13, UIFactory.Muted,
                TextAlignmentOptions.Center, false);
            SetAnchors(status.rectTransform, 0.23f, 0.28f, 0.07f, 0.93f);
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
