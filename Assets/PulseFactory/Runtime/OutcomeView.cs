using System;
using PulseFactory.Localization;
using PulseFactory.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PulseFactory.Presentation
{
    public sealed class OutcomeView : MonoBehaviour
    {
        private RectTransform root;
        private TMP_Text title;
        private TMP_Text message;
        private Button retry;
        private Button next;
        private Button levels;
        private TMP_FontAsset font;
        private bool victory;
        private bool finalLevel;
        private bool nextAvailable;
        private bool hasOutcome;
        private int stars;
        private string failureKey;

        public void Build(RectTransform parent, TMP_FontAsset fontAsset, Action onRetry,
            Action onNext, Action onLevels)
        {
            font = fontAsset;
            root = UIFactory.Rect("Outcome overlay", parent, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            UIFactory.Stretch(root);
            var shade = UIFactory.Image("Outcome shade", root, new Color(0.015f, 0.025f, 0.045f, 0.84f));
            shade.raycastTarget = true;
            UIFactory.Stretch((RectTransform)shade.transform);
            var card = UIFactory.Image("Outcome card", root, UIFactory.Panel, true);
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = new Vector2(0.08f, 0.28f);
            cardRect.anchorMax = new Vector2(0.92f, 0.72f);
            cardRect.offsetMin = cardRect.offsetMax = Vector2.zero;
            UIFactory.Shadow(card, new Color(0, 0, 0, 0.6f), new Vector2(0, -1), 8);

            title = UIFactory.Text("Outcome title", card.transform, font, "", 25,
                UIFactory.White, TextAlignmentOptions.Center, false, true);
            title.rectTransform.anchorMin = new Vector2(0.07f, 0.67f);
            title.rectTransform.anchorMax = new Vector2(0.93f, 0.92f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            message = UIFactory.Text("Outcome message", card.transform, font, "", 15,
                UIFactory.Muted, TextAlignmentOptions.Center, false, true);
            message.rectTransform.anchorMin = new Vector2(0.08f, 0.4f);
            message.rectTransform.anchorMax = new Vector2(0.92f, 0.68f);
            message.rectTransform.offsetMin = message.rectTransform.offsetMax = Vector2.zero;
            message.enableAutoSizing = true;
            message.fontSizeMin = 12;
            message.fontSizeMax = 15;

            retry = MakeButton(card.transform, "Retry", 0.08f, 0.34f, () => onRetry());
            next = MakeButton(card.transform, "Next level", 0.38f, 0.65f, () => onNext());
            levels = MakeButton(card.transform, "Levels", 0.69f, 0.92f, () => onLevels());
            root.gameObject.SetActive(false);
        }

        public void ShowVictory(int achievedStars, bool isFinalLevel, bool canAdvance, LocalizationCatalog text)
        {
            hasOutcome = true;
            victory = true;
            finalLevel = isFinalLevel;
            nextAvailable = canAdvance;
            stars = achievedStars;
            Refresh(text);
            root.gameObject.SetActive(true);
        }

        public void ShowFailure(string key, LocalizationCatalog text)
        {
            hasOutcome = true;
            victory = false;
            failureKey = key;
            Refresh(text);
            root.gameObject.SetActive(true);
        }

        public void Hide() => root.gameObject.SetActive(false);

        public void Refresh(LocalizationCatalog text)
        {
            if (root == null || !hasOutcome) return;
            title.text = text.Text(victory ? finalLevel ? "allComplete" : "levelComplete" : "failedTitle");
            message.text = victory ? text.Text("victory", stars) : text.Text(failureKey);
            title.isRightToLeftText = message.isRightToLeftText = text.RightToLeft;
            SetButton(retry, text.Text(victory ? "playAgain" : "tryAgain"), text.RightToLeft);
            SetButton(next, text.Text("nextLevel"), text.RightToLeft);
            SetButton(levels, text.Text("back"), text.RightToLeft);
            next.gameObject.SetActive(victory && nextAvailable);
            retry.gameObject.SetActive(true);
            next.interactable = victory && nextAvailable;
        }

        private Button MakeButton(Transform parent, string name, float left, float right, UnityEngine.Events.UnityAction action)
        {
            var button = UIFactory.Button(name, parent, font, "", 12, UIFactory.Raised, false, action);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(left, 0.09f);
            rect.anchorMax = new Vector2(right, 0.34f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return button;
        }

        private static void SetButton(Button button, string value, bool rtl)
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            label.text = value;
            label.isRightToLeftText = rtl;
            label.fontSize = value.Length > 18 ? 9 : 12;
        }
    }
}
