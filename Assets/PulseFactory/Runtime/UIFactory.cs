using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PulseFactory.UI
{
    internal static class UIFactory
    {
        internal static readonly Color Ink = new Color(0.055f, 0.078f, 0.12f);
        internal static readonly Color Panel = new Color(0.085f, 0.12f, 0.18f);
        internal static readonly Color Raised = new Color(0.12f, 0.17f, 0.24f);
        internal static readonly Color Border = new Color(0.23f, 0.31f, 0.4f);
        internal static readonly Color White = new Color(0.95f, 0.97f, 1f);
        internal static readonly Color Muted = new Color(0.63f, 0.7f, 0.79f);
        internal static readonly Color Mint = new Color(0.31f, 0.91f, 0.7f);
        internal static readonly Color Purple = new Color(0.68f, 0.57f, 1f);
        internal static readonly Color Red = new Color(1f, 0.36f, 0.38f);
        internal static readonly Color Blue = new Color(0.28f, 0.63f, 1f);
        internal static readonly Color Amber = new Color(1f, 0.75f, 0.31f);
        private static Sprite roundedSprite;

        internal static RectTransform Rect(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPosition)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = (RectTransform)gameObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }

        internal static Image Image(string name, Transform parent, Color color, bool rounded = false)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (rounded)
            {
                if (roundedSprite == null) roundedSprite = CreateRoundedSprite();
                image.sprite = roundedSprite;
                image.type = UnityEngine.UI.Image.Type.Sliced;
            }
            return image;
        }

        private static Sprite CreateRoundedSprite()
        {
            const int size = 32;
            const float radius = 6f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Runtime UI rounded panel",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius);
                    float dy = Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius);
                    float distance = new Vector2(Mathf.Max(dx, 0), Mathf.Max(dy, 0)).magnitude
                        + Mathf.Min(Mathf.Max(dx, dy), 0) - radius;
                    float alpha = 1f - Mathf.SmoothStep(0, 1, distance + 0.5f);
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
            sprite.name = "Runtime UI rounded panel";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        internal static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string value,
            float size, Color color, TextAlignmentOptions alignment, bool rtl, bool bold = false)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = alignment;
            text.isRightToLeftText = rtl;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }

        internal static Button Button(string name, Transform parent, TMP_FontAsset font, string text,
            float fontSize, Color normal, bool rtl, UnityEngine.Events.UnityAction onClick)
        {
            var image = Image(name, parent, normal, true);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f),
                pressedColor = new Color(0.78f, 0.82f, 0.88f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.58f, 0.62f, 0.68f, 0.42f),
                colorMultiplier = 1f,
                fadeDuration = 0.12f
            };
            button.onClick.AddListener(onClick);
            var label = Text("Label", button.transform, font, text, fontSize, White,
                TextAlignmentOptions.Center, rtl, true);
            Stretch(label.rectTransform, 12, 4);
            return button;
        }

        internal static void Stretch(RectTransform rect, float horizontalInset = 0, float verticalInset = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontalInset, verticalInset);
            rect.offsetMax = new Vector2(-horizontalInset, -verticalInset);
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        internal static void Shadow(Graphic graphic, Color color, Vector2 offset, float distance)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = offset * distance;
            shadow.useGraphicAlpha = true;
        }

        internal static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var eventSystem = new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(eventSystem);
        }

        internal static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
