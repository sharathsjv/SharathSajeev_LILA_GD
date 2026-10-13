using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // Someone in for lunch while the player works the bar shift. Walks in, sits, is ready to order (a "!" over
    // them), says what they want once (a second-long bubble), waits for it, drinks it, tips and goes.
    public class BarCustomer : MonoBehaviour
    {
        public enum State { Coming, Settling, Ready, Waiting, Drinking, Leaving }

        public State Now { get; private set; }
        public string Wants { get; private set; }
        public BarShift.Seat Seat { get; private set; }
        public bool AskedTwice { get; private set; }

        CharacterMover _mover;
        BarShift _shift;
        Vector3 _door;
        float _timer;            // counts down in each state
        float _orderedAt;

        const float ReadyPatience = 45f, WaitPatience = 75f;

        static readonly Dictionary<string, string[]> Asks = new Dictionary<string, string[]>
        {
            { "Beer", new[] { "A beer, please.", "Beer. Whatever's on tap.", "Just a cold beer." } },
            { "Whiskey", new[] { "Whiskey. Neat.", "A whiskey, please.", "Whiskey - make it a double. Kidding. Single." } },
            { "Soda", new[] { "Just a soda, thanks.", "A soda. I'm working.", "Soda, lots of ice." } },
            { "Wings", new[] { "Plate of wings!", "The wings, please.", "Wings. Extra hot." } },
        };

        public void Init(BarShift shift, BarShift.Seat seat, string wants, Vector3 door)
        {
            _shift = shift;
            Seat = seat;
            Wants = wants;
            _door = door;
            _mover = GetComponent<CharacterMover>();
            Now = State.Coming;
            _mover.WalkTo(seat.walkTo, SitDown);
        }

        void SitDown()
        {
            transform.SetPositionAndRotation(new Vector3(Seat.seat.x, transform.position.y, Seat.seat.z), Seat.facing);
            _mover.Seated = true;
            Now = State.Settling;
            _timer = Random.Range(2f, 5f);
        }

        void Update()
        {
            var g = Game.I;
            if (g == null || g.Busy || !g.ClockRunning) return;
            _timer -= Time.deltaTime;
            switch (Now)
            {
                case State.Settling:
                    if (_timer <= 0) { Now = State.Ready; _timer = ReadyPatience; }
                    break;
                case State.Ready:
                    if (_timer <= 0) { g.UI.Say(transform, "Forget it. Nobody's serving here."); Leave(false); }
                    break;
                case State.Waiting:
                    if (_timer <= 0) { g.UI.Say(transform, "I've waited long enough!"); Leave(false); }
                    break;
                case State.Drinking:
                    if (_timer <= 0) Leave(true);
                    break;
            }
        }

        // The player tapped them: over to take the order, hand over what they're carrying, or (costing the tip)
        // ask again.
        public void Tapped(PlayerController player, Vector2 screen)
        {
            var g = Game.I;
            if (!BarShift.Working) return;
            var holding = _shift.Holding;
            switch (Now)
            {
                case State.Ready:
                    player.GoThen(transform, false, TakeOrder);
                    break;
                case State.Waiting:
                    if (holding != null) { player.GoThen(transform, false, () => _shift.Give(this)); break; }
                    g.UI.ShowActions("Customer", new List<(string, System.Action)>
                    {
                        ("Ask what they wanted again (no tip)", () => player.GoThen(transform, false, AskAgain)),
                    }, screen);
                    break;
                case State.Coming:
                case State.Settling:
                    g.UI.Say(transform, "Give me a sec, I just sat down.", 1.5f);
                    break;
                default:
                    break;
            }
        }

        void TakeOrder()
        {
            if (Now != State.Ready) return;
            Face();
            var lines = Asks[Wants];
            Game.I.UI.Say(transform, lines[Random.Range(0, lines.Length)], 1f);
            Now = State.Waiting;
            _timer = WaitPatience;
            _orderedAt = Time.time;
        }

        void AskAgain()
        {
            if (Now != State.Waiting) return;
            Face();
            AskedTwice = true;
            Game.I.UI.Say(transform, $"I SAID: {Wants.ToLower()}. Weren't you listening?", 1f);
        }

        void Face() => Game.I.Player.Face(transform.position);

        public float WaitedSeconds => Time.time - _orderedAt;

        // Their order arrived.
        public void Served()
        {
            Now = State.Drinking;
            _timer = Random.Range(6f, 10f);
            _mover.SetPose(Wants == "Wings" ? "Sitting Idle" : "Sitting Drinking");
        }

        void Leave(bool happy)
        {
            Now = State.Leaving;
            _shift.Left(this, happy);
            _mover.Seated = false;
            _mover.ClearPose();
            _mover.WalkTo(_door, () => Destroy(gameObject));
        }

        // Shift over: off they go (or simply vanish if the player's gone).
        public void Dismiss(bool walk)
        {
            if (!walk) { Destroy(gameObject); return; }
            if (Now == State.Leaving) return;
            Now = State.Leaving;
            _mover.Seated = false;
            _mover.ClearPose();
            _mover.WalkTo(_door, () => Destroy(gameObject));
        }
    }
}
