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
        private RectTransform cardTransform;
        private TMP_Text title;
        private TMP_Text levelContext;
        private TMP_Text message;
        private readonly Image[] starImages = new Image[3];
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
        private bool animateIn;
        private bool reducedMotion;
        private string completedLevel;
        private bool revealStars;
        private float starRevealStarted;

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
            cardTransform = (RectTransform)card.transform;
            var cardRect = cardTransform;
            cardRect.anchorMin = new Vector2(0.08f, 0.28f);
            cardRect.anchorMax = new Vector2(0.92f, 0.72f);
            cardRect.offsetMin = cardRect.offsetMax = Vector2.zero;
            UIFactory.Shadow(card, new Color(0, 0, 0, 0.6f), new Vector2(0, -1), 8);
            var accent = UIFactory.Image("Outcome accent", card.transform, UIFactory.Mint, true);
            var accentRect = (RectTransform)accent.transform;
            accentRect.anchorMin = new Vector2(0.06f, 0.955f);
            accentRect.anchorMax = new Vector2(0.94f, 0.975f);
            accentRect.offsetMin = accentRect.offsetMax = Vector2.zero;

            title = UIFactory.Text("Outcome title", card.transform, font, "", 25,
                UIFactory.White, TextAlignmentOptions.Center, false, true);
            title.rectTransform.anchorMin = new Vector2(0.07f, 0.76f);
            title.rectTransform.anchorMax = new Vector2(0.93f, 0.94f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            title.enableAutoSizing = true;
            title.fontSizeMin = 18;
            title.fontSizeMax = 25;
            levelContext = UIFactory.Text("Completed level context", card.transform, font, "", 12,
                UIFactory.Muted, TextAlignmentOptions.Center, false, true);
            levelContext.rectTransform.anchorMin = new Vector2(0.08f, 0.67f);
            levelContext.rectTransform.anchorMax = new Vector2(0.92f, 0.76f);
            levelContext.rectTransform.offsetMin = levelContext.rectTransform.offsetMax = Vector2.zero;
            for (int i = 0; i < starImages.Length; i++)
            {
                starImages[i] = UIFactory.Star("Outcome star " + (i + 1), card.transform,
                    new Color(0.33f, 0.4f, 0.49f));
                var starRect = (RectTransform)starImages[i].transform;
                starRect.anchorMin = starRect.anchorMax = new Vector2(0.38f + i * 0.12f, 0.57f);
                starRect.sizeDelta = new Vector2(30, 30);
                starRect.anchoredPosition = Vector2.zero;
            }
            message = UIFactory.Text("Outcome message", card.transform, font, "", 15,
                UIFactory.Muted, TextAlignmentOptions.Center, false, true);
            message.rectTransform.anchorMin = new Vector2(0.08f, 0.36f);
            message.rectTransform.anchorMax = new Vector2(0.92f, 0.52f);
            message.rectTransform.offsetMin = message.rectTransform.offsetMax = Vector2.zero;
            message.enableAutoSizing = true;
            message.fontSizeMin = 12;
            message.fontSizeMax = 15;

            retry = MakeButton(card.transform, "Retry", () => onRetry(), new Color(0.15f, 0.27f, 0.34f));
            next = MakeButton(card.transform, "Next level", () => onNext(), new Color(0.12f, 0.39f, 0.33f));
            levels = MakeButton(card.transform, "Levels", () => onLevels(), UIFactory.Raised);
            root.gameObject.SetActive(false);
        }

        public void ShowVictory(string levelLabel, int achievedStars, bool isFinalLevel, bool canAdvance,
            LocalizationCatalog text, bool animate)
        {
            hasOutcome = true;
            victory = true;
            completedLevel = levelLabel;
            finalLevel = isFinalLevel;
            nextAvailable = canAdvance;
            stars = achievedStars;
            Refresh(text);
            root.gameObject.SetActive(true);
            BeginIntro(animate);
        }

        public void ShowFailure(string key, LocalizationCatalog text, bool animate)
        {
            hasOutcome = true;
            victory = false;
            failureKey = key;
            Refresh(text);
            root.gameObject.SetActive(true);
            BeginIntro(animate);
        }

        public void Hide()
        {
            animateIn = false;
            revealStars = false;
            root.gameObject.SetActive(false);
        }

        public void SetReducedMotion(bool value)
        {
            reducedMotion = value;
            foreach (var button in new[] { retry, next, levels })
                button.GetComponent<ButtonPressFeedback>().SetMotionEnabled(!value);
        }

        public void Refresh(LocalizationCatalog text)
        {
            if (root == null || !hasOutcome) return;
            title.text = text.Text(victory ? finalLevel ? "allComplete" : "levelComplete" : "failedTitle");
            message.text = victory ? text.Text("victory", stars) : text.Text(failureKey);
            levelContext.text = victory ? completedLevel : "";
            levelContext.gameObject.SetActive(victory);
            UIFactory.SetTextDirection(title, text.RightToLeft);
            UIFactory.SetTextDirection(message, text.RightToLeft);
            UIFactory.SetTextDirection(levelContext, text.RightToLeft);
            SetButton(retry, text.Text(victory ? "playAgain" : "tryAgain"), text.RightToLeft);
            SetButton(next, text.Text("nextLevel"), text.RightToLeft);
            SetButton(levels, text.Text("back"), text.RightToLeft);
            next.gameObject.SetActive(victory && nextAvailable);
            retry.gameObject.SetActive(true);
            next.interactable = victory && nextAvailable;
            foreach (var button in new[] { retry, next, levels })
                button.GetComponent<ButtonPressFeedback>().SetMotionEnabled(!reducedMotion);
            SetActionAnchors(victory && nextAvailable);
            for (int i = 0; i < starImages.Length; i++)
            {
                starImages[i].gameObject.SetActive(victory);
                starImages[i].color = i < stars ? UIFactory.Amber : new Color(0.33f, 0.4f, 0.49f, 0.55f);
                if (!animateIn) starImages[i].rectTransform.localScale = Vector3.one;
            }
            var accentImage = cardTransform.Find("Outcome accent").GetComponent<Image>();
            accentImage.color = victory ? UIFactory.Mint : UIFactory.Amber;
        }

        private Button MakeButton(Transform parent, string name, UnityEngine.Events.UnityAction action, Color color)
        {
            return UIFactory.Button(name, parent, font, "", 13, color, false, action);
        }

        private void SetActionAnchors(bool hasNext)
        {
            if (hasNext)
            {
                SetAnchors(next, 0.09f, 0.29f, 0.52f, 0.92f);
                SetAnchors(retry, 0.09f, 0.29f, 0.08f, 0.48f);
                SetAnchors(levels, 0.015f, 0.1f, 0.26f, 0.74f);
            }
            else
            {
                SetAnchors(retry, 0.09f, 0.29f, 0.08f, 0.92f);
                SetAnchors(levels, 0.015f, 0.1f, 0.26f, 0.74f);
            }
        }

        private static void SetAnchors(Button button, float bottom, float top, float left, float right)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private void BeginIntro(bool animate)
        {
            cardTransform.localScale = animate ? Vector3.one * 0.92f : Vector3.one;
            animateIn = animate;
            revealStars = animate && victory;
            starRevealStarted = Time.unscaledTime;
            for (int i = 0; i < starImages.Length; i++)
                starImages[i].rectTransform.localScale = animate && victory ? Vector3.one * 0.25f : Vector3.one;
        }

        private void Update()
        {
            if (animateIn)
            {
                cardTransform.localScale = Vector3.Lerp(cardTransform.localScale, Vector3.one,
                    1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
                if ((cardTransform.localScale - Vector3.one).sqrMagnitude < 0.0001f)
                {
                    cardTransform.localScale = Vector3.one;
                    animateIn = false;
                }
            }
            if (revealStars)
            {
                for (int i = 0; i < starImages.Length; i++)
                {
                    float progress = Mathf.Clamp01((Time.unscaledTime - starRevealStarted - i * 0.1f) / 0.2f);
                    float pop = Mathf.Sin(progress * Mathf.PI) * 0.22f;
                    starImages[i].rectTransform.localScale = Vector3.one * (0.25f + progress * 0.75f + pop);
                }
                if (Time.unscaledTime - starRevealStarted > 0.5f)
                {
                    revealStars = false;
                    foreach (var star in starImages) star.rectTransform.localScale = Vector3.one;
                }
            }
        }

        private static void SetButton(Button button, string value, bool rtl)
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            label.text = value;
            UIFactory.SetTextDirection(label, rtl);
            label.fontSize = value.Length > 14 ? 11 : 14;
        }
    }
}
