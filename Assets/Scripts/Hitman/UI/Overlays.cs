using System;
using System.Collections;
using System.Collections.Generic;
using QAOffice;
using UnityEngine;
using UnityEngine.UI;

namespace Hitman
{
    // Everything that pops up over the game: conversations, "busted", contract complete, speech bubbles, the
    // "being watched" meter, the watch, the pause menu and reflex vision. Built in code (all of it is dynamic).
    public class Overlays : MonoBehaviour
    {
        RectTransform _root;

        // dialog
        RectTransform _dialog, _replies;
        Text _speaker, _line;

        // busted / contract done
        RectTransform _card;
        Text _cardTitle, _cardBody;
        Button _cardButton;

        // bubbles
        class Bubble { public RectTransform rt; public Text text; public Transform who; public float until; }
        readonly List<Bubble> _bubbles = new List<Bubble>();

        // noticed meter
        RectTransform _noticed;

        // watch / pause
        RectTransform _watch, _pause;
        Text _watchTime, _watchDay, _watchInfo;

        // reflex vision
        Button _reflexButton;
        RectTransform _reflex, _markers;
        bool _reflexOn;
        readonly List<(RectTransform rt, BlendSpot spot)> _marks = new List<(RectTransform, BlendSpot)>();

        public bool Blocking => _dialog.gameObject.activeSelf || _card.gameObject.activeSelf || _watch.gameObject.activeSelf || _pause.gameObject.activeSelf;
        public bool Paused => _pause.gameObject.activeSelf;

        public void Init(RectTransform parent, Button notebookButton)
        {
            _root = parent;
            BuildHud(notebookButton);
            BuildNoticed();
            BuildKnown();
            BuildReflex();
            BuildDialog();
            BuildCard();
            BuildWatch();
            BuildPause();
        }

        // ---------- HUD: watch, pause (notebook is in the scene) ----------

        void BuildHud(Button notebookButton)
        {
            var watchBtn = UIKit.Button(_root, "", UIKit.Panel, ShowWatch);
            watchBtn.name = "Watch";
            WatchButton = (RectTransform)watchBtn.transform;
            ((RectTransform)watchBtn.transform).anchorMin = ((RectTransform)watchBtn.transform).anchorMax = new Vector2(0, 1);
            ((RectTransform)watchBtn.transform).offsetMin = new Vector2(24, -144);
            ((RectTransform)watchBtn.transform).offsetMax = new Vector2(144, -24);
            var face = UIKit.Box(watchBtn.transform, "Face", new Color(0.93f, 0.9f, 0.82f));
            face.sprite = Circle();
            face.type = Image.Type.Simple;
            face.rectTransform.Stretch(18, 18, 18, 18);
            face.raycastTarget = false;
            Hand(face.rectTransform, 0.32f, 6, 300);
            Hand(face.rectTransform, 0.42f, 4, 60);

            var pauseBtn = UIKit.Button(_root, "", UIKit.Panel, () => SetPaused(true));
            var prt = (RectTransform)pauseBtn.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(1, 1);
            prt.offsetMin = new Vector2(-144, -144);
            prt.offsetMax = new Vector2(-24, -24);
            foreach (var x in new[] { 0.32f, 0.58f })
            {
                var bar = UIKit.Box(prt, "Bar", Color.white, false);
                bar.rectTransform.anchorMin = new Vector2(x, 0.27f);
                bar.rectTransform.anchorMax = new Vector2(x + 0.1f, 0.73f);
                bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
                bar.raycastTarget = false;
            }
        }

        static void Hand(RectTransform face, float length, float width, float angle)
        {
            var h = UIKit.Box(face, "Hand", UIKit.Ink, false);
            h.raycastTarget = false;
            var rt = h.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(width, 84 * length);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.Euler(0, 0, -angle);
        }

        // ---------- Conversation ----------

        // A conversation is a speech bubble over whoever's talking (over the player for phone calls), with the
        // player's possible answers as small reply bubbles along the bottom of the screen.
        Transform _dialogWho;
        RectTransform _dialogBubble;

