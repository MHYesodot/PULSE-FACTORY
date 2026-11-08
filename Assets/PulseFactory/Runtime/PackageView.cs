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

        public void Build(Transform parent, TMP_FontAsset font, string kind, string symbol, bool rtl)
        {
            rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(34, 34);
            if (kind == "blue") rect.localRotation = Quaternion.Euler(0, 0, 45);
            var image = UIFactory.Image(kind == "red" ? "Red plus package" : "Blue cross package",
                parent, kind == "red" ? UIFactory.Red : UIFactory.Blue, true);
            var imageRect = (RectTransform)image.transform;
            UIFactory.Stretch(imageRect);
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            var mark = UIFactory.Text("Package identifier", image.transform, font, symbol, 21,
                Color.white, TextAlignmentOptions.Center, rtl, true);
            UIFactory.Stretch(mark.rectTransform, 2, 2);
            mark.transform.localRotation = kind == "blue" ? Quaternion.Euler(0, 0, -45) : Quaternion.identity;
            canvasGroup = image.gameObject.AddComponent<CanvasGroup>();
        }

        public void SetPosition(Vector2 position) => rect.anchoredPosition = position;
        public void SetDelivery(float amount)
        {
            rect.localScale = Vector3.one * Mathf.Lerp(1, 0.55f, amount);
            canvasGroup.alpha = 1f - amount;
        }
    }
}
