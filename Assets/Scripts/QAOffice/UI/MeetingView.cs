using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // Check-ins and performance reviews, played out in the conference room: the camera zooms in on the
    // table and the host and coworkers talk in a speech bubble above whoever is speaking.
    public class MeetingView : View
    {
        [Header("Speech")]
        [SerializeField] RectTransform bubble;
        [SerializeField] Text speakerName;
        [SerializeField] Text lineText;
        [SerializeField] float headHeight = 1.9f;

        [Header("Panels")]
        [SerializeField] GameObject statsPanel;
        [SerializeField] Text statsText;
        [SerializeField] RectTransform choices;
        [SerializeField] Button continueButton;

        [Header("Camera")]
        [SerializeField] float zoomSize = 4.2f;
        [SerializeField] float focusHeight = 1.2f;

        readonly Queue<(Actor speaker, string text, Action then)> _lines = new Queue<(Actor, string, Action)>();
        Actor _speaker;
        MeetingKind _kind;

        MeetingData M => G.Story.meeting;
        Actor Host => G.Office.Npc(M.hostId);

        public override void Init()
        {
            continueButton.onClick.AddListener(Next);
        }

        public void Play(MeetingKind kind)
        {
            _kind = kind;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            statsPanel.SetActive(false);
            ClearChoices();
            G.Office.Rig?.Focus(G.Office.MeetingFocus + Vector3.up * focusHeight, zoomSize);

            if (kind == MeetingKind.CheckIn) CheckIn();
            else Review();
        }

        public override void Close()
        {
            G.Office.Rig?.Release();
            base.Close();
        }

        // ---------- Scripts ----------

        void CheckIn()
        {
            var tier = G.TierFor(G.PerformancePercent);
            Say(Host, M.checkInOpening, ShowStats);
            Say(Host, $"If your review were today, I'd call it {tier.name.ToLowerInvariant()}.");
            React(tier);
            Say(Host, $"{M.checkInClosing} The real review is on {GameManager.Weekday(G.NextReviewDay)}.", Finish);
            Next();
        }

        void Review()
        {
            Say(Host, M.reviewOpening, ShowStats);
            Say(Host, $"Since the last review: QA {Pct(G.QAScore)}, working with people {Pct(G.InteractionScore)}.");
            Say(Host, M.askForMore, ShowSecretChoices);
            Next();
        }

        void ShowSecretChoices()
        {
            var usable = G.UsableSecrets.Where(s => M.attendees.Contains(s.about)).ToList();
            ClearChoices();
            continueButton.gameObject.SetActive(false);
            foreach (var s in usable)
            {
                var secret = s;
                AddChoice($"{G.Character(secret.about).name}: {secret.title}", UIKit.Warn, () =>
                {
                    ClearChoices();
                    G.UseSecret(secret);
                    Say(G.Office.Player, secret.useText);
                    Say(G.Office.Npc(secret.about), secret.useResponse, ShowSecretChoices);
                    Next();
                });
            }
            AddChoice("Nothing to add", UIKit.Ink, () =>
            {
                ClearChoices();
                Verdict();
            });
        }

        void Verdict()
        {
            var r = G.ConcludeReview();
            Say(Host, r.Tier.line);
            if (r.Fired)
            {
                Say(Host, M.firedEnding, () =>
                {
                    ClearChoices();
                    continueButton.gameObject.SetActive(false);
                    AddChoice("Pack up your desk", UIKit.Bad, () =>
                    {
                        Close();
                        G.UI.Screens.ShowFired(M.firedEnding, r.Percent);
                    });
                });
                Next();
                return;
            }
            if (r.PromotedNow) Say(Host, M.promotedLine);
            React(r.Tier);
            if (r.Tier.warning) Say(Host, "Another review like this and you're out.");
            Say(Host, $"Next review is on {GameManager.Weekday(G.NextReviewDay)}. That's all for today.", Finish);
            Next();
        }

        // One of the others at the table chimes in.
        void React(ReviewTier tier)
        {
            if (tier.reactions == null || tier.reactions.Length == 0) return;
            var others = G.Office.Npcs.Where(n => n.Data.id != M.hostId).ToList();
            if (others.Count == 0) return;
            var who = others[UnityEngine.Random.Range(0, others.Count)];
            Say(who, tier.reactions[UnityEngine.Random.Range(0, tier.reactions.Length)]);
        }

        void ShowStats()
        {
            statsText.text = G.UI.Screens.PeriodStats();
            statsPanel.SetActive(true);
        }

        void Finish()
        {
            ClearChoices();
            continueButton.gameObject.SetActive(false);
            AddChoice($"Head home  -  start {GameManager.Weekday(G.Day + 1)}", UIKit.Accent, () =>
            {
                Close();
                G.StartDay(G.Day + 1);
            });
        }

        static string Pct(float v) => $"{Mathf.RoundToInt(v * 100)}%";

        // ---------- Lines ----------

        void Say(Actor speaker, string text, Action then = null)
        {
            if (string.IsNullOrEmpty(text) && then == null) return;
            _lines.Enqueue((speaker, text, then));
        }

        void Next()
        {
            if (_lines.Count == 0) return;
            var (speaker, text, then) = _lines.Dequeue();
            if (!string.IsNullOrEmpty(text))
            {
                _speaker = speaker;
                speakerName.text = speaker == null ? "" : speaker.IsPlayer ? "You" : speaker.Data.name;
                speakerName.color = speaker == null || speaker.IsPlayer ? UIKit.Accent : UIKit.Hex(speaker.Data.color);
                lineText.text = text;
                bubble.gameObject.SetActive(true);
            }
            continueButton.gameObject.SetActive(_lines.Count > 0 || then == null);
            then?.Invoke();
        }

        void ClearChoices()
        {
            UIKit.Clear(choices);
            continueButton.gameObject.SetActive(true);
        }

        void AddChoice(string label, Color color, Action onClick) => UIKit.Button(choices, label, color, onClick, 24);

        // ---------- Bubble placement ----------

        void LateUpdate()
        {
            if (_speaker == null) return;
            var cam = G.Office.Cam;
            var screen = cam.WorldToScreenPoint(_speaker.transform.position + Vector3.up * headHeight);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Root, screen, null, out var local);
            var half = Root.rect.size / 2;
            var s = bubble.rect.size;
            local.x = Mathf.Clamp(local.x, -half.x + s.x / 2 + 20, half.x - s.x / 2 - 20);
            local.y = Mathf.Clamp(local.y, -half.y + 140, half.y - s.y - 20);
            bubble.anchoredPosition = local;
        }
    }
}
