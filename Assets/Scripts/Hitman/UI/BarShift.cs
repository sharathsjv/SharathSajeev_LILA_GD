using System;
using System.Collections.Generic;
using QAOffice;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using static Hitman.Win98;

namespace Hitman
{
    // The lunch shift at the Corner Bar (playable every day, for money), out on the floor: customers come in and sit
    // down; a "!" over someone means they're ready to order. Go over and they say what they want - once, for a
    // second. Pour it at the bar and take it over: the quicker, the bigger the tip; ask them again and there's no tip.
    // They drink up and go, and new ones come in. Kevin's lunch turns up too - serve it, or serve it with something
    // extra. His booth (and his coworker's side of it) is kept free for him.
    public class BarShift : MonoBehaviour
    {
        public static bool Working { get; private set; }

        public class Seat
        {
            public Collider collider;
            public Vector3 seat, walkTo;
            public Quaternion facing;
            public BarCustomer taken;
        }

        public static readonly string[] Drinks = { "Beer", "Whiskey", "Soda", "Wings" };
        const string KevinsLunch = "Kevin's lunch";
        const int MaxCustomers = 5;

        RectTransform _win, _layer;
        Text _title, _status;
        Vector2 _shift;
        int _pay, _tips, _served, _missed;
        Interior _bar;
        Transform _behindBar, _holder;
        readonly List<Seat> _seats = new List<Seat>();
        readonly List<BarCustomer> _customers = new List<BarCustomer>();
        readonly Dictionary<BarCustomer, RectTransform> _marks = new Dictionary<BarCustomer, RectTransform>();
        RectTransform _kevinMark;
        float _nextCustomer;
        bool _kevinOrdered;
        GameObject[] _models;
        RuntimeAnimatorController _controller;

        // What the player is carrying over (null = empty-handed).
        public string Holding { get; private set; }

        public void Init(RectTransform root)
        {
            Working = false;                                             // (a restarted game)
            _layer = UIKit.Rect(root, "Bar Shift Marks").Stretch();
            _win = Bevel(UIKit.Rect(root, "Bar Shift"), true);
            _win.anchorMin = new Vector2(0.2f, 0.86f);
            _win.anchorMax = new Vector2(0.8f, 0.985f);
            _win.offsetMin = _win.offsetMax = Vector2.zero;
            _title = TitleBar(_win, "", 40, 22);
            _status = Text98(_win, "", 24, TextAnchor.MiddleLeft);
            _status.rectTransform.Stretch(16, 8, 200, 46);
            var close = Button98(_win, "Clock out", () => End(true), 22);
            var cr = (RectTransform)close.transform;
            cr.anchorMin = cr.anchorMax = new Vector2(1, 0);
            cr.pivot = new Vector2(1, 0);
            cr.sizeDelta = new Vector2(170, 52);
            cr.anchoredPosition = new Vector2(-10, 8);
            _win.gameObject.SetActive(false);
        }

        void Start() => Game.I.Restored += () => { if (Working) Abort(); };

        // Back to a checkpoint mid-shift: the shift never happened.
        void Abort()
        {
            Working = false;
            _win.gameObject.SetActive(false);
            foreach (var c in _customers.ToArray()) if (c != null) c.Dismiss(false);
            _customers.Clear();
            foreach (var s in _seats) s.taken = null;
            foreach (var kv in _marks) if (kv.Value != null) Destroy(kv.Value.gameObject);
            _marks.Clear();
            if (_kevinMark != null) _kevinMark.gameObject.SetActive(false);
            Holding = null;
        }

        // Start the shift now (the caller fast-forwards to 12:30 first if it's early).
        public void Begin(Transform behindBar, Vector2 shiftHours, int pay)
        {
            var g = Game.I;
            _shift = shiftHours;
            _pay = pay;
            _tips = _served = _missed = 0;
            _kevinOrdered = false;
            Holding = null;
            _behindBar = behindBar;
            _bar = g.Current as Interior;
            _nextCustomer = 1.5f;
            Working = true;
            g.UI.HideActions();
            g.Player.Teleport(behindBar.position, behindBar.rotation);
            g.Player.GetComponent<PlayerController>().Locked = false;
            if (_holder == null) _holder = new GameObject("Bar Customers").transform;
            FindSeats();
            FindModels();
            foreach (var c in _bar.GetComponentsInChildren<Collider>())
                if ((c.name == "Bar_Counter" || c.name == "Bar_BackCounter") && c.GetComponent<BarCounter>() == null) c.gameObject.AddComponent<BarCounter>();
            _win.gameObject.SetActive(true);
            _win.SetAsLastSibling();
            g.S.Set($"job.{g.Day}");
            g.UI.Toast("Customers with a \"!\" are ready to order. Remember what they say!");
        }

