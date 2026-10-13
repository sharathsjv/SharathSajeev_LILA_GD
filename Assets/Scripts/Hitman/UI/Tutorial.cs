using System;
using System.Collections;
using QAOffice;
using UnityEngine;
using UnityEngine.UI;
using static Hitman.Win98;

namespace Hitman
{
    // The first morning, step by step (design doc: Tutorial). Explanations zoom in on what they're about and wait
    // for OK; instructions zoom back out, point at what to tap (an arrow in the world, a flashing frame on the
    // screen) and wait for the player to do it. Ends with the brief: find info, eliminate, report - no weapons.
    public class Tutorial : MonoBehaviour
    {
        public static bool Running { get; private set; }

        RectTransform _root, _box, _blocker, _arrow, _frame, _card;
        Text _text;
        Button _ok;
        bool _clicked;
        Transform _worldTarget;
        Func<RectTransform> _uiTarget;
        Area _home;
        Vector2 _boxLow = new Vector2(0.2f, 0.03f), _boxHigh = new Vector2(0.8f, 0.24f);

        public void Init(RectTransform root)
        {
            _root = root;
            Running = false;                                             // (a restarted game)
            // explanations: dim everything and wait for OK
            _blocker = UIKit.Box(_root, "Tutorial Blocker", new Color(0, 0, 0, 0.25f), false).rectTransform.Stretch();
            _blocker.gameObject.SetActive(false);

            _box = Bevel(UIKit.Rect(_root, "Tutorial"), true);
            TitleBar(_box, "Tutorial", 40, 22);
            var skip = Button98(_box.Find("Title"), "Skip", Skip, 18);
            var sr = (RectTransform)skip.transform;
            sr.anchorMin = sr.anchorMax = new Vector2(1, 0.5f);
            sr.pivot = new Vector2(1, 0.5f);
            sr.sizeDelta = new Vector2(80, 32);
            sr.anchoredPosition = new Vector2(-4, 0);
            _text = Text98(_box, "", 26, TextAnchor.UpperLeft);
            _text.rectTransform.Stretch(18, 14, 150, 54);
            _ok = Button98(_box, "OK", () => _clicked = true, 24);
            var ok = (RectTransform)_ok.transform;
            ok.anchorMin = ok.anchorMax = new Vector2(1, 0);
            ok.pivot = new Vector2(1, 0);
            ok.sizeDelta = new Vector2(120, 52);
            ok.anchoredPosition = new Vector2(-14, 14);
            _box.gameObject.SetActive(false);

            // the arrow over something in the world
            _arrow = UIKit.Rect(_root, "Tutorial Arrow");
            _arrow.sizeDelta = new Vector2(70, 70);
            var tri = _arrow.gameObject.AddComponent<Image>();
            tri.sprite = Triangle();
            tri.color = new Color(1f, 0.85f, 0.1f);
            tri.raycastTarget = false;
            _arrow.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
            _arrow.gameObject.SetActive(false);

            // the frame around something on the screen
            _frame = UIKit.Rect(_root, "Tutorial Frame");
            _frame.anchorMin = _frame.anchorMax = new Vector2(0.5f, 0.5f);
            foreach (var (aMin, aMax, oMin, oMax) in new[]
                     {
                         (new Vector2(0, 1), Vector2.one, new Vector2(-6, -2), new Vector2(6, 6)),
                         (Vector2.zero, new Vector2(1, 0), new Vector2(-6, -6), new Vector2(6, 2)),
                         (Vector2.zero, new Vector2(0, 1), new Vector2(-6, -6), new Vector2(2, 6)),
                         (new Vector2(1, 0), Vector2.one, new Vector2(-2, -6), new Vector2(6, 6)),
                     })
            {
                var e = UIKit.Box(_frame, "Edge", new Color(1f, 0.85f, 0.1f), false);
                e.raycastTarget = false;
                e.rectTransform.anchorMin = aMin;
                e.rectTransform.anchorMax = aMax;
                e.rectTransform.offsetMin = oMin;
                e.rectTransform.offsetMax = oMax;
            }
            _frame.gameObject.SetActive(false);

            BuildCard();
        }

        public void Begin()
        {
            if (Running || Game.I.S.Has("tutorial.done")) return;
            StartCoroutine(Run());
        }

        // ---------- the script ----------

