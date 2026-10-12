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
        [SerializeField] float realSecondsPerDay = 600;
        [SerializeField] int dayStartHour = 7;
        [SerializeField] int dayEndHour = 24;
        [Tooltip("If the player hasn't slept by this hour (e.g. 26 = 2 AM) they pass out.")]
        [SerializeField] int passOutHour = 26;
        [SerializeField] int subwayMinutes = 12;
        [SerializeField] int taxiMinutes = 15;
        [Tooltip("Real seconds a fast-forward takes, however much game time it skips.")]
        [SerializeField] float fastForwardSeconds = 2.5f;

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

        void Update()
        {
            if (!ClockRunning) return;
            float dm = Time.deltaTime * MinutesPerRealSecond * _ffRate;
            Minutes += dm;
            Hunger = Mathf.Clamp01(Hunger + hungerPerHour * dm / 60f);
            Tiredness = Mathf.Clamp01(Tiredness + tirednessPerHour * dm / 60f);
            foreach (var p in People) p.Tick(Minutes, MinutesPerRealSecond, Current.Id, dm);
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
                day = Day, minutes = Minutes, hunger = Hunger, tiredness = Tiredness,
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

        IEnumerator Fade(string label, Action swap)
        {
            Busy = true;
            player.Stop();
            ui.ShowSkip(true, label, false);
            yield return new WaitForSeconds(0.3f);
            swap();
            yield return new WaitForSeconds(0.25f);
            ui.ShowSkip(false, label, false);
            Busy = false;
        }

        public void Travel(string districtId, bool bySubway, Action arrived = null)
        {
            var dest = District(districtId);
            if (dest == null || Busy) return;
            int minutes = bySubway ? subwayMinutes : taxiMinutes;
            StartCoroutine(Skip(minutes, bySubway ? $"Subway to {dest.DisplayName}" : $"Taxi to {dest.DisplayName}", false, () =>
            {
                EnterArea(dest);
                var spot = bySubway ? dest.SubwayExit : dest.TaxiStop;
                if (spot != null) player.Teleport(spot.transform.position, spot.transform.rotation);
                cam.Snap(player.transform);
                arrived?.Invoke();
            }));
        }

        // ---------- Eating & sleeping ----------

        public void FastForward(float gameMinutes, string label, Action done) => StartCoroutine(Skip(gameMinutes, label, true, done));

        public void Eat(string what, int minutes = 20)
        {
            StartCoroutine(Skip(minutes, $"Eating {what}", true, () =>
            {
                Hunger = 0;
                if (Day == 1 && !Tutorial.Running) ui.OpenNotesAfterBreakfast();   // jot down a plan over breakfast
            }));
        }

        public void Sleep()
        {
            // Minutes keeps counting past midnight (e.g. 25:30), so next morning is always 24h + start.
            float toMorning = (24 + dayStartHour) * 60 - Minutes;
            StartCoroutine(Skip(toMorning, "Sleeping", true, () => BeginDay(Day + 1)));
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
        IEnumerator Skip(float gameMinutes, string label, bool tape, Action done)
        {
            Busy = true;
            player.Stop();
            ui.ShowSkip(true, label, tape);
            yield return new WaitForSeconds(0.25f);
            _ffRate = gameMinutes / (fastForwardSeconds * MinutesPerRealSecond);
            float target = Minutes + gameMinutes;
            while (Minutes < target) yield return null;
            _ffRate = 1;
            done?.Invoke();
            yield return new WaitForSeconds(0.2f);
            ui.ShowSkip(false, label, tape);
            Busy = false;
        }
    }
}
