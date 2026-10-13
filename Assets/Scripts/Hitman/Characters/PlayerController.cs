using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Hitman
{
    // Tap to move; tap people and objects for an action menu. While following someone, slide toward or away
    // from them on screen to close or widen the gap.
    [RequireComponent(typeof(CharacterMover))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] float followDistance = 3f;
        [SerializeField] float minFollow = 1.2f;
        [SerializeField] float maxFollow = 9f;
        [Tooltip("World units of distance change per screen-height of slide.")]
        [SerializeField] float slideSensitivity = 8f;

        public RoutineCharacter Following { get; private set; }
        public float FollowDistance => followDistance;

        CharacterMover _mover;
        readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
        readonly RaycastHit[] _tapHits = new RaycastHit[16];
        static readonly IComparer<RaycastHit> HitDistance = Comparer<RaycastHit>.Create((x, y) => x.distance.CompareTo(y.distance));
        Vector2 _pressPos, _lastPos;
        bool _pressed, _dragging;
        float _repath;
        bool _ridingWithTarget;

        public event Action<Usable, UseAction> CustomAction;

        // A one-off move for the player (pick up, put down, open...).
        public void Act(string clip) => _mover.PlayAction(clip);   // hook for kill methods etc.

        void Awake() => _mover = GetComponent<CharacterMover>();

        void Start() => Game.I.Restored += () => { Locked = false; EndBlend(); };

        // Control taken away (being walked out by security).
        public bool Locked { get; set; }
        // Doing something that fits in where they are (typing at a desk, getting coffee): restricted areas leave
        // them alone while they're at it. Cleared as soon as they walk off.
        public BlendSpot Blending { get; private set; }

        void Update()
        {
            var g = Game.I;
            if (g == null) return;
            if (!Locked && !Suspicion.Spotting) HandlePointer(g);
            UpdateFollow(g);
            if (Blending != null && (_mover.Moving || (transform.position - Blending.Stand.position).sqrMagnitude > 1f)) EndBlend();
        }

        public void Blend(BlendSpot spot)
        {
            Blending = spot;
            if (spot.Sit && !Game.I.S.Has("tip.wait")) { Game.I.S.Set("tip.wait"); Game.I.UI.Toast("Tip: tap yourself to wait and let time pass."); }
            _mover.Seated = spot.Sit;
            if (!string.IsNullOrEmpty(spot.Pose)) _mover.SetPose(spot.Pose);
        }

        public void EndBlend()
        {
            if (Blending == null) return;
            Blending = null;
            _mover.Seated = false;
            _mover.ClearPose();
        }

        // Taken by the arm: walk to a spot whatever the player taps, then carry on.
        public void Escort(Vector3 to, Action then)
        {
            if (Following != null) StopFollow();
            EndBlend();
            Locked = true;
            Game.I.UI.HideActions();
            _mover.WalkTo(to, () => { Locked = false; then?.Invoke(); });
        }

        // Walk over, then do something (actions from IActionSources).
        public void GoThen(Transform at, bool exact, Action act, bool sit = false)
        {
            if (Following != null) StopFollow();
            EndBlend();
            var target = at.position;
            if (!exact)
            {
                // Stand just in front of it, on the near side.
                var toMe = transform.position - at.position;
                toMe.y = 0;
                var col = at.GetComponentInChildren<Collider>();
                float r = col != null ? Mathf.Max(col.bounds.extents.x, col.bounds.extents.z) : 0.3f;
                if (toMe.sqrMagnitude > 0.01f) target = at.position + toMe.normalized * Mathf.Min(r + 0.5f, toMe.magnitude);
            }
            _mover.WalkTo(target, () =>
            {
                if (exact) transform.rotation = at.rotation;
                else _mover.Face(at.position);
                // Seats are off the walkable floor (a booth's bench, a couch): slide the last bit onto it.
                if (exact && sit) transform.position = new Vector3(at.position.x, transform.position.y, at.position.z);
                _mover.Seated = sit;
                act?.Invoke();
            });
        }

        void Document(RoutineCharacter who)
        {
            var step = who.Step;
            var r = who.Routine;
            Game.I.Note($"routine.{r.name}.{who.StepIndex}", $"{step.time} - {step.documented}", NoteKind.Routine, step.opening);
        }

        // Some static actions only make sense sometimes.
        public static bool Hidden(UseAction a)
        {
            var g = Game.I;
            switch (a.id)
            {
                case "stock": return !g.S.Holding("food");
                case "officework": return !g.S.Has("office.hired");
                default: return false;
            }
        }

        // ---------- Input ----------

        void HandlePointer(Game g)
        {
            var p = Pointer.current;
            if (p == null) return;
            var pos = p.position.ReadValue();
            if (p.press.wasPressedThisFrame && (g.AtDesk || g.Waiting)) { g.StopWork(); return; }   // a tap gets you up from the desk / stops waiting
            if (p.press.wasPressedThisFrame)
            {
                bool overUI = OverUI(pos);
                // A tap away from an open menu closes it and counts as a tap on the world.
                if (!overUI && g.UI.MenuOpen && !g.UI.BlockingExceptMenu) g.UI.HideActions();
                _pressed = !overUI && !g.Busy && !g.UI.Blocking && !_mover.Acting;   // (stay put mid-action)
                _dragging = false;
                _pressPos = _lastPos = pos;
            }
            else if (_pressed && p.press.isPressed)
            {
                if (!_dragging && (pos - _pressPos).magnitude > 30f * Screen.height / 1080f) _dragging = true;
                if (_dragging && Following != null) Slide(pos - _lastPos);
                _lastPos = pos;
            }
            else if (_pressed && p.press.wasReleasedThisFrame)
            {
                _pressed = false;
                if (!_dragging) Tap(g, pos);
            }
        }

        // Sliding toward the target (on screen) closes the gap; away widens it.
        void Slide(Vector2 delta)
        {
            var cam = Game.I.Cam.GetComponent<Camera>();
            Vector2 me = cam.WorldToScreenPoint(transform.position);
            Vector2 them = cam.WorldToScreenPoint(Following.transform.position);
            var toward = (them - me).sqrMagnitude > 1 ? (them - me).normalized : Vector2.up;
            float along = Vector2.Dot(delta, toward) / Screen.height;
            followDistance = Mathf.Clamp(followDistance - along * slideSensitivity, minFollow, maxFollow);
            Game.I.UI.SetFollowing(Following, followDistance);
        }

        void Tap(Game g, Vector2 pos)
        {
            var cam = g.Cam.GetComponent<Camera>();
            // Ignore buildings/walls that are see-through because they're in the way: tap what's behind them.
            var ray = cam.ScreenPointToRay(pos);
            int n = Physics.RaycastNonAlloc(ray, _tapHits, 300f);
            if (n == 0) return;
            Array.Sort(_tapHits, 0, n, HitDistance);
            int pick = -1;
            // Tapped yourself (first thing under the finger): let some time pass.
            if (_tapHits[0].collider.transform.IsChildOf(transform) && !g.Busy)
            {
                var self = new List<(string, Action)> { ("Wait here (tap to stop)", g.WaitHere) };
                g.UI.ShowActions("You", self, pos);
                return;
            }
            for (int i = 0; i < n; i++)
            {
                var c = _tapHits[i].collider;
                if (c.transform.IsChildOf(transform)) continue;                 // (yourself: tap what's behind you)
                // A door is always what you meant, even if the wall around it has faded.
                if (c.GetComponentInParent<Usable>() is Usable u0 && u0.Actions != null && Array.Exists(u0.Actions, x => x.id == "enter" || x.id == "exit" || x.id == "link")
                    && (!c.TryGetComponent<Renderer>(out var dr) || dr.enabled)) { pick = i; break; }
                // Skip see-through buildings and anything in a room that's hidden right now.
                if (OcclusionFader.IsFaded(c) || c.bounds.min.y > OcclusionFader.OverheadHeight ||
                    (c.TryGetComponent<Renderer>(out var r) && !r.enabled && c.GetComponentInParent<RoutineCharacter>() == null)) continue;
                pick = i;
                break;
            }
            if (pick < 0) return;
            var hit = _tapHits[pick];

            // A customer during the bar shift: straight over to them.
            var customer = hit.collider.GetComponentInParent<BarCustomer>();
            if (customer != null) { customer.Tapped(this, pos); return; }

            var person = hit.collider.GetComponentInParent<RoutineCharacter>();
            if (person != null && person.Visible)
            {
                if (Contract.I != null && person == Contract.I.Target) g.S.Set("target.tapped");
                var menu = new ActionMenu(this, person.transform, person.Routine.displayName);
                if (BarShift.Working) g.UI.BarShift.AddPersonActions(person, menu);
                if (person.Alive)
                {
                    if (Following != person) menu.Add($"Follow {person.Routine.displayName}", () => StartFollow(person));
                    else menu.Add("Stop following", StopFollow);
                    var step = person.Step;
                    if (step != null && !string.IsNullOrEmpty(step.documented) && person.Settled)
                        menu.Add("Document what they're doing", () => Document(person));
                }
                foreach (var src in person.GetComponents<IActionSource>()) src.AddActions(menu);
                g.UI.ShowActions(menu.Title, menu.Items, pos);
                return;
            }

            var usable = hit.collider.GetComponentInParent<Usable>();
            var sources = hit.collider.GetComponentsInParent<IActionSource>();
            if (usable != null || sources.Length > 0)
            {
                var menu = new ActionMenu(this, usable != null ? usable.transform : ((Component)sources[0]).transform,
                                          usable != null ? usable.Title : Pretty(((Component)sources[0]).name));
                if (usable != null)
                    foreach (var a in usable.Actions)
                    {
                        var action = a;
                        if (Hidden(action)) continue;
                        menu.Items.Add((a.label, () => GoUse(usable, action)));
                    }
                foreach (var src in sources) src.AddActions(menu);
                // A shop shelf: put back anything you've picked up and not paid for yet.
                if (usable != null && Array.Exists(usable.Actions, x => x.id == "take"))
                    foreach (var item in Shop.Unpaid())
                    {
                        var it = item;
                        menu.AddWalk($"Put back: {Items.Name(it)}", () => { _mover.PlayAction("Picking Up Object"); Shop.PutBackOne(it); });
                    }
                if (menu.Items.Count > 0) g.UI.ShowActions(menu.Title, menu.Items, pos);
                return;
            }

            // The road: offer to cross to the other side of it (the only place "Cross street" appears).
            if (IsRoad(hit.collider))
            {
                var rb = hit.collider.bounds;
                var me = transform.position;
                var spot = new Vector3(hit.point.x, 0, hit.point.z);
                // across the road's narrow way, to the side away from the player
                if (rb.size.x < rb.size.z) spot.x = me.x < rb.center.x ? rb.max.x + 1.0f : rb.min.x - 1.0f;
                else spot.z = me.z < rb.center.z ? rb.max.z + 1.0f : rb.min.z - 1.0f;
                if (NavMesh.SamplePosition(spot, out var nav, 2.5f, NavMesh.AllAreas))
                {
                    var acts = new List<(string, Action)> { ("Cross street", () => { if (Following != null) StopFollow(); EndBlend(); _mover.WalkTo(nav.position); }) };
                    g.UI.ShowActions("Street", acts, pos);
                }
                return;
            }

            // A building with nothing to use: walk to the sidewalk in front of it.
            if (IsBuilding(hit.collider))
            {
                var b = hit.collider.bounds;
                var face = b.ClosestPoint(transform.position);
                var outward = transform.position - face;
                outward.y = 0;
                // Snap to the face's axis so we end up straight in front of it, not off a corner.
                outward = Mathf.Abs(outward.x) > Mathf.Abs(outward.z) ? new Vector3(Mathf.Sign(outward.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(outward.z));
                var spot = new Vector3(hit.point.x, 0, hit.point.z);
                spot = new Vector3(Mathf.Clamp(spot.x, b.min.x, b.max.x), 0, Mathf.Clamp(spot.z, b.min.z, b.max.z));
                if (outward.x != 0) spot.x = (outward.x > 0 ? b.max.x : b.min.x) + outward.x * 1.2f;
                else spot.z = (outward.z > 0 ? b.max.z : b.min.z) + outward.z * 1.2f;
                if (NavMesh.SamplePosition(spot, out var nav, 2.5f, NavMesh.AllAreas))
                {
                    if (Following != null) StopFollow();
                    EndBlend();
                    _mover.WalkTo(nav.position);
                }
                return;
            }

            if (Following != null) StopFollow();
            EndBlend();
            _mover.WalkTo(hit.point);
        }

        // "Off_MopBucket" -> "Mop Bucket", "Bar_Stool2" -> "Stool".
        static string Pretty(string name)
        {
            int u = name.IndexOf('_');
            if (u >= 0 && u <= 4) name = name.Substring(u + 1);
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\d+$|[_]", " ").Trim();
            return System.Text.RegularExpressions.Regex.Replace(name, @"(?<=[a-z])(?=[A-Z])", " ");
        }

        static bool IsRoad(Collider c) => c.name.StartsWith("Gnd_") && c.name.EndsWith("Road");

        static bool IsBuilding(Collider c) =>
            c.bounds.size.y >= 3f && c.TryGetComponent<Unity.AI.Navigation.NavMeshModifier>(out var m) && m.overrideArea && m.area == 1;

        bool OverUI(Vector2 pos)
        {
            if (EventSystem.current == null) return false;
            _uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pos }, _uiHits);
            return _uiHits.Count > 0;
        }

        // ---------- Using things ----------

        void GoUse(Usable u, UseAction a)
        {
            if (Following != null) StopFollow();
            EndBlend();
            // Shop shelves: stand on the floor beside it (never in it), facing screen-left like the fridge.
            if (a.id == "take" && u.StandPoint == null && ShelfSpot(u, out var stand, out var face))
            {
                _mover.WalkTo(stand, () =>
                {
                    transform.rotation = Quaternion.LookRotation(face);
                    Perform(u, a);
                });
                return;
            }
            var spot = u.StandPoint != null ? u.StandPoint : u.transform;
            // Sitting in a blend spot (booth, bar stool...): head for its seat, not just the Usable's stand point.
            var blend = a.id == "blend" ? u.GetComponent<BlendSpot>() : null;
            if (blend != null && blend.Sit) spot = blend.Stand;
            if (a.sit && SeatTaken(spot.position)) { Game.I.UI.Toast("Someone's sitting there."); return; }
            _mover.WalkTo(spot.position, () =>
            {
                if (blend != null && blend.Sit) transform.rotation = spot.rotation;
                else if (u.StandPoint != null) transform.rotation = u.StandPoint.rotation;
                else _mover.Face(u.transform.position);
                // Seats are off the walkable floor (a booth's bench, a couch): slide the last bit onto it.
                if (a.sit && a.thenGoTo == null) transform.position = new Vector3(spot.position.x, transform.position.y, spot.position.z);
                if (a.thenGoTo == null)
                {
                    _mover.Seated = a.sit;
                    Perform(u, a);
                    return;
                }
                _mover.WalkTo(a.thenGoTo.position, () =>
                {
                    transform.rotation = a.thenGoTo.rotation;
                    _mover.Seated = a.sit;
                    Perform(u, a);
                });
            });
        }

        // Anyone (story character, staff, a bar customer) already sitting right there?
        public static bool SeatTaken(Vector3 seat, CharacterMover except = null)
        {
            foreach (var m in FindObjectsByType<CharacterMover>(FindObjectsSortMode.None))
            {
                if (m == except || m == Game.I.Player || !m.isActiveAndEnabled) continue;
                var d = m.transform.position - seat;
                d.y = 0;
                if (d.sqrMagnitude < 0.6f * 0.6f) return true;
            }
            return false;
        }

        bool ShelfSpot(Usable u, out Vector3 stand, out Vector3 face)
        {
            var cam = Game.I.Cam.transform;
            face = -Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;      // screen-left
            stand = default;
            var col = u.GetComponentInChildren<Collider>();
            var b = col != null ? col.bounds : new Bounds(u.transform.position, Vector3.one * 0.5f);
            // the shelf's edge on the screen-right side, then half a metre out
            var right = -face;
            float reach = Mathf.Abs(right.x) * b.extents.x + Mathf.Abs(right.z) * b.extents.z;
            var want = new Vector3(b.center.x, 0, b.center.z) + right * (reach + 0.55f);
            var near = new Vector3(Mathf.Clamp(transform.position.x, b.min.x, b.max.x), 0, Mathf.Clamp(transform.position.z, b.min.z, b.max.z));
            want += Vector3.ProjectOnPlane(near - new Vector3(b.center.x, 0, b.center.z), right);       // level with where you are along it
            if (NavMesh.SamplePosition(want, out var hit, 0.6f, NavMesh.AllAreas)) { stand = hit.position; return true; }
            // no room on that side: the nearest floor beside it, still facing it
            if (!NavMesh.SamplePosition(new Vector3(b.center.x, 0, b.center.z), out hit, 2f, NavMesh.AllAreas)) return false;
            stand = hit.position;
            var to = new Vector3(b.center.x, 0, b.center.z) - new Vector3(stand.x, 0, stand.z);
            if (to.sqrMagnitude > 0.01f) face = to.normalized;
            return true;
        }

        void Perform(Usable u, UseAction a)
        {
            var g = Game.I;
            switch (a.id)
            {
                case "laptop": g.UI.ShowComputer(); break;
                case "notepad": g.UI.ShowNotepad(); break;
                case "eat": Actions.Eat(a.arg); break;
                case "stock": Actions.StockFridge(); break;
                case "meal": Actions.Meal(a.arg); break;
                case "closed": g.UI.Toast(string.IsNullOrEmpty(a.arg) ? "Closed for good." : a.arg); break;
                case "search": g.UI.ShowSearch(); break;
                case "loiter": _mover.SetPose("Sitting"); Actions.Loiter(u); break;
                case "payphone": _mover.SetPose("Talking On Phone"); g.UI.ShowPhone(u.GetComponent<Payphone>()); break;
                case "buy": _mover.PlayAction(a.arg == "coffee" ? "Taking Item" : "Picking Up Object"); Actions.Buy(a.arg); break;
                case "take": _mover.PlayAction("Picking Up Object"); Shop.TakeFromShelf(a.arg); break;
                case "pay": Shop.PayAtTill(); break;
                case "blend": { var b = u.GetComponent<BlendSpot>(); if (b != null) Blend(b); break; }
                case "barber": g.UI.ShowBarber(); break;
                case "suit": Actions.BuySuit(int.TryParse(a.arg, out var price) ? price : 12); break;
                case "sleep": g.Sleep(); break;
                case "officework": _mover.Seated = true; g.WorkAtDesk(); break;
                case "subway": g.UI.ShowTravel(true); break;
                case "taxi": g.UI.ShowTravel(false); break;
                case "enter": if (Restricted.MayEnter(g.Area(a.arg) as Interior)) g.UseDoor(g.Area(a.arg) as Interior, true); break;
                case "exit": if (Shop.CanLeave()) g.UseDoor(g.Current as Interior, false); break;
                case "link": g.UseLink(g.Current as Interior, g.Area(a.arg) as Interior); break;
                case "wardrobe": g.UI.ShowWardrobe(Wardrobe.Nearest(u.transform.position)); break;
                default: CustomAction?.Invoke(u, a); break;
            }
        }

        // ---------- Following ----------

        public void StartFollow(RoutineCharacter who)
        {
            Following = who;
            _ridingWithTarget = false;
            Game.I.Cam.Follow(transform, who.transform);
            Game.I.UI.SetFollowing(who, followDistance);
        }

        public void StopFollow()
        {
            Following = null;
            _mover.SpeedScale = 1;
            _mover.Stop();
            Game.I.Cam.Follow(transform);
            Game.I.UI.SetFollowing(null, 0);
        }

        void UpdateFollow(Game g)
        {
            if (Following == null || g.Busy) return;
            var t = Following;

            // They got on the subway: ride along and pick the trail back up on the other side.
            if (t.InTransit && !_ridingWithTarget)
            {
                _ridingWithTarget = true;
                g.Travel(g.Area(t.AreaId).Map.Id, true, () => { _ridingWithTarget = false; g.Cam.Follow(transform, t.transform); });
                return;
            }
            // They went through a door: go in (or out) after them.
            if (!t.InTransit && t.AreaId != g.Current.Id)
            {
                var theirs = g.Area(t.AreaId);
                // One room at a time towards them (they may be a few rooms ahead): in, if they're somewhere inside
                // this one; otherwise back out.
                Interior inward = null;
                for (var a = theirs as Interior; a != null; a = a.Outside as Interior)
                    if (a.Outside == g.Current) { inward = a; break; }
                var next = inward != null ? inward : (g.Current as Interior)?.Outside;
                if (next == null || theirs.Map != g.Current.Map) return;
                if (Suspicion.I != null && !Suspicion.I.MayFollow(g.Current, next)) { if (Following != null) StopFollow(); return; }
                void Resume()
                {
                    g.Cam.Follow(transform, t.transform);
                    // already walking after them as the picture fades back in
                    if (t.GetComponent<CharacterMover>() is CharacterMover m && m.Moving) _mover.WalkTo(t.transform.position);
                    _repath = 0.3f;
                }
                if (inward != null && inward.Parent == null) g.UseDoor(inward, true, Resume);
                else if (inward != null) g.UseLink((Interior)g.Current, inward, Resume);
                else if (((Interior)g.Current).Parent == null) g.UseDoor((Interior)g.Current, false, Resume);
                else g.UseLink((Interior)g.Current, (Interior)next, Resume);
                return;
            }
            if (!t.Visible) { _mover.SpeedScale = 1; return; }   // e.g. in the shower: wait where you are

            var toThem = t.transform.position - transform.position;
            toThem.y = 0;
            float gap = toThem.magnitude;
            var legs = t.GetComponent<CharacterMover>();
            bool walking = legs != null && legs.Moving;
            if (walking)
            {
                // They're on the move: walk after them at their pace, a little quicker when dropping back and slower
                // when catching up, so the walk never stops and starts. (Heading for where they are, not for a spot
                // behind them that we'd reach and stop at.)
                float pace = legs.WalkSpeed * legs.SpeedScale * (1 + Mathf.Clamp((gap - followDistance) * 0.45f, -0.7f, 0.6f));
                _mover.SpeedScale = Mathf.Clamp(pace / Mathf.Max(0.1f, _mover.WalkSpeed), 0.3f, 1.5f);
                if (gap < followDistance * 0.5f) { _mover.Stop(); return; }   // (bumped into them: wait for the gap)
                if (!_mover.Moving) _repath = 0;                              // they've set off: so do we, right away
            }
            else _mover.SpeedScale = 1;

            _repath -= Time.deltaTime;
            if (_repath > 0) return;
            _repath = 0.3f;
            if (walking) { _mover.WalkTo(t.transform.position); return; }
            if (Mathf.Abs(gap - followDistance) < 0.4f) { _mover.Stop(); return; }
            var goal = t.transform.position - toThem.normalized * followDistance;
            _mover.WalkTo(goal);
        }
    }
}
