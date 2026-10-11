using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // The trust minigame. The camera zooms in and the coworker talks in a speech bubble above their head.
    // Inside the bubble, an icon flickers between guesses (you're not sure what they mean yet); small icons
    // orbit the bubble - tap one to commit to that topic. "Ask more" costs time, removes a wrong orbiting
    // icon and slows the flicker. Committing correctly earlier earns more trust.
    public class ConversationView : View
    {
        enum Phase { Talking, Guessing, Done }

        [Header("Header")]
        [SerializeField] Text nameText;
        [SerializeField] Text trustText;
        [SerializeField] Image trustFill;

        [Header("Speech bubbles (positioned above heads at runtime)")]
        [SerializeField] RectTransform npcBubble;
        [SerializeField] Text npcText;
        [SerializeField] Image innerIcon;
        [Tooltip("Countdown shown along the bottom of the speech bubble while the player decides.")]
        [SerializeField] GameObject timerBar;
        [SerializeField] Image timerFill;
        [SerializeField] Button npcBubbleButton;
        [SerializeField] RectTransform playerBubble;
        [SerializeField] Text playerText;
        [SerializeField] float npcHeadHeight = 1.9f;
        [SerializeField] float playerHeadHeight = 1.85f;

        [Header("Orbiting choices")]
        [SerializeField] RectTransform orbitRoot;
        [SerializeField] Button orbitTemplate;
        [SerializeField] float orbitDegreesPerSecond = 22f;
        [Tooltip("Orbit ellipse = bubble half-size x this scale, plus padding (scale > 1.4 keeps icons clear of the corners).")]
        [SerializeField] float orbitScale = 1.45f;
        [SerializeField] Vector2 orbitPadding = new Vector2(20, 30);

        [Header("Controls")]
        [SerializeField] Text prompt;
        [SerializeField] RectTransform actions;
        [SerializeField] Button askButton;
        [SerializeField] Button leaveButton;
        [SerializeField] Text floater;

        [Header("Camera")]
        [SerializeField] float zoomSize = 4f;
        [Tooltip("How far above the two people the camera centres; higher pushes them down the screen, leaving room for bubbles.")]
        [SerializeField] float focusHeight = 2.4f;

        Actor _npc;
        CharacterData _c;
        TopicData _topic;
        Phase _phase;
        int _asks;
        string _lastTopicIcon;

        readonly List<string> _candidates = new List<string>();
        readonly List<(Button button, string id)> _orbit = new List<(Button, string)>();
        readonly List<GameObject> _dynamicButtons = new List<GameObject>();
        int _shown;
        float _cycleTimer;
        float _orbitAngle;
        float _guessLeft;

        readonly Queue<(bool player, string text)> _speech = new Queue<(bool, string)>();
        float _speechTimer;
        float _playerTimer;
        Action _afterSpeech;
        float _floaterTimer;

        static readonly string[] AskLines =
        {
            "Sorry, what do you mean?", "Wait, go on...", "Huh - say more?", "Which part?", "Okay, and?",
        };

        public override void Init()
        {
            askButton.onClick.AddListener(Ask);
            leaveButton.onClick.AddListener(Close);
            npcBubbleButton.onClick.AddListener(SkipLine);
            orbitTemplate.gameObject.SetActive(false);
        }

        public void Open(Actor npc)
        {
            _npc = npc;
            _c = npc.Data;
            var player = G.Office.Player;
            npc.Hold(true);
            npc.Face(player.transform.position);
            player.Face(npc.transform.position);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _lastTopicIcon = null;
            npcText.text = "";
            playerBubble.gameObject.SetActive(false);
            floater.gameObject.SetActive(false);
            G.Office.Rig?.Focus((npc.transform.position + player.transform.position) / 2 + Vector3.up * focusHeight, zoomSize);
            G.Office.SetLabelsVisible(false);
            StartTopic(true);
        }

        public override void Close()
        {
            if (_npc != null) _npc.Hold(false);
            _npc = null;
            _speech.Clear();
            _afterSpeech = null;
            ClearOrbit();
            if (G != null)
            {
                G.Office.Rig?.Release();
                G.Office.SetLabelsVisible(true);
            }
            base.Close();
        }

        // ---------- Flow ----------

        void StartTopic(bool first)
        {
            G.SpendMinutes(G.S.talkCostMinutes);
            if (!IsOpen) return; // the day ended

            var topics = _c.topics.Where(t => t.minDay <= G.Day).ToList();
            var fresh = topics.Where(t => t.icon != _lastTopicIcon).ToList();
            var pool = fresh.Count > 0 ? fresh : topics;
            _topic = pool[UnityEngine.Random.Range(0, pool.Count)];
            _lastTopicIcon = _topic.icon;
            _asks = 0;

            // Correct icon plus three decoys, shuffled.
            _candidates.Clear();
            _candidates.Add(_topic.icon);
            _candidates.AddRange(G.Story.icons.Select(i => i.id).Where(id => id != _topic.icon).OrderBy(_ => UnityEngine.Random.value).Take(3));
            for (int i = _candidates.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (_candidates[i], _candidates[j]) = (_candidates[j], _candidates[i]);
            }

            _phase = Phase.Talking;
            ClearOrbit();
            ClearDynamicButtons();
            innerIcon.gameObject.SetActive(false);
            askButton.gameObject.SetActive(false);
            prompt.text = "";
            timerBar.SetActive(false);
            if (first && _c.introduction != null && _c.introduction.Length > 0 && G.MeetForFirstTime(_c.id))
                foreach (var line in _c.introduction) Say(false, line);   // first meeting: they introduce themselves
            else if (first && !string.IsNullOrEmpty(_c.greeting)) Say(false, _c.greeting);
            Say(false, _topic.lines[0], EnterGuessing);
            RefreshHeader();
        }

        void EnterGuessing()
        {
            _phase = Phase.Guessing;
            _guessLeft = G.S.guessSeconds;
            timerBar.SetActive(G.S.guessSeconds > 0);
            _shown = 0;
            _cycleTimer = 0;
            innerIcon.gameObject.SetActive(true);
            ShowInnerIcon();
            ClearOrbit();
            foreach (var id in _candidates) AddOrbitIcon(id);
            askButton.gameObject.SetActive(true);
            RefreshGuessUI();
        }

        int MaxAsks => Mathf.Min(_topic.lines.Length - 1, 3);

        void RefreshGuessUI()
        {
            bool certain = _candidates.Count == 1;
            bool stuck = G.Hungry && _candidates.Count <= 2;
            prompt.text = certain ? "You know what they mean now - tap the icon."
                : stuck ? "Your stomach is growling. You can't quite focus - you'll have to guess."
                : G.Hungry ? "What are they on about? (You skipped lunch - it's hard to focus.)"
                : "What are they on about? Tap an orbiting icon to answer, or ask more.";
            askButton.SetLabel($"Ask more (-{G.S.askCostMinutes} min)");
            askButton.interactable = !certain && !stuck && _asks < MaxAsks;
        }

        void Ask()
        {
            if (_phase != Phase.Guessing || _asks >= MaxAsks) return;
            _asks++;
            _guessLeft = G.S.guessSeconds; // fresh countdown once the new clue has been said
            G.SpendMinutes(G.S.askCostMinutes);
            if (!IsOpen) return;

            Say(true, AskLines[UnityEngine.Random.Range(0, AskLines.Length)]);
            if (_asks < _topic.lines.Length) Say(false, _topic.lines[_asks]);

            // Hungry (skipped lunch): you never get fully certain - at least one wrong icon always stays.
            int keepDecoys = G.Hungry ? 1 : 0;
            var decoys = _candidates.Where(id => id != _topic.icon).ToList();
            int removeCount = _asks >= MaxAsks ? decoys.Count - keepDecoys : Mathf.Min(1, decoys.Count - keepDecoys);
            foreach (var id in decoys.Take(Mathf.Max(0, removeCount))) RemoveCandidate(id);
            RefreshGuessUI();
        }

        static readonly string[] TimeoutLines =
        {
            "...Hello? Are you even listening?", "Never mind. Forget it.", "You know what, it doesn't matter.",
        };

        // Ran out of time: counts as getting it wrong.
        void TimeOut() => Commit(null);

        void Commit(string picked)
        {
            if (_phase != Phase.Guessing) return;
            _phase = Phase.Done;
            timerBar.SetActive(false);
            _speech.Clear();
            _afterSpeech = null;
            bool correct = picked == _topic.icon;
            var rewards = G.S.commitTrustRewards;
            int delta = correct ? rewards[Mathf.Min(_asks, rewards.Length - 1)] : -G.S.wrongTrustPenalty;

            ClearOrbit();
            innerIcon.gameObject.SetActive(false);
            askButton.gameObject.SetActive(false);
            prompt.text = "";

            G.AddTrust(_c.id, delta);
            G.RecordConversation(correct);
            var secret = G.NextSecretFrom(_c.id);
            if (secret != null) G.Learn(secret);

            bool timedOut = picked == null;
            if (!timedOut) Say(true, $"Oh - this is about {G.Icon(picked).label.ToLowerInvariant()}, right?");
            string answer = correct ? _topic.reply : timedOut ? TimeoutLines[UnityEngine.Random.Range(0, TimeoutLines.Length)] : _topic.wrongReply;
            Say(false, answer, () =>
            {
                Float(correct ? $"+{delta} trust" : $"-{G.S.wrongTrustPenalty} trust", correct ? UIKit.Good : UIKit.Bad);
                RefreshHeader();
                if (secret != null) Say(false, secret.revealText, ShowAfterOptions);
                else ShowAfterOptions();
            });
        }

        void ShowAfterOptions()
        {
            ClearDynamicButtons();
            prompt.text = "";
            AddDynamicButton($"Keep chatting (-{G.S.talkCostMinutes} min)", UIKit.Accent, () => StartTopic(false));
            if (_c.isBoss && G.UsableOn(_c.id).Any())
                AddDynamicButton("Bring something up...", UIKit.Warn, ShowSecretChoices);
            if (G.Office.PingPong != null && _c.pingPong != null)
            {
                var npc = _npc;
                AddDynamicButton($"Ping pong? (-{G.S.pingPongMinutes} min)", UIKit.Good, () =>
                {
                    Close();
                    G.StartPingPong(npc);
                });
            }
            leaveButton.transform.SetAsLastSibling();
        }

        void ShowSecretChoices()
        {
            ClearDynamicButtons();
            prompt.text = $"Use what you know about {_c.name}:";
            foreach (var s in G.UsableOn(_c.id).ToList())
            {
                var secret = s;
                AddDynamicButton(secret.title, UIKit.Warn, () =>
                {
                    ClearDynamicButtons();
                    prompt.text = "";
                    G.UseSecret(secret);
                    Say(true, secret.useText);
                    Say(false, secret.useResponse, ShowAfterOptions);
                });
            }
            AddDynamicButton("Never mind", UIKit.Muted, ShowAfterOptions);
            leaveButton.transform.SetAsLastSibling();
        }

        // ---------- Speech ----------

        void Say(bool player, string text, Action then = null)
        {
            if (!string.IsNullOrEmpty(text)) _speech.Enqueue((player, text));
            if (then != null) _afterSpeech = then;
            if (_speechTimer <= 0) NextLine();
        }

        void SkipLine()
        {
            if (_speechTimer > 0) _speechTimer = 0.01f;
        }

        void NextLine()
        {
            if (_speech.Count == 0)
            {
                _speechTimer = 0;
                var then = _afterSpeech;
                _afterSpeech = null;
                then?.Invoke();
                return;
            }
            var (player, text) = _speech.Dequeue();
            if (player)
            {
                playerText.text = text;
                playerBubble.gameObject.SetActive(true);
                _playerTimer = Duration(text);
            }
            else npcText.text = text;
            _speechTimer = Duration(text);
        }

        static float Duration(string text) => Mathf.Clamp(1.1f + text.Length * 0.035f, 1.4f, 5f);

        void Float(string text, Color color)
        {
            floater.text = text;
            floater.color = color;
            floater.gameObject.SetActive(true);
            _floaterTimer = 1.6f;
        }

        // ---------- Orbit ----------

        void AddOrbitIcon(string id)
        {
            var b = Instantiate(orbitTemplate, orbitRoot);
            b.gameObject.SetActive(true);
            var icon = G.Icon(id);
            var img = b.transform.Find("Icon").GetComponent<Image>();
            img.sprite = Icons.Shape(icon.shape);
            img.color = UIKit.Hex(icon.color);
            b.GetComponentInChildren<Text>().text = icon.label;
            b.onClick.AddListener(() => Commit(id));
            _orbit.Add((b, id));
        }

        void RemoveCandidate(string id)
        {
            _candidates.Remove(id);
            int i = _orbit.FindIndex(o => o.id == id);
            if (i >= 0)
            {
                Destroy(_orbit[i].button.gameObject);
                _orbit.RemoveAt(i);
            }
            _shown = Mathf.Clamp(_shown, 0, _candidates.Count - 1);
            ShowInnerIcon();
        }

        void ClearOrbit()
        {
            foreach (var (button, _) in _orbit) Destroy(button.gameObject);
            _orbit.Clear();
        }

        void ShowInnerIcon()
        {
            if (_candidates.Count == 0) return;
            var icon = G.Icon(_candidates[_shown]);
            innerIcon.sprite = Icons.Shape(icon.shape);
            innerIcon.color = UIKit.Hex(icon.color);
        }

        // ---------- Buttons ----------

        void AddDynamicButton(string label, Color color, Action onClick)
        {
            var b = UIKit.Button(actions, label, color, onClick, 26);
            _dynamicButtons.Add(b.gameObject);
        }

        void ClearDynamicButtons()
        {
            foreach (var go in _dynamicButtons) Destroy(go);
            _dynamicButtons.Clear();
        }

        void RefreshHeader()
        {
            nameText.text = $"{_c.name}  <size=24><color=#9AA0B4>{_c.role}</color></size>";
            int t = G.Trust(_c.id);
            trustFill.fillAmount = (float)t / G.S.maxTrust;
            trustText.text = $"Trust {t}/{G.S.maxTrust}";
        }

        // ---------- Per frame ----------

        void Update()
        {
            if (_c == null) return;

            if (_speechTimer > 0)
            {
                _speechTimer -= Time.deltaTime;
                if (_speechTimer <= 0) NextLine();
            }
            if (_playerTimer > 0)
            {
                _playerTimer -= Time.deltaTime;
                if (_playerTimer <= 0) playerBubble.gameObject.SetActive(false);
            }
            if (_floaterTimer > 0)
            {
                _floaterTimer -= Time.deltaTime;
                var c = floater.color;
                c.a = Mathf.Clamp01(_floaterTimer / 0.5f);
                floater.color = c;
                if (_floaterTimer <= 0) floater.gameObject.SetActive(false);
            }

            // Countdown while waiting for the player (paused while someone is still talking).
            if (_phase == Phase.Guessing && G.S.guessSeconds > 0 && _speechTimer <= 0)
            {
                _guessLeft -= Time.deltaTime;
                timerFill.fillAmount = Mathf.Clamp01(_guessLeft / G.S.guessSeconds);
                timerFill.color = Color.Lerp(UIKit.Bad, UIKit.Good, timerFill.fillAmount * 1.5f);
                if (_guessLeft <= 0)
                {
                    TimeOut();
                    return;
                }
            }

            if (_phase == Phase.Guessing && _candidates.Count > 1)
            {
                _cycleTimer += Time.deltaTime;
                if (_cycleTimer >= G.S.iconCycleSeconds * (1 + _asks * 0.9f))
                {
                    _cycleTimer = 0;
                    _shown = (_shown + 1) % _candidates.Count;
                    ShowInnerIcon();
                }
            }

            _orbitAngle += orbitDegreesPerSecond * Time.deltaTime;
        }

        void LateUpdate()
        {
            if (_npc == null) return;
            Pin(npcBubble, _npc.transform.position + Vector3.up * npcHeadHeight);
            if (playerBubble.gameObject.activeSelf)
            {
                Pin(playerBubble, G.Office.Player.transform.position + Vector3.up * playerHeadHeight);
                SeparateBubbles();
            }

            // Orbit around the bubble on an ellipse just outside it.
            var size = npcBubble.rect.size;
            var radius = size / 2 * orbitScale + orbitPadding;
            for (int i = 0; i < _orbit.Count; i++)
            {
                float a = (_orbitAngle + i * 360f / _orbit.Count) * Mathf.Deg2Rad;
                ((RectTransform)_orbit[i].button.transform).anchoredPosition = new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y);
            }
        }

        // Standing side by side, the two bubbles would overlap: slide the player's out to its side.
        void SeparateBubbles()
        {
            var a = npcBubble.anchoredPosition;
            var b = playerBubble.anchoredPosition;
            var sa = npcBubble.rect.size;
            var sb = playerBubble.rect.size;
            bool overlapY = b.y < a.y + sa.y && a.y < b.y + sb.y;
            float minDx = (sa.x + sb.x) / 2 + 16;
            if (!overlapY || Mathf.Abs(a.x - b.x) >= minDx) return;
            float dir = b.x >= a.x ? 1 : -1;
            float half = Root.rect.width / 2 - sb.x / 2 - 20;
            float x = a.x + dir * minDx;
            if (Mathf.Abs(x) > half) x = a.x - dir * minDx; // no room on that side: use the other
            playerBubble.anchoredPosition = new Vector2(x, b.y);
        }

        // Places a bottom-pivoted bubble above a world point, kept on screen.
        void Pin(RectTransform bubble, Vector3 world)
        {
            var cam = G.Office.Cam;
            var screen = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen, null, out var local);
            var half = Root.rect.size / 2;
            var s = bubble.rect.size;
            local.x = Mathf.Clamp(local.x, -half.x + s.x / 2 + 20, half.x - s.x / 2 - 20);
            local.y = Mathf.Clamp(local.y, -half.y + 20, half.y - s.y - 20);
            bubble.anchoredPosition = local;
        }
    }
}
