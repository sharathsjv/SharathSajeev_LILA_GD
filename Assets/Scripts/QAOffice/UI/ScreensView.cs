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
            Show($"{GameManager.Weekday(G.Day)} - 5:00 PM", TodayStats() + "\n\n" + PeriodStats() + $"\n\nNext performance review: end of Day {G.NextReviewDay}.",
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

        public string PeriodStats()
        {
            var tasks = G.Tasks.Where(t => t.Arrived && t.Data.day >= G.PeriodStart && t.Data.day <= G.Day).ToList();
            string days = G.PeriodStart == G.Day ? GameManager.Weekday(G.Day) : $"{GameManager.Weekday(G.PeriodStart)} - {GameManager.Weekday(G.Day)}";
            return $"<b>{days}</b>\n" +
                   $"QA: {tasks.Count(t => t.Done)}/{tasks.Count} builds reported - {Pct(G.QAScore)}\n" +
                   $"People: {G.ConversationsTotal} conversations ({G.ConversationsCorrect} read correctly) - {Pct(G.InteractionScore)}\n" +
                   $"Overall: {G.PerformancePercent}%";
        }

        (string, Color, Action) NextDayButton() =>
            ($"Start Day {G.Day + 1}", UIKit.Accent, () => { Close(); G.StartDay(G.Day + 1); });

        static string Pct(float v) => $"{Mathf.RoundToInt(v * 100)}%";

        string Host => G.Character(G.Story.meeting.hostId).name;

        // ---- Game over (after a second terrible review) ----

        public void ShowFired(string text, int percent)
        {
            Show("Let go", $"{text}\n\n<i>Final review: {percent}%. You lasted {G.Day} days.</i>",
                ("Play again", UIKit.Accent, SceneFlow.Restart));
            _title.color = UIKit.Bad;
        }

        public override void Close()
        {
            _title.color = UIKit.Ink;
            base.Close();
        }
    }
}
