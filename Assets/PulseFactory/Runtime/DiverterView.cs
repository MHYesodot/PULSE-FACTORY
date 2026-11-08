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
        private TMP_Text stateLabel;
        private bool isRightToLeft;
        private SwitchGroup group;
        private bool inverted;

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
            UIFactory.Stretch(gear.rectTransform);
            groupLabel = UIFactory.Text("Group label", parent, font, group.ToString(), 13,
                UIFactory.White, TextAlignmentOptions.Center, rtl, true);
            var groupRect = groupLabel.rectTransform;
            groupRect.anchorMin = groupRect.anchorMax = new Vector2(0.5f, 0.5f);
            groupRect.sizeDelta = new Vector2(28, 22);
            groupRect.anchoredPosition = new Vector2(0, -5);
            indicator = UIFactory.Image("Switch status", parent, UIFactory.Muted, true);
            var indicatorRect = (RectTransform)indicator.transform;
            indicatorRect.sizeDelta = new Vector2(12, 12);
            indicatorRect.anchorMin = indicatorRect.anchorMax = new Vector2(1, 1);
            indicatorRect.anchoredPosition = new Vector2(-2, -2);
            stateLabel = UIFactory.Text("Switch status text", parent, font, "OFF", 10,
                UIFactory.Muted, TextAlignmentOptions.Center, rtl);
            var statusRect = stateLabel.rectTransform;
            statusRect.anchorMin = statusRect.anchorMax = new Vector2(0.5f, 0);
            statusRect.sizeDelta = new Vector2(68, 18);
            statusRect.anchoredPosition = new Vector2(0, -12);
        }

        public void SetState(SwitchState switches, LocalizationCatalog text)
        {
            bool on = switches.Get(group);
            var color = on ? UIFactory.Mint : UIFactory.Muted;
            indicator.color = color;
            plate.color = on ? new Color(0.34f, 0.29f, 0.53f) : new Color(0.22f, 0.2f, 0.34f);
            stateLabel.text = text.Text(on ? "switchOn" : "switchOff");
            stateLabel.isRightToLeftText = isRightToLeft;
            groupLabel.text = text.Text(group == SwitchGroup.A ? "switchA" : "switchB");
        }

        public bool Inverted => inverted;
    }
}