        // ---------- Setting up ----------

        // Every booth bench and bar stool, where to sit on it and which way to face (its table / the counter).
        void FindSeats()
        {
            _seats.Clear();
            var tables = new List<Collider>();
            var all = _bar.GetComponentsInChildren<Collider>();
            foreach (var c in all) if (c.name.StartsWith("Bar_BoothTable") || c.name == "Bar_Counter") tables.Add(c);
            // Kevin's lunch seat and his coworker's stay free.
            var reserved = new List<Vector3>();
            foreach (var p in _bar.GetComponentsInChildren<Place>())
                if (p.name.Contains("cornerbar.stool")) reserved.Add(p.transform.position);
            foreach (var c in all)
            {
                bool booth = c.name.StartsWith("Bar_Booth") && !c.name.StartsWith("Bar_BoothTable");
                bool stool = c.name.StartsWith("Bar_Stool");
                if (!booth && !stool) continue;
                var at = CharacterMover.SeatOn(c, c.bounds.center);
                bool kept = false;
                foreach (var r in reserved) if (Flat(r - at).magnitude < 0.8f) kept = true;
                if (kept) continue;
                Collider near = null;
                float best = float.MaxValue;
                foreach (var t in tables)
                {
                    float d = Flat(t.bounds.center - at).sqrMagnitude;
                    if (d < best) { best = d; near = t; }
                }
                var dir = near != null ? Flat(near.bounds.ClosestPoint(at) - at) : Vector3.forward;
                if (dir.sqrMagnitude < 0.0001f) dir = Flat(near.bounds.center - at);
                dir = Mathf.Abs(dir.x) > Mathf.Abs(dir.z) ? new Vector3(Mathf.Sign(dir.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(dir.z));
                var walk = NavMesh.SamplePosition(at, out var hit, 1.5f, NavMesh.AllAreas) ? hit.position : at;
                _seats.Add(new Seat { collider = c, seat = new Vector3(at.x, 0, at.z), walkTo = walk, facing = Quaternion.LookRotation(dir) });
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        void FindModels()
        {
            if (_models != null && _models.Length > 0) return;
            var ride = FindFirstObjectByType<SubwayRide>(FindObjectsInactive.Include);
            _models = ride != null ? ride.PassengerModels : null;
            var k = Contract.I != null ? Contract.I.Target : null;
            var anim = k != null ? k.GetComponentInChildren<Animator>() : Game.I.Player.GetComponentInChildren<Animator>();
            _controller = anim != null ? anim.runtimeAnimatorController : null;
            if (_controller is AnimatorOverrideController o) _controller = o.runtimeAnimatorController;
        }

        bool SeatFree(Seat s)
        {
            if (s.taken != null) return false;
            var g = Game.I;
            if (Flat(g.Player.transform.position - s.seat).magnitude < 0.7f) return false;
            foreach (var p in g.People) if (p.AreaId == _bar.Id && Flat(p.transform.position - s.seat).magnitude < 0.7f) return false;
            if (PlayerController.SeatTaken(s.seat)) return false;                          // (staff, regulars, other customers)
            return true;
        }

        void NewCustomer()
        {
            if (_models == null || _models.Length == 0 || _controller == null) return;
            var free = _seats.FindAll(SeatFree);
            if (free.Count == 0) return;
            var seat = free[UnityEngine.Random.Range(0, free.Count)];
            var door = _bar.InsideDoor.transform;

            var root = new GameObject("Customer").transform;
            root.SetParent(_holder, false);
            root.SetPositionAndRotation(door.position, door.rotation);
            var model = Instantiate(_models[UnityEngine.Random.Range(0, _models.Length)], root, false);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            float h = 0;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) h = Mathf.Max(h, r.bounds.max.y - root.position.y);
            if (h > 0.1f) model.transform.localScale *= 1.75f * UnityEngine.Random.Range(0.94f, 1.05f) / h;
            var anim = model.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = _controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            var col = root.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.9f, 0);
            col.height = 1.8f;
            col.radius = 0.32f;
            col.isTrigger = true;                                     // (taps only: they don't block anyone)
            var mover = root.gameObject.AddComponent<CharacterMover>();
            mover.WalkSpeed = 1.7f;
            BlobShadow.Attach(model, new Vector2(0.7f, 0.7f));
            var c = root.gameObject.AddComponent<BarCustomer>();
            seat.taken = c;
            c.Init(this, seat, Drinks[UnityEngine.Random.Range(0, Drinks.Length)], door.position);
            _customers.Add(c);
        }

        // ---------- Pouring and serving ----------

        public void AddPourActions(ActionMenu m)
        {
            if (!Working) return;
            foreach (var d in Drinks)
            {
                var item = d;
                m.AddWalk(d == "Wings" ? "Grab a plate of wings" : "Pour a " + d.ToLower(), () => Pour(item), _behindBar);
            }
            if (_kevinOrdered && !Game.I.S.Has($"job.served.{Game.I.Day}") && Holding != KevinsLunch)
                m.AddWalk("Kevin's lunch (2 beers + wings)", () => Pour(KevinsLunch), _behindBar);
        }

        void Pour(string item)
        {
            var g = Game.I;
            if (Holding != null && Holding != item) g.UI.Toast($"You tip the {Holding.ToLower()} down the sink.");
            Holding = item;
        }

        public void Give(BarCustomer c)
        {
            var g = Game.I;
            if (Holding == null || c.Now != BarCustomer.State.Waiting) return;
            if (Holding != c.Wants)
            {
                g.UI.Say(c.transform, Holding == KevinsLunch ? "That's not mine." : "That's not what I ordered.", 1.6f);
                if (Holding != KevinsLunch) Holding = null;                        // (wasted)
                return;
            }
            Holding = null;
            c.Served();
            _served++;
            int tip = c.AskedTwice ? 0 : Tip(c.WaitedSeconds);
            _tips += tip;
            g.UI.Say(c.transform, tip == 0 ? "...Thanks." : tip >= 4 ? $"That was quick! Thanks. (+${tip})" : $"Thanks. (+${tip})", 1.8f);
        }

        // The quicker it arrives after they ordered, the bigger the tip.
        static int Tip(float seconds) => seconds <= 10 ? 5 : seconds <= 18 ? 4 : seconds <= 26 ? 3 : seconds <= 36 ? 2 : 1;

        public void Left(BarCustomer c, bool happy)
        {
            if (!happy) _missed++;
            if (c.Seat != null && c.Seat.taken == c) c.Seat.taken = null;
            _customers.Remove(c);
        }

        // Kevin, while the shift's on: take his order, then bring him his lunch (with or without extras).
        public void AddPersonActions(RoutineCharacter person, ActionMenu m)
        {
            var g = Game.I;
            var k = Contract.I != null ? Contract.I.Target : null;
            if (person != k || !KevinWaiting()) return;
            if (!_kevinOrdered)
            {
                m.AddWalk("Take his order", () =>
                {
                    _kevinOrdered = true;
                    g.Player.Face(k.transform.position);
                    g.UI.Say(k.transform, "Two beers and the wings. Chop chop.", 1f);
                });
                return;
            }
            if (Holding != KevinsLunch) return;
            m.AddWalk("Serve his lunch", () => ServeKevin(false));
            if (g.S.Holding("poison")) m.AddWalk("Serve his lunch (with the rat poison)", () => ServeKevin(true));
        }

        bool KevinWaiting()
        {
            var g = Game.I;
            var k = Contract.I != null ? Contract.I.Target : null;
            return Working && k != null && k.Alive && k.AreaId == _bar.Id && k.Settled && k.Activity == Activity.Eat
                   && !g.S.Has($"job.served.{g.Day}");
        }

        void ServeKevin(bool poisoned)
        {
            var g = Game.I;
            Holding = null;
            Contract.I.Serve(poisoned);
            _served++;
            _tips += 2;
            g.UI.Say(Contract.I.Target.transform, "Finally! Thanks, buddy.", 1.6f);
        }

        // ---------- Per frame ----------

        void Update()
        {
            if (!Working) return;
            var g = Game.I;
            _title.text = $"Corner Bar - lunch shift ({Game.Clock(_shift.x * 60)}-{Game.Clock(_shift.y * 60)})";
            _status.text = $"Tips ${_tips}    Served {_served}    Carrying: <b>{(Holding ?? "nothing")}</b>";
            if (g.Minutes / 60f >= _shift.y) { End(false); return; }
            if (g.Current != _bar) { End(true, "You walked out on your shift."); return; }
            if (!g.ClockRunning || g.Busy) return;
            _nextCustomer -= Time.deltaTime;
            if (_nextCustomer <= 0)
            {
                _nextCustomer = UnityEngine.Random.Range(5f, 10f);
                if (_customers.Count < MaxCustomers) NewCustomer();
            }
        }

        void LateUpdate()
        {
            var g = Game.I;
            if (g == null) return;
            var cam = g.Cam.GetComponent<Camera>();
            _layer.SetAsFirstSibling();
            bool show = Working && cam.isActiveAndEnabled && !g.Busy && !g.UI.BlockingExceptMenu;
            // "!" over everyone ready to order
            foreach (var c in _customers)
            {
                if (c == null) continue;
                if (!_marks.TryGetValue(c, out var rt)) _marks[c] = rt = Mark("!");
                bool on = show && c.Now == BarCustomer.State.Ready;
                if (rt.gameObject.activeSelf != on) rt.gameObject.SetActive(on);
                if (on) Put(rt, cam, c.transform.position + Vector3.up * 1.75f);
            }
            var gone = new List<BarCustomer>();
            foreach (var kv in _marks) if (kv.Key == null || !_customers.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var c in gone) { if (_marks[c] != null) Destroy(_marks[c].gameObject); _marks.Remove(c); }
            // ...and Kevin, before his order's taken
            if (_kevinMark == null) _kevinMark = Mark("!");
            bool kOn = show && !_kevinOrdered && KevinWaiting();
            if (_kevinMark.gameObject.activeSelf != kOn) _kevinMark.gameObject.SetActive(kOn);
            if (kOn) Put(_kevinMark, cam, Contract.I.Target.transform.position + Vector3.up * 1.75f);
        }

        // A little speech-bubble "!" over someone.
        RectTransform Mark(string text)
        {
            var box = UIKit.Box(_layer, "Order Mark", new Color(1f, 0.85f, 0.15f), true);
            box.raycastTarget = false;
            var rt = box.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(34, 40);
            box.gameObject.AddComponent<Outline>().effectColor = new Color(0.08f, 0.08f, 0.1f);
            var t = UIKit.Label(box.transform, text, 30, new Color(0.08f, 0.08f, 0.1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Stretch();
            t.raycastTarget = false;
            rt.gameObject.SetActive(false);
            return rt;
        }

        void Put(RectTransform rt, Camera cam, Vector3 world)
        {
            var sp = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, sp, null, out var local);
            rt.anchoredPosition = local + Vector2.up * (3f * Mathf.Sin(Time.unscaledTime * 4f));
        }

        void End(bool early, string why = null)
        {
            var g = Game.I;
            Working = false;
            _win.gameObject.SetActive(false);
            bool here = g.Current == _bar;
            foreach (var c in _customers.ToArray()) if (c != null) c.Dismiss(here);
            _customers.Clear();
            foreach (var s in _seats) s.taken = null;
            foreach (var kv in _marks) if (kv.Value != null) Destroy(kv.Value.gameObject);
            _marks.Clear();
            if (_kevinMark != null) _kevinMark.gameObject.SetActive(false);
            Holding = null;
            g.Player.ClearPose();
            g.Player.GetComponent<PlayerController>().Locked = false;
            int pay = early ? 0 : _pay;
            if (!g.S.Has($"job.paid.{g.Day}"))
            {
                g.S.Set($"job.paid.{g.Day}");
                if (pay + _tips > 0) g.Earn(pay + _tips, early ? "Tips from the lunch shift" : "Lunch shift at the Corner Bar");
            }
            g.UI.Toast((why != null ? why + " " : "") + (early ? $"You clock out early: ${_tips} in tips, no wage." : $"Shift over: ${_pay} plus ${_tips} in tips. Same time tomorrow?"));
        }
    }
}
