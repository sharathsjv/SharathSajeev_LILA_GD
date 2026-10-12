using System;
using System.Collections.Generic;
using QAOffice;
using UnityEngine;
using UnityEngine.UI;

namespace Hitman
{
    // The notebook: a yellow ruled legal pad with index tabs down its right edge (Notes, My notes, Pockets).
    // Every line of writing sits on a rule: entries are sized to whole lines and the rules scroll with the text.
    public class NotebookView : MonoBehaviour
    {
        static readonly Color PaperColour = new Color(0.99f, 0.95f, 0.62f);
        static readonly Color Ink = new Color(0.12f, 0.14f, 0.3f);
        static readonly Color RedInk = new Color(0.65f, 0.1f, 0.08f);
        static readonly Color GreenInk = new Color(0.1f, 0.4f, 0.2f);
        static readonly Color Pencil = new Color(0.35f, 0.38f, 0.5f);
        static readonly string[] TabNames = { "Notes", "My notes", "Pockets" };
        static readonly Color[] TabColours = { new Color(0.95f, 0.55f, 0.45f), new Color(0.55f, 0.75f, 0.95f), new Color(0.6f, 0.85f, 0.55f) };
        const int FontSize = 26;

        RectTransform _root, _pad, _viewport, _content, _tabs;
        RawImage _rules;
        ScrollRect _scroll;
        InputField _writing;
        float _line;           // height of one line of writing (= the rule spacing)
        int _tab;
        readonly HashSet<string> _openIdeas = new HashSet<string>();

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;
        public int Tab => _tab;

        public void Init(RectTransform parent)
        {
            // tapping anywhere off the pad closes it
            var dim = UIKit.Button(parent, "", new Color(0, 0, 0, 0.45f), Close);
            dim.GetComponent<Image>().sprite = null;
            _root = ((RectTransform)dim.transform).Stretch();
            dim.transition = Selectable.Transition.None;

            _pad = UIKit.Box(_root, "Pad", PaperColour, false).rectTransform;
            _pad.anchorMin = new Vector2(0.4f, 0.04f);
            _pad.anchorMax = new Vector2(0.88f, 0.96f);
            _pad.offsetMin = _pad.offsetMax = Vector2.zero;
            Win98.Bevel(_pad, true, PaperColour);            // hard edges, like the rest of the UI
            var shadow = _pad.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(10, -10);
            // swallow taps on the paper so they don't close it
            _pad.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            // the glued binding at the top
            var bind = UIKit.Box(_pad, "Binding", new Color(0.55f, 0.12f, 0.1f), false).rectTransform;
            bind.anchorMin = new Vector2(0, 1);
            bind.anchorMax = Vector2.one;
            bind.offsetMin = new Vector2(0, -46);
            bind.offsetMax = Vector2.zero;

            _line = MeasureLine();
            _viewport = UIKit.Rect(_pad, "Page");
            _viewport.Stretch(0, 20, 0, 70);
            _viewport.gameObject.AddComponent<RectMask2D>();
            _rules = UIKit.Rect(_viewport, "Rules").Stretch().gameObject.AddComponent<RawImage>();
            _rules.texture = RuleTexture();
            _rules.raycastTarget = false;
            var margin = UIKit.Box(_viewport, "Margin", new Color(0.85f, 0.3f, 0.3f, 0.8f), false);
            margin.raycastTarget = false;
            margin.rectTransform.anchorMin = new Vector2(0, 0);
            margin.rectTransform.anchorMax = new Vector2(0, 1);
            margin.rectTransform.offsetMin = new Vector2(78, 0);
            margin.rectTransform.offsetMax = new Vector2(81, 0);

            _content = UIKit.Rect(_viewport, "Writing");
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1);
            _content.offsetMin = new Vector2(96, 0);
            _content.offsetMax = new Vector2(-30, 0);
            var v = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.spacing = 0;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll = _viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.content = _content;
            _scroll.viewport = _viewport;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 30;

