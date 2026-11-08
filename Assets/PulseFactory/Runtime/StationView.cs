using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PulseFactory.Core;
using PulseFactory.Localization;
using PulseFactory.UI;

namespace PulseFactory.Presentation
{
    public sealed class StationView : MonoBehaviour
    {
        private Image body;
        private Image face;
        private Image deliveryRing;
        private Image statusLamp;
        private bool destination;
        private bool delivered;
        private string cargoKind;
        private float deliveryStarted = float.NegativeInfinity;

        public void Build(Transform parent, TMP_FontAsset font, NodeDefinition node,
            LocalizationCatalog text, bool rtl)
        {
            destination = node.Type == NodeType.Destination;
            cargoKind = node.Accepts;

            body = destination
                ? UIFactory.Image("Dock chassis", parent, UIFactory.Steel, true)
                : UIFactory.Circle("Loading port chassis", parent, UIFactory.Steel);
            var bodyRect = (RectTransform)body.transform;
            bodyRect.sizeDelta = destination ? new Vector2(66, 54) : new Vector2(58, 58);
            UIFactory.Shadow(body, new Color(0, 0, 0, 0.42f), new Vector2(0, -1), 4);
            var bodyOutline = body.gameObject.AddComponent<Outline>();
            bodyOutline.effectColor = UIFactory.SteelLight;
            bodyOutline.effectDistance = new Vector2(1.5f, -1.5f);

            deliveryRing = UIFactory.Circle("Delivery signal", parent, UIFactory.WithAlpha(UIFactory.Mint, 0));
            var ringRect = (RectTransform)deliveryRing.transform;
            ringRect.sizeDelta = new Vector2(70, 70);
            ringRect.SetAsFirstSibling();
            var ringOutline = deliveryRing.gameObject.AddComponent<Outline>();
            ringOutline.effectColor = UIFactory.WithAlpha(UIFactory.Mint, 0);
            ringOutline.effectDistance = new Vector2(2, -2);

            if (destination)
            {
                face = UIFactory.Image("Receiving dock", parent, UIFactory.BeltSurface, true);
                var faceRect = (RectTransform)face.transform;
                faceRect.sizeDelta = new Vector2(54, 42);
                var slot = UIFactory.Image("Cargo receiving slot", parent, CargoColor(cargoKind), true);
                var slotRect = (RectTransform)slot.transform;
                slotRect.sizeDelta = new Vector2(28, 30);
                var slotOutline = slot.gameObject.AddComponent<Outline>();
                slotOutline.effectColor = UIFactory.WithAlpha(UIFactory.SteelLight, 0.75f);
                slotOutline.effectDistance = new Vector2(1, -1);
                var cargoMark = UIFactory.Text("Accepted cargo mark", slot.transform, font,
                    text.Text(cargoKind + "Symbol"), 19, Color.white,
                    TextAlignmentOptions.Center, rtl, true);
                UIFactory.Stretch(cargoMark.rectTransform, 2, 2);
                statusLamp = UIFactory.Circle("Dock status lamp", parent, UIFactory.Muted);
                var lampRect = (RectTransform)statusLamp.transform;
                lampRect.sizeDelta = new Vector2(7, 7);
                lampRect.anchorMin = lampRect.anchorMax = new Vector2(0.82f, 0.78f);
                lampRect.anchoredPosition = Vector2.zero;
            }
            else
            {
                face = UIFactory.Circle("Loading port well", parent, UIFactory.BeltSurface);
                var faceRect = (RectTransform)face.transform;
                faceRect.sizeDelta = new Vector2(40, 40);
                var innerRim = UIFactory.Circle("Port inner rim", parent, UIFactory.SteelLight);
                var rimRect = (RectTransform)innerRim.transform;
                rimRect.sizeDelta = new Vector2(26, 26);
                var well = UIFactory.Circle("Port opening", parent, new Color(0.035f, 0.055f, 0.075f));
                var wellRect = (RectTransform)well.transform;
                wellRect.sizeDelta = new Vector2(19, 19);
                var direction = UIFactory.Text("Port direction", parent, font, "↑", 17,
                    UIFactory.Mint, TextAlignmentOptions.Center, false, true);
                direction.rectTransform.sizeDelta = new Vector2(20, 20);
                direction.rectTransform.anchoredPosition = new Vector2(0, -1);
            }
        }

        public void SetDelivered(bool value, bool animate)
        {
            if (!destination) return;
            delivered = value;
            if (statusLamp != null) statusLamp.color = value ? UIFactory.Mint : UIFactory.Muted;
            if (value && animate) deliveryStarted = Time.unscaledTime;
            if (!value) deliveryStarted = float.NegativeInfinity;
            body.color = value ? new Color(0.2f, 0.48f, 0.4f) : UIFactory.Steel;
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - deliveryStarted;
            if (!delivered || elapsed < 0 || elapsed > 0.42f) return;
            float progress = Mathf.Clamp01(elapsed / 0.42f);
            var rect = (RectTransform)deliveryRing.transform;
            rect.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.15f, progress);
            var color = UIFactory.Mint;
            color.a = 1f - progress;
            deliveryRing.color = color;
            var outline = deliveryRing.GetComponent<Outline>();
            outline.effectColor = color;
        }

        private static Color CargoColor(string kind) => kind == "red" ? UIFactory.Red : UIFactory.Blue;
    }
}
