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
        public bool Locked { get; private set; }
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
        static bool Hidden(UseAction a)
        {
            var g = Game.I;
            switch (a.id)
            {
                case "stock": return !g.S.Holding("food");
                default: return false;
            }
        }

        // ---------- Input ----------

        void HandlePointer(Game g)
        {
            var p = Pointer.current;
            if (p == null) return;
            var pos = p.position.ReadValue();
            if (p.press.wasPressedThisFrame)
            {
                _pressed = !OverUI(pos) && !g.Busy && !g.UI.Blocking;
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

            var person = hit.collider.GetComponentInParent<RoutineCharacter>();
            if (person != null && person.Visible)
            {
                var menu = new ActionMenu(this, person.transform, person.Routine.displayName);
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
                if (menu.Items.Count > 0) g.UI.ShowActions(menu.Title, menu.Items, pos);
                return;
            }

            // A building with nothing to use: offer to cross over to the sidewalk in front of it.
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
                    var acts = new List<(string, Action)> { ("Cross road", () => { if (Following != null) StopFollow(); _mover.WalkTo(nav.position); }) };
                    g.UI.ShowActions(hit.collider.name, acts, pos);
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
            var spot = u.StandPoint != null ? u.StandPoint : u.transform;
            _mover.WalkTo(spot.position, () =>
            {
                if (u.StandPoint != null) transform.rotation = u.StandPoint.rotation;
                else _mover.Face(u.transform.position);
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
                void Resume() => g.Cam.Follow(transform, t.transform);
                if (theirs is Interior into && into.Parent == null && into.Map == g.Current) g.UseDoor(into, true, Resume);
                else if (g.Current is Interior here && here.Parent == null && theirs == here.Map) g.UseDoor(here, false, Resume);
                else if (theirs is Interior upper && upper.Parent == g.Current) g.UseLink((Interior)g.Current, upper, Resume);
                else if (g.Current is Interior floor && floor.Parent != null && theirs == floor.Parent) g.UseLink(floor, floor.Parent, Resume);
                return;
            }
            if (!t.Visible) return;   // e.g. in the shower: wait where you are

            _repath -= Time.deltaTime;
            if (_repath > 0) return;
            _repath = 0.3f;
            var toThem = t.transform.position - transform.position;
            toThem.y = 0;
            float gap = toThem.magnitude;
            if (Mathf.Abs(gap - followDistance) < 0.4f) { if (!t.GetComponent<CharacterMover>().Moving) _mover.Stop(); return; }
            var goal = t.transform.position - toThem.normalized * followDistance;
            _mover.WalkTo(goal);
        }
    }
}
