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
        float MinutesPerRealSecond => (dayEndHour - dayStartHour) * 60f / realSecondsPerDay;

        float _ffRate = 1;

        public event Action<int> DayStarted;

        void Awake()
        {
            I = this;
            StreetLife.Density = streetDensity;
            People = FindObjectsByType<RoutineCharacter>(FindObjectsInactive.Include);
            Areas = FindObjectsByType<Area>(FindObjectsInactive.Include);
            foreach (var a in Areas) a.Leave();
        }

        void Start()
        {
            EnterArea(District(startDistrict));
            BeginDay(1);
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

        public void Eat(string what, int minutes = 20)
        {
            StartCoroutine(Skip(minutes, $"Eating {what}", true, () =>
            {
                Hunger = 0;
                if (Day == 1) ui.OpenNotesAfterBreakfast();   // jot down a plan over breakfast
            }));
        }

        public void Sleep()
        {
            // Minutes keeps counting past midnight (e.g. 25:30), so next morning is always 24h + start.
            float toMorning = (24 + dayStartHour) * 60 - Minutes;
            StartCoroutine(Skip(toMorning, "Sleeping", true, () => BeginDay(Day + 1)));
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
