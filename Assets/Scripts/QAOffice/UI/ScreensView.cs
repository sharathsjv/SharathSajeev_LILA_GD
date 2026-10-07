using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // Full-screen story beats: intro, end-of-day summary, the performance meeting and the ending.
    public class ScreensView : View
    {
        [SerializeField] Text _title, _body;
        [SerializeField] RectTransform _buttons;

        void Show(string title, string body, params (string label, Color color, Action action)[] buttons)
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _title.text = title;
            _body.text = body;
            UIKit.Clear(_buttons);
            foreach (var (label, color, action) in buttons)
                UIKit.Button(_buttons, label, color, action, 30);
        }

        // ---- Intro ----

        public void ShowIntro()
        {
            Show($"QA Tester 1  <size=28><color=#9AA0B4>v{Application.version}</color></size>",
                "You've just started as a junior QA tester. Every build that lands on your desk is a little broken, and so is the office.\n\n" +
                "<b>Work:</b> go to your desk and play the builds you're assigned. Flag bugs, file reports, decide what ships.\n" +
                "<b>People:</b> tap coworkers to chat. Work out what they're actually talking about to earn their trust, and they might tell you things they shouldn't.\n" +
                "<b>Climb:</b> secrets about the bosses can be used at the right moment.\n\n" +
                $"Your first performance review is at the end of Day {G.S.reviewEveryDays}. Two terrible reviews in a row and you're out.",
                ("Start Day 1", UIKit.Accent, () => { Close(); G.StartDay(1); }));
        }

        // ---- End of day ----

        public void ShowDaySummary()
        {
            Show($"Day {G.Day} - 5:00 PM", TodayStats() + "\n\n" + PeriodStats() + $"\n\nNext performance review: end of Day {G.NextReviewDay}.",
                NextDayButton());
        }

        string TodayStats()
        {
            var today = G.TasksToday.ToList();
            int done = today.Count(t => t.Done);
            string tasks = today.Count == 0 ? "No builds today." :
                $"Builds tested today: {done}/{today.Count}" + (done > 0 ? $"   (average report score {Pct(today.Where(t => t.Done).Average(t => t.Result.Score))})" : "");
            return $"{tasks}\nSecrets known: {G.Known.Count}";
        }

        string PeriodStats()
        {
            var tasks = G.Tasks.Where(t => t.Data.day >= G.PeriodStart && t.Data.day <= G.Day).ToList();
            string days = G.PeriodStart == G.Day ? $"Day {G.Day}" : $"Days {G.PeriodStart}-{G.Day}";
            return $"<b>{days}</b>\n" +
                   $"QA: {tasks.Count(t => t.Done)}/{tasks.Count} builds reported - {Pct(G.QAScore)}\n" +
                   $"People: {G.ConversationsTotal} conversations ({G.ConversationsCorrect} read correctly) - {Pct(G.InteractionScore)}\n" +
                   $"Overall: {G.PerformancePercent}%";
        }

        (string, Color, Action) NextDayButton() =>
            ($"Start Day {G.Day + 1}", UIKit.Accent, () => { Close(); G.StartDay(G.Day + 1); });

        static string Pct(float v) => $"{Mathf.RoundToInt(v * 100)}%";

        string Host => G.Character(G.Story.meeting.hostId).name;

        // ---- Check-in (no consequences) ----

        public void ShowCheckIn()
        {
            var m = G.Story.meeting;
            var tier = G.TierFor(G.PerformancePercent);
            Show("Check-in",
                $"<b>{Host}:</b> {m.checkInOpening}\n\n{PeriodStats()}\n\n" +
                $"<b>{Host}:</b> If your review were today, I'd call it <b>{tier.name.ToLowerInvariant()}</b>. {m.checkInClosing}\n\n" +
                $"<i>Performance review at the end of Day {G.NextReviewDay}.</i>",
                NextDayButton());
        }

        // ---- Performance review: stats -> raise secrets -> verdict ----

        public void ShowReview()
        {
            Show("Performance review", $"<b>{Host}:</b> {G.Story.meeting.reviewOpening}\n\n{PeriodStats()}",
                ("Continue", UIKit.Accent, ShowReviewFloor));
        }

        void ShowReviewFloor()
        {
            var m = G.Story.meeting;
            var usable = G.UsableSecrets.Where(s => m.attendees.Contains(s.about)).ToList();
            var buttons = usable
                .Select(s => ($"{G.Character(s.about).name}: {s.title}", UIKit.Warn, (Action)(() => UseInReview(s))))
                .Append(("Nothing to add", UIKit.Ink, (Action)ShowVerdict))
                .ToArray();
            Show("Performance review",
                $"<b>{Host}:</b> {m.askForMore}" +
                (usable.Count > 0 ? "\n\n<i>You could bring up something you know...</i>" : ""),
                buttons);
        }

        void UseInReview(SecretData s)
        {
            G.UseSecret(s);
            Show("Performance review",
                $"<b>You:</b> {s.useText}\n\n<b>{G.Character(s.about).name}:</b> {s.useResponse}",
                ("Continue", UIKit.Accent, ShowReviewFloor));
        }

        void ShowVerdict()
        {
            var m = G.Story.meeting;
            var r = G.ConcludeReview();
            if (r.Fired)
            {
                Show("Let go",
                    $"<b>{Host}:</b> {r.Tier.line}\n\n{m.firedEnding}\n\n<i>Final review: {r.Percent}%. You lasted {G.Day} days.</i>",
                    ("Play again", UIKit.Accent, SceneFlow.Restart));
                _title.color = UIKit.Bad;
                return;
            }
            Show($"Review: {r.Tier.name} ({r.Percent}%)",
                $"<b>{Host}:</b> {r.Tier.line}" +
                (r.PromotedNow ? $"\n\n{m.promotedLine}" : "") +
                (r.Tier.warning ? "\n\n<i>Another review like this and you're out.</i>" : "") +
                $"\n\n<i>Next performance review: end of Day {G.NextReviewDay}.</i>",
                NextDayButton());
            _title.color = r.Tier.warning ? UIKit.Bad : r.Percent > 70 ? UIKit.Good : UIKit.Ink;
        }

        public override void Close()
        {
            _title.color = UIKit.Ink;
            base.Close();
        }
    }
}
