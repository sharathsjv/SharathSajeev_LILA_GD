using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QAOffice
{
    public class TaskState
    {
        public TaskData Data;
        public bool Arrived;
        public bool Done;
        public QAResult Result;
        public float ArrivalMinutes(int startHour) => (Data.hour - startHour) * 60 + Data.minute;
    }

    public class ChatState
    {
        public ChatMessageData Data;
        public bool Arrived;
        public int Chosen = -1;
        public bool Answered => Chosen >= 0;
    }

    public enum MeetingKind { None, CheckIn, Review }

    public enum LunchState { NotYet, Open, Eaten, Skipped }

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

        // Work days only: Day 1 is a Monday, Day 6 the next Monday (weekends are skipped).
        static readonly string[] Weekdays = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" };
        public static string Weekday(int day) => Weekdays[(Mathf.Max(1, day) - 1) % Weekdays.Length];

        readonly Dictionary<string, int> _trust = new Dictionary<string, int>();
        public readonly HashSet<string> Known = new HashSet<string>();
        public readonly HashSet<string> Used = new HashSet<string>();
        public readonly List<TaskState> Tasks = new List<TaskState>();
        public readonly List<ChatState> Chats = new List<ChatState>();
        readonly List<(int day, bool correct)> _conversations = new List<(int, bool)>();
        public float SecretBonus { get; private set; }      // from secrets used this review period
        public bool PromotionEarned { get; private set; }
        public int PeriodStart { get; private set; } = 1;   // first day counted by the next review
        readonly List<(int day, float score)> _lunchScores = new List<(int, float)>();
        public LunchState Lunch { get; private set; }
        public bool Hungry => Lunch == LunchState.Skipped;   // skipped lunch: conversation icons never fully settle
        bool _warnedLastReview;
        public Actor PendingInvite { get; private set; }     // a coworker asking you to play ping pong
        float _inviteExpires, _nextInviteCheck;
        readonly HashSet<string> _invitedToday = new HashSet<string>();
        Actor _pingPongOpponent;
        float _nextNpcMatchCheck;
        readonly HashSet<string> _met = new HashSet<string>();
        public bool Paused { get; private set; }
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
            {
                // Unwritten days: more builds as the weeks go on, landing through the day.
                int count = S.tasksPerUnscriptedDay + Mathf.Min(2, (day - 1) / 5);
                var givers = Story.characters.Where(c => c.isBoss).Select(c => c.id).ToArray();
                for (int i = 0; i < count; i++)
                {
                    int at = i == 0 ? 0 : UnityEngine.Random.Range(60, (S.dayEndHour - S.dayStartHour - 2) * 60);
                    Tasks.Add(new TaskState { Data = new TaskData
                    {
                        day = day, from = givers[UnityEngine.Random.Range(0, givers.Length)], genre = "random",
                        brief = Story.meeting.unscriptedTaskBrief, hour = S.dayStartHour + at / 60, minute = at % 60,
                    } });
                }
            }
            Minutes = 0;
            _warned = false;
            Lunch = LunchState.NotYet;
            PendingInvite = null;
            _invitedToday.Clear();
            _nextInviteCheck = S.pingPongInviteEveryMinutes;
            _nextNpcMatchCheck = S.pingPongNpcMatchEveryMinutes;
            Office.ResetPositions();
            ClockRunning = true;
            Advance(0);
            UI.Toast($"{Weekday(Day)} - {ClockText}");
            NotifyChanged();
        }

        public void SpendMinutes(int minutes) => Advance(minutes);

        void Advance(float minutes)
        {
            Minutes = Mathf.Min(DayLength, Minutes + minutes);
            foreach (var t in Tasks)
            {
                if (t.Arrived || t.Data.day != Day || t.ArrivalMinutes(S.dayStartHour) > Minutes) continue;
                t.Arrived = true;
                if (Minutes > 0 && Minutes < DayLength) UI.Toast($"New build from {Character(t.Data.from).name}");
                NotifyChanged();
            }
            if (S.pingPongNpcMatchEveryMinutes > 0 && Minutes >= _nextNpcMatchCheck)
            {
                _nextNpcMatchCheck = Minutes + S.pingPongNpcMatchEveryMinutes;
                TryStartNpcMatch();
            }
            foreach (var c in Chats)
            {
                if (c.Arrived || c.Data.day != Day) continue;
                if ((c.Data.hour - S.dayStartHour) * 60 + c.Data.minute > Minutes) continue;
                c.Arrived = true;
                if (Minutes < DayLength) UI.Toast($"New message from {Character(c.Data.from).name}");
                NotifyChanged();
            }
            float lunchStart = (S.lunchHour - S.dayStartHour) * 60f;
            float lunchEnd = lunchStart + S.lunchMinutes;
            if (Lunch == LunchState.NotYet && Minutes >= lunchStart && S.lunchMinutes > 0)
            {
                Lunch = LunchState.Open;
                UI.Toast("Lunch time! Coworkers are heading to the break room.");
                NotifyChanged();
            }
            if (Lunch == LunchState.Open && Minutes >= lunchEnd)
            {
                Lunch = LunchState.Skipped;
                UI.Toast("You skipped lunch. Hard to focus on what people say...");
                NotifyChanged();
            }
            if (PendingInvite != null && Minutes >= _inviteExpires) PendingInvite = null;
            if (S.pingPongInviteEveryMinutes > 0 && Minutes >= _nextInviteCheck)
            {
                _nextInviteCheck = Minutes + S.pingPongInviteEveryMinutes;
                if (PendingInvite == null && Lunch != LunchState.Open && Office.PingPong != null && !Office.PingPong.InUse && DayLength - Minutes > S.pingPongMinutes)
                {
                    foreach (var n in Office.Npcs.OrderBy(_ => UnityEngine.Random.value))
                    {
                        if (n.Held || _invitedToday.Contains(n.Data.id) || n.Data.pingPong == null) continue;
                        if (UnityEngine.Random.value >= n.Data.pingPong.inviteChance) continue;
                        PendingInvite = n;
                        _invitedToday.Add(n.Data.id);
                        _inviteExpires = Minutes + 30;
                        break;
                    }
                }
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
                UI.Meeting.Play(meeting);
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

        // ---- Lunch ----

        public void SkipLunch()
        {
            if (Lunch != LunchState.Open) return;
            Lunch = LunchState.Skipped;
            UI.Toast("You skipped lunch. Hard to focus on what people say...");
            NotifyChanged();
        }

        public void StartLunch()
        {
            if (Lunch != LunchState.Open) return;
            Lunch = LunchState.Eaten;
            ClockRunning = false;
            UI.CloseAll();
            var pool = Office.Npcs.Where(n => !n.Data.isBoss).OrderBy(_ => UnityEngine.Random.value)
                .Concat(Office.Npcs.Where(n => n.Data.isBoss).OrderBy(_ => UnityEngine.Random.value));
            var companions = pool.Take(S.lunchCompanions).ToList();
            Office.GatherForLunch(companions);
            UI.Lunch.Play(companions);
            NotifyChanged();
        }

        public void FinishLunch()
        {
            Office.ReturnFromLunch();
            float lunchEnd = (S.lunchHour - S.dayStartHour) * 60f + S.lunchMinutes;
            ClockRunning = true;
            if (Minutes < lunchEnd) Advance(lunchEnd - Minutes);
            NotifyChanged();
        }

        public void RecordLunchReply(float score) => _lunchScores.Add((Day, score));

        // True the first time it's called for a character (they introduce themselves).
        public bool MeetForFirstTime(string id) => _met.Add(id);

        public void SetPaused(bool paused)
        {
            Paused = paused;
            Time.timeScale = paused ? 0 : 1;
            NotifyChanged();
        }

        // Set dressing: two idle coworkers play a game of their own.
        void TryStartNpcMatch()
        {
            var table = Office.PingPong;
            if (table == null || table.InUse || Lunch == LunchState.Open || UnityEngine.Random.value > S.pingPongNpcMatchChance) return;
            var free = Office.Npcs.Where(n => !n.Held && !n.Data.isBoss && n.Data.pingPong != null).OrderBy(_ => UnityEngine.Random.value).Take(2).ToList();
            if (free.Count < 2) return;
            table.BeginNpcMatch(free[0], free[1], S.pingPongBallSpeed, S.pingPongNpcMatchSeconds);
        }

        // ---- Ping pong ----

        public void AcceptInvite()
        {
            var n = PendingInvite;
            PendingInvite = null;
            if (n != null) StartPingPong(n);
        }

        public void DeclineInvite() => PendingInvite = null;

        public void StartPingPong(Actor opponent)
        {
            var table = Office.PingPong;
            if (table == null || opponent?.Data?.pingPong == null) return;
            if (table.InUse)
            {
                UI.Toast("The table's taken - wait for their game to finish.");
                return;
            }
            if (PendingInvite == opponent) PendingInvite = null;
            ClockRunning = false;
            UI.CloseAll();
            _pingPongOpponent = opponent;
            UI.PingPong.ShowMatch(opponent, table); // subscribe to table messages before the first serve
            table.Begin(Office.Player, opponent, opponent.Data.pingPong, S.pingPongPointsToWin, S.pingPongBallSpeed);
            NotifyChanged();
        }

        public void FinishPingPong()
        {
            Office.PingPong.End();
            _pingPongOpponent = null;
            ClockRunning = true;
            SpendMinutes(S.pingPongMinutes);
            NotifyChanged();
        }

        // ---- Lookups ----

        public CharacterData Character(string id) => Story.characters.First(c => c.id == id);

        public IconData Icon(string id) => Story.icons.FirstOrDefault(i => i.id == id) ?? Story.icons[0];

        public int Trust(string id) => _trust.TryGetValue(id, out var t) ? t : 0;

        public void AddTrust(string id, int delta)
        {
            _trust[id] = Mathf.Clamp(Trust(id) + delta, 0, S.maxTrust);
            NotifyChanged();
        }

        public IEnumerable<TaskState> TasksToday => Tasks.Where(t => t.Data.day == Day && t.Arrived);

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
                var due = Tasks.Where(t => t.Arrived && t.Data.day >= PeriodStart && t.Data.day <= Day).ToList();
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
                // Chat replies (unanswered = 0) and lunch-table replies both count as "how you talk to people".
                var social = ChatsSoFar.Where(c => c.Data.day >= PeriodStart)
                    .Select(c => c.Answered ? c.Data.replies[c.Chosen].score : 0f)
                    .Concat(_lunchScores.Where(l => l.day >= PeriodStart).Select(l => l.score))
                    .ToList();
                float chat = social.Count == 0 ? 0 : social.Average();
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
