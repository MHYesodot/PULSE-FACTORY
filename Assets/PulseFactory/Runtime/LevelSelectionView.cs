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
            TMP_Text Detail, Image[] Stars, Image Badge, Image Accent, Outline Border)> cards =
            new List<(LevelCatalogEntry, Button, TMP_Text, TMP_Text, TMP_Text, Image[], Image, Image, Outline)>();
        private readonly List<LevelDefinition> definitions = new List<LevelDefinition>();
        private LevelCatalog catalog;
        private TMP_FontAsset font;
        private RectTransform root;
        private TMP_Text title;
        private TMP_Text subtitle;
        private TMP_Text routeCount;
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
            title = AddText("Factory title", 0.89f, 0.96f, 27, UIFactory.White,
                TextAlignmentOptions.Center, true);
            title.rectTransform.anchorMin = new Vector2(0.04f, 0.89f);
            title.rectTransform.anchorMax = new Vector2(0.75f, 0.96f);
            title.enableAutoSizing = true;
            title.fontSizeMin = 20;
            title.fontSizeMax = 27;
            subtitle = AddText("Factory subtitle", 0.84f, 0.89f, 12, UIFactory.Muted,
                TextAlignmentOptions.Center, false);
            AddText("Level selection title", 0.765f, 0.81f, 19, UIFactory.Mint,
                TextAlignmentOptions.MidlineLeft, true).name = "Route heading";
            routeCount = AddText("Level progress", 0.765f, 0.81f, 12, UIFactory.Muted,
                TextAlignmentOptions.MidlineRight, true);

            var viewport = UIFactory.Rect("Level list viewport", root, new Vector2(0.04f, 0.17f),
                new Vector2(0.96f, 0.75f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var maskImage = viewport.gameObject.AddComponent<Image>();
            maskImage.sprite = UIFactory.RoundedSprite;
            maskImage.type = Image.Type.Sliced;
            maskImage.color = new Color(0.07f, 0.1f, 0.15f, 0.92f);
            maskImage.raycastTarget = true;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
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
            layout.padding = new RectOffset(8, 8, 10, 10);
            layout.spacing = 8;
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
                UIFactory.Shadow(button.image, new Color(0, 0, 0, 0.25f), new Vector2(0, -1), 3);
                var border = button.gameObject.AddComponent<Outline>();
                border.effectColor = UIFactory.Steel;
                border.effectDistance = new Vector2(1, -1);
                var accent = UIFactory.Image("Card machine rail", button.transform, UIFactory.Steel, true);
                var accentRect = (RectTransform)accent.transform;
                accentRect.anchorMin = new Vector2(0.015f, 0.18f);
                accentRect.anchorMax = new Vector2(0.03f, 0.82f);
                accentRect.offsetMin = accentRect.offsetMax = Vector2.zero;
                var element = button.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 78;
                element.minHeight = 68;
                var badge = UIFactory.Circle("Level number badge", button.transform,
                    new Color(UIFactory.Mint.r, UIFactory.Mint.g, UIFactory.Mint.b, 0.15f));
                var badgeRect = (RectTransform)badge.transform;
                badgeRect.anchorMin = new Vector2(0.035f, 0.14f);
                badgeRect.anchorMax = new Vector2(0.16f, 0.86f);
                badgeRect.offsetMin = badgeRect.offsetMax = Vector2.zero;
                var number = UIFactory.Text("Level number", button.transform, font, "", 13,
                    UIFactory.Mint, TextAlignmentOptions.Center, false, true);
                var numberRect = number.rectTransform;
                numberRect.anchorMin = new Vector2(0.035f, 0.14f);
                numberRect.anchorMax = new Vector2(0.16f, 0.86f);
                numberRect.offsetMin = numberRect.offsetMax = Vector2.zero;
                var name = UIFactory.Text("Level name", button.transform, font, "", 13,
                    UIFactory.White, TextAlignmentOptions.MidlineLeft, false, true);
                var nameRect = name.rectTransform;
                nameRect.anchorMin = new Vector2(0.19f, 0.47f);
                nameRect.anchorMax = new Vector2(0.7f, 0.94f);
                nameRect.offsetMin = nameRect.offsetMax = Vector2.zero;
                var detail = UIFactory.Text("Level detail", button.transform, font, "", 11,
                    UIFactory.Muted, TextAlignmentOptions.MidlineLeft, false);
                var detailRect = detail.rectTransform;
                detailRect.anchorMin = new Vector2(0.19f, 0.04f);
                detailRect.anchorMax = new Vector2(0.96f, 0.48f);
                detailRect.offsetMin = detailRect.offsetMax = Vector2.zero;
                var stars = new Image[3];
                for (int starIndex = 0; starIndex < stars.Length; starIndex++)
                {
                    stars[starIndex] = UIFactory.Star("Progress star " + (starIndex + 1), button.transform,
                        starIndex == 0 ? UIFactory.Amber : new Color(0.33f, 0.4f, 0.49f));
                    var starRect = (RectTransform)stars[starIndex].transform;
                    starRect.anchorMin = starRect.anchorMax = new Vector2(0.77f + starIndex * 0.065f, 0.72f);
                    starRect.sizeDelta = new Vector2(16, 16);
                    starRect.anchoredPosition = Vector2.zero;
                }
                cards.Add((entry, button, number, name, detail, stars, badge, accent, border));
            }

            exportButton = UIFactory.Button("Export playtest data", root, font, "", 10,
                new Color(0.14f, 0.3f, 0.29f), false, () => export());
            SetButtonAnchors(exportButton, 0.025f, 0.105f, 0.04f, 0.51f);
            motionButton = UIFactory.Button("Motion setting", root, font, "", 10,
                UIFactory.Raised, false, () => motion());
            SetButtonAnchors(motionButton, 0.025f, 0.105f, 0.54f, 0.96f);
            exportStatus = AddText("Playtest privacy note", 0.11f, 0.15f, 10, UIFactory.Muted,
                TextAlignmentOptions.Center, false);
        }

        public void Refresh(LocalizationCatalog localization, SaveData save, bool reducedMotion, string exportResultKey)
        {
            currentSave = save;
            currentReducedMotion = reducedMotion;
            currentExportResultKey = exportResultKey;
            rtl = localization.RightToLeft;
            title.text = localization.Text("title");
            subtitle.text = localization.Text("menuSubtitle");
            var routeHeading = root.Find("Route heading").GetComponent<TMP_Text>();
            routeHeading.text = localization.Text("levelSelect");
            UIFactory.SetTextDirection(routeHeading, rtl);
            routeHeading.alignment = rtl ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            routeHeading.rectTransform.anchorMin = new Vector2(rtl ? 0.5f : 0.05f, 0.765f);
            routeHeading.rectTransform.anchorMax = new Vector2(rtl ? 0.95f : 0.5f, 0.81f);
            routeCount.text = localization.Text("levelCounter", save.HighestUnlockedLevel, catalog.Levels.Count);
            UIFactory.SetTextDirection(routeCount, rtl);
            routeCount.alignment = rtl ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
            routeCount.rectTransform.anchorMin = new Vector2(rtl ? 0.05f : 0.5f, 0.765f);
            routeCount.rectTransform.anchorMax = new Vector2(rtl ? 0.5f : 0.95f, 0.81f);
            SetButtonAnchors(exportButton, 0.025f, 0.105f, rtl ? 0.54f : 0.04f, rtl ? 0.96f : 0.51f);
            SetButtonAnchors(motionButton, 0.025f, 0.105f, rtl ? 0.04f : 0.54f, rtl ? 0.51f : 0.96f);
            UIFactory.SetTextDirection(title, rtl);
            UIFactory.SetTextDirection(subtitle, rtl);
            exportStatus.text = localization.Text(exportResultKey);
            UIFactory.SetTextDirection(exportStatus, rtl);
            SetButtonText(exportButton, localization.Text("playtestExport"), rtl);
            SetButtonText(motionButton, localization.Text(reducedMotion ? "motionOff" : "motionOn"), rtl);
            exportButton.GetComponent<ButtonPressFeedback>().SetMotionEnabled(!reducedMotion);
            motionButton.GetComponent<ButtonPressFeedback>().SetMotionEnabled(!reducedMotion);

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var entry = card.Entry;
                var definition = definitions[i];
                bool unlocked = entry.Number <= save.HighestUnlockedLevel;
                int stars = save.BestResults.FirstOrDefault(item => item.LevelId == entry.LevelId)?.Stars ?? 0;
                card.Button.interactable = unlocked;
                card.Button.GetComponent<ButtonPressFeedback>().SetMotionEnabled(!reducedMotion);
                card.Number.text = localization.Text("playtestLevel", entry.Number);
                card.Title.text = localization.Text(entry.TitleKey);
                string detail = unlocked
                    ? localization.Text("levelSummary", definition.Spawns.Count, definition.MaxBeats)
                    : localization.Text("locked");
                if (stars > 0) detail = localization.Text("completed") + "  ·  " + detail;
                card.Detail.text = detail;
                foreach (var label in new[] { card.Number, card.Title, card.Detail })
                    UIFactory.SetTextDirection(label, rtl);
                card.Title.alignment = rtl ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                card.Detail.alignment = rtl ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                var numberRect = card.Number.rectTransform;
                var titleRect = card.Title.rectTransform;
                var detailRect = card.Detail.rectTransform;
                var badgeRect = (RectTransform)card.Badge.transform;
                float badgeLeft = rtl ? 0.84f : 0.035f;
                float badgeRight = rtl ? 0.965f : 0.16f;
                numberRect.anchorMin = badgeRect.anchorMin = new Vector2(badgeLeft, 0.14f);
                numberRect.anchorMax = badgeRect.anchorMax = new Vector2(badgeRight, 0.86f);
                titleRect.anchorMin = new Vector2(rtl ? 0.3f : 0.19f, 0.47f);
                titleRect.anchorMax = new Vector2(rtl ? 0.81f : 0.7f, 0.94f);
                detailRect.anchorMin = new Vector2(rtl ? 0.04f : 0.19f, 0.04f);
                detailRect.anchorMax = new Vector2(rtl ? 0.81f : 0.96f, 0.48f);
                for (int starIndex = 0; starIndex < card.Stars.Length; starIndex++)
                {
                    var starRect = (RectTransform)card.Stars[starIndex].transform;
                    float x = rtl ? 0.23f - starIndex * 0.065f : 0.77f + starIndex * 0.065f;
                    starRect.anchorMin = starRect.anchorMax = new Vector2(x, 0.72f);
                    card.Stars[starIndex].color = starIndex < stars
                        ? UIFactory.Amber : new Color(0.33f, 0.4f, 0.49f, 0.72f);
                }
                if (!unlocked)
                {
                    card.Title.color = UIFactory.Muted;
                    card.Number.color = new Color(UIFactory.Muted.r, UIFactory.Muted.g, UIFactory.Muted.b, 0.7f);
                    card.Badge.color = new Color(UIFactory.Muted.r, UIFactory.Muted.g, UIFactory.Muted.b, 0.12f);
                    card.Button.image.color = new Color(0.085f, 0.11f, 0.15f);
                    card.Accent.color = new Color(UIFactory.Steel.r, UIFactory.Steel.g, UIFactory.Steel.b, 0.35f);
                    card.Border.effectColor = new Color(UIFactory.Steel.r, UIFactory.Steel.g, UIFactory.Steel.b, 0.5f);
                }
                else
                {
                    card.Title.color = UIFactory.White;
                    card.Number.color = UIFactory.Mint;
                    card.Badge.color = new Color(UIFactory.Mint.r, UIFactory.Mint.g, UIFactory.Mint.b, 0.15f);
                    card.Button.image.color = stars > 0
                        ? new Color(0.12f, 0.18f, 0.22f) : new Color(0.1f, 0.15f, 0.21f);
                    var stateColor = stars == 3 ? UIFactory.Brass : stars > 0 ? UIFactory.Mint : UIFactory.SteelLight;
                    card.Accent.color = stateColor;
                    card.Border.effectColor = stars == 3 ? UIFactory.Brass
                        : stars > 0 ? new Color(UIFactory.Mint.r, UIFactory.Mint.g, UIFactory.Mint.b, 0.8f)
                        : new Color(UIFactory.SteelLight.r, UIFactory.SteelLight.g, UIFactory.SteelLight.b, 0.45f);
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
            UIFactory.SetTextDirection(label, rightToLeft);
            label.fontSize = value.Length > 20 ? 10 : 12;
        }
    }
}
