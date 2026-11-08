using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PulseFactory.UI;

namespace PulseFactory.Presentation
{
    public sealed class PackageView : MonoBehaviour
    {
        private RectTransform rect;
        private CanvasGroup canvasGroup;
        private readonly Vector3 baseScale = Vector3.one;

        public void Build(Transform parent, TMP_FontAsset font, string kind, string symbol, bool rtl)
        {
            rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(UIFactory.PackageSize, UIFactory.PackageSize);

            var shadow = UIFactory.Image("Cargo drop shadow", parent,
                new Color(0.01f, 0.02f, 0.03f, 0.72f), true);
            UIFactory.Stretch((RectTransform)shadow.transform);
            ((RectTransform)shadow.transform).anchoredPosition = new Vector2(0, -3);

            Color cargo = kind == "blue" ? UIFactory.Blue : UIFactory.Red;
            var shell = UIFactory.Image("Cargo crate shell", parent, UIFactory.Steel, true);
            UIFactory.Stretch((RectTransform)shell.transform);
            var outline = shell.gameObject.AddComponent<Outline>();
            outline.effectColor = UIFactory.SteelLight;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var face = UIFactory.Image("Cargo crate top", parent, cargo, true);
            UIFactory.Stretch((RectTransform)face.transform, 4, 4);
            var inset = UIFactory.Image("Cargo top inset", parent, UIFactory.WithAlpha(Color.white, 0.12f), true);
            UIFactory.Stretch((RectTransform)inset.transform, 8, 8);

            var band = UIFactory.Image("Cargo securing band", parent, UIFactory.WithAlpha(UIFactory.Steel, 0.82f), true);
            var bandRect = (RectTransform)band.transform;
            bandRect.anchorMin = bandRect.anchorMax = new Vector2(0.5f, 0.5f);
            bandRect.sizeDelta = new Vector2(5, 33);
            bandRect.anchoredPosition = Vector2.zero;

            var mark = UIFactory.Text("Cargo identifier", parent, font, symbol, 21,
                Color.white, TextAlignmentOptions.Center, rtl, true);
            UIFactory.Stretch(mark.rectTransform, 4, 4);
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void SetPosition(Vector2 position) => rect.anchoredPosition = position;

        public void SetTravel(float amount)
        {
            float scale = 1f + Mathf.Sin(Mathf.Clamp01(amount) * Mathf.PI) * 0.075f;
            rect.localScale = baseScale * scale;
        }

        public void SetDelivery(float amount)
        {
            rect.localScale = Vector3.one * Mathf.Lerp(1, 0.55f, amount);
            canvasGroup.alpha = 1f - amount;
        }
    }
}
