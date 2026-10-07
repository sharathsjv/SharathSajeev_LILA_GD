using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // The trust minigame. The coworker talks; a topic icon flickers between guesses (you're not sure what
    // they mean yet). Commit to the icon showing, or ask more: each question costs time, removes a wrong
    // guess and slows the flicker. Committing correctly earlier earns more trust.
    public class ConversationView : View
    {
        enum Phase { Talking, Guessing, Done }

        const int MaxBubbles = 5;

        Actor _npc;
        CharacterData _c;
        TopicData _topic;
        Phase _phase;
        int _asks;
        readonly List<string> _candidates = new List<string>();
        int _shown;
        float _cycleTimer;
        readonly Queue<string> _pendingLines = new Queue<string>();
        float _lineTimer;
        string _lastTopicIcon;

        [SerializeField] Text _name, _trustText;
        [SerializeField] Image _trustBar;
        [SerializeField] RectTransform _bubbles, _actions;
        [SerializeField] Button _iconButton;
        [SerializeField] Image _icon;
        [SerializeField] Text _iconLabel, _prompt;
        Button _askButton;

        public override void Init() => _iconButton.onClick.AddListener(Commit);

        public void Open(Actor npc)
        {
            _npc = npc;
            _c = npc.Data;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UIKit.Clear(_bubbles);
            _lastTopicIcon = null;
            StartTopic(true);
        }

        public override void Close()
        {
            if (_npc != null) _npc.Hold(false);
            _npc = null;
            base.Close();
        }

        void StartTopic(bool first)
        {
            G.SpendMinutes(G.S.talkCostMinutes);
            if (!IsOpen) return; // the day ended

            var topics = _c.topics.Where(t => t.minDay <= G.Day).ToList();
            var fresh = topics.Where(t => t.icon != _lastTopicIcon).ToList();
            _topic = (fresh.Count > 0 ? fresh : topics)[Random.Range(0, (fresh.Count > 0 ? fresh : topics).Count)];
            _lastTopicIcon = _topic.icon;
            _asks = 0;

            // Correct icon plus three decoys, shuffled.
            _candidates.Clear();
            _candidates.Add(_topic.icon);
            _candidates.AddRange(G.Story.icons.Select(i => i.id).Where(id => id != _topic.icon).OrderBy(_ => Random.value).Take(3));
            for (int i = _candidates.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_candidates[i], _candidates[j]) = (_candidates[j], _candidates[i]);
            }

            _phase = Phase.Talking;
            _pendingLines.Clear();
            if (first && !string.IsNullOrEmpty(_c.greeting)) _pendingLines.Enqueue(_c.greeting);
            _pendingLines.Enqueue(_topic.lines[0]);
            _lineTimer = 0.3f;
            SetIconVisible(false);
            _prompt.text = "";
            UIKit.Clear(_actions);
            UIKit.Button(_actions, "Leave", UIKit.Muted, Close, 26);
            RefreshHeader();
        }

        void Update()
        {
            if (_c == null) return;
            if (_pendingLines.Count > 0)
            {
                _lineTimer -= Time.deltaTime;
                if (_lineTimer <= 0)
                {
                    Bubble(_c.name, _pendingLines.Dequeue(), false);
                    _lineTimer = 1.1f;
                    if (_pendingLines.Count == 0 && _phase == Phase.Talking) EnterGuessing();
                }
            }

            if (_phase != Phase.Guessing || _candidates.Count <= 1) return;
            _cycleTimer += Time.deltaTime;
            float interval = G.S.iconCycleSeconds * (1 + _asks * 0.9f);
            if (_cycleTimer >= interval)
            {
                _cycleTimer = 0;
                _shown = (_shown + 1) % _candidates.Count;
                ShowIcon();
            }
        }

        int MaxAsks => Mathf.Min(_topic.lines.Length - 1, 3);

        void EnterGuessing()
        {
            _phase = Phase.Guessing;
            _shown = 0;
            _cycleTimer = 0;
            SetIconVisible(true);
            ShowIcon();
            UIKit.Clear(_actions);
            _askButton = UIKit.Button(_actions, "", UIKit.PanelLight, Ask, 26);
            UIKit.Button(_actions, "Leave", UIKit.Muted, Close, 26);
            RefreshGuessUI();
        }

        void RefreshGuessUI()
        {
            bool certain = _candidates.Count == 1;
            _prompt.text = certain
                ? "You know what they mean now. Tap the icon to respond."
                : "What are they on about? Tap the icon to commit to the topic showing,\nor ask more to narrow it down.";
            _askButton.SetLabel($"Ask more (-{G.S.askCostMinutes} min)");
            _askButton.interactable = !certain && _asks < MaxAsks;
        }

        void Ask()
        {
            if (_phase != Phase.Guessing) return;
            _asks++;
            Bubble("You", AskLines[Random.Range(0, AskLines.Length)], true);
            G.SpendMinutes(G.S.askCostMinutes);
            if (!IsOpen) return;

            if (_asks < _topic.lines.Length) _pendingLines.Enqueue(_topic.lines[_asks]);
            _lineTimer = 0.6f;
            var decoys = _candidates.Where(id => id != _topic.icon).ToList();
            if (_asks >= MaxAsks) _candidates.RemoveAll(id => id != _topic.icon);
            else if (decoys.Count > 0) _candidates.Remove(decoys[Random.Range(0, decoys.Count)]);
            _shown = Mathf.Clamp(_shown, 0, _candidates.Count - 1);
            ShowIcon();
            RefreshGuessUI();
        }

        static readonly string[] AskLines =
        {
            "Sorry, what do you mean?", "Wait, go on...", "Huh - say more?", "Which part?", "Okay, and?",
        };

        void Commit()
        {
            if (_phase != Phase.Guessing) return;
            _phase = Phase.Done;
            _pendingLines.Clear();
            var picked = _candidates[_shown];
            bool correct = picked == _topic.icon;
            var rewards = G.S.commitTrustRewards;
            int delta = correct ? rewards[Mathf.Min(_asks, rewards.Length - 1)] : -G.S.wrongTrustPenalty;

            Bubble("You", $"Oh - this is about {G.Icon(picked).label.ToLowerInvariant()}, right?", true);
            Bubble(_c.name, correct ? _topic.reply : _topic.wrongReply, false);
            G.AddTrust(_c.id, delta);
            G.RecordConversation(correct);
            Bubble(null, correct
                ? $"{_c.name} feels understood.  +{delta} trust"
                : $"{_c.name} looks a bit put off.  -{G.S.wrongTrustPenalty} trust", false, correct ? UIKit.Good : UIKit.Bad);

            var secret = G.NextSecretFrom(_c.id);
            if (secret != null)
            {
                Bubble(_c.name, secret.revealText, false);
                G.Learn(secret);
                Bubble(null, $"Secret learned: {secret.title}", false, UIKit.Warn);
            }

            SetIconVisible(false);
            ShowAfterOptions();
            RefreshHeader();
        }

        void ShowAfterOptions()
        {
            UIKit.Clear(_actions);
            _prompt.text = "";
            UIKit.Button(_actions, $"Keep chatting (-{G.S.talkCostMinutes} min)", UIKit.Accent, () => StartTopic(false), 26);
            var usable = G.UsableOn(_c.id).ToList();
            if (_c.isBoss && usable.Count > 0)
                UIKit.Button(_actions, "Bring something up...", UIKit.Warn, ShowSecretChoices, 26);
            UIKit.Button(_actions, "Leave", UIKit.Muted, Close, 26);
        }

        void ShowSecretChoices()
        {
            UIKit.Clear(_actions);
            _prompt.text = $"Use what you know about {_c.name}:";
            foreach (var s in G.UsableOn(_c.id))
            {
                var secret = s;
                UIKit.Button(_actions, secret.title, UIKit.Warn, () =>
                {
                    Bubble("You", secret.useText, true);
                    Bubble(_c.name, secret.useResponse, false);
                    G.UseSecret(secret);
                    ShowAfterOptions();
                }, 24);
            }
            UIKit.Button(_actions, "Never mind", UIKit.Muted, ShowAfterOptions, 24);
        }

        void SetIconVisible(bool on)
        {
            _iconButton.gameObject.SetActive(on);
        }

        void ShowIcon()
        {
            var icon = G.Icon(_candidates[_shown]);
            _icon.sprite = Icons.Shape(icon.shape);
            _icon.color = UIKit.Hex(icon.color);
            _iconLabel.text = _candidates.Count > 1 ? icon.label + "?" : icon.label;
        }

        void RefreshHeader()
        {
            _name.text = $"{_c.name}  <size=26><color=#9AA0B4>{_c.role}</color></size>";
            int t = G.Trust(_c.id);
            _trustBar.fillAmount = (float)t / G.S.maxTrust;
            _trustText.text = $"Trust {t}/{G.S.maxTrust}";
        }

        void Bubble(string speaker, string text, bool player, Color? systemColor = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            while (_bubbles.childCount >= MaxBubbles) DestroyImmediate(_bubbles.GetChild(0).gameObject);

            bool system = speaker == null;
            var color = system ? new Color(0, 0, 0, 0.35f) : player ? UIKit.Accent : UIKit.Paper;
            var box = UIKit.Box(_bubbles, "Bubble", color);
            var layout = UIKit.VList(box.rectTransform, 0, 14);
            layout.padding = new RectOffset(player ? 80 : 18, player ? 18 : 80, 10, 10);
            var msg = UIKit.Label(box.transform,
                system ? text : $"<b>{speaker}:</b> {text}", 28,
                system ? systemColor ?? Color.white : player ? Color.white : UIKit.Ink,
                system ? TextAnchor.MiddleCenter : player ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
            msg.supportRichText = true;
            if (player) box.color = UIKit.Accent;
        }
    }
}
