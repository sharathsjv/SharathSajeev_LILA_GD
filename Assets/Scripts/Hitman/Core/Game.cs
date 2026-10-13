using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Hitman
{
    // Runs the day: clock, the player's needs, fast-forwarding (eating / sleeping / travel), switching
    // districts, and ticking everyone's routine.
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }

        [Header("World")]
        [SerializeField] District[] districts;
        [SerializeField] string startDistrict = "shackleton";
        [Tooltip("Where the player starts each morning (at their laptop).")]
        [SerializeField] Place homeStart;
        [SerializeField] CharacterMover player;
        [SerializeField] HitmanCamera cam;
        [SerializeField] HitmanUI ui;
        [Tooltip("Multiplies the number of passers-by and cars in every district.")]
        [SerializeField, Range(0, 3)] float streetDensity = 1f;

        [Header("Time")]
        [Tooltip("Real seconds for one day from Day Start to Day End.")]
        [SerializeField] float realSecondsPerDay = 1500;
        [SerializeField] int dayStartHour = 7;
        [SerializeField] int dayEndHour = 24;
        [Tooltip("If the player hasn't slept by this hour (e.g. 26 = 2 AM) they pass out.")]
        [SerializeField] int passOutHour = 26;
        [SerializeField] int subwayMinutes = 12;
        [SerializeField] int taxiMinutes = 15;
        [Tooltip("Real seconds a fast-forward takes, however much game time it skips.")]
        [SerializeField] float fastForwardSeconds = 2.5f;
        [Tooltip("How long (real seconds) the subway ride screen shows while the clock skips ahead.")]
        [SerializeField] float subwayRideSeconds = 6f;
        [Tooltip("How long (real seconds) the screen takes to fade out, and again to fade in, between scenes.")]
        [SerializeField] float fadeSeconds = 0.35f;

        [Header("Needs (0 = fine, 1 = desperate)")]
        [SerializeField] float hungerPerHour = 0.08f;
        [Tooltip("Not eating for so long that hunger hits the top: the player passes out for this many hours.")]
        [SerializeField] float faintHours = 4f;
        [Tooltip("How hungry the player wakes up on the first morning (so there's breakfast to go and get).")]
        [SerializeField, Range(0, 1)] float firstMorningHunger = 0.65f;
        [SerializeField] float tirednessPerHour = 0.05f;

        public int Day { get; private set; }
        public float Minutes { get; private set; }
        public bool ClockRunning { get; set; }
        public bool Busy { get; private set; }          // fast-forward / travel in progress: no input
        public float Hunger { get; private set; }
        public float Tiredness { get; private set; }
        public Area Current { get; private set; }
        public Area[] Areas { get; private set; }
        public CharacterMover Player => player;
        public HitmanCamera Cam => cam;
        public HitmanUI UI => ui;
        public int SubwayMinutes => subwayMinutes;
        public int TaxiMinutes => taxiMinutes;
        public RoutineCharacter[] People { get; private set; }
        public string ClockText => Clock(Minutes);
        public static readonly string[] Weekdays = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        public string Weekday => Weekdays[(Day - 1) % 7];
        // The player's progress: cash, pockets, notes, flags. Brought back from the last checkpoint when they're caught.
        public State S { get; private set; } = new State();
        public PlayerLook Look => player != null ? player.GetComponent<PlayerLook>() : null;
        public bool Failing { get; private set; }
        // Game minutes passing per real second right now (including fast-forward).
        public float MinutesPerRealSecondNow => MinutesPerRealSecond * _ffRate;
        float MinutesPerRealSecond => (dayEndHour - dayStartHour) * 60f / realSecondsPerDay;

        float _ffRate = 1;
        float _fullFor;                                  // game minutes before hunger starts rising again
        public bool Full => _fullFor > 0;

        public event Action<int> DayStarted;
        public event Action Restored;            // after going back to a checkpoint
        public event Action StateChanged;        // cash / pockets / notes changed

        Checkpoint _checkpoint;

        void Awake()
        {
            I = this;
            if (GetComponent<Suspicion>() == null) gameObject.AddComponent<Suspicion>();
            if (GetComponent<Eavesdrop>() == null) gameObject.AddComponent<Eavesdrop>();
            StreetLife.Density = streetDensity;
            People = FindObjectsByType<RoutineCharacter>(FindObjectsInactive.Include);
            Areas = FindObjectsByType<Area>(FindObjectsInactive.Include);
            foreach (var a in Areas) a.Leave();
        }

        void Start()
        {
            EnterArea(District(startDistrict));
            S.Put("fridge.meals", 2);
            // A new game starts in the player's own clothes, which are all they own.
            var look = Look;
            if (look != null)
            {
                look.Wear(new Outfit { who = look.SceneCharacter }, false);
                foreach (var slot in new[] { Slot.Top, Slot.Bottoms, Slot.Shoes }) S.AddClothes(slot, Outfit.Own, 0);
            }
            Hunger = firstMorningHunger;
            BeginDay(1);
            SaveCheckpoint();
        }

        public District District(string id) => districts.FirstOrDefault(d => d.Id == id);
        public Area Area(string id) => Areas.FirstOrDefault(a => a.Id == id);

        public static string Clock(float minutes)
        {
            int m = Mathf.FloorToInt(minutes) % (24 * 60);
            return $"{m / 60:00}:{m % 60:00}";
        }

        void BeginDay(int day)
        {
            Day = day;
            Minutes = dayStartHour * 60;
            Tiredness = 0;
            foreach (var p in People) p.BeginDay(day);
            if (Current != homeStart.Area) EnterArea(homeStart.Area);
            player.Teleport(homeStart.transform.position, homeStart.transform.rotation);
            player.Seated = true;
            cam.Snap(player.transform);
            ClockRunning = true;
            DayStarted?.Invoke(day);
        }

        // A fast-forward / time skip is in progress (the world is simulated through it, just faster).
        public bool FastForwarding => _ffRate > 1.01f;

        void Update()
        {
            if (!ClockRunning) return;
            float dt = FastForwarding ? Time.unscaledDeltaTime : Time.deltaTime;
            float dm = dt * MinutesPerRealSecond * _ffRate;
            // Small slices, so every routine step and every trigger (poison, wet floor...) happens in order, even
            // when hours go by in a couple of seconds.
            int n = Mathf.Max(1, Mathf.CeilToInt(dm / 0.5f));
            float slice = dm / n;
            for (int i = 0; i < n; i++)
            {
                Minutes += slice;
                if (_fullFor > 0) _fullFor -= slice;
                else Hunger = Mathf.Clamp01(Hunger + hungerPerHour * slice / 60f);
                Tiredness = Mathf.Clamp01(Tiredness + tirednessPerHour * slice / 60f);
                foreach (var p in People) p.Tick(Minutes, MinutesPerRealSecond, Current.Id, slice);
                if (Contract.I != null) Contract.I.Simulate(slice);
            }
            if (Minutes >= passOutHour * 60 && !Busy) PassOut();
            else if (Hunger >= 1f && !Busy && !Failing) Faint();
        }

        // ---------- Progress: cash, pockets, notes ----------

        public void Changed() => StateChanged?.Invoke();

        public bool Spend(int amount)
        {
            if (S.cash < amount) { ui.Toast($"Not enough cash (${S.cash})."); return false; }
            S.cash -= amount;
            Changed();
            return true;
        }

        public void Earn(int amount, string why)
        {
            S.cash += amount;
            ui.Toast($"+${amount}  {why}");
            Changed();
        }

        // Put something in the player's pockets (up to 5 things). False if they're full.
        public bool Give(string item, bool quiet = false)
        {
            if (S.Full) { ui.Toast("Your pockets are full (5 things). Leave something in the wardrobe at home."); return false; }
            S.items.Add(item);
            if (!quiet) ui.Toast($"Got: {Items.Name(item)}");
            Changed();
            return true;
        }

        public bool Take(string item)
        {
            if (!S.items.Remove(item)) return false;
            Changed();
            return true;
        }

        // Write something down in the notebook (once per id). Every new note is also a checkpoint.
        public bool Note(string id, string text, NoteKind kind = NoteKind.Info, int opening = 0)
        {
            foreach (var n in S.notes) if (n.id == id) return false;
            S.notes.Add(new Note { id = id, text = text, kind = kind, day = Day, minutes = Minutes, opening = opening });
            ui.Toast(opening > 0 ? "Noted - and you've got an idea. Check the notebook." : "Noted in your notebook.");
            Changed();
            SaveCheckpoint();
            return true;
        }

        // ---------- Checkpoints ----------

        public void SaveCheckpoint()
        {
            var look = Look;
            _checkpoint = new Checkpoint
            {
                day = Day, minutes = Minutes, hunger = Hunger, tiredness = Tiredness, full = _fullFor,
                area = Current.Id, position = player.transform.position, rotation = player.transform.rotation,
                state = S.Copy(), outfit = look != null ? look.Current : null,
                alive = People.Select(p => p.Alive).ToArray(),
            };
            _checkpoint.state.unpaid.Clear();
        }

        // Caught: say why, then go back to the last checkpoint.
        public void Fail(string reason)
        {
            if (Failing) return;
            StartCoroutine(FailRoutine(reason));
        }

        IEnumerator FailRoutine(string reason)
        {
            Failing = true;
            Busy = true;
            ClockRunning = false;
            player.Stop();
            player.GetComponent<PlayerController>()?.StopFollow();
            yield return ui.ShowBusted(reason);
            Restore(_checkpoint);
            Busy = false;
            Failing = false;
        }

        public void Restore(Checkpoint c)
        {
            if (c == null) return;
            _ffRate = 1;
            Day = c.day;
            Minutes = c.minutes;
            Hunger = c.hunger;
            _fullFor = c.full;
            Tiredness = c.tiredness;
            S = c.state.Copy();
            var area = Area(c.area) ?? homeStart.Area;
            if (Current != area) EnterArea(area);
            player.Teleport(c.position, c.rotation);
            player.Seated = false;
            var look = Look;
            if (look != null && c.outfit != null) look.Wear(c.outfit);
            for (int i = 0; i < People.Length; i++) People[i].ResyncTo(Day, Minutes, c.alive == null || i >= c.alive.Length || c.alive[i]);
            cam.Snap(player.transform);
            ClockRunning = true;
            Restored?.Invoke();
            Changed();
        }

        public void RestartFromCheckpoint() => Restore(_checkpoint);

        // Start over from the title screen: a fresh first morning, nothing carried over.
        public void RestartGame()
        {
            Time.timeScale = 1;
            var scene = gameObject.scene;
            if (scene.buildIndex >= 0) UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
            else UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
        }

        // ---------- Areas, doors & travel ----------

        void EnterArea(Area a)
        {
            if (Current != null && Current != a) Current.Leave();
            Current = a;
            a.Enter();
            cam.Apply(a);
        }

        // Step through a building's door: into the interior, or back out to the street.
        public void UseDoor(Interior interior, bool entering, Action done = null)
        {
            if (interior == null || Busy) return;
            StartCoroutine(Fade(entering ? interior.DisplayName : interior.Map.DisplayName, () =>
            {
                EnterArea(entering ? interior : interior.Map);
                var spot = entering ? interior.InsideDoor : interior.OutsideDoor;
                player.Teleport(spot.transform.position, spot.transform.rotation);
                cam.Snap(player.transform);
                done?.Invoke();
            }));
        }

        // Ride the elevator (or take the stairs) between an interior and the floor it's linked to.
        public void UseLink(Interior from, Interior to, Action done = null)
        {
            if (from == null || to == null || Busy) return;
            bool up = to.Parent == from;                     // into the linked floor
            if (!up && from.Parent != to) return;
            var via = up ? to : from;
            StartCoroutine(Fade($"{via.LinkLabel}: {to.DisplayName}", () =>
            {
                EnterArea(to);
                var spot = up ? to.LinkHere : from.LinkInParent;
                player.Teleport(spot.transform.position, spot.transform.rotation);
                cam.Snap(player.transform);
                done?.Invoke();
            }));
        }

        // A scene change: fade to black (where you're going on the black), swap, fade back in.
        IEnumerator Fade(string label, Action swap)
        {
            Busy = true;
            player.Stop();
            ui.HideActions();
            yield return ui.FadeScreen(1, fadeSeconds, label);
            swap();
            yield return new WaitForSecondsRealtime(0.15f);
            yield return ui.FadeScreen(0, fadeSeconds);
            Busy = false;
        }

        public void Travel(string districtId, bool bySubway, Action arrived = null)
        {
            var dest = District(districtId);
            if (dest == null || Busy) return;
            int minutes = bySubway ? subwayMinutes : taxiMinutes;
            // By subway the screen shows the ride itself (SubwayRide) rather than going dark.
            var ride = bySubway ? SubwayRide.I : null;
            Action<bool> screen = null;
            if (ride != null) screen = on => { if (on) ride.Show(dest.DisplayName); else ride.Hide(); };
            StartCoroutine(Skip(minutes, bySubway ? $"Subway to {dest.DisplayName}" : $"Taxi to {dest.DisplayName}", false, () =>
            {
                EnterArea(dest);
                var spot = bySubway ? dest.SubwayExit : dest.TaxiStop;
                if (spot != null) player.Teleport(spot.transform.position, spot.transform.rotation);
                cam.Snap(player.transform);
                arrived?.Invoke();
            }, ride != null ? subwayRideSeconds : 0, screen, true));
        }

        // ---------- Eating & sleeping ----------

        public void FastForward(float gameMinutes, string label, Action done) => StartCoroutine(Skip(gameMinutes, label, true, done));

        // fullHours: a proper meal keeps hunger away for a while before it starts creeping back (cereal doesn't).
        public void Eat(string what, int minutes = 20, float fullHours = 0)
        {
            StartCoroutine(Skip(minutes, $"Eating {what}", true, () =>
            {
                Hunger = 0;
                _fullFor = Mathf.Max(_fullFor, fullHours * 60);
                if (Day == 1 && !Tutorial.Running) ui.OpenNotesAfterBreakfast();   // jot down a plan over breakfast
            }));
        }

        public void Sleep()
        {
            // Minutes keeps counting past midnight (e.g. 25:30), so next morning is always 24h + start.
            float toMorning = (24 + dayStartHour) * 60 - Minutes;
            StartCoroutine(Skip(toMorning, "Sleeping", true, () => BeginDay(Day + 1)));
        }

        // ---------- The office job: sit and type while time races, until 17:00 or until they tap ----------

        [Header("Office job")]
        [SerializeField] int officePayPerHour = 8;
        [Tooltip("Game hours of work per real second while typing.")]
        [SerializeField] float workHoursPerSecond = 0.5f;
        public bool AtDesk { get; private set; }
        bool _stopWork;

        public void WorkAtDesk(float endHour = 17f)
        {
            if (Busy || Minutes >= endHour * 60) { ui.Toast("Nobody's working now. Come back in the morning."); return; }
            StartCoroutine(Work(endHour));
        }

        public void StopWork() => _stopWork = true;

        IEnumerator Work(float endHour)
        {
            Busy = true;
            AtDesk = true;
            _stopWork = false;
            player.SetPose("Typing");
            float start = Minutes;
            ui.ShowSkip(true, "Working... (tap to stop)", true);
            _ffRate = workHoursPerSecond * 60f / MinutesPerRealSecond;
            Time.timeScale = Mathf.Clamp(_ffRate, 1f, 8f);
            while (Minutes < endHour * 60 && !_stopWork && !Failing) yield return null;
            _ffRate = 1;
            Time.timeScale = 1;
            ui.ShowSkip(false, "", true);
            AtDesk = false;
            Busy = false;
            int hours = Mathf.FloorToInt((Minutes - start) / 60f);
            if (hours > 0) Earn(hours * officePayPerHour, $"{hours} hours at the paper company");
            else ui.Toast("You stop before the hour's up - no pay for that.");
        }

        // Gone all day without eating: everything goes black for a few hours.
        void Faint()
        {
            ui.HideActions();
            ui.Toast("You haven't eaten all day... everything goes black.");
            StartCoroutine(Skip(faintHours * 60, "Unconscious", true, () =>
            {
                Hunger = 0.8f;                       // still starving: go and eat
                ui.Toast($"You come to {faintHours:0} hours later, starving. Eat something.");
            }));
        }

        void PassOut()
        {
            ui.Toast("You can't keep your eyes open...");
            Sleep();
        }

        // Fast-forward: game time races by over a couple of real seconds with the tape effect (or a fade for travel).
        // realSeconds: how long the skip takes (0 = the usual); screen: shows/hides something else in place of the
        // overlay (the subway ride).
        // fade: a trip to another scene (taxi, subway), faded out and in at both ends.
        IEnumerator Skip(float gameMinutes, string label, bool tape, Action done, float realSeconds = 0, Action<bool> screen = null,
                         bool fade = false)
        {
            Busy = true;
            player.Stop();
            ui.HideActions();
            if (fade) yield return ui.FadeScreen(1, fadeSeconds, "");
            if (screen != null) screen(true);
            else ui.ShowSkip(true, label, tape);
            if (fade) yield return ui.FadeScreen(0, fadeSeconds);
            yield return new WaitForSeconds(0.25f);
            _ffRate = gameMinutes / ((realSeconds > 0 ? realSeconds : fastForwardSeconds) * MinutesPerRealSecond);
            Time.timeScale = Mathf.Clamp(_ffRate, 1f, 8f);                   // the world visibly races along
            float target = Minutes + gameMinutes;
            while (Minutes < target) yield return null;
            _ffRate = 1;
            Time.timeScale = 1;
            if (fade) yield return ui.FadeScreen(1, fadeSeconds);
            done?.Invoke();
            if (fade) yield return new WaitForSecondsRealtime(0.1f);
            else yield return new WaitForSeconds(0.2f);
            if (screen != null) screen(false);
            else ui.ShowSkip(false, label, tape);
            if (fade) yield return ui.FadeScreen(0, fadeSeconds);
            Busy = false;
        }
    }
}