            // index tabs sticking out of the right edge
            _tabs = UIKit.Rect(_pad, "Tabs");
            _tabs.anchorMin = new Vector2(1, 1);
            _tabs.anchorMax = new Vector2(1, 1);
            _tabs.pivot = new Vector2(0, 1);
            _tabs.sizeDelta = new Vector2(64, 3 * 170);
            _tabs.anchoredPosition = new Vector2(-6, -80);
            for (int i = 0; i < TabNames.Length; i++)
            {
                int k = i;
                var tab = UIKit.Box(_tabs, "Tab " + TabNames[i], TabColours[i]);
                var tr = tab.rectTransform;
                tr.anchorMin = tr.anchorMax = new Vector2(0, 1);
                tr.pivot = new Vector2(0, 1);
                tr.sizeDelta = new Vector2(64, 160);
                tr.anchoredPosition = new Vector2(0, -i * 170);
                tab.gameObject.AddComponent<Button>().onClick.AddListener(() => Open(k));
                var t = UIKit.Label(tr, TabNames[i], 22, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.GetComponent<Outline>().enabled = false;
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                t.rectTransform.sizeDelta = new Vector2(150, 50);
                t.rectTransform.localRotation = Quaternion.Euler(0, 0, -90);
            }
            _tabs.SetAsFirstSibling();          // behind the paper, so they look tucked under it

            _root.gameObject.SetActive(false);
        }

        public void Open(int tab)
        {
            _tab = tab;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            for (int i = 0; i < _tabs.childCount; i++)
            {
                var r = (RectTransform)_tabs.GetChild(i);
                r.anchoredPosition = new Vector2(i == tab ? 6 : 0, r.anchoredPosition.y);
                r.GetComponent<Image>().color = i == tab ? PaperColour : TabColours[i];
            }
            Refresh();
            _scroll.verticalNormalizedPosition = 1;
        }

        public void Close()
        {
            Save();
            _root.gameObject.SetActive(false);
            Game.I.Player.Seated = false;
        }

        void Save()
        {
            if (_writing != null) Game.I.S.notepad = _writing.text;
        }

        void LateUpdate()
        {
            if (!IsOpen) return;
            // the rules move with the writing
            float y = _content.anchoredPosition.y;
            float rows = _viewport.rect.height / _line;
            _rules.uvRect = new Rect(0, -rows + y / _line, 1, rows);
        }

        // ---------- pages ----------

        public void Refresh()
        {
            if (!IsOpen) return;
            Save();
            _writing = null;
            UIKit.Clear(_content);
            _scroll.enabled = true;
            switch (_tab)
            {
                case 0: Notes(); break;
                case 1: MyNotes(); break;
                default: Pockets(); break;
            }
        }

        void Notes()
        {
            var g = Game.I;
            // The to-do list, struck out as I get there.
            var todo = Contract.I != null ? Contract.I.Objectives() : null;
            if (todo != null && todo.Count > 0)
            {
                Write("To do", Ink, FontStyle.Bold);
                foreach (var o in todo)
                {
                    var t = Write("  - " + o.text + (o.complete && o.done != null ? $"  <color=#6A6F99>({o.done})</color>" : ""), o.complete ? Pencil : Ink);
                    if (o.complete) Strike(t, "  - " + o.text);
                }
                Write(" ", Ink);
            }
            Write("Things I've found out", Ink, FontStyle.Bold);
            if (g.S.notes.Count == 0)
            {
                Write("Nothing yet. What I find out, and the routines I write down, go here.", Pencil);
                Write("(Every new note is a checkpoint.)", Pencil);
                return;
            }
            for (int i = g.S.notes.Count - 1; i >= 0; i--)
            {
                var n = g.S.notes[i];
                string when = $"{Game.Weekdays[(n.day - 1) % 7].Substring(0, 3)} {Game.Clock(n.minutes)}";
                var idea = n.opening > 0 ? Contract.Idea(n.opening) : null;
                var colour = idea != null ? RedInk : n.kind == NoteKind.Tip ? GreenInk : Ink;
                var line = Write($"<color=#6A6F99>{when}</color>  {(idea != null ? "* " : "")}{n.text}", colour);
                if (idea == null) continue;
                var id = n.id;
                var b = line.gameObject.AddComponent<Button>();
                b.targetGraphic = line;
                line.raycastTarget = true;
                b.onClick.AddListener(() => { if (!_openIdeas.Add(id)) _openIdeas.Remove(id); Refresh(); });
                if (_openIdeas.Contains(id)) Write("   Idea: " + idea, RedInk, FontStyle.Italic);
                else Write("   (tap for the idea)", Pencil, FontStyle.Italic);
            }
        }

        void MyNotes()
        {
            var g = Game.I;
            _scroll.enabled = false;
            var box = UIKit.Rect(_content, "Field");
            box.gameObject.AddComponent<LayoutElement>().preferredHeight = Mathf.Floor(_viewport.rect.height / _line) * _line;
            var hit = box.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var input = box.gameObject.AddComponent<InputField>();
            input.lineType = InputField.LineType.MultiLineNewline;
            var t = Pen(box, "", Ink);
            t.rectTransform.Stretch();
            var ph = Pen(box, "Write your own notes here...", Pencil);
            ph.rectTransform.Stretch();
            input.textComponent = t;
            input.placeholder = ph;
            input.text = g.S.notepad;
            input.onValueChanged.AddListener(s => g.S.notepad = s);
            _writing = input;
        }

