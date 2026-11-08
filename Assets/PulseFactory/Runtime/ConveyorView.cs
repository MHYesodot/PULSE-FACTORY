using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PulseFactory.UI;

namespace PulseFactory.Presentation
{
    public sealed class ConveyorView : MonoBehaviour
    {
        public readonly struct SegmentLayout
        {
            public Vector2 Midpoint { get; }
            public float Length { get; }
            public float AngleDegrees { get; }
            public SegmentLayout(Vector2 midpoint, float length, float angleDegrees)
            { Midpoint = midpoint; Length = length; AngleDegrees = angleDegrees; }
        }

        private RectTransform root;
        private Image body;
        private Image lane;
        private Image[] treads;
        private Image routeLine;
        private TMP_Text arrow;
        private bool rtl;
        private bool animateColor;
        private Color targetBody;
        private Color targetLane;
        private Color targetRoute;
        private Color targetHardware;

        public void Build(TMP_FontAsset font, bool isRightToLeft)
        {
            rtl = isRightToLeft;
            root = (RectTransform)transform;
            var shadow = UIFactory.Image("Belt shadow", root, new Color(0.015f, 0.025f, 0.04f, 0.78f), true);
            UIFactory.Stretch((RectTransform)shadow.transform);
            shadow.rectTransform.anchoredPosition = new Vector2(0, -2);
            body = UIFactory.Image("Raised steel chassis", root, UIFactory.Steel, true);
            UIFactory.Stretch((RectTransform)body.transform);
            lane = UIFactory.Image("Recessed belt", root, UIFactory.BeltSurface, true);
            var laneRect = (RectTransform)lane.transform;
            laneRect.anchorMin = Vector2.zero;
            laneRect.anchorMax = Vector2.one;
            laneRect.offsetMin = new Vector2(4, 5);
            laneRect.offsetMax = new Vector2(-4, -5);

            AddRail("Near side rail", root, 0, 0.08f, UIFactory.SteelLight);
            AddRail("Far side rail", root, 0.92f, 1, UIFactory.SteelLight);
            treads = new Image[7];
            for (int i = 0; i < treads.Length; i++)
            {
                treads[i] = UIFactory.Image("Belt tread", root, UIFactory.Steel, true);
                var markerRect = (RectTransform)treads[i].transform;
                markerRect.anchorMin = markerRect.anchorMax = new Vector2((i + 1f) / (treads.Length + 1), 0.5f);
                markerRect.sizeDelta = new Vector2(3, 13);
                markerRect.anchoredPosition = Vector2.zero;
            }
            routeLine = UIFactory.Image("Live route inlay", root, UIFactory.Mint, true);
            var routeRect = (RectTransform)routeLine.transform;
            routeRect.anchorMin = new Vector2(0.08f, 0.5f);
            routeRect.anchorMax = new Vector2(0.67f, 0.5f);
            routeRect.sizeDelta = new Vector2(0, 2);
            routeRect.anchoredPosition = Vector2.zero;
            arrow = UIFactory.Text("Direction", root, font, ">", 27, UIFactory.Mint,
                TextAlignmentOptions.Center, rtl, true);
            var arrowRect = arrow.rectTransform;
            arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(0.84f, 0.5f);
            arrowRect.sizeDelta = new Vector2(25, 30);
            arrowRect.anchoredPosition = Vector2.zero;
            SetActive(false, false);
        }

        public void Layout(Vector2 start, Vector2 end, float thickness, bool selected)
        {
            var layout = CalculateLayout(start, end);
            root.anchoredPosition = layout.Midpoint;
            root.sizeDelta = new Vector2(layout.Length, thickness);
            root.localRotation = Quaternion.Euler(0, 0, layout.AngleDegrees);
            SetActive(selected, false);
        }

        public void SetActive(bool selected, bool animate = true)
        {
            targetBody = selected ? new Color(0.33f, 0.56f, 0.53f) : UIFactory.Steel;
            targetLane = selected ? new Color(0.1f, 0.19f, 0.19f) : UIFactory.BeltSurface;
            targetRoute = selected ? UIFactory.Mint : new Color(0.12f, 0.17f, 0.21f, 0.15f);
            targetHardware = selected ? new Color(0.72f, 0.91f, 0.84f) : new Color(0.55f, 0.65f, 0.72f, 0.78f);
            animateColor = animate;
            if (!animate)
            {
                body.color = targetBody;
                lane.color = targetLane;
                routeLine.color = targetRoute;
                arrow.color = targetHardware;
                foreach (var tread in treads) tread.color = targetHardware;
            }
        }

        private void Update()
        {
            if (!animateColor) return;
            float blend = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            body.color = Color.Lerp(body.color, targetBody, blend);
            lane.color = Color.Lerp(lane.color, targetLane, blend);
            foreach (var tread in treads) tread.color = Color.Lerp(tread.color, targetHardware, blend);
            arrow.color = Color.Lerp(arrow.color, targetHardware, blend);
            routeLine.color = Color.Lerp(routeLine.color, targetRoute, blend);
        }

        private static void AddRail(string name, Transform parent, float bottom, float top, Color color)
        {
            var rail = UIFactory.Image(name, parent, color, true);
            var railRect = (RectTransform)rail.transform;
            railRect.anchorMin = new Vector2(0.04f, bottom);
            railRect.anchorMax = new Vector2(0.96f, top);
            railRect.offsetMin = railRect.offsetMax = Vector2.zero;
        }

        public static SegmentLayout CalculateLayout(Vector2 start, Vector2 end)
        {
            var delta = end - start;
            if (delta.sqrMagnitude < 0.0001f) throw new ArgumentException("Conveyor endpoints must be distinct.");
            return new SegmentLayout((start + end) / 2f, delta.magnitude, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }
}
