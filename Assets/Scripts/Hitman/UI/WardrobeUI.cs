using System.Collections;
using QAOffice;
using UnityEngine;
using UnityEngine.UI;

namespace Hitman
{
    // The wardrobe sequence: the doors open, the player turns to the camera for a close-up, and a panel lets them
    // choose who they are, whose hair / top / bottoms / shoes to wear and what colour. Changes show straight away.
    // The clock stops while they're choosing.
    public class WardrobeUI : MonoBehaviour
    {
        // The wardrobe at home (clothes + storing things), or the barber's chair (hair only, paid for).
        public enum Mode { Wardrobe, Barber }
        public const int HaircutPrice = 5;
        Mode _mode;
        Outfit _start;
        Text _title;
        static readonly string[] SlotNames = { "Hair", "Top", "Bottoms", "Shoes" };
        const float PanelShare = 0.45f;

        RectTransform _root, _panel, _content;
        ScrollRect _scroll;
        PlayerLook _look;
        Wardrobe _wardrobe;
        Outfit _o;
        bool _clockWas, _busy;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public void Init(RectTransform parent)
        {
            // Full-screen catcher so nothing behind (HUD buttons, the world) gets taps while dressing.
            var blocker = UIKit.Box(parent, "Wardrobe", new Color(0, 0, 0, 0), false);
            _root = blocker.rectTransform.Stretch();

            _panel = UIKit.Box(_root, "Panel", UIKit.Panel).rectTransform.Anchor(1 - PanelShare + 0.015f, 0.04f, 0.985f, 0.96f);
            var title = _title = UIKit.Label(_panel, "Wardrobe", 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.rectTransform.anchorMin = new Vector2(0, 1);
            title.rectTransform.anchorMax = new Vector2(1, 1);
            title.rectTransform.offsetMin = new Vector2(24, -66);
            title.rectTransform.offsetMax = new Vector2(-24, -10);

            _content = UIKit.ScrollList(_panel, 10, 18);
            _scroll = _content.GetComponentInParent<ScrollRect>();
            ((RectTransform)_scroll.transform).Stretch(12, 96, 12, 70);

            var bar = UIKit.Rect(_panel, "Buttons");
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(1, 0);
            bar.offsetMin = new Vector2(12, 12);
            bar.offsetMax = new Vector2(-12, 86);
            UIKit.HList(bar, 10, 0);
            UIKit.Button(bar, "< Turn", UIKit.PanelLight, () => Spin(-45), 24);
            UIKit.Button(bar, "Turn >", UIKit.PanelLight, () => Spin(45), 24);
            UIKit.Button(bar, "Reset", UIKit.Muted, () => Wear(_mode == Mode.Barber ? _start : new Outfit { who = _look.SceneCharacter }), 24);
            UIKit.Button(bar, "Done", UIKit.Accent, Close, 26);

            _root.gameObject.SetActive(false);
        }

        public void Open(Wardrobe w, Mode mode = Mode.Wardrobe)
        {
            var g = Game.I;
            _look = g.Player.GetComponent<PlayerLook>();
            if (_look == null || _busy || IsOpen) return;
            _mode = mode;
            _title.text = mode == Mode.Barber ? $"Barber - ${HaircutPrice} a cut" : "Wardrobe";
            _start = _look.Current;
            StartCoroutine(OpenRoutine(w));
        }

        IEnumerator OpenRoutine(Wardrobe w)
        {
            var g = Game.I;
            var player = g.Player.transform;
            _busy = true;
            _wardrobe = w;
            _clockWas = g.ClockRunning;
            g.ClockRunning = false;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _panel.gameObject.SetActive(false);

            if (w != null) w.Open(true);
            yield return new WaitForSeconds(0.6f);
            var cam = g.Cam.transform.forward;
            cam.y = 0;
            yield return Turn(player, Quaternion.LookRotation(-cam.normalized));
            g.Cam.Showcase(player, PanelShare);
            yield return new WaitForSeconds(0.25f);

            _o = _look.Current;
            Fill();
            _panel.gameObject.SetActive(true);
            _scroll.verticalNormalizedPosition = 1;
            _busy = false;
        }

        void Close()
        {
            if (_busy) return;
            if (_mode == Mode.Barber && Changed(Slot.Hair))
            {
                if (!Game.I.Spend(HaircutPrice)) { Wear(_start); return; }
                Game.I.UI.Toast("Fresh cut. Even your mother wouldn't recognise you. (Probably.)");
            }
            StartCoroutine(CloseRoutine());
        }

        bool Changed(Slot s) => _o.style[(int)s] != _start.style[(int)s] || _o.colour[(int)s] != _start.colour[(int)s] || _o.who != _start.who;

        // Caught mid-change (back to a checkpoint): shut without the sequence.
        public void ForceClose()
        {
            StopAllCoroutines();
            _panel.gameObject.SetActive(false);
            Game.I.Cam.Showcase(null);
            if (_wardrobe != null) _wardrobe.Open(false);
            if (IsOpen) Game.I.ClockRunning = _clockWas;      // (the open sequence stopped the clock)
            _root.gameObject.SetActive(false);
            _busy = false;
        }

        // The suit has to be bought (the suit shop in the mall) before it's in the wardrobe.
        bool Owned(int character) => !_look.Characters[character].suit || Game.I.S.Has("owns.suit") || character == _o.who;

        IEnumerator CloseRoutine()
        {
            var g = Game.I;
            var player = g.Player.transform;
            _busy = true;
            _panel.gameObject.SetActive(false);
            g.Cam.Showcase(null);
            if (_wardrobe != null)
            {
                var toward = _wardrobe.transform.position - player.position;
                toward.y = 0;
                if (toward.sqrMagnitude > 0.01f) yield return Turn(player, Quaternion.LookRotation(toward));
                _wardrobe.Open(false);
                yield return new WaitForSeconds(0.45f);
            }
            g.ClockRunning = _clockWas;
            _root.gameObject.SetActive(false);
            _busy = false;
        }

        static IEnumerator Turn(Transform t, Quaternion to)
        {
            var from = t.rotation;
            for (float k = 0; k < 1; k += Time.deltaTime / 0.35f)
            {
                t.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0, 1, k));
                yield return null;
            }
            t.rotation = to;
        }

