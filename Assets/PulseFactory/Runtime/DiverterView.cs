using PulseFactory.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PulseFactory.Localization;
using PulseFactory.UI;

namespace PulseFactory.Presentation
{
    public sealed class DiverterView : MonoBehaviour
    {
        private Image plate;
        private Image indicator;
        private TMP_Text groupLabel;
        private bool isRightToLeft;
        private SwitchGroup group;
        private bool inverted;
        private bool hasState;
        private bool currentState;
        private float pulseStarted = float.NegativeInfinity;

        public void Build(Transform parent, TMP_FontAsset font, SwitchGroup switchGroup, bool nodeInverted, bool rtl)
        {
            group = switchGroup;
            inverted = nodeInverted;
            isRightToLeft = rtl;
            plate = UIFactory.Image("Diverter", parent, UIFactory.Purple, true);
            var rect = (RectTransform)plate.transform;
            rect.sizeDelta = new Vector2(58, 58);
            UIFactory.Shadow(plate, new Color(0, 0, 0, 0.4f), new Vector2(0, -1), 5);
            var outline = plate.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.79f, 0.72f, 1f, 0.8f);
            outline.effectDistance = new Vector2(2, -2);
            var gear = UIFactory.Text("Route glyph", plate.transform, font, nodeInverted ? "<>" : ">",
                24, UIFactory.White, TextAlignmentOptions.Center, rtl, true);
            gear.rectTransform.anchorMin = gear.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            gear.rectTransform.sizeDelta = new Vector2(38, 34);
            gear.rectTransform.anchoredPosition = new Vector2(0, 7);
            groupLabel = UIFactory.Text("Group label", parent, font, group.ToString(), 13,
                UIFactory.White, TextAlignmentOptions.Center, rtl, true);
            var groupRect = groupLabel.rectTransform;
            groupRect.anchorMin = groupRect.anchorMax = new Vector2(0.5f, 0.5f);
            groupRect.sizeDelta = new Vector2(24, 18);
            groupRect.anchoredPosition = new Vector2(0, -16);
            indicator = UIFactory.Circle("Switch status", parent, UIFactory.Muted);
            var indicatorRect = (RectTransform)indicator.transform;
            indicatorRect.sizeDelta = new Vector2(14, 14);
            indicatorRect.anchorMin = indicatorRect.anchorMax = new Vector2(1, 1);
            indicatorRect.anchoredPosition = new Vector2(-2, -2);
        }

        public void SetState(SwitchState switches, LocalizationCatalog text, bool animate = true)
        {
            bool on = switches.Get(group);
            if (hasState && currentState != on)
                pulseStarted = animate ? Time.unscaledTime : float.NegativeInfinity;
            currentState = on;
            hasState = true;
            var color = on ? UIFactory.Mint : UIFactory.Muted;
            indicator.color = color;
            plate.color = on ? new Color(0.34f, 0.29f, 0.53f) : new Color(0.22f, 0.2f, 0.34f);
            groupLabel.text = text.Text(group == SwitchGroup.A ? "switchA" : "switchB");
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - pulseStarted;
            float pulse = elapsed < 0.42f
                ? Mathf.Sin((1f - elapsed / 0.42f) * Mathf.PI * 0.5f) : 0;
            plate.rectTransform.localScale = Vector3.one * (1f + pulse * 0.12f);
            indicator.rectTransform.localScale = Vector3.one * (1f + pulse * 0.65f);
        }

        public bool Inverted => inverted;
    }
}
