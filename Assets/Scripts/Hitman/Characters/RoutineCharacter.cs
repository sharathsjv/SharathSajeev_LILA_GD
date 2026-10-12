using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // A person who lives by a Routine. They are simulated all day in every district; when they're in the
    // district the player is in (and not hidden, e.g. showering or on the subway) their body is shown and
    // walks on the NavMesh, otherwise their position is advanced in the background.
    [RequireComponent(typeof(CharacterMover))]
    public class RoutineCharacter : MonoBehaviour
    {
        [SerializeField] Routine routine;
        [Tooltip("The rigged model child; hidden while they're off-screen or Hidden.")]
        [SerializeField] Transform model;

        public Routine Routine => routine;
        public string AreaId { get; private set; }
        public Activity Activity { get; private set; }
        public bool InTransit => _transitUntil > 0;
        public bool Visible { get; private set; } = true;   // so the first hide actually hides
        public string CurrentPlaceId { get; private set; }
        public float Intoxication { get; set; }   // used by kill methods
        public bool Alive { get; set; } = true;
        // The step they're on right now (for documenting it in the notebook, eavesdropping, kill set-ups).
        public int StepIndex => _stepIndex;
        public RoutineStep Step => routine != null && _stepIndex >= 0 && _stepIndex < routine.steps.Length ? routine.steps[_stepIndex] : null;
        // At the step's place and doing it (not still on their way).
        public bool Settled => _legs.Count == 0 && !InTransit && CurrentPlaceId != null;
        public bool IsAwake => Alive && Activity != Activity.Sleep && Activity != Activity.PassedOut && Activity != Activity.Hidden;

        // Journey = list of legs: walk to a point in an area, ride the subway, or step through a door.
        enum LegKind { Walk, Subway, Door }

        struct Leg
        {
            public LegKind kind;
            public string area;       // Walk: area walked in. Subway/Door: area arrived in.
            public Vector3 point;     // Walk: destination. Door: where you appear on the other side.
            public Quaternion facing;
        }

        static Leg Walk(Area a, Place p) => new Leg { kind = LegKind.Walk, area = a.Id, point = p.transform.position, facing = p.transform.rotation };
        // An area and everything it sits inside, up to its street: [area, parent floor, ..., building, district].
        static List<Area> Chain(Area a)
        {
            var list = new List<Area>();
            while (a != null)
            {
                list.Add(a);
                a = a is Interior i ? i.Outside : null;
            }
            return list;
        }

        static Leg Through(Area to, Place p) => new Leg { kind = LegKind.Door, area = to.Id, point = p.transform.position, facing = p.transform.rotation };

        readonly Queue<Leg> _legs = new Queue<Leg>();
        CharacterMover _mover;
        int _stepIndex = -1;
        readonly Dictionary<int, RoutineOption> _todaysChoices = new Dictionary<int, RoutineOption>();
        float _transitUntil;
        bool _legActive;

        void Awake() => _mover = GetComponent<CharacterMover>();

        void Start()
        {
            if (routine != null && !string.IsNullOrEmpty(routine.walk)) _mover.SetWalk(routine.walk);
            ApplyPose();
        }

        // Start of each day: re-roll branching steps and put them where yesterday ended.
        public void BeginDay(int day)
        {
            _todaysChoices.Clear();
            var rng = new System.Random(day * 7919 + name.GetHashCode());
            for (int i = 0; i < routine.steps.Length; i++)
            {
                var s = routine.steps[i];
                if (s.options == null || s.options.Length == 0) continue;
                float total = 0;
                foreach (var o in s.options) total += Mathf.Max(0, o.weight);
                float roll = (float)rng.NextDouble() * total;
                foreach (var o in s.options)
                {
                    roll -= Mathf.Max(0, o.weight);
                    if (roll <= 0) { _todaysChoices[i] = o; break; }
                }
            }
            _legs.Clear();
            _transitUntil = 0;
            _stepIndex = -1;
            Intoxication = 0;
            // Snap to where the last step of the day would leave them.
            int last = routine.steps.Length - 1;
            var (place, act) = Resolve(last);
            var p = Place.Find(place);
            if (p != null) Arrive(p.Area.Id, p.transform.position, p.transform.rotation, act, place);
        }

        // Back to a checkpoint: where they'd be at this time of day, already there.
        public void ResyncTo(int day, float minutes, bool alive)
        {
            Alive = true;
            _mover.Freeze(false);
            BeginDay(day);
            int idx = StepAt(minutes);
            _stepIndex = idx;
            var (placeId, act) = Resolve(idx);
            var p = Place.Find(placeId);
            if (p != null) Arrive(p.Area.Id, p.transform.position, p.transform.rotation, act, placeId);
            if (!alive) Die();
            Visible = !Visible;      // force SetVisible to re-apply next tick
            SetVisible(!Visible);
        }

        (string place, Activity act) Resolve(int i)
        {
            var s = routine.steps[i];
            if (_todaysChoices.TryGetValue(i, out var o)) return (o.place, o.activity);
            return (s.place, s.activity);
        }

        int StepAt(float minutes)
        {
            int idx = -1;
            for (int i = 0; i < routine.steps.Length; i++)
                if (routine.steps[i].Minutes <= minutes) idx = i;
            return idx < 0 ? routine.steps.Length - 1 : idx;   // before the first step: still in last night's step
        }

        // Called every frame by the game with the clock (minutes since midnight) and the player's district.
        public void Tick(float minutes, float gameMinutesPerRealSecond, string playerArea, float gameMinutesThisFrame)
        {
            if (!Alive) { SetVisible(playerArea == AreaId && Activity != Activity.Hidden); return; }

            int idx = StepAt(minutes);
            if (idx != _stepIndex)
            {
                _stepIndex = idx;
                var (placeId, act) = Resolve(idx);
                var place = Place.Find(placeId);
                if (place != null) PlanJourney(place, act, placeId);
                else Debug.LogWarning($"[Hitman] {routine.displayName}: place '{placeId}' not found.");
            }

            // Subway ride.
            if (_transitUntil > 0)
            {
                if (minutes < _transitUntil) { SetVisible(false); return; }
                _transitUntil = 0;
                _legActive = false;
            }

            bool onScreen = playerArea == AreaId && !InTransit;
            if (!_legActive && _legs.Count > 0) StartLeg(_legs.Peek(), minutes, onScreen);

            if (_legActive)
            {
                var leg = _legs.Peek();
                if (!onScreen)
                {
                    // Off-screen: move in a straight line at walking pace (game-time based, so fast-forward works).
                    _mover.Stop();
                    var step = _mover.WalkSpeed / gameMinutesPerRealSecond * gameMinutesThisFrame;
                    transform.position = Vector3.MoveTowards(transform.position, leg.point, step);
                    if ((transform.position - leg.point).sqrMagnitude < 0.01f) FinishLeg();
                }
                else if (!_mover.Moving)
                {
                    _mover.WalkTo(leg.point, FinishLeg);
                }
            }

            SetVisible(onScreen && Activity != Activity.Hidden);
        }

        void PlanJourney(Place place, Activity act, string placeId)
        {
            _legs.Clear();
            _mover.Stop();
            _legActive = false;
            Activity = Activity.Stand;
            CurrentPlaceId = null;
            _mover.Seated = false;
            ApplyPose();   // get up off the couch / out of the chair before walking
            var destArea = place.Area;
            var cur = Game.I.Area(AreaId) ?? destArea;
            var down = Chain(destArea);
            Area common = null;
            foreach (var a in Chain(cur)) if (down.Contains(a)) { common = a; break; }
            // 1) Climb out (down the elevator / out of the door) until we reach a shared area or the street.
            while (cur != common && cur is Interior inside)
            {
                if (inside.Parent != null)
                {
                    _legs.Enqueue(Walk(inside, inside.LinkHere));
                    _legs.Enqueue(Through(inside.Parent, inside.LinkInParent));
                }
                else
                {
                    _legs.Enqueue(Walk(inside, inside.InsideDoor));
                    _legs.Enqueue(Through(inside.Map, inside.OutsideDoor));
                }
                cur = inside.Outside;
            }
            // 2) Subway to another district.
            var destDistrict = destArea.Map;
            if (common == null)
            {
                var from = cur.Map;
                if (from.SubwayExit != null) _legs.Enqueue(Walk(from, from.SubwayExit));
                _legs.Enqueue(new Leg { kind = LegKind.Subway, area = destDistrict.Id, point = destDistrict.SubwayExit.transform.position, facing = destDistrict.SubwayExit.transform.rotation });
                cur = destDistrict;
            }
            // 3) Into the destination: through the street door, then up by elevator/stairs, floor by floor.
            int k = down.IndexOf(cur);
            for (int n = (k < 0 ? down.Count : k) - 1; n >= 0; n--)
            {
                var into = (Interior)down[n];
                if (into.Parent != null)
                {
                    _legs.Enqueue(Walk(into.Parent, into.LinkInParent));
                    _legs.Enqueue(Through(into, into.LinkHere));
                }
                else
                {
                    _legs.Enqueue(Walk(into.Map, into.OutsideDoor));
                    _legs.Enqueue(Through(into, into.InsideDoor));
                }
            }
            _legs.Enqueue(Walk(destArea, place));
            _pendingActivity = act;
            _pendingPlace = placeId;
            _pendingPose = _stepIndex >= 0 && _stepIndex < routine.steps.Length ? routine.steps[_stepIndex].pose : null;
        }

        Activity _pendingActivity;
        string _pendingPlace, _pendingPose, _posing;

        void StartLeg(Leg leg, float minutes, bool onScreen)
        {
            _legActive = true;
            if (leg.kind != LegKind.Walk)
            {
                // Subway: gone for the ride. Door: straight through to the other side.
                if (leg.kind == LegKind.Subway) _transitUntil = minutes + Game.I.SubwayMinutes;
                else _legActive = false;
                AreaId = leg.area;
                _legs.Dequeue();
                transform.SetPositionAndRotation(leg.point, leg.facing);
                _mover.Stop();
                if (_legs.Count == 0) Arrive(leg.area, leg.point, leg.facing, _pendingActivity, _pendingPlace);
                return;
            }
            AreaId = leg.area;
        }

        void FinishLeg()
        {
            var leg = _legs.Dequeue();
            _legActive = false;
            if (_legs.Count == 0) Arrive(leg.area, leg.point, leg.facing, _pendingActivity, _pendingPlace);
        }

        void Arrive(string district, Vector3 pos, Quaternion facing, Activity act, string placeId)
        {
            _posing = _pendingPose;
            _pendingPose = null;
            AreaId = district;
            transform.SetPositionAndRotation(pos, facing);
            Activity = act;
            CurrentPlaceId = placeId;
            _mover.Seated = act == Activity.Sit || act == Activity.Work || act == Activity.Drink || act == Activity.Eat ||
                            (act == Activity.PassedOut && placeId != null && !placeId.Contains("bed") && !placeId.Contains("couch"));
            ApplyPose();
        }

        // The pose for what they're doing: the step's own pose, else one that fits the activity.
        // Lying on a bed or couch uses the sleeping clip, lifted onto the cushions.
        void ApplyPose()
        {
            if (model == null) return;
            bool soft = CurrentPlaceId != null && (CurrentPlaceId.Contains("bed") || CurrentPlaceId.Contains("couch"));
            bool lying = Activity == Activity.Sleep || (Activity == Activity.PassedOut && soft) || (!Alive && soft);
            model.localRotation = Quaternion.identity;
            model.localPosition = lying ? new Vector3(0, soft ? 0.45f : 0, 0) : Vector3.zero;
            if (!Alive) return;   // the death pose stays
            string pose = _posing;
            if (string.IsNullOrEmpty(pose))
                switch (Activity)
                {
                    case Activity.Stand: pose = routine.standPose; break;
                    case Activity.Sit: pose = "Sitting Idle"; break;
                    case Activity.Drink:
                    case Activity.Eat: pose = "Sitting Drinking"; break;
                    case Activity.Sleep: pose = "Sleeping Idle"; break;
                    case Activity.PassedOut: pose = lying ? "Sleeping Idle" : "Seated Idle"; break;
                }
            if (string.IsNullOrEmpty(pose)) _mover.ClearPose();
            else _mover.SetPose(pose);
        }

        // Dead: they drop (or stay lying where they passed out) and stop moving.
        public void Die(string how = null)
        {
            Alive = false;
            bool soft = CurrentPlaceId != null && (CurrentPlaceId.Contains("bed") || CurrentPlaceId.Contains("couch"));
            bool lying = soft && (Activity == Activity.Sleep || Activity == Activity.PassedOut || Activity == Activity.Eat || Activity == Activity.Sit);
            Activity = Activity.PassedOut;
            _legs.Clear();
            _mover.Stop();
            ApplyPose();
            if (lying)
            {
                _mover.SetPose("Sleeping Idle");
                _mover.Freeze(true);
            }
            else
            {
                _mover.Seated = false;
                model.localPosition = Vector3.zero;
                // They're usually facing something (a counter, the urinal): fall away from it.
                if (how != "slip") transform.Rotate(0, 180, 0);
                _mover.SetPose(how == "slip" ? "Stumble Backwards" : "Falling Forward Death");
            }
        }

        void SetVisible(bool v)
        {
            if (Visible == v) return;
            Visible = v;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = v;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = v;
            if (!v) _mover.Stop();
        }
    }
}