        IEnumerator Run()
        {
            var g = Game.I;
            var ui = g.UI;
            Running = true;
            _home = g.Current;
            yield return null;

            // Wake up in the bedroom.
            var bed = Place.Find("shackleton.home.bed");
            g.Player.Seated = false;
            if (bed != null) g.Player.Teleport(bed.transform.position, bed.transform.rotation);
            g.Cam.Snap(g.Player.transform);
            var start = g.Player.transform.position;

            yield return Explain("Morning. This is you.\nTo walk somewhere, tap the floor where you want to go.", g.Player.transform);
            yield return Instruct("Try it: tap anywhere on the floor.", () => (g.Player.transform.position - start).magnitude > 0.6f);
            yield return new WaitForSeconds(1.5f);

            // To the hall (pointing at the way out of the bedroom if they haven't found it).
            if (Room() != "Liv")
                yield return Instruct("Tap here to go to the hall.", () => Room() == "Liv", world: Find("Lead_Liv_Bed"));
            yield return Instruct("You need breakfast. Find the kitchen.", () => Room() == "Kit", hintAfter: 8f, world: Find("Lead_Kit_Liv"));

            // Hunger, on the watch.
            yield return Instruct("How are you doing? Open your watch.", () => ui.Overlays.WatchOpen, screen: () => ui.Overlays.WatchButton);
            ui.Overlays.FlashHungry = true;
            yield return Explain("See that? <b>HUNGRY</b>.\nKeep eating through the day. Go too long without food and you'll pass out for 4 hours.", null);
            ui.Overlays.FlashHungry = false;
            ui.Overlays.CloseWatch();

            // ...and on you: growling stomach, sleepy Z!
            ui.Marks.ShowHungry = true;
            yield return Explain("You can see it on you too: when you're hungry, your stomach growls - lines ripple out from it.", g.Player.transform);
            ui.Marks.ShowHungry = false;
            ui.Marks.ShowSleepy = true;
            yield return Explain("And when you're getting sleepy, a <b>Z!</b> floats over your head. Get to a bed at night - stay up too late and you'll pass out wherever you are.", g.Player.transform);
            ui.Marks.ShowSleepy = false;

            // Things you can touch.
            var fridge = Find("Kit_Fridge");
            while (g.Hunger > 0.3f)
            {
                yield return Explain("You can touch things to interact with them.", fridge);
                yield return Instruct("Tap the fridge.", () => ui.OpenMenuTitle == "Fridge", world: fridge);
                yield return Explain("These are the things you can do with it. Some things have more than one option.", null, high: true);
                yield return Instruct("Pick one: eat some cereal.", () => ui.OpenMenuTitle == null || g.Busy, screen: () => ui.Menu, high: true);
                // (they walk over to the fridge first, then eat)
                for (float w = 0; !g.Busy && g.Hunger > 0.3f && w < 10f; w += Time.unscaledDeltaTime) yield return null;
                while (g.Busy) yield return null;
                yield return new WaitForSeconds(0.5f);
            }
            yield return Explain("Tip: not sure what you can use? Open the pause menu and turn on <b>Interaction icons</b> - a tiny marker shows over everything you can interact with.", null);

            // The computer.
            yield return Instruct("Now head back to the hall.", () => Room() == "Liv");
            yield return Instruct("Open the computer.", () => ui.Computer.IsOpen, world: Find("Liv_Laptop"));
            yield return Explain("This is your computer. It's where your gigs come in - mostly by email - and where you dig up information.", null, high: true);
            yield return Instruct("Open your email: tap Inbox.", () => ui.Computer.MailOpen, screen: () => ui.Computer.InboxIcon, high: true);
            yield return Instruct("Read the email (the bar on the side scrolls it), then accept the job.", () => Contract.I == null || Contract.I.Accepted,
                                  screen: () => ui.Computer.AcceptButton, high: true);
            yield return Instruct("Log off: press Start, then Log Off.", () => !ui.Computer.IsOpen,
                                  screen: () => ui.Computer.StartMenu != null && ui.Computer.StartMenu.gameObject.activeSelf ? ui.Computer.StartMenu : ui.Computer.StartButton, high: true);

            // Out the door.
            yield return Instruct("Touch the door to head out.", () => g.Current != _home, world: Find("Liv_EntranceDoor"));
            while (g.Busy) yield return null;

            // Crossing the street.
            var street = g.Current;
            if (street is District && street.StreetHalfWidth > 0)
            {
                float Side() => Mathf.Sign(street.transform.InverseTransformPoint(g.Player.transform.position).x - street.StreetCenterX);
                float side = Side();
                var mark = new GameObject("Tutorial Road Mark").transform;
                var local = street.transform.InverseTransformPoint(g.Player.transform.position);
                local.x = street.StreetCenterX;
                mark.position = street.transform.TransformPoint(local);
                yield return Explain("This is the street. Shops, people, the subway - and the other side of the road.\nTo get across, tap the road and pick <b>Cross street</b>.", null);
                yield return Instruct("Cross the street: tap the road, then Cross street.", () => Side() != side && !g.Player.Moving, world: mark);
                Destroy(mark.gameObject);
                yield return new WaitForSeconds(0.5f);
            }
            yield return Card();
            Finish();
        }

