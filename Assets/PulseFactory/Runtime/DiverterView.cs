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
        private Image chassis;
        private Image face;
        private Image signalRing;
        private Image indicator;
        private RectTransform pointer;
        private TMP_Text groupLabel;
        private SwitchGroup group;
        private bool hasState;
        private bool currentState;
        private float pulseStarted = float.NegativeInfinity;
        private float targetAngle;
        private bool animateRoute;

        internal bool SwitchIsOn => currentState;

        public void Build(Transform parent, TMP_FontAsset font, SwitchGroup switchGroup, bool rtl)
        {
            group = switchGroup;
            signalRing = UIFactory.Circle("Pulse signal ring", parent, UIFactory.WithAlpha(UIFactory.Purple, 0));
            ((RectTransform)signalRing.transform).sizeDelta = new Vector2(70, 70);

            chassis = UIFactory.Circle("Diverter chassis", parent, UIFactory.Steel);
            var chassisRect = (RectTransform)chassis.transform;
            chassisRect.sizeDelta = new Vector2(64, 64);
            UIFactory.Shadow(chassis, new Color(0, 0, 0, 0.48f), new Vector2(0, -2), 5);
            var chassisOutline = chassis.gameObject.AddComponent<Outline>();
            chassisOutline.effectColor = UIFactory.SteelLight;
            chassisOutline.effectDistance = new Vector2(2, -2);

            face = UIFactory.Circle("Rotary selector face", parent, new Color(0.2f, 0.21f, 0.32f));
            ((RectTransform)face.transform).sizeDelta = new Vector2(48, 48);
            var faceOutline = face.gameObject.AddComponent<Outline>();
            faceOutline.effectColor = new Color(UIFactory.Purple.r, UIFactory.Purple.g, UIFactory.Purple.b, 0.8f);
            faceOutline.effectDistance = new Vector2(1.5f, -1.5f);

            for (int i = 0; i < 4; i++)
            {
                var fastener = UIFactory.Circle("Diverter fastener", parent, UIFactory.Brass);
                var fastenerRect = (RectTransform)fastener.transform;
                float angle = 45f + i * 90f;
                var offset = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * 27f;
                fastenerRect.sizeDelta = new Vector2(4, 4);
                fastenerRect.anchoredPosition = offset;
            }

            var pointerImage = UIFactory.Image("Route selector arm", parent, UIFactory.Mint, true);
            pointer = (RectTransform)pointerImage.transform;
            pointer.anchorMin = pointer.anchorMax = new Vector2(0.5f, 0.5f);
            pointer.pivot = new Vector2(0.5f, 0.05f);
            pointer.sizeDelta = new Vector2(8, 24);
            pointer.anchoredPosition = Vector2.zero;

            var pivot = UIFactory.Circle("Selector pivot", parent, UIFactory.SteelLight);
            var pivotRect = (RectTransform)pivot.transform;
            pivotRect.sizeDelta = new Vector2(12, 12);

            groupLabel = UIFactory.Text("Switch group label", parent, font,
                switchGroup == SwitchGroup.A ? "A" : "B", 10, UIFactory.White,
                TextAlignmentOptions.Center, rtl, true);
            groupLabel.rectTransform.anchorMin = groupLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.84f);
            groupLabel.rectTransform.sizeDelta = new Vector2(18, 12);
            groupLabel.rectTransform.anchoredPosition = Vector2.zero;
            indicator = UIFactory.Circle("Switch state lamp", parent, UIFactory.Muted);
            var indicatorRect = (RectTransform)indicator.transform;
            indicatorRect.sizeDelta = new Vector2(8, 8);
            indicatorRect.anchorMin = indicatorRect.anchorMax = new Vector2(0.78f, 0.79f);
            indicatorRect.anchoredPosition = Vector2.zero;
        }

        public void SetState(SwitchState switches, LocalizationCatalog text, bool animate = true)
        {
            bool on = switches.Get(group);
            if (hasState && currentState != on)
                pulseStarted = animate ? Time.unscaledTime : float.NegativeInfinity;
            currentState = on;
            hasState = true;
            var accent = on ? UIFactory.Mint : UIFactory.SteelLight;
            indicator.color = accent;
            face.color = on ? new Color(0.26f, 0.32f, 0.36f) : new Color(0.2f, 0.21f, 0.32f);
            var label = group == SwitchGroup.A ? "switchA" : "switchB";
            groupLabel.text = text.Text(label);
            UIFactory.SetTextDirection(groupLabel, text.RightToLeft);
        }

        public void SetRoute(Vector2 direction, bool animate)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            animateRoute = animate;
            if (!animate) pointer.localRotation = Quaternion.Euler(0, 0, targetAngle);
        }

        private void Update()
        {
            float blend = 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime);
            float elapsed = Time.unscaledTime - pulseStarted;
            float pulse = elapsed >= 0 && elapsed < UIFactory.PulseSignalSeconds * 3f
                ? Mathf.Sin(Mathf.Clamp01(1f - elapsed / (UIFactory.PulseSignalSeconds * 3f)) * Mathf.PI)
                : 0f;
            chassis.rectTransform.localScale = Vector3.one * (1f + pulse * 0.07f);
            signalRing.rectTransform.localScale = Vector3.one * (1.05f + pulse * 0.35f);
            var ringColor = UIFactory.Purple;
            ringColor.a = pulse * 0.6f;
            signalRing.color = ringColor;
            if (animateRoute)
                pointer.localRotation = Quaternion.Euler(0, 0,
                    Mathf.LerpAngle(pointer.localEulerAngles.z, targetAngle, blend));
            indicator.rectTransform.localScale = Vector3.one * (1f + pulse * 0.55f);
        }
    }
}