        void Spin(float degrees)
        {
            if (_busy) return;
            var t = Game.I.Player.transform;
            StartCoroutine(Turn(t, Quaternion.Euler(0, degrees, 0) * t.rotation));
        }

        void Wear(Outfit o)
        {
            _look.Wear(o);
            _o = _look.Current;
            float scroll = _scroll.verticalNormalizedPosition;
            Fill();
            Canvas.ForceUpdateCanvases();
            _scroll.verticalNormalizedPosition = scroll;
        }

        // ---------- Panel contents ----------

        void Fill()
        {
            UIKit.Clear(_content);
            var chars = _look.Characters;

            Canvas.ForceUpdateCanvases();
            if (_mode == Mode.Wardrobe)
            {
                OwnedClothes();
                Storage();
                return;
            }

            bool onePiece = _look.OnePiece(_o.who);
            bool bald = !onePiece && !_look.HasPart(_o.who, Slot.Hair);      // (one-piece hair is in the body)
            for (int s = 0; s < 4; s++)
            {
                var slot = (Slot)s;
                int si = s;
                // Hair is done at the barber's; the barber only does hair.
                if ((slot == Slot.Hair) != (_mode == Mode.Barber)) continue;
                Header(SlotNames[s]);
                if (onePiece && slot != Slot.Hair)
                {
                    Note($"{chars[_o.who].name}'s clothes are one piece with the body, so they can't be changed.");
                    continue;
                }

                var styles = Grid(4, 56);
                Option(styles, bald && slot == Slot.Hair ? "Own (bald)" : "Own", _o.style[s] == Outfit.Own, () => SetStyle(si, Outfit.Own));
                if (slot == Slot.Hair && !bald && !onePiece) Option(styles, "None", _o.style[s] == Outfit.None, () => SetStyle(si, Outfit.None));
                for (int i = 0; i < chars.Length; i++)
                {
                    if (i == _o.who || !_look.HasPart(i, slot) || !_look.Fits(_o.who, i, slot) || !Owned(i)) continue;
                    int from = i;
                    Option(styles, chars[i].name, _o.style[s] == i, () => SetStyle(si, from));
                }

                // Nothing to colour when they're bald or have taken the part off.
                if (_o.style[s] == Outfit.None || (slot == Slot.Hair && bald && _o.style[s] == Outfit.Own)) continue;
                var colours = PlayerLook.Colours(slot);
                var swatches = Grid(9, 52);
                for (int c = 0; c < colours.Length; c++)
                {
                    int pick = c;
                    Swatch(swatches, c == 0 ? (Color?)null : colours[c], _o.colour[s] == c, () => SetColour(si, pick));
                }
            }
        }

