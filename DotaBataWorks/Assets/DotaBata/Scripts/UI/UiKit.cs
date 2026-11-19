using UnityEngine;
using UnityEngine.UI;

namespace DotaBata
{
    /// <summary>
    /// HUDをコードで組み立てるための小道具。丸・角丸の画像もここで作るので、画像ファイルは要らない。
    /// 文字は端末の日本語フォントを使う（本制作では専用フォントに差し替える）。
    /// </summary>
    public static class UiKit
    {
        static Font font;
        static Sprite circle;
        static Sprite ring;
        static Sprite rounded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            font = null;
            circle = null;
            ring = null;
            rounded = null;
        }

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                try
                {
                    font = Font.CreateDynamicFontFromOSFont(
                        new[] { "Yu Gothic UI", "Meiryo UI", "Meiryo", "Hiragino Sans", "Noto Sans CJK JP", "Noto Sans JP", "Arial" }, 32);
                }
                catch (System.Exception)
                {
                    font = null;
                }
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Sprite Circle => circle != null ? circle : (circle = MakeCircle(128, 0f));
        public static Sprite Ring => ring != null ? ring : (ring = MakeCircle(128, 0.8f));
        public static Sprite Rounded => rounded != null ? rounded : (rounded = MakeRounded(64, 22));

        static Sprite MakeCircle(int size, float innerRatio)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float radius = size * 0.5f - 1f;
            float inner = radius * innerRatio;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    if (innerRatio > 0f) alpha *= Mathf.Clamp01(distance - inner + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float cx = Mathf.Clamp(px, radius, size - radius);
                    float cy = Mathf.Clamp(py, radius, size - radius);
                    float distance = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>親の中の1点に、決まった大きさで置く。</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>親いっぱいに広げる。</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, bool raycast = false)
        {
            RectTransform rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            if (sprite != null && sprite.border.sqrMagnitude > 0f) image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold)
        {
            RectTransform rt = Rect(name, parent);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = false;
            return label;
        }

        /// <summary>文字にふちを付けて、背景の上でも読めるようにする。</summary>
        public static void AddOutline(Graphic graphic, Color color, float distance = 3f)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
        }

        public static Button Button(string name, Transform parent, string text, int fontSize, Color background, Color textColor, out Text label)
        {
            Image image = Image(name, parent, Rounded, background, true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.highlightedColor = Color.white;
            button.colors = colors;
            label = Label("Label", image.transform, text, fontSize, textColor);
            Stretch(label.rectTransform);
            return button;
        }
    }
}