        void Pockets()
        {
            var g = Game.I;
            Write($"Cash: ${g.S.cash}", Ink, FontStyle.Bold);
            Write($"In my pockets ({g.S.items.Count}/{State.PocketSize}):", Ink, FontStyle.Bold);
            if (g.S.items.Count == 0) Write("   nothing", Pencil);
            foreach (var item in g.S.items.ToArray())
            {
                var it = Items.Get(item);
                bool unpaid = g.S.unpaid.Exists(u => u.EndsWith("|" + item));
                Write($" - {it.name}{(unpaid ? "  (not paid for!)" : "")}   <color=#6A6F99>{it.info}</color>", unpaid ? RedInk : Ink);
                var acts = new List<(string, Action)>();
                Contract.I?.AddPocketActions(item, acts);
                foreach (var (label, act) in acts)
                {
                    var a = act;
                    var l = Write($"      >> {label}", new Color(0.1f, 0.25f, 0.8f), FontStyle.Bold);
                    var b = l.gameObject.AddComponent<Button>();
                    b.targetGraphic = l;
                    l.raycastTarget = true;
                    b.onClick.AddListener(() => a());
                }
            }
            var known = new List<string>();
            foreach (var w in Watcher.All)
            {
                float v = Suspicion.I != null ? Suspicion.I.InfamyWith(w) : 0;
                if (v > 0) known.Add($" - {w.DisplayName} {new string('*', Mathf.CeilToInt(v))}");
            }
            if (known.Count > 0)
            {
                Write("Who'd recognise me right now:", RedInk, FontStyle.Bold);
                foreach (var k in known) Write(k, RedInk);
                Write("(change my top and hair to be a stranger again)", Pencil, FontStyle.Italic);
            }
        }

        // A pen line through the first line of some writing (up to the end of `struck`).
        void Strike(Text t, string struck)
        {
            var line = UIKit.Box(t.transform, "Strike", Ink, false);
            line.raycastTarget = false;
            var r = line.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 0.5f);
            var gen = new TextGenerator();
            var settings = t.GetGenerationSettings(new Vector2(10000, 1000));
            float w = gen.GetPreferredWidth(struck, settings) / t.pixelsPerUnit;
            float lead = gen.GetPreferredWidth("  - ", settings) / t.pixelsPerUnit;
            r.sizeDelta = new Vector2(Mathf.Max(0, w - lead), 2.5f);
            r.anchoredPosition = new Vector2(lead, -_line * 0.55f);
        }

        // A block of writing, sized to whole lines so it sits on the rules.
        Text Write(string text, Color colour, FontStyle style = FontStyle.Normal)
        {
            var t = Pen(_content, text, colour, style);
            var le = t.gameObject.AddComponent<LayoutElement>();
            float w = Mathf.Max(100, _viewport.rect.width - 126);
            var settings = t.GetGenerationSettings(new Vector2(w, 0));
            float h = t.cachedTextGeneratorForLayout.GetPreferredHeight(text, settings) / t.pixelsPerUnit;
            le.preferredHeight = Mathf.Max(1, Mathf.Round(h / _line)) * _line;
            return t;
        }

        Text Pen(Transform parent, string text, Color colour, FontStyle style = FontStyle.Normal)
        {
            var t = UIKit.Label(parent, text, FontSize, colour, TextAnchor.UpperLeft, style);
            t.GetComponent<Outline>().enabled = false;
            t.supportRichText = true;
            t.lineSpacing = 1;
            return t;
        }

        // The height of one line of writing at this font size (the rule spacing).
        float MeasureLine()
        {
            var t = Pen(_pad, "", Ink);
            var s = t.GetGenerationSettings(new Vector2(1000, 0));
            float one = t.cachedTextGeneratorForLayout.GetPreferredHeight("Ag", s);
            float three = t.cachedTextGeneratorForLayout.GetPreferredHeight("Ag\nAg\nAg", s);
            Destroy(t.gameObject);
            float line = (three - one) / 2f / Mathf.Max(0.01f, t.pixelsPerUnit);
            return line > 4 ? line : FontSize * 1.15f;
        }

        // One rule: pale blue line at the bottom of each line of writing.
        static Texture2D RuleTexture()
        {
            var t = new Texture2D(4, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 4; x++)
                    t.SetPixel(x, y, y <= 1 ? new Color(0.45f, 0.6f, 0.85f, 0.85f) : new Color(0, 0, 0, 0));
            t.Apply();
            return t;
        }
    }
}