        void BuildDialog()
        {
            _dialog = UIKit.Rect(_root, "Dialog").Stretch();
            _dialogBubble = Win98.Bevel(UIKit.Rect(_dialog, "Bubble"), true);
            _dialogBubble.anchorMin = _dialogBubble.anchorMax = new Vector2(0.5f, 0.5f);
            _dialogBubble.pivot = new Vector2(0.5f, 0);
            var v = UIKit.VList(_dialogBubble, 4, 18);
            v.childAlignment = TextAnchor.UpperLeft;
            v.padding.top = 58;
            var fit = _dialogBubble.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _dialogBubble.sizeDelta = new Vector2(560, 100);
            _speaker = Win98.TitleBar(_dialogBubble, "", 42, 22);
            _line = Win98.Text98(_dialogBubble, "", 26, TextAnchor.UpperLeft);
            // the tail pointing down at the speaker
            var tail = UIKit.Box(_dialogBubble, "Tail", Win98.Face, false);
            tail.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(0.5f, 0);
            tail.rectTransform.sizeDelta = new Vector2(28, 28);
            tail.rectTransform.anchoredPosition = new Vector2(0, 2);
            tail.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            tail.raycastTarget = false;

            _replies = UIKit.Rect(_dialog, "Replies");
            _replies.anchorMin = new Vector2(0.3f, 0.03f);
            _replies.anchorMax = new Vector2(0.7f, 0.03f);
            _replies.pivot = new Vector2(0.5f, 0);
            var rv = UIKit.VList(_replies, 10, 0);
            rv.childAlignment = TextAnchor.LowerCenter;
            _replies.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _dialog.gameObject.SetActive(false);
        }

        public void ShowDialog(string speaker, string line, List<(string label, Action act)> replies, Transform who = null)
        {
            _dialogWho = who;
            _speaker.text = speaker;
            if (string.IsNullOrEmpty(speaker)) _speaker.text = "...";
            _line.text = line;
            UIKit.Clear(_replies);
            if (replies == null || replies.Count == 0) replies = new List<(string, Action)> { ("OK", null) };
            foreach (var (label, act) in replies)
            {
                var a = act;
                var b = Win98.Button98(_replies, label, () =>
                {
                    _dialog.gameObject.SetActive(false);
                    a?.Invoke();
                }, 24);
                b.Size(64);
            }
            _dialog.gameObject.SetActive(true);
            _dialog.SetAsLastSibling();
            PlaceDialog();
        }

        void PlaceDialog()
        {
            var g = Game.I;
            var cam = g.Cam.GetComponent<Camera>();
            var who = _dialogWho != null ? _dialogWho : g.Player.transform;
            var sp = cam.WorldToScreenPoint(who.position + Vector3.up * 2.1f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var local);
            var half = _root.rect.size / 2;
            Canvas.ForceUpdateCanvases();
            float h = _dialogBubble.rect.height;
            local.x = Mathf.Clamp(local.x, -half.x + 300, half.x - 300);
            local.y = Mathf.Clamp(local.y + 20, -half.y + 260, half.y - h - 20);   // keep clear of the replies
            _dialogBubble.anchoredPosition = local;
        }

        // ---------- Busted / contract done ----------

        void BuildCard()
        {
            var blocker = UIKit.Box(_root, "Card", new Color(0.05f, 0.02f, 0.02f, 0.88f), false);
            _card = blocker.rectTransform.Stretch();
            var box = UIKit.Rect(_card, "Box").Anchor(0.2f, 0.2f, 0.8f, 0.8f);
            UIKit.VList(box, 20, 20, TextAnchor.MiddleCenter);
            _cardTitle = UIKit.Label(box, "", 72, UIKit.Bad, TextAnchor.MiddleCenter, FontStyle.Bold);
            _cardTitle.Size(100);
            _cardBody = UIKit.Label(box, "", 32, Color.white, TextAnchor.MiddleCenter);
            _cardBody.Size(160);
            _cardButton = UIKit.Button(box, "OK", UIKit.Accent, () => _card.gameObject.SetActive(false), 30);
            _cardButton.Size(80);
            _card.gameObject.SetActive(false);
        }

        public IEnumerator ShowBusted(string reason)
        {
            _cardTitle.text = "BUSTED";
            _cardTitle.color = UIKit.Bad;
            _cardBody.text = reason + "\n\n<size=24>Back to your last note...</size>";
            _cardButton.gameObject.SetActive(false);
            _card.gameObject.SetActive(true);
            _card.SetAsLastSibling();
            yield return new WaitForSecondsRealtime(3.5f);
            _card.gameObject.SetActive(false);
        }

