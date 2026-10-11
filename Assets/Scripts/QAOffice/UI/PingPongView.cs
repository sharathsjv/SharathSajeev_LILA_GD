using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace QAOffice
{
    // Ping pong UI and touch input. Drag anywhere to slide along your end of the table; start a swipe on
    // the ball to hit it (direction = swipe direction, power = swipe speed). Also shows invites: a coworker
    // asking you to play, and the opponent picker when you walk up to the table.
    public class PingPongView : View
    {
        [Header("Match")]
        [SerializeField] GameObject matchPanel;
        [SerializeField] Text scoreText;
        [SerializeField] Text messageText;
        [SerializeField] Text hintText;
        [SerializeField] float zoomSize = 3.2f;
        [SerializeField] float focusHeight = -0.4f;
        [Tooltip("Swipe speed (screen heights per second) that counts as full power.")]
        [SerializeField] float fullPowerSpeed = 2.5f;

        [Header("Result")]
        [SerializeField] GameObject resultPanel;
        [SerializeField] Text resultText;
        [SerializeField] Button resultButton;

        [Header("Invite / opponent picker")]
        [SerializeField] GameObject promptPanel;
        [SerializeField] Text promptText;
        [SerializeField] RectTransform promptButtons;

        Actor _opponent;
        PingPongTable _table;
        bool _pickerOpen;

        public new bool IsOpen => matchPanel.activeSelf || resultPanel.activeSelf || _pickerOpen;

        public override void Init()
        {
            resultButton.onClick.AddListener(Finish);
            gameObject.SetActive(true);
            matchPanel.SetActive(false);
            resultPanel.SetActive(false);
            promptPanel.SetActive(false);
        }

        // ---------- Prompts ----------

        void Prompt(string text, params (string label, Color color, Action action)[] buttons)
        {
            promptText.text = text;
            UIKit.Clear(promptButtons);
            foreach (var (label, color, action) in buttons) UIKit.Button(promptButtons, label, color, action, 24);
            promptPanel.SetActive(true);
            transform.SetAsLastSibling();
        }

        void HidePrompt()
        {
            promptPanel.SetActive(false);
            _pickerOpen = false;
        }

        PingPongTable _commentedOn;
        Actor _commentedA;

        // Comments from the sidelines on a coworkers' game: once per game, no time cost.
        public void ShowComments()
        {
            var table = G.Office.PingPong;
            if (!table.IsNpcMatch || table.NpcA == null || table.NpcB == null) return;
            var a = table.NpcA;
            var b = table.NpcB;
            if (_commentedA == a && _commentedOn == table)
            {
                G.UI.Toast("You've already said your piece. Let them play!");
                return;
            }
            var comments = (G.Story.pingPongComments ?? new PingPongComment[0]).OrderBy(_ => UnityEngine.Random.value).Take(3).ToList();
            if (comments.Count == 0) return;
            _pickerOpen = true;
            string Fill(string s) => s.Replace("{a}", a.Data.name).Replace("{b}", b.Data.name);
            var buttons = comments.Select(c => (Fill(c.text), UIKit.PanelLight, (Action)(() =>
                {
                    HidePrompt();
                    _commentedOn = table;
                    _commentedA = a;
                    if (c.trustA != 0) G.AddTrust(a.Data.id, c.trustA);
                    if (c.trustB != 0) G.AddTrust(b.Data.id, c.trustB);
                    var speaker = c.trustA >= c.trustB ? a : b;
                    string Delta(Actor x, int d) => d == 0 ? "" : $" ({x.Data.name} {(d > 0 ? "+" : "")}{d})";
                    G.UI.Toast($"{speaker.Data.name}: {Fill(c.response)}{Delta(a, c.trustA)}{Delta(b, c.trustB)}");
                })))
                .Append(("Say nothing", UIKit.Muted, (Action)HidePrompt)).ToArray();
            Prompt($"{a.Data.name} vs {b.Data.name}. Shout something?", buttons);
        }

        public void ShowOpponentPicker()
        {
            var available = G.Office.Npcs.Where(n => !n.Held).ToList();
            _pickerOpen = true;
            var buttons = available.Select(n => (n.Data.name, UIKit.Accent, (Action)(() => { HidePrompt(); G.StartPingPong(n); })))
                .Append(("Never mind", UIKit.Muted, (Action)HidePrompt)).ToArray();
            Prompt($"Who do you want to play? (-{G.S.pingPongMinutes} min)", buttons);
        }

        void Update()
        {
            if (G == null) return;

            // A coworker's invite shows while you're free in the office.
            var invite = G.PendingInvite;
            bool showInvite = invite != null && !_pickerOpen && !IsOpen && G.ClockRunning && !G.UI.Modal;
            if (showInvite && !promptPanel.activeSelf)
                Prompt($"<b>{invite.Data.name}:</b> {invite.Data.pingPong.inviteLine}",
                    ($"Play (-{G.S.pingPongMinutes} min)", UIKit.Good, (Action)(() => { HidePrompt(); G.AcceptInvite(); })),
                    ("Not now", UIKit.PanelLight, (Action)(() => { HidePrompt(); G.DeclineInvite(); })));
            else if (!showInvite && promptPanel.activeSelf && !_pickerOpen) promptPanel.SetActive(false);

            if (matchPanel.activeSelf) HandleInput();
        }

        // ---------- Match ----------

        public void ShowMatch(Actor opponent, PingPongTable table)
        {
            _opponent = opponent;
            _table = table;
            HidePrompt();
            matchPanel.SetActive(true);
            resultPanel.SetActive(false);
            transform.SetAsLastSibling();
            messageText.text = "";
            hintText.text = "Left thumb: drag to move      Right thumb: swipe to hit (faster = harder)";
            _fingers.Clear();
            table.Message += OnMessage;
            table.MatchOver += OnMatchOver;
            G.Office.Rig?.Focus(table.Center + Vector3.up * focusHeight, zoomSize);
            G.Office.SetLabelsVisible(false);
        }

        void OnMessage(string msg) => messageText.text = msg;

        void OnMatchOver(bool playerWon)
        {
            _table.Message -= OnMessage;
            _table.MatchOver -= OnMatchOver;
            matchPanel.SetActive(false);
            var p = _opponent.Data.pingPong;
            int delta = playerWon ? p.winTrust : p.loseTrust;
            G.AddTrust(_opponent.Data.id, delta);
            string trust = delta > 0 ? $"<color=#3FBF7F>+{delta} trust</color>" : delta < 0 ? $"<color=#E5534B>{delta} trust</color>" : "no change in trust";
            resultText.text = $"<b>{(playerWon ? "You win" : "You lose")} {_table.PlayerScore}-{_table.OpponentScore}</b>\n\n" +
                              $"<b>{_opponent.Data.name}:</b> {(playerWon ? p.playerWinsLine : p.playerLosesLine)}\n\n{trust}";
            resultPanel.SetActive(true);
        }

        void Finish()
        {
            resultPanel.SetActive(false);
            G.Office.Rig?.Release();
            G.Office.SetLabelsVisible(true);
            G.FinishPingPong();
        }

        public override void Close()
        {
            // End of day mid-match: abandon it.
            if (_table != null && (matchPanel.activeSelf || resultPanel.activeSelf))
            {
                _table.Message -= OnMessage;
                _table.MatchOver -= OnMatchOver;
                _table.End();
                G.Office.Rig?.Release();
                G.Office.SetLabelsVisible(true);
            }
            matchPanel.SetActive(false);
            resultPanel.SetActive(false);
            HidePrompt();
        }

        void RefreshScore()
        {
            scoreText.text = $"You {_table.PlayerScore}  -  {_table.OpponentScore} {_opponent.Data.name}";
        }

        // Each finger is tracked on its own: a finger that starts on the left half of the screen moves you,
        // one that starts on the right half swings (direction = swipe direction, power = swipe speed).
        // Both can be down at once. The mouse works the same way in the editor.
        class Finger
        {
            public bool Swing;
            public Vector2 Start, Last;
            public double StartTime;
        }

        readonly Dictionary<int, Finger> _fingers = new Dictionary<int, Finger>();
        const int MouseId = -1;

        void OnEnable() => EnhancedTouchSupport.Enable();

        void HandleInput()
        {
            RefreshScore();
            if (G.Paused) return;
            var touches = Touch.activeTouches;
            if (touches.Count > 0)
            {
                foreach (var t in touches)
                    Track(t.touchId, t.phase, t.screenPosition, t.startScreenPosition, t.startTime);
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;
            var pos = mouse.position.ReadValue();
            if (mouse.leftButton.wasPressedThisFrame) Track(MouseId, TouchPhase.Began, pos, pos, Time.realtimeSinceStartupAsDouble);
            else if (mouse.leftButton.isPressed) Track(MouseId, TouchPhase.Moved, pos, pos, 0);
            else if (mouse.leftButton.wasReleasedThisFrame) Track(MouseId, TouchPhase.Ended, pos, pos, 0);
        }

        void Track(int id, TouchPhase phase, Vector2 pos, Vector2 start, double startTime)
        {
            switch (phase)
            {
                case TouchPhase.Began:
                    _fingers[id] = new Finger { Swing = start.x >= Screen.width / 2f, Start = start, Last = pos, StartTime = startTime };
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (!_fingers.TryGetValue(id, out var f))
                    {
                        // Missed the Began frame: classify from where it started.
                        f = _fingers[id] = new Finger { Swing = start.x >= Screen.width / 2f, Start = start, Last = start, StartTime = startTime };
                    }
                    if (!f.Swing && TryTable(f.Last, out var a) && TryTable(pos, out var b)) _table.PlayerSlide(b.y - a.y);
                    f.Last = pos;
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (_fingers.TryGetValue(id, out var done))
                    {
                        _fingers.Remove(id);
                        if (done.Swing && phase == TouchPhase.Ended) Swing(done.Start, pos, Time.realtimeSinceStartupAsDouble - done.StartTime);
                    }
                    break;
            }
        }

        void Swing(Vector2 from, Vector2 to, double seconds)
        {
            var delta = to - from;
            if (delta.magnitude < 25f * Screen.height / 1080f) return;
            if (!TryTable(from, out var a) || !TryTable(to, out var b)) return;
            float speed = delta.magnitude / Screen.height / Mathf.Max(0.05f, (float)seconds);
            _table.PlayerSwing(b - a, Mathf.Clamp01(speed / fullPowerSpeed));
        }

        // Screen point -> table-space point on the table's top plane.
        bool TryTable(Vector2 screen, out Vector2 local)
        {
            var ray = G.Office.Cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0, _table.TopY, 0));
            if (plane.Raycast(ray, out float d))
            {
                local = _table.ToLocal(ray.GetPoint(d));
                return true;
            }
            local = default;
            return false;
        }
    }
}
