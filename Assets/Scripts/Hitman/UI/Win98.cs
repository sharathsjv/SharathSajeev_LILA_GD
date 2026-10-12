using System;
using QAOffice;
using UnityEngine;
using UnityEngine.UI;

namespace Hitman
{
    // The late-90s desktop look shared by the computer, tap menus, conversations and side panels: grey bevelled
    // frames, navy-to-blue title bars, flat grey buttons with black text.
    public static class Win98
    {
        public static readonly Color Face = new Color32(192, 192, 192, 255);
        public static readonly Color Hi = Color.white;
        public static readonly Color Shadow = new Color32(128, 128, 128, 255);
        public static readonly Color Dark = new Color32(32, 32, 32, 255);
        public static readonly Color Navy = new Color32(0, 0, 128, 255);
        public static readonly Color TitleEnd = new Color32(16, 132, 208, 255);
        public static readonly Color Teal = new Color32(0, 128, 128, 255);

        // Raised (buttons, windows) or sunken (fields, panes) grey frame.
        public static RectTransform Bevel(RectTransform rt, bool raised, Color? face = null)
        {
            var bg = rt.gameObject.GetComponent<Image>();
            if (bg == null) bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = null;
            bg.type = Image.Type.Simple;
            bg.color = face ?? Face;
            const float k = 3;
            Edge(rt, raised ? Hi : Shadow, new Vector2(0, 1), Vector2.one, new Vector2(0, -k), Vector2.zero);     // top
            Edge(rt, raised ? Hi : Shadow, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(k, 0));     // left
            Edge(rt, raised ? Dark : Hi, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, k));       // bottom
            Edge(rt, raised ? Dark : Hi, new Vector2(1, 0), Vector2.one, new Vector2(-k, 0), Vector2.zero);       // right
            return rt;
        }

        public static RectTransform Sunken(RectTransform rt) => Bevel(rt, false);

        static void Edge(RectTransform rt, Color c, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var e = UIKit.Box(rt, "Edge", c, false);
            e.raycastTarget = false;
            e.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var r = e.rectTransform;
            r.anchorMin = aMin;
            r.anchorMax = aMax;
            r.offsetMin = oMin;
            r.offsetMax = oMax;
        }

        public static Button Button98(Transform parent, string label, Action click, int size, bool bevel = true)
        {
            var rt = UIKit.Rect(parent, "Button " + label);
            if (bevel) Bevel(rt, true);
            else rt.gameObject.AddComponent<Image>().color = Face;
            var b = rt.gameObject.AddComponent<Button>();
            var cols = b.colors;
            cols.highlightedColor = new Color(0.85f, 0.88f, 1f);
            cols.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            b.colors = cols;
            if (click != null) b.onClick.AddListener(() => click());
            var t = Text98(rt, label, size, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Stretch(10, 2, 10, 2);
            return b;
        }

        public static Text Text98(Transform parent, string text, int size, TextAnchor align, FontStyle style = FontStyle.Normal)
        {
            var t = UIKit.Label(parent, text, size, Color.black, align, style);
            t.supportRichText = true;
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
            return t;
        }

        // A Win98 vertical scrollbar down the right of a scroll view (the view's viewport makes room for it).
        public static Scrollbar ScrollBar98(ScrollRect sr, float width = 26)
        {
            var bar = UIKit.Rect(sr.transform, "Scrollbar");
            bar.anchorMin = new Vector2(1, 0);
            bar.anchorMax = Vector2.one;
            bar.pivot = new Vector2(1, 0.5f);
            bar.sizeDelta = new Vector2(width, 0);
            bar.anchoredPosition = Vector2.zero;
            bar.gameObject.AddComponent<Image>().color = new Color32(224, 224, 224, 255);   // the dotted track, flat
            var area = UIKit.Rect(bar, "Sliding Area").Stretch(0, 0, 0, 0);
            var handle = Bevel(UIKit.Rect(area, "Handle"), true);
            handle.offsetMin = handle.offsetMax = Vector2.zero;
            var sb = bar.gameObject.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            sb.handleRect = handle;
            sb.targetGraphic = handle.GetComponent<Image>();
            sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            sr.viewport.offsetMax = new Vector2(-width, sr.viewport.offsetMax.y);
            return sb;
        }

        static Texture2D _title;
        public static Texture2D TitleGradient()
        {
            if (_title != null) return _title;
            _title = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++) _title.SetPixel(x, 0, Color.Lerp(Navy, TitleEnd, x / 63f));
            _title.Apply();
            return _title;
        }

        // A blue title bar across the top of a frame (height px); returns its label.
        public static Text TitleBar(RectTransform parent, string title, float height, int size, Action close = null)
        {
            var bar = UIKit.Rect(parent, "Title");
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = Vector2.one;
            bar.offsetMin = new Vector2(5, -5 - height);
            bar.offsetMax = new Vector2(-5, -5);
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var g = bar.gameObject.AddComponent<RawImage>();
            g.texture = TitleGradient();
            g.raycastTarget = false;
            var t = UIKit.Label(bar, title, size, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            t.rectTransform.Stretch(10, 0, close != null ? 50 : 8, 0);
            t.GetComponent<Outline>().enabled = false;
            if (close != null)
            {
                var x = Button98(bar, "X", close, size - 4);
                var xr = (RectTransform)x.transform;
                xr.anchorMin = xr.anchorMax = new Vector2(1, 0.5f);
                xr.pivot = new Vector2(1, 0.5f);
                xr.sizeDelta = new Vector2(height - 6, height - 8);
                xr.anchoredPosition = new Vector2(-4, 0);
            }
            return t;
        }
    }
}
