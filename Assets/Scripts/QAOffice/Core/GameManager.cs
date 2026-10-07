using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QAOffice
{
    public class TaskState
    {
        public TaskData Data;
        public bool Done;
        public QAResult Result;
    }

    public class ChatState
    {
        public ChatMessageData Data;
        public bool Arrived;
        public int Chosen = -1;
        public bool Answered => Chosen >= 0;
    }

    public enum MeetingKind { None, CheckIn, Review }

    public class ReviewResult
    {
        public int Percent;
        public ReviewTier Tier;
        public bool Fired;
        public bool PromotedNow;
    }

    // Owns the run: clock, days, trust, secrets, tasks, chats and the periodic performance reviews.
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        [Tooltip("All story content and tuning (Assets/Content/story.json).")]
        [SerializeField] TextAsset storyJson;
        [SerializeField] Office office;
        [SerializeField] UIRoot ui;

        public StoryData Story { get; private set; }
        public GameSettings S => Story.settings;
        public Office Office => office;
        public UIRoot UI => ui;

        public int Day { get; private set; }
        public float Minutes { get; private set; }
        public bool ClockRunning { get; set; }
        public string JobTitle { get; private set; }
        public float DayLength => (S.dayEndHour - S.dayStartHour) * 60f;
        public string ClockText => UIKit.Clock(Minutes, S.dayStartHour);

        readonly Dictionary<string, int> _trust = new Dictionary<string, int>();
        public readonly HashSet<string> Known = new HashSet<string>();
        public readonly HashSet<string> Used = new HashSet<string>();
        public readonly List<TaskState> Tasks = new List<TaskState>();
        public readonly List<ChatState> Chats = new List<ChatState>();
        readonly List<(int day, bool correct)> _conversations = new List<(int, bool)>();
        public float SecretBonus { get; private set; }      // from secrets used this review period
        public bool PromotionEarned { get; private set; }
        public int PeriodStart { get; private set; } = 1;   // first day counted by the next review
        bool _warnedLastReview;
        bool _promotionAnnounced;

        public event Action Changed;
        bool _warned;

        void Awake()
        {
            I = this;
            Story = JsonUtility.FromJson<StoryData>(storyJson.text);
            JobTitle = S.startTitle;
            foreach (var c in Story.characters) _trust[c.id] = 0;
            foreach (var t in Story.tasks) Tasks.Add(new TaskState { Data = t });
            foreach (var m in Story.chats.OrderBy(m => m.day * 10000 + m.hour * 60 + m.minute)) Chats.Add(new ChatState { Data = m });

            office.Init(Story);
            ui.Init();
        }

        void Start()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            UI.Screens.ShowIntro();
        }

        void Update()
        {
            if (ClockRunning) Advance(Time.deltaTime * DayLength / S.realSecondsPerDay);
        }

        public void StartDay(int day)
        {
            Day = day;
            if (!Tasks.Any(t => t.Data.day == day))
                for (int i = 0; i < S.tasksPerUnscriptedDay; i++)
                    Tasks.Add(new TaskState { Data = new TaskData { day = day, from = Story.meeting.hostId, genre = "random", brief = Story.meeting.unscriptedTaskBrief } });
            Minutes = 0;
            _warned = false;
            Office.ResetPositions();
            ClockRunning = true;
            Advance(0);
            UI.Toast($"Day {Day} - {ClockText}");
            NotifyChanged();
        }

        public void SpendMinutes(int minutes) => Advance(minutes);

        void Advance(float minutes)
        {
            Minutes = Mathf.Min(DayLength, Minutes + minutes);
            foreach (var c in Chats)
            {
                if (c.Arrived || c.Data.day != Day) continue;
                if ((c.Data.hour - S.dayStartHour) * 60 + c.Data.minute > Minutes) continue;
                c.Arrived = true;
                if (Minutes < DayLength) UI.Toast($"New message from {Character(c.Data.from).name}");
                NotifyChanged();
            }
            if (!_warned && DayLength - Minutes <= 30)
            {
                _warned = true;
                UI.Toast("30 minutes until 5 PM");
            }
            if (Minutes >= DayLength && ClockRunning) EndDay();
        }

        void EndDay()
        {
            ClockRunning = false;
            UI.CloseAll();
            UI.ClearToasts();
            var meeting = MeetingAfter(Day);
            if (meeting == MeetingKind.None) UI.Screens.ShowDaySummary();
            else
            {
                Office.GatherForMeeting();
                if (meeting == MeetingKind.Review) UI.Screens.ShowReview();
                else UI.Screens.ShowCheckIn();
            }
            NotifyChanged();
        }

        public MeetingKind MeetingAfter(int day)
        {
            if (S.reviewEveryDays > 0 && day % S.reviewEveryDays == 0) return MeetingKind.Review;
            return S.checkInDays != null && S.checkInDays.Contains(day) ? MeetingKind.CheckIn : MeetingKind.None;
        }

        public int NextReviewDay => S.reviewEveryDays <= 0 ? 0 : (Day / S.reviewEveryDays + 1) * S.reviewEveryDays;

        public void NotifyChanged() => Changed?.Invoke();

        // ---- Lookups ----

        public CharacterData Character(string id) => Story.characters.First(c => c.id == id);

        public IconData Icon(string id) => Story.icons.FirstOrDefault(i => i.id == id) ?? Story.icons[0];

        public int Trust(string id) => _trust.TryGetValue(id, out var t) ? t : 0;

        public void AddTrust(string id, int delta)
        {
            _trust[id] = Mathf.Clamp(Trust(id) + delta, 0, S.maxTrust);
            NotifyChanged();
        }

        public IEnumerable<TaskState> TasksToday => Tasks.Where(t => t.Data.day == Day);

        public IEnumerable<ChatState> ChatsSoFar => Chats.Where(c => c.Arrived);

        public int UnreadCount => Chats.Count(c => c.Arrived && !c.Answered && c.Data.day == Day);

        public int OpenTaskCount => TasksToday.Count(t => !t.Done);

        // ---- Conversations & chat ----

        public void RecordConversation(bool correct) => _conversations.Add((Day, correct));

        public int ConversationsTotal => _conversations.Count(c => c.day >= PeriodStart);
        public int ConversationsCorrect => _conversations.Count(c => c.day >= PeriodStart && c.correct);

        public void AnswerChat(ChatState chat, int replyIndex)
        {
            chat.Chosen = replyIndex;
            AddTrust(chat.Data.from, chat.Data.replies[replyIndex].trust);
        }

        public void CompleteTask(TaskState task, QAResult result)
        {
            task.Done = true;
            task.Result = result;
            SpendMinutes(S.reportCostMinutes);
            NotifyChanged();
        }

        // ---- Secrets ----

        public SecretData NextSecretFrom(string sourceId) =>
            Story.secrets.FirstOrDefault(s =>
                s.source == sourceId && !Known.Contains(s.id) &&
                (s.requires ?? Array.Empty<string>()).All(Known.Contains) &&
                Trust(sourceId) >= s.trustRequired);

        public void Learn(SecretData s)
        {
            Known.Add(s.id);
            UI.Toast($"Secret learned: {s.title}");
            NotifyChanged();
        }

        public IEnumerable<SecretData> UsableOn(string bossId) =>
            Story.secrets.Where(s => s.about == bossId && Known.Contains(s.id) && !Used.Contains(s.id));

        public IEnumerable<SecretData> UsableSecrets =>
            Story.secrets.Where(s => Known.Contains(s.id) && !Used.Contains(s.id));

        public void UseSecret(SecretData s)
        {
            Used.Add(s.id);
            SecretBonus += s.performanceBonus;
            if (s.effect == "promotion") PromotionEarned = true;
            NotifyChanged();
        }

        // ---- Evaluation (always over the current review period: PeriodStart..Day) ----

        public float QAScore
        {
            get
            {
                var due = Tasks.Where(t => t.Data.day >= PeriodStart && t.Data.day <= Day).ToList();
                return due.Count == 0 ? 0 : due.Average(t => t.Done ? t.Result.Score : 0f);
            }
        }

        public float InteractionScore
        {
            get
            {
                int total = ConversationsTotal;
                float hitRate = total == 0 ? 0 : (float)ConversationsCorrect / total;
                float engagement = Mathf.Clamp01(total / (2f * Mathf.Max(1, Day - PeriodStart + 1)));
                var arrived = ChatsSoFar.Where(c => c.Data.day >= PeriodStart).ToList();
                float chat = arrived.Count == 0 ? 0 : arrived.Average(c => c.Answered ? c.Data.replies[c.Chosen].score : 0f);
                return 0.7f * hitRate * engagement + 0.3f * chat;
            }
        }

        public float Performance => Mathf.Clamp01((QAScore + InteractionScore) / 2f + SecretBonus);

        public int PerformancePercent => Mathf.RoundToInt(Performance * 100);

        public ReviewTier TierFor(int percent) =>
            Story.meeting.tiers.Where(t => percent >= t.minPercent).OrderBy(t => t.minPercent).LastOrDefault() ?? Story.meeting.tiers[0];

        // Call once at the end of a review: decides the tier, applies promotion/firing and starts a new period.
        public ReviewResult ConcludeReview()
        {
            var r = new ReviewResult { Percent = PerformancePercent };
            r.Tier = TierFor(r.Percent);
            r.Fired = r.Tier.warning && _warnedLastReview;
            _warnedLastReview = r.Tier.warning;
            if (PromotionEarned && !_promotionAnnounced)
            {
                _promotionAnnounced = true;
                r.PromotedNow = true;
                JobTitle = S.promotedTitle;
            }
            PeriodStart = Day + 1;
            SecretBonus = 0;
            NotifyChanged();
            return r;
        }
    }
}