        // The wardrobe at home: only the clothes the player owns (their own, plus anything bought).
        void OwnedClothes()
        {
            var g = Game.I;
            var chars = _look.Characters;
            foreach (var slot in new[] { Slot.Top, Slot.Bottoms, Slot.Shoes })
            {
                int si = (int)slot;
                Header(SlotNames[si]);
                var grid = Grid(2, 60);
                int n = 0;
                foreach (var key in g.S.clothes)
                {
                    var parts = key.Split('|');
                    if (parts.Length != 3 || parts[0] != slot.ToString()) continue;
                    int from = int.Parse(parts[1]), colour = int.Parse(parts[2]);
                    if (from >= 0 && !_look.Fits(_o.who, from, slot)) continue;
                    bool on = _o.style[si] == from && _o.colour[si] == colour;
                    Option(grid, GarmentName(slot, from), on, () =>
                    {
                        var o = _o.Copy();
                        o.style[si] = from;
                        o.colour[si] = colour;
                        Wear(o);
                    });
                    n++;
                }
                if (n == 0) Note("Nothing here.");
            }
        }

        string GarmentName(Slot slot, int from)
        {
            if (from < 0) return slot == Slot.Shoes ? "My shoes" : slot == Slot.Top ? "My top" : "My trousers";
            var c = _look.Characters[from];
            if (c.suit) return slot == Slot.Top ? "Suit jacket" : slot == Slot.Bottoms ? "Suit trousers" : "Suit shoes";
            return $"{c.name}'s {SlotNames[(int)slot].ToLower()}";
        }

        // Leaving things in the wardrobe (pockets hold 5).
        void Storage()
        {
            var g = Game.I;
            Header($"Pockets ({g.S.items.Count}/{State.PocketSize}) - tap to leave it here");
            var pockets = Grid(2, 60);
            foreach (var item in g.S.items.ToArray())
            {
                var it = item;
                Option(pockets, Items.Name(it), false, () => { g.S.items.Remove(it); g.S.stash.Add(it); g.Changed(); Wear(_o); });
            }
            Header("In the wardrobe - tap to take it");
            var stash = Grid(2, 60);
            if (g.S.stash.Count == 0) Note("Nothing stored.");
            foreach (var item in g.S.stash.ToArray())
            {
                var it = item;
                Option(stash, Items.Name(it), false, () =>
                {
                    if (g.S.Full) { g.UI.Toast("Your pockets are full."); return; }
                    g.S.stash.Remove(it); g.S.items.Add(it); g.Changed(); Wear(_o);
                });
            }
        }

        void SetStyle(int slot, int style)
        {
            var o = _o.Copy();
            o.style[slot] = style;
            Wear(o);
        }

        void SetColour(int slot, int colour)
        {
            var o = _o.Copy();
            o.colour[slot] = colour;
            Wear(o);
        }

        void Header(string text) => UIKit.Label(_content, text, 28, UIKit.Warn, TextAnchor.LowerLeft, FontStyle.Bold).Size(46);

        void Note(string text) => UIKit.Label(_content, text, 21, UIKit.Muted).Size(56);

        // Columns sized to fill the panel's width.
        RectTransform Grid(int columns, float height)
        {
            var rt = UIKit.Rect(_content, "Grid");
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            var pad = _content.GetComponent<VerticalLayoutGroup>().padding;
            float width = _content.rect.width - pad.left - pad.right;
            g.cellSize = new Vector2(Mathf.Floor((width - 8 * (columns - 1)) / columns), height);
            g.spacing = new Vector2(8, 8);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = columns;
            return rt;
        }

        static void Option(RectTransform grid, string label, bool on, System.Action pick)
        {
            UIKit.Button(grid, label, on ? UIKit.Accent : UIKit.PanelLight, on ? null : pick, 22);
        }

        static void Swatch(RectTransform grid, Color? colour, bool on, System.Action pick)
        {
            var b = UIKit.Button(grid, colour == null ? "Orig." : "", colour ?? UIKit.PanelLight, on ? null : pick, 18);
            if (!on) return;
            var o = b.gameObject.AddComponent<Outline>();
            o.effectColor = Color.white;
            o.effectDistance = new Vector2(4, -4);
        }
    }
}
