using System.Collections;
using System.Collections.Generic;
using QAOffice;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hitman
{
    // Reading the paper: it lies folded on a table, unfolds, then the player has control. Swipe (or the arrows)
    // for the next page; tap an article to note it. Pages: front page, local news, classifieds.
    public class NewspaperView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        static readonly Color PaperColour = new Color(0.93f, 0.92f, 0.88f);
        static readonly Color InkColour = new Color(0.1f, 0.1f, 0.1f);
        static readonly string[] PageNames = { "", "Local News", "Classifieds" };

        RectTransform _root, _paper, _pages, _controls;
        Text _pageLabel;
        int _page;
        bool _ready;
        float _dragX;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public void Init(RectTransform parent)
        {
            var table = UIKit.Rect(parent, "Newspaper");
            _root = table.Stretch();
            var img = table.gameObject.AddComponent<RawImage>();
            img.texture = Planks();
            // forward drags on the table to the swipe handler
            var relay = table.gameObject.AddComponent<DragRelay>();
            relay.target = this;

            _paper = UIKit.Box(_root, "Paper", PaperColour, false).rectTransform;
            _paper.anchorMin = _paper.anchorMax = new Vector2(0.5f, 0.5f);
            _paper.sizeDelta = new Vector2(1180, 930);
            _paper.pivot = new Vector2(0.5f, 0.5f);
            var shadow = _paper.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.45f);
            shadow.effectDistance = new Vector2(14, -14);
            var prelay = _paper.gameObject.AddComponent<DragRelay>();
            prelay.target = this;
            _pages = UIKit.Rect(_paper, "Page").Stretch(40, 30, 40, 30);

            _controls = UIKit.Rect(_root, "Controls").Stretch();
            Arrow("<", new Vector2(0.06f, 0.5f), () => Turn(-1));
            Arrow(">", new Vector2(0.94f, 0.5f), () => Turn(1));
            var close = UIKit.Button(_controls, "Fold it away", UIKit.Panel, Close, 26);
            var cr = (RectTransform)close.transform;
            cr.anchorMin = cr.anchorMax = new Vector2(1, 1);
            cr.pivot = new Vector2(1, 1);
            cr.sizeDelta = new Vector2(240, 70);
            cr.anchoredPosition = new Vector2(-24, -24);
            _pageLabel = UIKit.Label(_controls, "", 24, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _pageLabel.rectTransform.Anchor(0.3f, 0.005f, 0.7f, 0.05f);
            _root.gameObject.SetActive(false);
        }

        void Arrow(string label, Vector2 at, System.Action act)
        {
            var b = UIKit.Button(_controls, label, new Color(0, 0, 0, 0.45f), act, 48);
            var r = (RectTransform)b.transform;
            r.anchorMin = r.anchorMax = at;
            r.sizeDelta = new Vector2(90, 120);
        }

        public void Open()
        {
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _page = 0;
            Fill();
            StopAllCoroutines();
            StartCoroutine(Unfold());
        }

        public void Close()
        {
            StopAllCoroutines();
            _root.gameObject.SetActive(false);
        }

        // Folded in half and lying at an angle, then opened out flat.
        IEnumerator Unfold()
        {
            _ready = false;
            _controls.gameObject.SetActive(false);
            _paper.pivot = new Vector2(0.5f, 1f);
            _paper.anchoredPosition = new Vector2(0, 465);
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / 0.5f)        // folded, lying there
            {
                float k = Mathf.SmoothStep(0, 1, t);
                _paper.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-14, -8, k));
                _paper.localScale = new Vector3(0.62f, 0.31f, 1);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.25f);
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / 0.7f)        // opening out
            {
                float k = Mathf.SmoothStep(0, 1, t);
                _paper.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-8, 0, k));
                _paper.localScale = new Vector3(Mathf.Lerp(0.62f, 0.95f, k), Mathf.Lerp(0.31f, 0.95f, k), 1);
                yield return null;
            }
            _paper.localRotation = Quaternion.identity;
            _paper.localScale = Vector3.one * 0.95f;
            _controls.gameObject.SetActive(true);
            _ready = true;
        }

        void Turn(int dir)
        {
            if (!_ready) return;
            int next = Mathf.Clamp(_page + dir, 0, PageNames.Length - 1);
            if (next == _page) return;
            StartCoroutine(Slide(dir, next));
        }

        IEnumerator Slide(int dir, int next)
        {
            _ready = false;
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / 0.18f)
            {
                _pages.anchoredPosition = new Vector2(-dir * 1200 * t, 0);
                yield return null;
            }
            _page = next;
            Fill();
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime / 0.18f)
            {
                _pages.anchoredPosition = new Vector2(dir * 1200 * (1 - t), 0);
                yield return null;
            }
            _pages.anchoredPosition = Vector2.zero;
            _ready = true;
        }

        public void OnBeginDrag(PointerEventData e) => _dragX = 0;
        public void OnDrag(PointerEventData e) => _dragX += e.delta.x;
        public void OnEndDrag(PointerEventData e)
        {
            if (Mathf.Abs(_dragX) > Screen.width * 0.06f) Turn(_dragX < 0 ? 1 : -1);
            _dragX = 0;
        }

        // ---------- the pages ----------

        void Fill()
        {
            UIKit.Clear(_pages);
            var g = Game.I;
            var articles = Contract.I != null ? Contract.I.Paper() : new List<Contract.Article>();
            _pageLabel.text = $"Page {_page + 1} of {PageNames.Length}  -  swipe to turn";
            var col = UIKit.Rect(_pages, "Column").Stretch();
            UIKit.VList(col, 8, 0);
            if (_page == 0)
            {
                Line(col, $"VOL. LXII   No. {g.Day + 211}", 18, TextAnchor.MiddleLeft, FontStyle.Normal, 24);
                Line(col, "The Daily Ledger", 92, TextAnchor.MiddleCenter, FontStyle.Bold, 110);
                Line(col, $"{g.Weekday.ToUpper()}, DAY {g.Day}            THE CITY'S PAPER            25 CENTS", 20, TextAnchor.MiddleCenter, FontStyle.Bold, 30);
                Rule(col, 4);
            }
            else
            {
                Line(col, $"The Daily Ledger  -  {g.Weekday}", 22, TextAnchor.MiddleLeft, FontStyle.Italic, 30);
                Line(col, PageNames[_page].ToUpper(), 60, TextAnchor.MiddleLeft, FontStyle.Bold, 76);
                Rule(col, 3);
            }
            bool lead = _page == 0;
            foreach (var a in articles)
            {
                if (a.page != _page) continue;
                var art = a;
                var box = UIKit.Rect(col, "Article");
                box.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
                var btn = box.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() => { if (_ready) Contract.I.NoteArticle(art); });
                UIKit.VList(box, 6, 4);
                int hs = lead ? 54 : 34;
                Line(box, a.headline, hs, TextAnchor.UpperLeft, FontStyle.Bold, 0);
                if (lead)
                {
                    var photo = UIKit.Rect(box, "Photo");
                    var ri = photo.gameObject.AddComponent<RawImage>();
                    ri.texture = Photo(g.Day + (Contract.I != null && Contract.I.StoryOut ? 50 : 0));
                    ri.raycastTarget = false;
                    photo.Size(240);
                }
                int bs = lead ? 28 : 24;
                Line(box, a.body, bs, TextAnchor.UpperLeft, FontStyle.Normal, 0);
                if (_page == 2) Rule(box, 1);
                lead = false;
                Rule(col, 1);
            }
            if (_page == 0)
            {
                // the bottom of the front page: weather and what's inside
                var row = UIKit.Rect(col, "Briefs");
                UIKit.HList(row, 24, 0).childForceExpandHeight = false;
                row.Size(150);
                var w = UIKit.Rect(row, "Weather");
                UIKit.VList(w, 2, 0);
                Line(w, "WEATHER", 24, TextAnchor.UpperLeft, FontStyle.Bold, 30);
                Line(w, new[] { "Grey skies, light rain by evening. High 16°C.", "Overcast. Smog warning downtown. High 18°C.", "Drizzle all day. High 14°C." }[g.Day % 3], 22, TextAnchor.UpperLeft, FontStyle.Normal, 0);
                var ix = UIKit.Rect(row, "Index");
                UIKit.VList(ix, 2, 0);
                Line(ix, "INSIDE", 24, TextAnchor.UpperLeft, FontStyle.Bold, 30);
                Line(ix, "Local News ........ page 2\nClassifieds ........ page 3", 22, TextAnchor.UpperLeft, FontStyle.Normal, 0);
            }
        }

        static int Rows(string s, int perRow) => Mathf.Max(1, Mathf.CeilToInt(s.Length / (float)perRow));

        static void Line(Transform parent, string text, int size, TextAnchor align, FontStyle style, float height)
        {
            var t = UIKit.Label(parent, text, size, InkColour, align, style);
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
            if (height > 0) t.Size(height);          // else it sizes itself to the wrapped text
        }

        static void Rule(Transform parent, float h)
        {
            var r = UIKit.Box(parent, "Rule", InkColour, false);
            r.raycastTarget = false;
            r.Size(h);
        }

        // A grainy black-and-white "photo": a street of buildings under a grey sky.
        static Texture2D Photo(int seed)
        {
            const int w = 192, h = 96;
            var rng = new System.Random(seed);
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) { float v = 0.72f + (i / w) * 0.002f; px[i] = new Color(v, v, v); }
            int x = 0;
            while (x < w)
            {
                int bw = rng.Next(12, 30), bh = rng.Next(25, 80);
                float shade = 0.25f + (float)rng.NextDouble() * 0.3f;
                for (int yy = 0; yy < bh; yy++)
                    for (int xx = x; xx < Mathf.Min(w, x + bw); xx++)
                    {
                        bool win = (xx - x) % 5 == 2 && yy % 6 == 3 && yy < bh - 3;
                        float v = win ? shade + 0.3f : shade;
                        px[yy * w + xx] = new Color(v, v, v);
                    }
                x += bw + rng.Next(0, 3);
            }
            for (int i = 0; i < px.Length; i++)
            {
                float n = ((float)rng.NextDouble() - 0.5f) * 0.12f;          // newsprint grain
                var c = px[i];
                px[i] = new Color(c.r + n, c.g + n, c.b + n);
            }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        // Blue-grey painted wooden planks.
        static Texture2D Planks()
        {
            const int w = 256, h = 256;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < h; y++)
            {
                int plank = y / 42;
                for (int x = 0; x < w; x++)
                {
                    float grain = Mathf.PerlinNoise(x * 0.02f + plank * 13, y * 0.4f) * 0.25f + Mathf.PerlinNoise(x * 0.3f, y * 0.05f) * 0.08f;
                    var c = Color.Lerp(new Color(0.42f, 0.55f, 0.62f), new Color(0.62f, 0.72f, 0.78f), grain + (plank % 2) * 0.1f);
                    if (y % 42 < 2) c *= 0.6f;
                    c.a = 1;
                    t.SetPixel(x, y, c);
                }
            }
            t.Apply();
            return t;
        }

        // Passes drags on child graphics up to the view.
        class DragRelay : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public NewspaperView target;
            public void OnBeginDrag(PointerEventData e) => target.OnBeginDrag(e);
            public void OnDrag(PointerEventData e) => target.OnDrag(e);
            public void OnEndDrag(PointerEventData e) => target.OnEndDrag(e);
        }
    }
}
