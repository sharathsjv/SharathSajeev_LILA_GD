using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // Lunch: a non-blocking invite while the lunch window is open, then a short scene at the break-room
    // table where coworkers ask to try your food or strike up conversation. Replies change trust.
    public class LunchView : View
    {
        [Header("Invite (shown while lunch is open)")]
        [SerializeField] GameObject invitePanel;
        [SerializeField] Button eatButton;
        [SerializeField] Button skipButton;

        [Header("Lunch table dialogue")]
        [SerializeField] GameObject dialogPanel;
        [SerializeField] Text speakerText;
        [SerializeField] Text lineText;
        [SerializeField] RectTransform replies;
        [SerializeField] float zoomSize = 4.5f;
        [Tooltip("Camera aim relative to the lunch seats; negative lifts the table up the screen, clear of the dialogue box.")]
        [SerializeField] float focusHeight = -1.6f;

        readonly Queue<(CharacterData speaker, LunchEventData ev)> _events = new Queue<(CharacterData, LunchEventData)>();
        List<Actor> _companions;

        // The lunch view object stays active so the invite can show; "open" means the lunch scene is playing.
        public new bool IsOpen => dialogPanel.activeSelf;

        public override void Init()
        {
            eatButton.onClick.AddListener(() => G.StartLunch());
            skipButton.onClick.AddListener(() => G.SkipLunch());
            gameObject.SetActive(true);
            invitePanel.SetActive(false);
            dialogPanel.SetActive(false);
        }

        void Update()
        {
            if (G == null) return;
            bool invite = G.Lunch == LunchState.Open && G.ClockRunning && !IsOpen &&
                          !G.UI.Computer.IsOpen && !G.UI.Conversation.IsOpen && !G.UI.Screens.IsOpen;
            if (invitePanel.activeSelf != invite) invitePanel.SetActive(invite);
        }

        public void Play(List<Actor> companions)
        {
            _companions = companions;
            invitePanel.SetActive(false);
            dialogPanel.SetActive(true);
            transform.SetAsLastSibling();
            G.Office.Rig?.Focus(G.Office.LunchFocus + Vector3.up * focusHeight, zoomSize);

            var people = companions.Select(c => c.Data).ToList();
            var pool = (G.Story.lunchEvents ?? new LunchEventData[0])
                .Where(e => e.minDay <= G.Day && (string.IsNullOrEmpty(e.from) || people.Any(p => p.id == e.from)))
                .OrderBy(_ => UnityEngine.Random.value)
                .Take(G.S.lunchEventsPerLunch);
            _events.Clear();
            foreach (var e in pool)
            {
                var speaker = string.IsNullOrEmpty(e.from) ? people[UnityEngine.Random.Range(0, people.Count)] : people.First(p => p.id == e.from);
                _events.Enqueue((speaker, e));
            }

            string names = people.Count == 0 ? "nobody" : string.Join(" and ", people.Select(p => p.name));
            Show("", $"You sit down for lunch with {names}.", ("Dig in", UIKit.Accent, Next));
        }

        void Next()
        {
            if (_events.Count == 0)
            {
                Show("", "Lunch is over. Back to work.", ("Back to work", UIKit.Accent, Finish));
                return;
            }
            var (speaker, ev) = _events.Dequeue();
            var buttons = ev.replies.Select(r => (r.text, UIKit.PanelLight, (Action)(() => Answer(speaker, r)))).ToArray();
            Show(speaker.name, ev.text, buttons);
        }

        void Answer(CharacterData speaker, ChatReplyData r)
        {
            G.AddTrust(speaker.id, r.trust);
            G.RecordLunchReply(r.score);
            string trust = r.trust > 0 ? $"  <color=#3FBF7F>(+{r.trust} trust)</color>" : r.trust < 0 ? $"  <color=#E5534B>({r.trust} trust)</color>" : "";
            Show(speaker.name, (string.IsNullOrEmpty(r.response) ? "..." : r.response) + trust, ("Continue", UIKit.Accent, Next));
        }

        void Show(string speaker, string line, params (string label, Color color, Action action)[] buttons)
        {
            speakerText.text = speaker;
            lineText.text = line;
            UIKit.Clear(replies);
            foreach (var (label, color, action) in buttons) UIKit.Button(replies, label, color, action, 24);
        }

        void Finish()
        {
            dialogPanel.SetActive(false);
            G.Office.Rig?.Release();
            G.FinishLunch();
        }

        public override void Close()
        {
            // Called by CloseAll at end of day; lunch can't be open then, but keep the view object alive.
            if (dialogPanel.activeSelf)
            {
                dialogPanel.SetActive(false);
                G.Office.Rig?.Release();
                G.Office.ReturnFromLunch();
            }
        }
    }
}