        public void ShowContractDone(string name, int pay, bool onTime)
        {
            _cardTitle.text = "CONTRACT COMPLETE";
            _cardTitle.color = UIKit.Good;
            _cardBody.text = $"{name}'s name is in the morning paper.\n\n" +
                             (onTime ? $"Paid in full: ${pay}." : $"It took too long. You get ${pay}.") +
                             "\n\n<size=24>You can carry on living your life.</size>";
            _cardButton.gameObject.SetActive(true);
            _card.gameObject.SetActive(true);
            _card.SetAsLastSibling();
        }

        // ---------- Speech bubbles ----------

        public void Say(Transform who, string text, float seconds = 3.2f)
        {
            Bubble b = null;
            foreach (var x in _bubbles) if (x.who == who) { b = x; break; }
            if (b == null)
            {
                var box = UIKit.Box(_root, "Bubble", new Color(0.97f, 0.96f, 0.92f, 0.96f));
                box.raycastTarget = false;
                var t = UIKit.Label(box.transform, "", 24, UIKit.Ink, TextAnchor.MiddleCenter);
                t.rectTransform.Stretch(14, 8, 14, 8);
                b = new Bubble { rt = box.rectTransform, text = t, who = who };
                b.rt.sizeDelta = new Vector2(420, 90);
                _bubbles.Add(b);
            }
            b.text.text = text;
            b.until = Time.time + seconds;
            b.rt.gameObject.SetActive(true);
            b.rt.SetAsLastSibling();
            Place(b);
        }

        void Place(Bubble b)
        {
            var g = Game.I;
            var cam = g.Cam.GetComponent<Camera>();
            Vector3 world = b.who != null ? b.who.position + Vector3.up * 2.15f : g.Player.transform.position + Vector3.up * 2.3f;
            var sp = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var local);
            var half = _root.rect.size / 2;
            local.x = Mathf.Clamp(local.x, -half.x + 220, half.x - 220);
            local.y = Mathf.Clamp(local.y + 50, -half.y + 60, half.y - 60);
            b.rt.anchorMin = b.rt.anchorMax = new Vector2(0.5f, 0.5f);
            b.rt.anchoredPosition = local;
        }

        // ---------- Being watched: an exclamation mark over whoever's watching ----------
        // It fills from the dot up (yellow to red) as they get suspicious; a full red one pulses when they spot you.

        Transform _markWho;
        Image _markBar, _markDot;
        bool _spotted;

        void BuildNoticed()
        {
            _noticed = UIKit.Rect(_root, "Exclamation");
            _noticed.anchorMin = _noticed.anchorMax = new Vector2(0.5f, 0.5f);
            _noticed.pivot = new Vector2(0.5f, 0);
            _noticed.sizeDelta = new Vector2(40, 120);
            _markBar = Mark(_noticed, new Vector2(0, 38), new Vector2(26, 80));
            _markDot = Mark(_noticed, new Vector2(0, 0), new Vector2(26, 26));
            _noticed.gameObject.SetActive(false);
        }