        string Room() => _home != null && _home.GetComponent<RoomVisibility>() is RoomVisibility rv ? rv.CurrentRoom : null;

        Transform Find(string name)
        {
            foreach (var t in _home.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // Explanation: zoom in on the subject, wait for OK.
        IEnumerator Explain(string text, Transform subject, bool high = false)
        {
            var g = Game.I;
            if (subject != null) g.Cam.Spotlight(subject, subject, 2.4f);
            Show(text, true, high);
            _blocker.gameObject.SetActive(true);
            _blocker.SetSiblingIndex(_box.GetSiblingIndex());
            _clicked = false;
            while (!_clicked && Running) yield return null;
            _blocker.gameObject.SetActive(false);
            if (subject != null) g.Cam.Spotlight(null, null);
        }

        // Instruction: zoom back out, point at the thing, wait until it's done.
        IEnumerator Instruct(string text, Func<bool> done, Transform world = null, Func<RectTransform> screen = null, float hintAfter = 0, bool high = false)
        {
            Game.I.Cam.Spotlight(null, null);
            Show(text, false, high);
            float t = 0;
            while (Running && !done())
            {
                t += Time.unscaledDeltaTime;
                _worldTarget = t >= hintAfter ? world : null;
                _uiTarget = screen;
                yield return null;
            }
            _worldTarget = null;
            _uiTarget = null;
        }

        bool _high;

        void PlaceBox(bool high)
        {
            _high = high;
            // over the computer the box sits at the top, out of the way of the taskbar
            _box.anchorMin = high ? new Vector2(0.24f, 0.875f) : _boxLow;      // a thin strip over the monitor's top edge
            _box.anchorMax = high ? new Vector2(0.76f, 0.995f) : _boxHigh;
            _box.offsetMin = _box.offsetMax = Vector2.zero;
        }

        void Show(string text, bool ok, bool high)
        {
            _text.text = text;
            _ok.gameObject.SetActive(ok);
            _text.rectTransform.offsetMax = new Vector2(ok ? -150 : -18, -54);
            PlaceBox(high);
            _box.gameObject.SetActive(true);
        }

        void LateUpdate()
        {
            if (!Running) return;
            // Time stands still while you learn (fast-forwards like eating still need the clock).
            Game.I.ClockRunning = Game.I.Busy;
            // keep on top of whatever opened since
            _blocker.SetAsLastSibling();
            _box.SetAsLastSibling();
            _frame.SetAsLastSibling();
            _arrow.SetAsLastSibling();
            if (!_blocker.gameObject.activeSelf) _blocker.SetSiblingIndex(0);

            var cam = Game.I.Cam.GetComponent<Camera>();
            bool showArrow = _worldTarget != null && !_blocker.gameObject.activeSelf;
            if (showArrow)
            {
                var b = _worldTarget.TryGetComponent<Renderer>(out var r) ? r.bounds : new Bounds(_worldTarget.position, Vector3.one);
                var sp = cam.WorldToScreenPoint(new Vector3(b.center.x, b.max.y + 0.2f, b.center.z));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, null, out var local);
                _arrow.anchorMin = _arrow.anchorMax = new Vector2(0.5f, 0.5f);
                _arrow.pivot = new Vector2(0.5f, 0);
                // what it points at is under the box: move the box up out of the way
                if (!_high && sp.y < Screen.height * (_boxHigh.y + 0.04f) && sp.x > Screen.width * (_boxLow.x - 0.05f) && sp.x < Screen.width * (_boxHigh.x + 0.05f)) PlaceBox(true);
                _arrow.anchoredPosition = local - _root.rect.center + Vector2.up * (12 + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) * 22);
            }
            _arrow.gameObject.SetActive(showArrow);

            var target = _uiTarget?.Invoke();
            bool showFrame = target != null && target.gameObject.activeInHierarchy && !_blocker.gameObject.activeSelf;
            if (showFrame)
            {
                var corners = new Vector3[4];
                target.GetWorldCorners(corners);
                var lo = (Vector2)_root.InverseTransformPoint(corners[0]);
                var hi = (Vector2)_root.InverseTransformPoint(corners[2]);
                _frame.sizeDelta = hi - lo;
                _frame.anchoredPosition = (lo + hi) / 2 - _root.rect.center;
                foreach (var img in _frame.GetComponentsInChildren<Image>())
                    img.color = new Color(1f, 0.85f, 0.1f, Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f ? 1f : 0.35f);
            }
            _frame.gameObject.SetActive(showFrame);
        }

