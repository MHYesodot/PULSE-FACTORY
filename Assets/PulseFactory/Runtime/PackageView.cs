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
            rect.sizeDelta = new Vector2(40, 40);
            var image = kind == "blue"
                ? UIFactory.Circle("Blue cross package", parent, UIFactory.Blue)
                : UIFactory.Image("Red plus package", parent, UIFactory.Red, true);
            var imageRect = (RectTransform)image.transform;
            UIFactory.Stretch(imageRect);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.88f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var mark = UIFactory.Text("Package identifier", image.transform, font, symbol, 23,
                Color.white, TextAlignmentOptions.Center, rtl, true);
            UIFactory.Stretch(mark.rectTransform, 2, 2);
            canvasGroup = image.gameObject.AddComponent<CanvasGroup>();
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
