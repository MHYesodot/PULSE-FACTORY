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
        private Image[] markers;
        private TMP_Text arrow;
        private bool rtl;
        private bool active;

        public void Build(TMP_FontAsset font, bool isRightToLeft)
        {
            rtl = isRightToLeft;
            root = (RectTransform)transform;
            body = UIFactory.Image("Steel rail", root, UIFactory.Border, true);
            UIFactory.Stretch((RectTransform)body.transform);
            lane = UIFactory.Image("Belt surface", root, new Color(0.1f, 0.15f, 0.21f), true);
            var laneRect = (RectTransform)lane.transform;
            laneRect.anchorMin = Vector2.zero;
            laneRect.anchorMax = Vector2.one;
            laneRect.offsetMin = new Vector2(2, 3);
            laneRect.offsetMax = new Vector2(-2, -3);
            markers = new Image[5];
            for (int i = 0; i < markers.Length; i++)
            {
                markers[i] = UIFactory.Image("Belt cleat", root, UIFactory.Muted, true);
                var markerRect = (RectTransform)markers[i].transform;
                markerRect.anchorMin = markerRect.anchorMax = new Vector2((i + 1f) / (markers.Length + 1), 0.5f);
                markerRect.sizeDelta = new Vector2(3, 10);
                markerRect.anchoredPosition = Vector2.zero;
            }
            arrow = UIFactory.Text("Direction", root, font, ">", 25, UIFactory.Mint,
                TextAlignmentOptions.Center, rtl, true);
            var arrowRect = arrow.rectTransform;
            arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(0.78f, 0.5f);
            arrowRect.sizeDelta = new Vector2(30, 32);
            arrowRect.anchoredPosition = Vector2.zero;
        }

        public void Layout(Vector2 start, Vector2 end, float thickness, bool selected)
        {
            var layout = CalculateLayout(start, end);
            root.anchoredPosition = layout.Midpoint;
            root.sizeDelta = new Vector2(layout.Length, thickness);
            root.localRotation = Quaternion.Euler(0, 0, layout.AngleDegrees);
            SetActive(selected);
        }

        public void SetActive(bool selected)
        {
            active = selected;
            body.color = selected ? new Color(0.23f, 0.52f, 0.46f) : UIFactory.Border;
            lane.color = selected ? new Color(0.11f, 0.22f, 0.23f) : new Color(0.1f, 0.15f, 0.21f);
            arrow.color = selected ? UIFactory.Mint : new Color(0.58f, 0.65f, 0.72f);
            foreach (var marker in markers)
                marker.color = selected ? new Color(0.45f, 0.83f, 0.73f, 0.8f) : new Color(0.52f, 0.59f, 0.66f, 0.55f);
        }

        public static SegmentLayout CalculateLayout(Vector2 start, Vector2 end)
        {
            var delta = end - start;
            if (delta.sqrMagnitude < 0.0001f) throw new ArgumentException("Conveyor endpoints must be distinct.");
            return new SegmentLayout((start + end) / 2f, delta.magnitude, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }
}
