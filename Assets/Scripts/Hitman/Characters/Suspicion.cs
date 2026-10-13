using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // Who's noticing the player:
    //  - Security desks check for a pass as you come in.
    //  - In staff-only areas, anyone who watches you not fitting in for a few seconds calls you out and you're thrown
    //    out (and they remember you: infamy).
    //  - Hanging about right next to the target without blending in does the same (without throwing you out).
    //  - Once the target knows you (infamy 2+), they call the cops whenever they see you nearby.
    //  - Caught inside someone's home by them: cops.
    public class Suspicion : MonoBehaviour
    {
        [Tooltip("In private / staff-only places: how close to the target counts as hanging about them, and for how long.")]
        [SerializeField] float nearTarget = 3f;
        [SerializeField] float targetPatience = 4f;
        [Tooltip("In public (streets, the bar, shops, the lobby) people stand near each other all the time: only " +
                 "lingering right next to the target for a long while makes them suspicious.")]
        [SerializeField] float nearTargetPublic = 1.5f;
        [SerializeField] float targetPatiencePublic = 12f;
        [SerializeField] int copsAtInfamy = 2;
        [SerializeField] float copsRange = 10f;
        [Header("Spotted")]
        [Tooltip("Real seconds of slow motion when someone spots the player.")]
        [SerializeField] float spottedSeconds = 3f;
        [SerializeField, Range(0.05f, 1f)] float spottedTimeScale = 0.25f;

        // The slow-motion "spotted" moment is playing (no input meanwhile).
        public static bool Spotting { get; private set; }

        readonly Dictionary<Watcher, float> _noticed = new Dictionary<Watcher, float>();
        Area _checked;
        bool _throwing;

        public static Suspicion I { get; private set; }
        void Awake() { I = this; Spotting = LastChance = false; }      // (statics: reset when the game restarts)
        void Start() => Game.I.Restored += () => { _throwing = false; _noticed.Clear(); _checked = Game.I.Current; LastChance = false; Game.I.Player.Unscaled = false; };

        public int CopsAtInfamy => copsAtInfamy;

        public float InfamyWith(Watcher w)
        {
            var look = Game.I.Look;
            return look == null || w == null ? 0 : Game.I.S.InfamyOf(w.PersonId, look.TopKey, look.HairKey);
        }

        void Update()
        {
            var g = Game.I;
            if (g == null || g.Busy || g.Failing || !g.ClockRunning || _throwing || Spotting || LastChance) { if (!Spotting && !LastChance) g?.UI.SetNoticed(0, null); return; }
            var player = g.Player.GetComponent<PlayerController>();
            if (player.Locked) return;
            var pos = player.transform.position;
            var r = Restricted.Of(g.Current);
            // Hired at the paper company: its floor is your office now.
            if (r != null && r.Type == Restricted.Kind.StaffOnly && g.Current.Id == "business.tower1.floor5" && (g.S.Has("office.hired") || g.S.Has($"pass.{g.Day}"))) r = null;
            // (sent up for the interview: nobody minds a visitor on their way to reception)

            // Security desk: checked once each time you come in.
            if (g.Current != _checked)
            {
                var from = _checked;
                _checked = g.Current;
                _noticed.Clear();
                // (only when walking in from the street, not coming down in the elevator)
                if (r != null && r.Type == Restricted.Kind.Security && from == g.Current.Map) { SecurityCheck(r); return; }
            }

            // Cops: the target knows this face.
            var target = Contract.I != null ? Contract.I.Target : null;
            var tw = Watcher.Of(target);
            if (tw != null && InfamyWith(tw) >= copsAtInfamy && tw.CanSee(pos, copsRange))
            {
                Spot(tw, () => g.Fail($"{tw.DisplayName} recognised you and called the cops."));
                return;
            }

            // Caught in someone's home.
            if (r != null && r.Type == Restricted.Kind.Private && r.Owner != null && !(g.S.Has("kevin.friend") && Contract.I != null && r.Owner == Contract.I.Target))
            {
                var ow = Watcher.Of(r.Owner);
                if (ow != null && ow.CanSee(pos, 7f)) { Spot(ow, () => g.Fail($"{ow.DisplayName} caught you in his house and called the cops.")); return; }
            }

            // Being watched while not fitting in.
            Watcher worst = null;
            float worstShare = 0;
            bool blending = player.Blending != null;
            foreach (var w in Watcher.All)
            {
                float limit;
                if (r != null && r.Type == Restricted.Kind.StaffOnly) limit = r.Patience;
                else if (w == tw && (w.transform.position - pos).magnitude < (r == null ? nearTargetPublic : nearTarget))
                    limit = r == null ? targetPatiencePublic : targetPatience;
                else { Decay(w); continue; }
                if (blending || !w.CanSee(pos)) { Decay(w); continue; }
                _noticed.TryGetValue(w, out var t);
                t += Time.deltaTime;
                _noticed[w] = t;
                if (t >= limit) { CalledOut(w, r); return; }
                if (t / limit > worstShare) { worstShare = t / limit; worst = w; }
            }
            g.UI.SetNoticed(worstShare, worst != null ? worst.transform : null);
        }

        void Decay(Watcher w)
        {
            if (_noticed.TryGetValue(w, out var t)) _noticed[w] = Mathf.Max(0, t - Time.deltaTime * 0.5f);
        }

        // Spotted: a "!" over them, time slows right down and the camera closes in on the two of you for a
        // few seconds; then whatever happens next (thrown out, the cops, busted).
        public void Spot(Watcher w, System.Action then)
        {
            if (Spotting) return;
            StartCoroutine(SpotRoutine(w, then));
        }

        System.Collections.IEnumerator SpotRoutine(Watcher w, System.Action then)
        {
            var g = Game.I;
            Spotting = true;
            _noticed.Clear();
            var player = g.Player.GetComponent<PlayerController>();
            if (player.Following != null) player.StopFollow();
            g.Player.Stop();
            var wm = w != null ? w.GetComponent<CharacterMover>() : null;
            if (wm != null) { wm.Face(g.Player.transform.position); wm.PlayAction("Angry Point"); }
            g.UI.HideActions();
            g.UI.ShowSpotted(w != null ? w.transform : g.Player.transform);
            g.Cam.Spotlight(w != null ? w.transform : null, g.Player.transform);
            Time.timeScale = spottedTimeScale;
            yield return new WaitForSecondsRealtime(spottedSeconds);
            Time.timeScale = 1;
            g.Cam.Spotlight(null, null);
            g.UI.HideSpotted();
            Spotting = false;
            then?.Invoke();
        }

        // About to be called out: the world slows right down (the player doesn't) for a few seconds - long enough to
        // switch on reflex vision and get to somewhere to blend in. Blending in by the end = they let it go.
        void CalledOut(Watcher w, Restricted r)
        {
            if (Spotting || LastChance) return;
            StartCoroutine(LastChanceRoutine(w, r));
        }

        // The last-chance slow motion is running (the player can still move and blend in).
        public static bool LastChance { get; private set; }

        System.Collections.IEnumerator LastChanceRoutine(Watcher w, Restricted r)
        {
            var g = Game.I;
            LastChance = true;
            var player = g.Player.GetComponent<PlayerController>();
            if (player.Following != null) player.StopFollow();
            g.Player.Unscaled = true;
            var wm = w.GetComponent<CharacterMover>();
            if (wm != null) wm.Face(g.Player.transform.position);
            g.UI.HideActions();
            g.UI.ShowSpotted(w.transform);
            g.UI.LastChancePrompt(true);
            g.Cam.Spotlight(w.transform, g.Player.transform, 4.5f);
            Time.timeScale = spottedTimeScale;
            float end = Time.unscaledTime + spottedSeconds;
            while (Time.unscaledTime < end && player.Blending == null && !g.Failing) yield return null;
            // (a moment to settle into the spot before they look again)
            if (player.Blending != null) yield return new WaitForSecondsRealtime(0.3f);
            Time.timeScale = 1;
            g.Player.Unscaled = false;
            g.Cam.Spotlight(null, null);
            g.UI.HideSpotted();
            g.UI.LastChancePrompt(false);
            LastChance = false;
            _noticed.Clear();
            if (player.Blending != null) g.UI.Say(w.transform, "...Huh. Must've been nothing.");
            else
            {
                if (wm != null) wm.PlayAction("Angry Point");
                AfterCalledOut(w, r);
            }
        }

        void AfterCalledOut(Watcher w, Restricted r)
        {
            var g = Game.I;
            _noticed.Clear();
            g.UI.SetNoticed(0, null);
            var look = g.Look;
            float infamy = look != null ? g.S.RaiseInfamy(w.PersonId, look.TopKey, look.HairKey) : 1;
            g.Changed();
            bool throwOut = r != null && r.Type == Restricted.Kind.StaffOnly;

            g.UI.Say(w.transform, throwOut ? "Hey! You don't work here. Out!" : "Can I help you?! Back off, creep.");
            g.UI.Toast(infamy >= copsAtInfamy
                ? $"{w.DisplayName} will remember your face now. Change your top and hair before they see you again."
                : $"{w.DisplayName} noticed you. A change of clothes would help.");
            if (throwOut) ThrowOut(r.Interior);
        }

        // Walked out: down the elevator if it's an upper floor, then out of the front door.
        public void ThrowOut(Interior from)
        {
            if (from == null) return;
            _throwing = true;
            var g = Game.I;
            var player = g.Player.GetComponent<PlayerController>();
            if (from.Parent != null)
                player.Escort(from.LinkHere.transform.position, () => g.UseLink(from, from.Parent, () => ThrowOut(from.Parent)));
            else
                player.Escort(from.InsideDoor.transform.position, () => g.UseDoor(from, false, () => _throwing = false));
        }

        // ---------- Security desk ----------

        // Past the security desk: a keycard, hired, a suit, or today's interview pass.
        public bool Cleared(Restricted r)
        {
            if (r == null || r.Type != Restricted.Kind.Security) return true;
            var g = Game.I;
            return g.S.Holding("keycard") || g.S.Has("office.hired") || (g.Look != null && g.Look.WearingSuit) || g.S.Has($"pass.{g.Day}");
        }

        // Following someone further in: not past a security desk you haven't cleared (the guard walks you out), not
        // through a locked front door.
        public bool MayFollow(Area from, Area to)
        {
            var g = Game.I;
            var r = Restricted.Of(from);
            if (to is Interior up && up.Parent == from && r != null && r.Type == Restricted.Kind.Security && !Cleared(r))
            {
                g.UI.Say(r.Guard != null ? r.Guard.transform : null, "Hey! Not without a pass, pal. Out.");
                if (r.Guard != null) r.Guard.GetComponent<CharacterMover>()?.PlayAction("Angry Point");
                var pc = g.Player.GetComponent<PlayerController>();
                if (pc.Following != null) pc.StopFollow();   // (before the escort starts: stopping halts the walk)
                ThrowOut(r.Interior);
                return false;
            }
            if (to is Interior into && into.Parent == null && into.Map == from && !Restricted.MayEnter(into)) return false;
            return true;
        }

        void SecurityCheck(Restricted r)
        {
            var g = Game.I;
            var look = g.Look;
            var guard = r.Guard;
            // Tailing someone in doesn't get you past the desk: the guard stops you here.
            if (!Cleared(r)) { var pc = g.Player.GetComponent<PlayerController>(); if (pc.Following != null) pc.StopFollow(); }
            if (g.S.Holding("keycard")) { g.UI.Say(guard ? guard.transform : null, "Morning."); return; }
            if (g.S.Has("office.hired")) { g.UI.Say(guard ? guard.transform : null, "Morning. Fifth floor - you're one of them now."); return; }
            if (look != null && look.WearingSuit) { g.UI.Say(guard ? guard.transform : null, "Morning, sir."); return; }
            if (g.S.Has($"pass.{g.Day}")) { g.UI.Say(guard ? guard.transform : null, "Fifth floor, like I said."); return; }

            var replies = new List<(string, System.Action)>();
            if (g.S.Has("jobad") && g.S.Holding("newspaper"))
                replies.Add(("I'm here about the job in the paper. (Show the ad)", () =>
                {
                    g.S.Set($"pass.{g.Day}");
                    g.UI.Say(guard ? guard.transform : null, "Interviews are on the fifth floor. Go on up.");
                }));
            replies.Add(("Sorry, wrong building.", () => ThrowOut(r.Interior)));
            if (guard != null) guard.GetComponent<CharacterMover>()?.PlayAction("Angry Gesture");
            g.UI.ShowDialog(guard != null ? guard.DisplayName : "Security",
                "Whoa, whoa. This building's for employees - keycard, please. Visitors on business need at least a suit. Not dressed like that.", replies, guard != null ? guard.transform : null);
        }
    }
}
