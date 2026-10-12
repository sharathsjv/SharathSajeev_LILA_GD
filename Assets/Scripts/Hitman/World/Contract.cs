using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // The job on Kevin Malone: the openings in his routine (numbered in the design doc as elimination situations),
    // how each one ends him, the tip you can buy, and getting paid once his name is in the paper.
    // Post elimination: the story runs the day after the incident if someone reports it anonymously (a payphone),
    // or 4 days after if nobody does. The client reads it and wires the money by email.
    //  1  Breakfast at the Corner Bar: poison his food (he leaves it while he's in the washroom).
    //  2  The office men's room: soak the floor with the mop from the utilities cupboard; drunk, he slips.
    //  3  The break room coffee machine: poison the pot (whoever drinks it dies).
    //  4  Lunch at the Corner Bar: take a part-time job there and serve him, or catch his food unattended.
    //  5  His house: gas on and every window shut while he sleeps, or take his delivery, poison it, leave it at the door.
    // Being seen poisoning anything fails the mission (back to the last checkpoint).
    public class Contract : MonoBehaviour
    {
        public static Contract I { get; private set; }

        [SerializeField] Assignment assignment;
        [SerializeField] RoutineCharacter target;
        [Tooltip("People who also drink from the office coffee machine.")]
        [SerializeField] RoutineCharacter[] coffeeDrinkers;
        [Header("Payment")]
        [SerializeField] int fullPay = 500;
        [SerializeField] int latePay = 100;
        [SerializeField] int tipPrice = 50;
        [SerializeField] int tipDelayMinutes = 120;
        [SerializeField] int shiftPay = 20;
        [Tooltip("Days until the paper finds out on its own when nobody reports the incident.")]
        [SerializeField] int unreportedDelay = 4;
        [Header("Places")]
        [SerializeField] string barArea = "business.cornerbar";
        [SerializeField] string barStool = "business.cornerbar.stool1";
        [SerializeField] string officeUrinal = "business.tower1.floor5.urinal";
        [SerializeField] string officeCoffee = "business.tower1.floor5.coffee";
        [SerializeField] string[] houseAreas = { "suburbs.house4", "suburbs.house4.up" };
        [Tooltip("Lunch shift at the bar (24h).")]
        [SerializeField] Vector2 shift = new Vector2(12.5f, 14f);

        public RoutineCharacter Target => target;
        public Assignment Assignment => assignment;
        public int TipPrice => tipPrice;

        Transform _plate;
        int _lastStep = -1;
        float _gasClock, _playerGas;
        readonly List<Setup> _windows = new List<Setup>();
        readonly List<Setup> _cards = new List<Setup>();
        [Tooltip("When the office worker's keycard is left on the coffee shop table (24h).")]
        [SerializeField] Vector2 cardLeftOut = new Vector2(8.5f, 11f);

        void Awake() => I = this;

        void Start()
        {
            var g = Game.I;
            g.DayStarted += OnDayStarted;
            g.Restored += ApplyWorld;
            g.Restored += () => { _lastStep = -1; _gasClock = _playerGas = 0; };
            foreach (var s in FindObjectsByType<Setup>(FindObjectsInactive.Include))
            {
                if (s.Type == Setup.Kind.ForgottenCard) _cards.Add(s);
                if (s.Type == Setup.Kind.Window)
                {
                    _windows.Add(s);
                    if (s.StartsOpen && !g.S.Has("windowinit." + s.name)) { g.S.Set("windowinit." + s.name); g.S.Set("open." + s.name); }
                }
            }
            MakePlate();
            ApplyWorld();
        }

        string Today(string what) => $"{what}.{Game.I.Day}";
        bool Dead => Game.I.S.Has("target.dead");

        // ---------- Time ----------

        void Update()
        {
            var g = Game.I;
            if (g == null || !g.ClockRunning || target == null) return;
            var st = g.S;

            // The tip arrives.
            float tipAt = st.Get("tip.at", -1);
            if (tipAt >= 0 && Abs(g) >= tipAt)
            {
                st.Put("tip.at", -1);
                st.Put("tip.received", Abs(g));
                st.Set("tip.arrived");
                g.UI.Toast("New email: your tip.");
                g.Note("tip.breakfast", "Tip: he has a few drinks with his breakfast before he reports to work.", NoteKind.Tip);
            }

            // Someone forgets their keycard on the coffee shop table every morning (until you take it once).
            bool cardOut = !st.Has("card.taken") && Hours(g) >= cardLeftOut.x && Hours(g) < cardLeftOut.y;
            foreach (var c in _cards) if (c.gameObject.activeSelf != cardOut) c.gameObject.SetActive(cardOut);

            if (Dead) { UpdatePlate(false); return; }

            // Scheduled death (poison takes a while).
            float deathAt = st.Get("death.at", -1);
            if (deathAt >= 0 && Abs(g) >= deathAt) Kill(st.Has("cause.slip") ? "slip" : "poison");

            var step = target.Step;
            bool settled = target.Settled;

            // Finishing a step counts as having done it (eaten, drunk the coffee, used the urinal), even if he never
            // quite sat down (e.g. off-screen, or the clock racing during a fast-forward).
            if (target.StepIndex != _lastStep)
            {
                var prev = _lastStep >= 0 && _lastStep < target.Routine.steps.Length ? target.Routine.steps[_lastStep].place : null;
                _lastStep = target.StepIndex;
                if (deathAt < 0 && prev == barStool && st.Has(Today("plate.poisoned"))) Schedule(25, "poison");
                else if (deathAt < 0 && prev == officeCoffee && st.Has(Today("coffee.poisoned"))) Schedule(15, "poison");
                else if (prev == officeUrinal && st.Has(Today("floor.wet"))) { st.Set("cause.slip"); Kill("slip"); return; }
                deathAt = st.Get("death.at", -1);
            }

            // 1 & 4: food at the bar.
            bool eating = settled && target.CurrentPlaceId == barStool;
            // The plate stays on the counter while he's in the bar (including his trip to the washroom).
            UpdatePlate(step != null && target.AreaId == barArea && ((step.place ?? "") == barStool || (step.place ?? "").StartsWith(barArea + ".washroom")));
            if (eating && st.Has(Today("plate.poisoned")) && deathAt < 0) Schedule(30, "poison");

            // 2: the wet floor.
            if (settled && target.CurrentPlaceId == officeUrinal && st.Has(Today("floor.wet")) && deathAt < 0)
            {
                st.Set("cause.slip");
                Kill("slip");
            }

            // 3: the coffee.
            if (st.Has(Today("coffee.poisoned")))
            {
                if (settled && target.CurrentPlaceId == officeCoffee && deathAt < 0) Schedule(15, "poison");
                foreach (var c in coffeeDrinkers)
                    if (c != null && c.Alive && c.Settled && c.CurrentPlaceId != null && c.CurrentPlaceId.Contains("coffee") && !st.Has("victim." + c.name))
                    {
                        st.Set("victim." + c.name);
                        c.Die("poison");
                        st.Put("victims", st.Get("victims") + 1);
                    }
            }

            // 5a: the poisoned takeout at his door.
            if (st.Has(Today("takeout.left")) && InHouse(target) && target.IsAwake && deathAt < 0) Schedule(40, "poison");

            // 5b: gas, windows shut, him asleep.
            bool gas = st.Has("gas.on");
            bool sealedUp = gas && AllWindowsShut();
            if (sealedUp && InHouse(target) && !target.IsAwake) _gasClock += Dt(g);
            else _gasClock = 0;
            if (_gasClock > 45) { st.Set("cause.gas"); Kill("gas"); }
            if (sealedUp && System.Array.IndexOf(houseAreas, g.Current.Id) >= 0) _playerGas += Dt(g);
            else _playerGas = 0;
            if (_playerGas > 20) { g.Fail("You stayed in the house with the gas on and passed out."); _playerGas = 0; }

            // Getting paid for the lunch shift.
            if (st.Has(Today("job.served")) && !st.Has(Today("job.paid")) && Hours(g) >= shift.y)
            {
                st.Set(Today("job.paid"));
                g.Earn(shiftPay, "Lunch shift at the Corner Bar");
            }
        }

        static float Abs(Game g) => (g.Day - 1) * 24 * 60 + g.Minutes;
        static float Hours(Game g) => g.Minutes / 60f;
        static float Dt(Game g) => Time.deltaTime * g.MinutesPerRealSecondNow;
        bool InHouse(RoutineCharacter c) => System.Array.IndexOf(houseAreas, c.AreaId) >= 0;
        bool AllWindowsShut() { foreach (var w in _windows) if (Game.I.S.Has("open." + w.name)) return false; return true; }

        void Schedule(float minutes, string cause)
        {
            var g = Game.I;
            g.S.Put("death.at", Abs(g) + minutes);
            g.S.Set("cause." + cause);
        }

        void Kill(string cause)
        {
            var g = Game.I;
            g.S.Put("death.at", -1);
            g.S.Set("target.dead");
            g.S.Put("death.day", g.Day);
            g.S.Put("death.abs", Abs(g));
            g.S.Set("cause." + cause);
            target.Die(cause);
            if (g.Current.Id == target.AreaId) g.UI.Toast($"{target.Routine.displayName} collapsed.");
            g.Changed();
        }

        // ---------- New day: the paper, getting paid ----------

        void OnDayStarted(int day)
        {
            var g = Game.I;
            var st = g.S;
            // The story is in today's paper: the client wires the money (by email).
            if (StoryOut && !st.Has("paid"))
            {
                st.Set("paid");
                bool onTime = StoryDay <= 2;            // "if I see his name on the paper tomorrow"
                int pay = onTime ? fullPay : latePay;
                st.Put("paid.amount", pay);
                st.Set("contract.done");
                st.cash += pay;
                g.Changed();
                g.UI.Toast("New email.");
            }
            // The washed coffee pot, a dry floor, nobody waiting on a delivery.
            ApplyWorld();
        }

        // ---------- Post elimination ----------

        // Something's been set in motion (or he's already dead): now it can be reported.
        public bool Initiated
        {
            get
            {
                var st = Game.I.S;
                if (st.Has("target.dead") || st.Has("gas.on")) return true;
                foreach (var f in st.flags)
                    if (f.StartsWith("plate.poisoned.") || f.StartsWith("coffee.poisoned.") || f.StartsWith("floor.wet.") || f.StartsWith("takeout.left."))
                        return true;
                return false;
            }
        }

        public bool Reported => Game.I.S.Has("reported");

        // Anonymous call to the police from a payphone.
        public void Report()
        {
            var g = Game.I;
            if (Reported) return;
            g.S.Set("reported");
            g.S.Put("report.day", g.Day);
            g.Changed();
        }

        // The day his name is in the paper (or a long way off if he isn't dead).
        public int StoryDay
        {
            get
            {
                var st = Game.I.S;
                if (!st.Has("target.dead")) return int.MaxValue;
                int died = Mathf.RoundToInt(st.Get("death.day"));
                return Reported ? Mathf.Max(died, Mathf.RoundToInt(st.Get("report.day"))) + 1 : died + unreportedDelay;
            }
        }

        public bool StoryOut => Game.I.Day >= StoryDay;

        // ---------- What you can do ----------

        public void AddActions(Setup s, ActionMenu m)
        {
            var g = Game.I;
            var st = g.S;
            bool poison = st.Holding("poison");
            switch (s.Type)
            {
                case Setup.Kind.BarPlate:
                {
                    if (Dead) break;
                    bool lunch = Hours(g) >= shift.x && Hours(g) < shift.y;
                    if (lunch && st.Has(Today("job")) && !st.Has(Today("job.served")))
                    {
                        m.AddWalk("Serve his lunch", () => Serve(false));
                        if (poison) m.AddWalk("Serve his lunch (add the rat poison)", () => Serve(true));
                    }
                    else if (poison && !st.Has(Today("plate.poisoned")))
                        m.AddWalk("Poison his food", () => Poison("plate.poisoned", "his food", s.transform));
                    else if (!poison) m.Add("(You'd need something to put in it)", () => { });
                    break;
                }
                case Setup.Kind.Bartender:
                    if (!st.Has(Today("job")) && Hours(g) < shift.y)
                        m.Add("Ask about part-time work", () => g.UI.ShowDialog("Bartender",
                            "Part-time? I could use someone on the lunch rush. 12:30 till 2, twenty bucks.",
                            new List<(string, System.Action)>
                            {
                                ("I'll take it.", () => { st.Set(Today("job")); g.Note("bar.job", "Got a lunch shift at the Corner Bar (12:30-14:00). Kevin eats lunch there."); }),
                                ("Maybe another time.", null),
                            }, s.transform));
                    m.AddWalk("Buy a beer ($3)", () => { if (g.Spend(3)) g.UI.Toast("You nurse a beer at the bar."); });
                    break;
                case Setup.Kind.MopBucket:
                    if (!st.Holding("mop")) m.AddWalk("Take the mop", () => { Act("Picking Up Object"); g.Give("mop"); });
                    break;
                case Setup.Kind.MensRoomFloor:
                    if (st.Holding("mop") && !st.Has(Today("floor.wet")))
                        m.AddWalk("Soak the floor with the mop", () =>
                        {
                            st.Set(Today("floor.wet"));
                            Act("Kneeling Inspecting");
                            g.Take("mop");
                            g.UI.Toast("The tiles are like an ice rink. Anyone a bit drunk is going down.");
                        });
                    else if (!st.Holding("mop")) m.Add("(A wet floor would be dangerous in here...)", () => { });
                    m.Add("Use the urinal", () => { });
                    break;
                case Setup.Kind.CoffeeMachine:
                    m.AddWalk("Get a coffee", () => { Act("Drinking"); g.UI.Toast("Burnt, but hot."); });
                    if (poison && !st.Has(Today("coffee.poisoned")))
                        m.AddWalk("Poison the coffee pot", () => Poison("coffee.poisoned", "the coffee pot", s.transform));
                    break;
                case Setup.Kind.PlantPot:
                    if (!st.Has("key.found")) m.AddWalk("Look under the plant pot", () =>
                    {
                        Act("Kneeling Inspecting");
                        st.Set("key.found");
                        g.Give("housekey");
                        g.Note("house.key", "Kevin keeps a spare key under the plant pot by his front door.");
                    });
                    break;
                case Setup.Kind.KevinsDoor:
                    if (st.Holding("takeout.poisoned"))
                        m.AddWalk("Leave the takeout at the door and knock", () =>
                        {
                            g.Take("takeout.poisoned");
                            Act("Putting Down");
                            st.Set(Today("takeout.left"));
                            g.UI.Toast("You knock and walk away. Dinner is served.");
                        });
                    else if (st.Holding("takeout"))
                        m.AddWalk("Leave the takeout at the door and knock", () => { g.Take("takeout"); g.UI.Toast("Delivered. Nice of you."); });
                    break;
                case Setup.Kind.Stove:
                    bool on = st.Has("gas.on");
                    m.AddWalk(on ? "Turn the gas off" : "Turn the gas on (don't light it)", () =>
                    {
                        Act("Opening");
                        st.Set("gas.on", !on);
                        g.UI.Toast(on ? "Gas off." : "Hissss. Better shut the windows - and get out.");
                    });
                    break;
                case Setup.Kind.Window:
                    bool open = st.Has("open." + s.name);
                    m.AddWalk(open ? "Close the window" : "Open the window", () =>
                    {
                        Act("Opening");
                        st.Set("open." + s.name, !open);
                        ApplyWorld();
                    });
                    break;
                case Setup.Kind.DeliveryPerson:
                    if (!st.Has(Today("takeout.taken")) && !Dead)
                        m.Add("\"That's mine - I'm Kevin Malone.\"", () => g.UI.ShowDialog("Delivery", "Kevin Malone? Large chili, extra cheese, six beers. It's paid for.",
                            new List<(string, System.Action)>
                            {
                                ("\"Thanks, I'm starving.\"", () => { if (g.Give("takeout")) st.Set(Today("takeout.taken")); }),
                                ("\"Actually, never mind.\"", null),
                            }, s.transform));
                    break;
                case Setup.Kind.ForgottenCard:
                    if (!st.Has("card.taken")) m.AddWalk("Pocket the keycard", () => { Act("Picking Up Object"); if (g.Give("keycard")) { st.Set("card.taken"); ApplyWorld(); } });
                    break;
                case Setup.Kind.Receptionist:
                    m.Add("Ask about the job ad", () => g.UI.ShowDialog("Receptionist",
                        g.S.Has($"pass.{g.Day}") ? "The manager's running late. Have a seat. Help yourself to coffee in the break room."
                                                 : "Do you have an appointment?", new List<(string, System.Action)> { ("OK", null) }, s.transform));
                    break;
            }
        }

        // Pockets: things you can do with what you're carrying.
        public void AddPocketActions(string item, List<(string, System.Action)> list)
        {
            var g = Game.I;
            if (item == "takeout" && g.S.Holding("poison"))
                list.Add(("Mix the rat poison in", () =>
                {
                    var w = Watcher.Seeing(g.Player.transform.position);
                    if (w != null) { Suspicion.I.Spot(w, () => g.Fail($"{w.DisplayName} saw you poisoning food.")); return; }
                    g.Take("poison");
                    g.Take("takeout");
                    g.Give("takeout.poisoned", true);
                    g.UI.Toast("Extra cheese, extra rat poison.");
                }));
            if (item == "newspaper") list.Add(("Read it", () => g.UI.ShowNewspaper()));
        }

        void Poison(string flag, string what, Transform at)
        {
            var g = Game.I;
            var seen = Watcher.Seeing(g.Player.transform.position);
            if (seen != null) { Suspicion.I.Spot(seen, () => g.Fail($"{seen.DisplayName} saw you put something in {what}.")); return; }
            g.Take("poison");
            g.S.Set(Today(flag));
            Act("Putting Down");
            g.UI.Toast($"You tip the rat poison into {what}. Nobody saw.");
        }

        static void Act(string clip) => Game.I.Player.GetComponent<PlayerController>().Act(clip);

        void Serve(bool poisoned)
        {
            var g = Game.I;
            Act("Putting Down");
            g.S.Set(Today("job.served"));
            if (poisoned)
            {
                g.Take("poison");
                g.S.Set(Today("plate.poisoned"));
                g.UI.Toast("\"Enjoy your lunch, sir.\"");
            }
            else g.UI.Toast("Lunch served.");
        }

        // ---------- The world showing what's been done ----------

        void ApplyWorld()
        {
            var st = Game.I.S;
            foreach (var w in _windows) if (w.Pane != null) w.Pane.enabled = !st.Has("open." + w.name);

        }

        // His plate on the bar counter, there while he's at the bar.
        void MakePlate()
        {
            var stool = Place.Find(barStool);
            if (stool == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Kevin's breakfast";
            go.transform.SetParent(stool.transform.parent, true);
            go.transform.position = stool.transform.position + stool.transform.forward * 0.55f + Vector3.up * 1.14f;
            go.transform.localScale = new Vector3(0.28f, 0.012f, 0.28f);
            var mr = go.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.92f, 0.9f, 0.85f) };
            mr.sharedMaterial = mat;
            var col = go.GetComponent<Collider>();
            Destroy(col);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(1.4f, 30f, 1.4f);
            go.AddComponent<Setup>().Init(Setup.Kind.BarPlate);
            // the beer next to it
            var beer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(beer.GetComponent<Collider>());
            beer.transform.SetParent(go.transform, false);
            beer.transform.localPosition = new Vector3(0.9f, 10f, 0.4f);
            beer.transform.localScale = new Vector3(0.25f, 9f, 0.25f);
            beer.GetComponent<MeshRenderer>().sharedMaterial = new Material(mat) { color = new Color(0.85f, 0.55f, 0.12f) };
            _plate = go.transform;
            _plate.gameObject.SetActive(false);
        }

        void UpdatePlate(bool on)
        {
            if (_plate != null && _plate.gameObject.activeSelf != on) _plate.gameObject.SetActive(on);
        }

        // ---------- Taking the job: the to-do list ----------

        public bool Accepted => Game.I.S.Has("job.accepted");

        public void Accept()
        {
            var g = Game.I;
            if (Accepted) return;
            g.S.Set("job.accepted");
            g.Changed();
            g.UI.Toast("Job accepted. Your to-do list is in the notebook.");
        }

        public class Objective
        {
            public string text, done;   // done: what's shown after it (in brackets) once it's struck out
            public bool complete;
        }

        // What the player set out to do, struck out as they get there.
        public List<Objective> Objectives()
        {
            var st = Game.I.S;
            var list = new List<Objective>();
            if (!Accepted) return list;
            string who = Assignment != null && Assignment.target != null ? Assignment.target.displayName : "the target";
            bool info = st.notes.Exists(n => n.kind == NoteKind.Routine || n.kind == NoteKind.Info || n.kind == NoteKind.Tip);
            bool ways = st.notes.Exists(n => n.opening > 0) || Initiated;
            list.Add(new Objective { text = $"Find info about {who}.", complete = info, done = "You can always keep finding info" });
            list.Add(new Objective { text = $"Find ways to eliminate {who}.", complete = ways });
            list.Add(new Objective { text = "Wait for the news to be official.", complete = StoryOut });
            return list;
        }

        // ---------- Tip ----------

        public void BuyTip()
        {
            var g = Game.I;
            if (g.S.Has("tip.bought")) return;
            if (!g.Spend(tipPrice)) return;
            g.S.Set("tip.bought");
            g.S.Put("tip.at", Abs(g) + tipDelayMinutes);
            g.UI.Toast("Tip paid for. It'll come through in a couple of hours.");
        }

        // ---------- The newspaper ----------

        public class Article
        {
            public string headline, body, noteId, note;
            public int page;          // 0 front page, 1 local news, 2 classifieds
        }

        public List<Article> Paper()
        {
            var g = Game.I;
            var st = g.S;
            var list = new List<Article>();
            if (StoryOut)
            {
                string how = st.Has("cause.slip") ? "slipped on a wet bathroom floor at Schrute Towers and hit his head"
                           : st.Has("cause.gas") ? "was found dead at his Kensington home after a gas leak"
                           : "died of what police believe was poisoning";
                int victims = Mathf.RoundToInt(st.Get("victims"));
                list.Add(new Article
                {
                    headline = "LOCAL ACCOUNTANT KEVIN MALONE DEAD",
                    body = $"Kevin Malone, 41, an accountant at a paper company in Schrute Towers, {how}." +
                           (victims > 0 ? $" {victims} of his co-workers also died; investigators are looking at the office coffee machine." : " Police say there are no suspects.") +
                           (Reported ? " The incident was reported by an anonymous caller." : " He had not been seen for days."),
                });
            }
            else list.Add(new Article
            {
                headline = "SUBWAY FARES UP AGAIN",
                body = "Commuters on the Shackleton line will pay a nickel more from next month. \"Same trains, same smell,\" said one rider.",
            });
            list.Add(new Article
            {
                page = 1,
                headline = "Corner Bar voted 'Best Breakfast Beer' third year running",
                body = "Regulars from the towers start the day there. \"Some of them are in before nine, every single day,\" says the owner.",
                noteId = "paper.cornerbar", note = "The Corner Bar (Business District) is full of office workers drinking before nine.",
            });
            list.Add(new Article
            {
                page = 1,
                headline = "Gas safety: council reminds residents",
                body = "An unlit gas ring in a closed-up house can be deadly within the hour. Keep a window open.",
            });
            list.Add(new Article
            {
                page = 2,
                headline = "NOW HIRING - Paper company, Schrute Towers (5th floor)",
                body = "Junior sales associate. Walk-in interviews all week. Bring this ad to security at the front desk.",
                noteId = "jobad", note = "Job ad in the paper: walk-in interviews at the paper company, Schrute Towers 5th floor. Show the ad (bring the newspaper) to security.",
            });
            list.Add(new Article
            {
                page = 2,
                headline = "Kensington Takeout - we deliver!",
                body = "Chili, burgers, beer. Delivery every evening across the Suburbs. Call 555-0199.",
            });
            list.Add(new Article
            {
                page = 2,
                headline = "Shop n Save, Shackleton Ave",
                body = "Groceries, newspapers, pest control. Open 7 days.",
            });
            return list;
        }

        // Tapping something worth remembering in the paper.
        public void NoteArticle(Article a)
        {
            if (a.noteId == null) return;
            if (a.noteId == "jobad") Game.I.S.Set("jobad");
            Game.I.Note(a.noteId, a.note);
        }

        // ---------- CrewSpace: people's Pings ----------

        public class Ping
        {
            public string name, handle, text, noteId, note;
            public float at;          // absolute game minutes
            public Color colour;
        }

        struct Poster { public string name, handle; public Color colour; }
        static readonly Poster KevinP = new Poster { name = "Kevin Malone", handle = "kevin_m", colour = new Color(0.85f, 0.45f, 0.15f) };
        static readonly Poster PamP = new Poster { name = "Pam B.", handle = "pam_draws", colour = new Color(0.75f, 0.3f, 0.55f) };
        static readonly Poster OscarP = new Poster { name = "Oscar", handle = "oscar_accounts", colour = new Color(0.25f, 0.5f, 0.35f) };
        static readonly Poster[] Locals =
        {
            new Poster { name = "Shackleton Sal", handle = "sal_shk", colour = new Color(0.4f, 0.4f, 0.7f) },
            new Poster { name = "Marge", handle = "marge_kensington", colour = new Color(0.6f, 0.55f, 0.2f) },
            new Poster { name = "Dee", handle = "dee_does_doubles", colour = new Color(0.5f, 0.15f, 0.15f) },
            new Poster { name = "Rita", handle = "rita_rides", colour = new Color(0.2f, 0.55f, 0.7f) },
            new Poster { name = "Gus", handle = "gus_on_5th", colour = new Color(0.45f, 0.3f, 0.2f) },
        };
        static readonly string[] Chatter =
        {
            "Subway was 20 minutes late again. Love this city.", "Who keeps stealing the newspaper from the machine on Shackleton?",
            "Happy hour at the Corner Bar starts at 5. Wings half price.", "My landlord says the heating is 'a state of mind'.",
            "Saw a guy in a suit buy rat poison at the Shop n Save. Normal Tuesday.", "Taxi driver took the long way. Again.",
            "The mall fountain has more coins in it than my bank account.", "Raining. Obviously.", "Barber on Shackleton does a great cut for five bucks.",
            "Kensington Takeout chili is a religious experience.", "Schrute Towers security guard looked at me like I owed him money.",
        };

        public List<Ping> Pings()
        {
            var g = Game.I;
            var st = g.S;
            float now = Abs(g);
            float deadAt = st.Has("target.dead") ? st.Get("death.abs", now) : float.MaxValue;
            var list = new List<Ping>();
            void Add(Poster p, int day, float minute, string text, string noteId = null, string note = null)
            {
                float at = (day - 1) * 1440 + minute;
                if (at > now) return;
                list.Add(new Ping { name = p.name, handle = p.handle, colour = p.colour, at = at, text = text, noteId = noteId, note = note });
            }
            for (int d = Mathf.Max(1, g.Day - 1); d <= g.Day; d++)
            {
                float day0 = (d - 1) * 1440;
                // Kevin, while he's around to ping
                void K(float minute, string text, string noteId = null, string note = null) { if (day0 + minute < deadAt) Add(KevinP, d, minute, text, noteId, note); }
                K(8 * 60 + 40, d % 2 == 1 ? "Shower, then beer. In that order. Mostly." : "Monday again? Feels like a Monday.");
                K(9 * 60 + 6, "Beer and eggs at the Corner Bar. Breakfast of champions. #CornerBar", "ping.breakfast",
                  "Kevin pings that he has beer and eggs at the Corner Bar every morning around 9.");
                K(11 * 60 + 34, "Fell asleep at my desk. Oscar woke me up by dropping a stapler. Rude.", "ping.asleep",
                  "Kevin falls asleep at his desk late in the morning (Oscar wakes him up).");
                K(13 * 60 + 4, "Lunch at the Corner Bar w/ Oscar. 4 beers, he had water. Who raised him?", "ping.lunch",
                  "Kevin has lunch at the Corner Bar around 13:00 - four beers.");
                K(15 * 60 + 33, "Office coffee tastes like the break room smells. Still drinking it.", "ping.coffee",
                  "Kevin drinks the break room coffee every afternoon around 15:30.");
                K(19 * 60 + 12, "Large chili from Kensington Takeout. They don't even ask for my order anymore.", "ping.takeout",
                  "Kevin gets chili delivered from Kensington Takeout every evening around 19:00.");
                K(22 * 60 + 25, "Couch. Beer. TV. Don't wake me.", "ping.couch", "Kevin passes out on his couch around 22:30.");
                // Pam and her keycard (the forgotten card event)
                if (!st.Has("card.taken"))
                    Add(PamP, d, 8 * 60 + 52, "Left my Schrute Towers keycard on a table at the coffee shop AGAIN. Every. Single. Morning. #help",
                        "ping.keycard", "Someone (Pam) keeps leaving her Schrute Towers keycard on a table at the Business District coffee shop in the mornings.");
                else Add(PamP, d, 9 * 60 + 30, "Lost my keycard for good this time. Security is NOT happy with me.");
                if (day0 + 11 * 60 + 31 < deadAt) Add(OscarP, d, 11 * 60 + 31, "Woke Kevin up. Again.");
                if (day0 + 15 * 60 + 36 < deadAt) Add(OscarP, d, 15 * 60 + 36, "Someone needs to clean the coffee machine on 5.");
                // the locals
                var rng = new System.Random(d * 977);
                for (float m = 7 * 60 + 20; m < 23 * 60; m += 70 + rng.Next(0, 60))
                    Add(Locals[rng.Next(Locals.Length)], d, m, Chatter[rng.Next(Chatter.Length)]);
            }
            if (StoryOut)
            {
                int sd = StoryDay;
                Add(OscarP, sd, 8 * 60 + 15, "Can't believe it about Kevin. He made the best chili. RIP buddy.");
                Add(Locals[1], sd, 8 * 60 + 40, "Did you see the paper this morning? That accountant from Kensington. Awful.");
            }
            list.Sort((a, b) => b.at.CompareTo(a.at));        // newest first
            return list;
        }

        public void NotePing(Ping p)
        {
            if (p.noteId != null) Game.I.Note(p.noteId, p.note);
        }

        public static string PingTime(float at)
        {
            var g = Game.I;
            float ago = Abs(g) - at;
            if (ago < 60) return $"{Mathf.Max(1, Mathf.RoundToInt(ago))}m";
            if (ago < 1440) return $"{Mathf.FloorToInt(ago / 60)}h";
            return "yesterday";
        }

        // ---------- Searching online ----------

        public List<Article> Search(string query)
        {
            var q = (query ?? "").ToLowerInvariant();
            var list = new List<Article>();
            if (q.Contains("kevin") || q.Contains("malone"))
            {
                list.Add(new Article
                {
                    headline = "Kevin Malone - Accountant - Paper company, Schrute Towers",
                    body = "Accounts. 12 years. Fun fact: makes the best chili in town.",
                    noteId = "web.work", note = "Kevin works in accounts at the paper company, Schrute Towers (5th floor).",
                });
                list.Add(new Article
                {
                    headline = "Review of the Corner Bar by kevinM: ★★★★★",
                    body = "\"Beer AND eggs at 9am. Best way to start a work day. Come back for lunch too.\"",
                    noteId = "web.review", note = "Kevin's online review: he has beer and eggs at the Corner Bar at 9 and goes back for lunch.",
                });
                list.Add(new Article
                {
                    headline = "Kensington Takeout - order history (public wishlist)",
                    body = "kevinM: Large chili, extra cheese + six-pack, every night ~7:15pm.",
                    noteId = "web.takeout", note = "Kevin gets chili delivered to his house every evening around 19:15.",
                });
            }
            else if (q.Contains("schrute") || q.Contains("paper"))
                list.Add(new Article { headline = "Schrute Towers - visitor information", body = "All visitors must show a keycard at security. Business attire expected." ,
                    noteId = "web.schrute", note = "Schrute Towers security lets you in with a keycard - or if you look the part (business attire)." });
            else if (q.Contains("poison"))
                list.Add(new Article { headline = "Shop n Save - pest control aisle", body = "Rat poison, $6. Shackleton Ave branch." });
            else
                list.Add(new Article { headline = "No results", body = "Try a name." });
            return list;
        }

        // ---------- Ideas for highlighted routine notes ----------

        public static string Idea(int opening)
        {
            switch (opening)
            {
                case 1: return "He leaves his breakfast on the counter when he goes to the washroom. Rat poison from the Shop n Save, a moment when the bartender isn't looking...";
                case 2: return "He's drunk by the time he goes to the men's room. The utilities cupboard next door has a mop and bucket. A soaking wet floor...";
                case 3: return "The break room coffee machine. Everyone drinks from that pot, though - it won't only be him.";
                case 4: return "The bar's short-staffed at lunch. If you worked there, you'd be the one carrying his plate. Or wait for him to leave it.";
                case 5: return "He orders chili to the house every night - the delivery guy has never seen his face. Or: he passes out at home... the gas stove, the windows. The spare key has to be somewhere near the door.";
                default: return null;
            }
        }
    }
}