        // ---------- the brief, full screen ----------

        void BuildCard()
        {
            _card = UIKit.Box(_root, "Tutorial Brief", new Color(0, 0, 0, 0.8f), false).rectTransform.Stretch();
            var win = Bevel(UIKit.Rect(_card, "Window"), true);
            win.anchorMin = new Vector2(0.14f, 0.1f);
            win.anchorMax = new Vector2(0.86f, 0.9f);
            win.offsetMin = win.offsetMax = Vector2.zero;
            TitleBar(win, "The job", 46, 26);
            var page = Sunken(UIKit.Rect(win, "Page"));
            page.Stretch(16, 96, 16, 62);
            UIKit.Box(page, "White", Color.white, false).rectTransform.Stretch(3, 3, 3, 3);
            var t = Text98(page, "", 28, TextAnchor.UpperLeft);
            t.rectTransform.Stretch(28, 20, 28, 20);
            t.name = "Text";
            var go = Button98(win, "Let's go", () => _clicked = true, 28);
            var gr = (RectTransform)go.transform;
            gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0);
            gr.pivot = new Vector2(0.5f, 0);
            gr.sizeDelta = new Vector2(240, 66);
            gr.anchoredPosition = new Vector2(0, 16);
            _card.gameObject.SetActive(false);
        }

        IEnumerator Card()
        {
            _box.gameObject.SetActive(false);
            var a = Contract.I != null ? Contract.I.Assignment : null;
            string who = a != null && a.target != null ? a.target.displayName : "The target";
            _card.Find("Window/Page/Text").GetComponent<Text>().text =
                $"<b>{who} has to go - and it has to look like an accident.</b>\n\n" +
                "<b>1. Find information.</b> Where they go, what they do, who they see. Everyone in this city keeps a routine: watch, listen, read, and write it down.\n\n" +
                "<b>2. Eliminate the target.</b> You have no weapons. The city is your weapon - a wet floor, a bad meal, a gas tap. How you do it is up to you.\n\n" +
                "<b>3. Report it.</b> An anonymous call to the police from a payphone gets it in the papers - and gets you paid.\n\n" +
                "<b>Following.</b> Tap someone and pick <i>Follow</i> to tail them - you'll walk behind them through doors and onto the subway. " +
                "Slide toward or away from them to change the gap; tap the floor or <i>Stop</i> to quit. Tailing someone won't get you past security or a locked door.\n\n" +
                "The world is yours to use. Your to-do list is in the notebook.";
            _card.gameObject.SetActive(true);
            _card.SetAsLastSibling();
            _clicked = false;
            while (!_clicked) yield return null;
            _card.gameObject.SetActive(false);
        }

        void Skip()
        {
            Finish();
            Game.I.UI.Toast("Tutorial skipped.");
        }

        void Finish()
        {
            StopAllCoroutines();
            Running = false;
            Game.I.ClockRunning = true;
            Game.I.S.Set("tutorial.done");
            Game.I.Cam.Spotlight(null, null);
            if (Game.I.UI.Overlays != null) Game.I.UI.Overlays.FlashHungry = false;
            if (Game.I.UI.Marks != null) Game.I.UI.Marks.ShowHungry = Game.I.UI.Marks.ShowSleepy = false;
            _box.gameObject.SetActive(false);
            _blocker.gameObject.SetActive(false);
            _arrow.gameObject.SetActive(false);
            _frame.gameObject.SetActive(false);
            _card.gameObject.SetActive(false);
        }

        static Sprite _tri;
        static Sprite Triangle()
        {
            if (_tri != null) return _tri;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float half = y / (float)n * (n / 2f);          // pointing down: wide at the top
                    tex.SetPixel(x, y, Mathf.Abs(x - n / 2f + 0.5f) <= half ? Color.white : Color.clear);
                }
            tex.Apply();
            _tri = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0));
            return _tri;
        }
    }
}