        static Image Mark(RectTransform parent, Vector2 pos, Vector2 size)
        {
            var back = UIKit.Box(parent, "Back", new Color(0.08f, 0.08f, 0.1f, 0.85f));
            back.raycastTarget = false;
            var r = back.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0);
            r.pivot = new Vector2(0.5f, 0);
            r.sizeDelta = size + new Vector2(10, 10);
            r.anchoredPosition = pos - new Vector2(0, 5);
            var fill = UIKit.Box(back.transform, "Fill", UIKit.Warn, false);
            fill.raycastTarget = false;
            fill.rectTransform.Stretch(5, 5, 5, 5);
            fill.type = Image.Type.Filled;
            fill.sprite = UIKit.Square;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            return fill;
        }

        public void SetNoticed(float share, Transform who)
        {
            if (_spotted) return;
            bool on = share > 0.05f && who != null;
            if (_noticed.gameObject.activeSelf != on) _noticed.gameObject.SetActive(on);
            _markWho = who;
            if (!on) return;
            // the dot fills first, then the bar
            _markDot.fillAmount = Mathf.Clamp01(share / 0.2f);
            _markBar.fillAmount = Mathf.Clamp01((share - 0.2f) / 0.8f);
            var c = Color.Lerp(UIKit.Warn, UIKit.Bad, share);
            _markDot.color = _markBar.color = c;
            _noticed.localScale = Vector3.one;
            PlaceMark();
        }

        // Spotted: a full red "!" over them, pulsing, until HideSpotted.
        public void ShowSpotted(Transform who)
        {
            _spotted = true;
            _markWho = who;
            _markDot.fillAmount = _markBar.fillAmount = 1;
            _markDot.color = _markBar.color = UIKit.Bad;
            _noticed.gameObject.SetActive(true);
            _noticed.SetAsLastSibling();
            PlaceMark();
        }

        public void HideSpotted()
        {
            _spotted = false;
            _noticed.gameObject.SetActive(false);
        }

        void PlaceMark()
        {
            if (_markWho == null) return;
            var cam = Game.I.Cam.GetComponent<Camera>();
            var sp = cam.WorldToScreenPoint(_markWho.position + Vector3.up * 2.05f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var local);
            _noticed.anchoredPosition = local;
            if (_spotted) _noticed.localScale = Vector3.one * (1.15f + 0.15f * Mathf.Sin(Time.unscaledTime * 12f));
        }

        // ---------- Known: how well people would recognise the player as they look now ----------
        // A ring at the top of the screen that fills with infamy (full = they'd call the cops), with a dress icon
        // while the player still wears the top they were seen in and a face icon while their hair is the same.

        RectTransform _known;
        Image _knownRing;
        RawImage _dressIcon, _faceIcon;
        Text _knownText;
        readonly Dictionary<string, string> _names = new Dictionary<string, string>();

        void BuildKnown()
        {
            _known = UIKit.Rect(_root, "Known");
            _known.anchorMin = _known.anchorMax = new Vector2(0.5f, 1);
            _known.pivot = new Vector2(0.5f, 1);
            _known.sizeDelta = new Vector2(150, 150);
            _known.anchoredPosition = new Vector2(0, -16);
            var back = UIKit.Box(_known, "Back", new Color(0.05f, 0.05f, 0.08f, 0.7f), false);
            back.sprite = Circle();
            back.rectTransform.Stretch();
            back.raycastTarget = false;
            _knownRing = UIKit.Box(_known, "Ring", UIKit.Bad, false);
            _knownRing.sprite = Circle();
            _knownRing.type = Image.Type.Filled;
            _knownRing.fillMethod = Image.FillMethod.Radial360;
            _knownRing.fillOrigin = (int)Image.Origin360.Top;
            _knownRing.fillClockwise = true;
            _knownRing.rectTransform.Stretch(6, 6, 6, 6);
            _knownRing.raycastTarget = false;
            var inner = UIKit.Box(_known, "Inner", new Color(0.1f, 0.1f, 0.14f, 1f), false);
            inner.sprite = Circle();
            inner.rectTransform.Stretch(20, 20, 20, 20);
            inner.raycastTarget = false;
            _dressIcon = Icon(_known, IconDress());
            _faceIcon = Icon(_known, IconFace());
            _knownText = UIKit.Label(_known, "", 18, Color.white, TextAnchor.UpperCenter, FontStyle.Bold);
            _knownText.rectTransform.anchorMin = new Vector2(-1, 0);
            _knownText.rectTransform.anchorMax = new Vector2(2, 0);
            _knownText.rectTransform.offsetMin = new Vector2(0, -30);
            _knownText.rectTransform.offsetMax = new Vector2(0, -4);
            _known.gameObject.SetActive(false);
        }

        static RawImage Icon(RectTransform parent, Texture2D tex)
        {
            var rt = UIKit.Rect(parent, "Icon");
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(48, 48);
            var ri = rt.gameObject.AddComponent<RawImage>();
            ri.texture = tex;
            ri.raycastTarget = false;
            return ri;
        }

        void UpdateKnown()
        {
            var g = Game.I;
            var look = g.Look;
            float v = 0;
            bool top = false, hair = false;
            string who = null;
            if (look != null && !g.Failing) v = g.S.MostKnown(look.TopKey, look.HairKey, out top, out hair, out who);
            bool on = v > 0.01f;
            if (_known.gameObject.activeSelf != on) _known.gameObject.SetActive(on);
            if (!on) return;
            float full = Suspicion.I != null ? Suspicion.I.CopsAtInfamy : 2;
            float share = Mathf.Clamp01(v / full);
            _knownRing.fillAmount = Mathf.MoveTowards(_knownRing.fillAmount, share, Time.unscaledDeltaTime * 2f);
            _knownRing.color = Color.Lerp(UIKit.Warn, UIKit.Bad, share);
            _dressIcon.gameObject.SetActive(top);
            _faceIcon.gameObject.SetActive(hair);
            // side by side when both show, centred when only one does
            ((RectTransform)_dressIcon.transform).anchoredPosition = new Vector2(top && hair ? -22 : 0, 0);
            ((RectTransform)_faceIcon.transform).anchoredPosition = new Vector2(top && hair ? 22 : 0, 0);
            _knownText.text = $"{Name(who)} knows you";
        }

        string Name(string personId)
        {
            if (personId == null) return "Someone";
            if (_names.TryGetValue(personId, out var n)) return n;
            n = personId;
            foreach (var w in Resources.FindObjectsOfTypeAll<Watcher>())
                if (w.gameObject.scene.IsValid() && w.PersonId == personId) { n = w.DisplayName; break; }
            return _names[personId] = n;
        }

        // A shirt, in white.
        static Texture2D IconDress()
        {
            var t = BlankIcon();
            var c = new Color(0.95f, 0.95f, 0.95f);
            Rect(t, 10, 4, 21, 22, c);            // body
            Rect(t, 4, 17, 9, 25, c);             // sleeves
            Rect(t, 22, 17, 27, 25, c);
            Rect(t, 9, 23, 22, 26, c);            // shoulders
            Rect(t, 14, 23, 17, 26, new Color(0, 0, 0, 0));   // collar
            Rect(t, 15, 8, 16, 21, new Color(0.6f, 0.6f, 0.65f));   // buttons line
            t.Apply();
            return t;
        }

        // A face with hair.
        static Texture2D IconFace()
        {
            var t = BlankIcon();
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 14f));
                    if (d < 10) t.SetPixel(x, y, new Color(0.93f, 0.78f, 0.65f));
                    if (d < 11 && y > 17) t.SetPixel(x, y, new Color(0.35f, 0.22f, 0.12f));   // hair
                }
            Rect(t, 11, 12, 12, 14, Color.black);  // eyes
            Rect(t, 19, 12, 20, 14, Color.black);
            Rect(t, 13, 7, 18, 8, new Color(0.6f, 0.3f, 0.25f));   // mouth
            t.Apply();
            return t;
        }

        static Texture2D BlankIcon()
        {
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color[32 * 32];
            t.SetPixels(clear);
            return t;
        }

        static void Rect(Texture2D t, int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) t.SetPixel(x, y, c);
        }

        // ---------- Watch ----------

        void BuildWatch()
        {
            var blocker = UIKit.Button(_root, "", new Color(0, 0, 0, 0.6f), () => _watch.gameObject.SetActive(false));
            blocker.GetComponent<Image>().sprite = null;
            _watch = ((RectTransform)blocker.transform).Stretch();
            // the forearm coming in from the bottom left, and the watch on the wrist
            var arm = UIKit.Box(_watch, "Arm", new Color(0.78f, 0.6f, 0.47f), true);
            arm.raycastTarget = false;
            var art = arm.rectTransform;
            art.anchorMin = art.anchorMax = new Vector2(0.5f, 0.5f);
            art.sizeDelta = new Vector2(1500, 300);
            art.anchoredPosition = new Vector2(-380, -260);
            art.localRotation = Quaternion.Euler(0, 0, 28);
            var cuff = UIKit.Box(art, "Sleeve", new Color(0.2f, 0.22f, 0.3f), true);
            cuff.raycastTarget = false;
            cuff.rectTransform.Anchor(0, -0.05f, 0.42f, 1.05f);
            var strap = UIKit.Box(_watch, "Strap", new Color(0.25f, 0.16f, 0.1f), false);
            strap.raycastTarget = false;
            strap.rectTransform.anchorMin = strap.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            strap.rectTransform.sizeDelta = new Vector2(170, 520);
            strap.rectTransform.anchoredPosition = new Vector2(30, 30);
            strap.rectTransform.localRotation = Quaternion.Euler(0, 0, 28);
            var bezel = UIKit.Box(_watch, "Bezel", new Color(0.72f, 0.66f, 0.5f), false);
            bezel.sprite = Circle();
            bezel.raycastTarget = false;
            bezel.rectTransform.anchorMin = bezel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            bezel.rectTransform.sizeDelta = new Vector2(400, 400);
            bezel.rectTransform.anchoredPosition = new Vector2(30, 30);
            var face = UIKit.Box(bezel.transform, "Face", new Color(0.12f, 0.14f, 0.12f), false);
            face.sprite = Circle();
            face.raycastTarget = false;
            face.rectTransform.Stretch(26, 26, 26, 26);
            _watchDay = UIKit.Label(face.transform, "", 34, new Color(0.6f, 0.9f, 0.6f), TextAnchor.MiddleCenter, FontStyle.Bold);
            _watchDay.rectTransform.Anchor(0, 0.62f, 1, 0.82f);
            _watchTime = UIKit.Label(face.transform, "", 92, new Color(0.6f, 0.95f, 0.6f), TextAnchor.MiddleCenter, FontStyle.Bold);
            _watchTime.rectTransform.Anchor(0, 0.32f, 1, 0.66f);
            _watchInfo = UIKit.Label(face.transform, "", 24, new Color(0.6f, 0.9f, 0.6f), TextAnchor.MiddleCenter);
            _watchInfo.rectTransform.Anchor(0.1f, 0.1f, 0.9f, 0.34f);
            _watch.gameObject.SetActive(false);
        }

        void ShowWatch()
        {
            _watch.gameObject.SetActive(true);
            _watch.SetAsLastSibling();
            UpdateWatch();
        }

        public RectTransform WatchButton { get; private set; }
        public void CloseWatch() => _watch.gameObject.SetActive(false);
        public RectTransform WatchFace => _watch;
        public bool WatchOpen => _watch != null && _watch.gameObject.activeSelf;
        // The tutorial points at the HUNGRY reading: it flashes.
        public bool FlashHungry { get; set; }

        void UpdateWatch()
        {
            var g = Game.I;
            _watchTime.text = g.ClockText;
            _watchDay.text = $"{g.Weekday.Substring(0, 3).ToUpper()}  DAY {g.Day}";
            var bits = new List<string> { $"${g.S.cash}" };
            if (g.Hunger > 0.6f)
                bits.Add(FlashHungry ? (Mathf.Repeat(Time.unscaledTime, 0.7f) < 0.4f ? "<color=#FF5050><b>HUNGRY</b></color>" : "<color=#00000000>HUNGRY</color>") : "HUNGRY");
            else if (g.Full) bits.Add("FULL");
            if (g.Tiredness > 0.6f) bits.Add("TIRED");
            _watchInfo.text = string.Join("   ", bits);
        }

        // ---------- Pause ----------

        void BuildPause()
        {
            var blocker = UIKit.Box(_root, "Pause", new Color(0, 0, 0, 0.75f), false);
            _pause = blocker.rectTransform.Stretch();
            var box = UIKit.Box(_pause, "Box", UIKit.Panel).rectTransform.Anchor(0.32f, 0.15f, 0.68f, 0.85f);
            UIKit.VList(box, 18, 30, TextAnchor.MiddleCenter);
            UIKit.Label(box, "Paused", 48, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold).Size(80);
            UIKit.Button(box, "Resume", UIKit.Accent, () => SetPaused(false), 32).Size(90);
            UIKit.Button(box, "Back to last note", UIKit.PanelLight, () => { SetPaused(false); Game.I.RestartFromCheckpoint(); }, 28).Size(80);
            UIKit.Button(box, "Restart game", UIKit.PanelLight, () =>
                ShowDialog("Restart", "Start over from the very first morning? Everything you've done so far will be lost.",
                    new List<(string, Action)> { ("Restart", () => Game.I.RestartGame()), ("Cancel", null) }), 26).Size(70);
            Button icons = null;
            icons = UIKit.Button(box, "", UIKit.PanelLight, () =>
            {
                var m = Game.I.UI.Marks;
                m.SetIcons(!m.IconsOn);
                icons.SetLabel(IconsLabel());
            }, 26);
            icons.Size(70);
            _iconsButton = icons;
            UIKit.Label(box, "<size=20>Every new note in your notebook is a checkpoint.</size>", 20, UIKit.Muted, TextAnchor.MiddleCenter).Size(40);
            _pause.gameObject.SetActive(false);
        }

        Button _iconsButton;
        static string IconsLabel() => Game.I != null && Game.I.UI.Marks != null && Game.I.UI.Marks.IconsOn ? "Interaction icons: ON" : "Interaction icons: OFF";

        public void SetPaused(bool on)
        {
            if (on && _iconsButton != null) _iconsButton.SetLabel(IconsLabel());
            _pause.gameObject.SetActive(on);
            if (on) _pause.SetAsLastSibling();
            Time.timeScale = on ? 0 : 1;
        }

        // ---------- Reflex vision ----------

        void BuildReflex()
        {
            var dark = UIKit.Box(_root, "Reflex", new Color(0.02f, 0.03f, 0.06f, 0.78f), false);
            dark.raycastTarget = false;
            _reflex = dark.rectTransform.Stretch();
            _markers = UIKit.Rect(_reflex, "Markers").Stretch();
            _reflex.gameObject.SetActive(false);

            _reflexButton = UIKit.Button(_root, "Reflex\nvision", new Color(0.55f, 0.25f, 0.7f), () => SetReflex(!_reflexOn), 22);
            var rt = (RectTransform)_reflexButton.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.offsetMin = new Vector2(24, 24);
            rt.offsetMax = new Vector2(164, 144);
            _reflexButton.gameObject.SetActive(false);
        }

        bool _lastChance;

        // About to be called out: the reflex button pulses and says what to do.
        public void LastChancePrompt(bool on)
        {
            _lastChance = on;
            _reflexButton.SetLabel(on ? "Blend in!\n(Reflex)" : _reflexOn ? "Normal\nvision" : "Reflex\nvision");
            _reflexButton.transform.localScale = Vector3.one;
            if (!on && _reflexOn) SetReflex(false);
        }

        // Shown where blending in matters: restricted areas, and near the target.
        bool ReflexUseful()
        {
            var g = Game.I;
            if (_lastChance) return true;
            if (Restricted.Of(g.Current) != null) return true;
            var t = Contract.I != null ? Contract.I.Target : null;
            return t != null && t.Visible && (t.transform.position - g.Player.transform.position).magnitude < 12f;
        }

        void SetReflex(bool on)
        {
            _reflexOn = on;
            _reflex.gameObject.SetActive(on);
            _reflexButton.SetLabel(on ? "Normal\nvision" : "Reflex\nvision");
            if (on) { _reflex.SetAsLastSibling(); _reflexButton.transform.SetAsLastSibling(); RebuildMarkers(); }
        }

        void RebuildMarkers()
        {
            UIKit.Clear(_markers);
            _marks.Clear();
            var g = Game.I;
            var me = g.Player.transform.position;
            foreach (var spot in BlendSpot.All)
            {
                if ((spot.transform.position - me).magnitude > 30f) continue;
                var s = spot;
                var b = UIKit.Button(_markers, s.Doing, new Color(0.95f, 0.75f, 0.2f, 0.92f), () =>
                {
                    SetReflex(false);
                    var p = g.Player.GetComponent<PlayerController>();
                    p.GoThen(s.Stand, true, () => p.Blend(s), s.Sit);
                }, 20);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(190, 52);
                b.GetComponentInChildren<Text>().fontSize = 17;
                _marks.Add((rt, s));
            }
        }

        // ---------- Per frame ----------

        void Update()
        {
            var g = Game.I;
            if (g == null) return;
            var cam = g.Cam.GetComponent<Camera>();

            for (int i = _bubbles.Count - 1; i >= 0; i--)
            {
                var b = _bubbles[i];
                if (!b.rt.gameObject.activeSelf) continue;
                if (Time.time > b.until || (b.who != null && !b.who.gameObject.activeInHierarchy)) b.rt.gameObject.SetActive(false);
                else Place(b);
            }

            if (_watch.gameObject.activeSelf) UpdateWatch();
            if (_dialog.gameObject.activeSelf) PlaceDialog();
            if (_noticed.gameObject.activeSelf) PlaceMark();
            UpdateKnown();

            bool useful = ReflexUseful() && (!g.Busy || _lastChance);
            if (_lastChance) _reflexButton.transform.localScale = Vector3.one * (1.1f + 0.12f * Mathf.Sin(Time.unscaledTime * 10f));
            if (_reflexButton.gameObject.activeSelf != useful) _reflexButton.gameObject.SetActive(useful);
            if (!useful && _reflexOn) SetReflex(false);
            if (_reflexOn)
                foreach (var (rt, spot) in _marks)
                {
                    if (spot == null) continue;
                    var sp = cam.WorldToScreenPoint(spot.Stand.position + Vector3.up * 0.9f);
                    rt.gameObject.SetActive(sp.z > 0);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_reflex, sp, null, out var local);
                    rt.anchoredPosition = local;
                }
        }

        // ---------- Helpers ----------

        static Sprite _circle;
        static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n / 2f - d)));
                }
            tex.Apply();
            return _circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
