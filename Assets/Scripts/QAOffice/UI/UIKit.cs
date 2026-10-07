using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // Helpers for building uGUI from code. Everything uses the built-in font so no TMP setup is needed.
    public static class UIKit
    {
        public static readonly Color Ink = Hex("#1E2230");
        public static readonly Color Paper = Hex("#F4F1EA");
        public static readonly Color Panel = Hex("#2B3042");
        public static readonly Color PanelLight = Hex("#3A4058");
        public static readonly Color Accent = Hex("#4F8EF7");
        public static readonly Color Good = Hex("#3FBF7F");
        public static readonly Color Bad = Hex("#E5534B");
        public static readonly Color Warn = Hex("#F2B33D");
        public static readonly Color Muted = Hex("#9AA0B4");

        static Font _font;
        public static Font Font => _font ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        // Sprite assets in Resources/UI (scene UI references them too); procedural fallback if missing.
        static Sprite _rounded;
        public static Sprite Rounded => _rounded ? _rounded : (_rounded = Load("UI/Rounded") ?? Sprite.Create(RoundedTexture(), new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, Vector4.one * RoundedRadius));
        public const int RoundedRadius = 16;

        static Sprite Load(string path)
        {
            var s = Resources.Load<Sprite>(path);
            return s != null ? s : null;
        }

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        // Anchors in 0..1 parent space, with optional pixel insets.
        public static RectTransform Anchor(this RectTransform rt, float minX, float minY, float maxX, float maxY, float inset = 0)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Box(Transform parent, string name, Color color, bool rounded = true)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            if (rounded)
            {
                img.sprite = Rounded;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public static Button Button(Transform parent, string label, Color color, Action onClick, int fontSize = 30)
        {
            var img = Box(parent, "Button " + label, color);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var t = Label(img.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Stretch(10, 4, 10, 4);
            return btn;
        }

        public static void SetLabel(this Button b, string text)
        {
            b.GetComponentInChildren<Text>().text = text;
        }

        public static VerticalLayoutGroup VList(RectTransform rt, float spacing, int padding, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = align;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HList(RectTransform rt, float spacing, int padding)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(padding, padding, padding, padding);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            return h;
        }

        public static LayoutElement Size(this Component c, float prefHeight = -1, float prefWidth = -1, float flexW = -1)
        {
            if (!c.TryGetComponent<LayoutElement>(out var le)) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = prefHeight;
            le.preferredWidth = prefWidth;
            le.flexibleWidth = flexW;
            return le;
        }

        // A vertical scroll view; returns the content transform to put rows into.
        public static RectTransform ScrollList(Transform parent, float spacing = 12, int padding = 16)
        {
            var bg = Box(parent, "Scroll", new Color(0, 0, 0, 0.15f));
            var scroll = bg.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            var viewport = Rect(bg.transform, "Viewport").Stretch();
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            VList(content, spacing, padding);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        // Horizontal bar: background + fill; returns the fill image (use fillAmount).
        public static Image Bar(Transform parent, Color fill, float height = 18)
        {
            var bg = Box(parent, "Bar", new Color(0, 0, 0, 0.35f));
            bg.Size(height);
            var f = Box(bg.transform, "Fill", fill);
            f.rectTransform.Stretch();
            f.type = Image.Type.Filled;
            f.sprite = Square;
            f.fillMethod = Image.FillMethod.Horizontal;
            return f;
        }

        static Sprite _square;
        public static Sprite Square
        {
            get
            {
                if (_square) return _square;
                return _square = Load("UI/Square") ?? Sprite.Create(SquareTexture(), new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100);
            }
        }

        public static Texture2D SquareTexture()
        {
            var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
            var px = new Color32[16];
            for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        public static Texture2D RoundedTexture()
        {
            const int size = 64, radius = RoundedRadius;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        public static string Clock(float minutesSinceStart, int startHour)
        {
            int total = Mathf.FloorToInt(minutesSinceStart) + startHour * 60;
            return $"{total / 60:00}:{total % 60:00}";
        }
    }

    // Procedural icon sprites so topics/tiles need no art assets.
    public static class Icons
    {
        public static readonly string[] ShapeNames = { "circle", "square", "triangle", "diamond", "star", "heart", "ring", "cross", "hexagon" };

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Shape(string shape)
        {
            if (Cache.TryGetValue(shape, out var s)) return s;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var p = new Vector2((x + 0.5f) / n * 2 - 1, (y + 0.5f) / n * 2 - 1); // -1..1
                float d = Sdf(shape, p) * n * 0.5f; // in pixels, negative inside
                byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255);
                px[y * n + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
            Cache[shape] = s;
            return s;
        }

        static float Sdf(string shape, Vector2 p)
        {
            switch (shape)
            {
                case "square": return Box(p, new Vector2(0.72f, 0.72f));
                case "diamond": return (Mathf.Abs(p.x) + Mathf.Abs(p.y) - 0.9f) * 0.7071f;
                case "triangle":
                {
                    var q = new Vector2(p.x, p.y + 0.15f);
                    float k = Mathf.Sqrt(3);
                    return Mathf.Max(Mathf.Abs(q.x) * k * 0.5f + q.y * 0.5f, -q.y) - 0.42f;
                }
                case "ring": return Mathf.Abs(p.magnitude - 0.62f) - 0.2f;
                case "cross": return Mathf.Min(Box(p, new Vector2(0.82f, 0.26f)), Box(p, new Vector2(0.26f, 0.82f)));
                case "hexagon":
                {
                    var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
                    return Mathf.Max(q.x * 0.866f + q.y * 0.5f, q.y) - 0.75f;
                }
                case "star":
                {
                    // Exact 5-point star SDF (Inigo Quilez), point facing up.
                    const float r = 0.92f, rf = 0.45f;
                    var k1 = new Vector2(0.809017f, -0.587785f);
                    var k2 = new Vector2(-k1.x, k1.y);
                    p.x = Mathf.Abs(p.x);
                    p -= 2f * Mathf.Max(Vector2.Dot(k1, p), 0f) * k1;
                    p -= 2f * Mathf.Max(Vector2.Dot(k2, p), 0f) * k2;
                    p.x = Mathf.Abs(p.x);
                    p.y -= r;
                    var ba = rf * new Vector2(-k1.y, k1.x) - new Vector2(0, 1);
                    float h = Mathf.Clamp(Vector2.Dot(p, ba) / Vector2.Dot(ba, ba), 0f, r);
                    return (p - ba * h).magnitude * Mathf.Sign(p.y * ba.x - p.x * ba.y);
                }
                case "heart":
                {
                    var q = new Vector2(p.x * 1.1f, p.y * 1.1f + 0.2f);
                    float a = q.x * q.x + q.y * q.y - 0.5f;
                    return (a * a * a - q.x * q.x * q.y * q.y * q.y) * 2f;
                }
                default: return p.magnitude - 0.85f; // circle
            }
        }

        static float Box(Vector2 p, Vector2 b)
        {
            var d = new Vector2(Mathf.Abs(p.x) - b.x, Mathf.Abs(p.y) - b.y);
            return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0);
        }
    }
}
