using System;
using System.Collections.Generic;
using System.Linq;
using PulseFactory.Core;
using PulseFactory.Localization;
using PulseFactory.Persistence;
using PulseFactory.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PulseFactory.Presentation
{
    public sealed class LevelSelectionView : MonoBehaviour
    {
        private readonly List<(LevelCatalogEntry Entry, Button Button, TMP_Text Number, TMP_Text Title,
            TMP_Text Detail)> cards = new List<(LevelCatalogEntry, Button, TMP_Text, TMP_Text, TMP_Text)>();
        private readonly List<LevelDefinition> definitions = new List<LevelDefinition>();
        private LevelCatalog catalog;
        private TMP_FontAsset font;
        private RectTransform root;
        private TMP_Text title;
        private TMP_Text subtitle;
        private TMP_Text exportStatus;
        private Button exportButton;
        private Button motionButton;
        private bool rtl;
        private SaveData currentSave = new SaveData();
        private bool currentReducedMotion;
        private string currentExportResultKey = "statsDisclaimer";
        private Action<int> start;
        private Action export;
        private Action motion;

        public void Build(RectTransform parent, TMP_FontAsset fontAsset, LevelCatalog levelCatalog,
            IReadOnlyList<LevelDefinition> levels, Action<int> onStart, Action onExport, Action onMotion)
        {
            font = fontAsset;
            catalog = levelCatalog ?? throw new ArgumentNullException(nameof(levelCatalog));
            definitions.AddRange(levels ?? throw new ArgumentNullException(nameof(levels)));
            start = onStart;
            export = onExport;
            motion = onMotion;

            root = UIFactory.Rect("Level selection", parent, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFactory.Stretch(root);
            title = AddText("Level selection title", 0.85f, 0.95f, 26, UIFactory.White,
                TextAlignmentOptions.Center, true);
            subtitle = AddText("Level selection subtitle", 0.81f, 0.86f, 12, UIFactory.Muted,
                TextAlignmentOptions.Center, false);

            var viewport = UIFactory.Rect("Level list viewport", root, new Vector2(0.06f, 0.19f),
                new Vector2(0.94f, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var maskImage = viewport.gameObject.AddComponent<Image>();
            maskImage.color = new Color(0.055f, 0.08f, 0.12f, 0.35f);
            maskImage.raycastTarget = true;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36;
            var content = UIFactory.Rect("Level list", viewport, new Vector2(0, 1),
                new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            scroll.content = content;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 9, 9);
            layout.spacing = 7;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var entry in catalog.Levels)
            {
                var button = UIFactory.Button("Level " + entry.Number, content, font, "", 13,
                    new Color(0.1f, 0.15f, 0.21f), false, () => start(entry.Number));
                var cardRect = (RectTransform)button.transform;
                var element = button.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 69;
                element.minHeight = 58;
                var number = UIFactory.Text("Level number", button.transform, font, "", 10,
                    UIFactory.Mint, TextAlignmentOptions.Center, false, true);
                var numberRect = number.rectTransform;
                numberRect.anchorMin = new Vector2(0.02f, 0.2f);
                numberRect.anchorMax = new Vector2(0.18f, 0.8f);
                numberRect.offsetMin = numberRect.offsetMax = Vector2.zero;
                var name = UIFactory.Text("Level name", button.transform, font, "", 13,
                    UIFactory.White, TextAlignmentOptions.MidlineLeft, false, true);
                var nameRect = name.rectTransform;
                nameRect.anchorMin = new Vector2(0.2f, 0.48f);
                nameRect.anchorMax = new Vector2(0.96f, 0.95f);
                nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;
                var detail = UIFactory.Text("Level detail", button.transform, font, "", 10,
                    UIFactory.Muted, TextAlignmentOptions.MidlineLeft, false);
                var detailRect = detail.rectTransform;
                detailRect.anchorMin = new Vector2(0.2f, 0.05f);
                detailRect.anchorMax = new Vector2(0.96f, 0.5f);
                detailRect.offsetMin = detailRect.offsetMax = Vector2.zero;
                cards.Add((entry, button, number, name, detail));
            }

            exportButton = UIFactory.Button("Export playtest data", root, font, "", 10,
                new Color(0.14f, 0.3f, 0.29f), false, () => export());
            SetButtonAnchors(exportButton, 0.035f, 0.095f, 0.04f, 0.51f);
            motionButton = UIFactory.Button("Motion setting", root, font, "", 10,
                UIFactory.Raised, false, () => motion());
            SetButtonAnchors(motionButton, 0.035f, 0.095f, 0.54f, 0.96f);
            exportStatus = AddText("Playtest privacy note", 0.1f, 0.135f, 9, UIFactory.Muted,
                TextAlignmentOptions.Center, false);
        }

        public void Refresh(LocalizationCatalog localization, SaveData save, bool reducedMotion, string exportResultKey)
        {
            currentSave = save;
            currentReducedMotion = reducedMotion;
            currentExportResultKey = exportResultKey;
            rtl = localization.RightToLeft;
            title.text = localization.Text("levelSelect");
            subtitle.text = localization.Text("menuSubtitle");
            title.isRightToLeftText = subtitle.isRightToLeftText = rtl;
            exportStatus.text = localization.Text(exportResultKey);
            exportStatus.isRightToLeftText = rtl;
            SetButtonText(exportButton, localization.Text("playtestExport"), rtl);
            SetButtonText(motionButton, localization.Text(reducedMotion ? "motionOff" : "motionOn"), rtl);

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var entry = card.Entry;
                var definition = definitions[i];
                bool unlocked = entry.Number <= save.HighestUnlockedLevel;
                int stars = save.BestResults.FirstOrDefault(item => item.LevelId == entry.LevelId)?.Stars ?? 0;
                card.Button.interactable = unlocked;
                card.Number.text = localization.Text("playtestLevel", entry.Number);
                card.Title.text = localization.Text(entry.TitleKey);
                string detail = localization.Text("levelSummary", definition.Spawns.Count, definition.MaxBeats);
                if (!unlocked) detail += "  ·  " + localization.Text("locked");
                else if (stars > 0) detail += "  ·  " + localization.Text("best", stars);
                card.Detail.text = detail;
                foreach (var label in new[] { card.Number, card.Title, card.Detail })
                    label.isRightToLeftText = rtl;
                card.Title.alignment = rtl ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                card.Detail.alignment = rtl ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                var numberRect = card.Number.rectTransform;
                var titleRect = card.Title.rectTransform;
                var detailRect = card.Detail.rectTransform;
                numberRect.anchorMin = new Vector2(rtl ? 0.82f : 0.02f, 0.2f);
                numberRect.anchorMax = new Vector2(rtl ? 0.98f : 0.18f, 0.8f);
                titleRect.anchorMin = new Vector2(rtl ? 0.04f : 0.2f, 0.48f);
                titleRect.anchorMax = new Vector2(rtl ? 0.8f : 0.96f, 0.95f);
                detailRect.anchorMin = new Vector2(rtl ? 0.04f : 0.2f, 0.05f);
                detailRect.anchorMax = new Vector2(rtl ? 0.8f : 0.96f, 0.5f);
                if (!unlocked)
                {
                    card.Title.color = UIFactory.Muted;
                    card.Number.color = new Color(UIFactory.Muted.r, UIFactory.Muted.g, UIFactory.Muted.b, 0.7f);
                }
                else
                {
                    card.Title.color = UIFactory.White;
                    card.Number.color = UIFactory.Mint;
                }
            }
        }

        public void RefreshExportStatus(LocalizationCatalog localization, string exportResultKey)
        {
            currentExportResultKey = exportResultKey;
            Refresh(localization, currentSave, currentReducedMotion, currentExportResultKey);
        }

        private TMP_Text AddText(string name, float bottom, float top, float size, Color color,
            TextAlignmentOptions alignment, bool isRtl)
        {
            var label = UIFactory.Text(name, root, font, "", size, color, alignment, isRtl, true);
            label.rectTransform.anchorMin = new Vector2(0.05f, bottom);
            label.rectTransform.anchorMax = new Vector2(0.95f, top);
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            return label;
        }

        private static void SetButtonAnchors(Button button, float bottom, float top, float left, float right)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void SetButtonText(Button button, string value, bool rightToLeft)
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            label.text = value;
            label.isRightToLeftText = rightToLeft;
            label.fontSize = value.Length > 20 ? 8 : 10;
        }
    }
}
