using System;
using UnityEngine;

namespace QAOffice
{
    // All authored content lives in Assets/Content/story.json and is loaded into these classes.
    // Edit it with QA Office > Story Editor. The attributes below only affect how that window draws fields.
    // (The first string field of each class is what the editor shows as a list item's name.)

    [Serializable]
    public class StoryData
    {
        public GameSettings settings;
        public IconData[] icons;
        public CharacterData[] characters;
        public SecretData[] secrets;
        public TaskData[] tasks;
        public ChatMessageData[] chats;
        public LunchEventData[] lunchEvents;
        public PingPongComment[] pingPongComments;
        public MeetingData meeting;
    }

    [Serializable]
    public class GameSettings
    {
        [Header("Schedule")]
        [Tooltip("Days ending with a no-consequence progress check-in.")]
        public int[] checkInDays;
        [Tooltip("A performance review ends every Nth day (5 = days 5, 10, 15...).")]
        public int reviewEveryDays;
        [Tooltip("Builds auto-assigned on days with no authored tasks.")]
        public int tasksPerUnscriptedDay;

        [Header("Clock")]
        [Tooltip("Real seconds for one 9-to-5 day.")]
        public float realSecondsPerDay;
        public int dayStartHour;
        public int dayEndHour;

        [Header("Conversations")]
        [Tooltip("In-game minutes spent starting a conversation.")]
        public int talkCostMinutes;
        [Tooltip("In-game minutes per 'Ask more'.")]
        public int askCostMinutes;
        [Tooltip("Trust for a correct commit after 0, 1, 2, 3 asks.")]
        public int[] commitTrustRewards;
        public int wrongTrustPenalty;
        public int maxTrust;
        [Tooltip("Icon flicker interval with no asks; it slows with each ask.")]
        public float iconCycleSeconds;
        [Tooltip("Real seconds the player has to pick an orbiting icon; restarts after each new clue. Running out counts as a wrong answer.")]
        public float guessSeconds;

        [Header("Lunch")]
        [Tooltip("Hour lunch starts (e.g. 12).")]
        public int lunchHour;
        [Tooltip("How long the lunch window lasts, in in-game minutes. Not eating by the end of it counts as skipping.")]
        public int lunchMinutes;
        [Tooltip("How many coworkers sit with you.")]
        public int lunchCompanions;
        [Tooltip("How many lunch moments (food requests, conversations) happen per lunch.")]
        public int lunchEventsPerLunch;

        [Header("Ping pong")]
        public int pingPongPointsToWin;
        [Tooltip("In-game minutes a match takes.")]
        public int pingPongMinutes;
        [Tooltip("Ball speed across the table (table lengths per second, roughly).")]
        public float pingPongBallSpeed;
        [Tooltip("In-game minutes between checks for a coworker inviting the player.")]
        public int pingPongInviteEveryMinutes;
        [Tooltip("In-game minutes between checks for two coworkers starting a game of their own (set dressing).")]
        public int pingPongNpcMatchEveryMinutes;
        [Tooltip("Chance a check actually starts a coworker match.")]
        [Range(0, 1)] public float pingPongNpcMatchChance;
        [Tooltip("Real seconds a coworker match lasts.")]
        public float pingPongNpcMatchSeconds;

        [Header("QA")]
        [Tooltip("Real seconds the player gets with each build.")]
        public float qaTestSeconds;
        [Tooltip("In-game minutes spent writing each report.")]
        public int reportCostMinutes;

        [Header("Titles")]
        public string startTitle;
        public string promotedTitle;
    }

    [Serializable]
    public class IconData
    {
        public string id;
        public string label;
        [StoryRef(StoryRefKind.Shape)] public string shape;
        [StoryRef(StoryRefKind.Color)] public string color;
    }

    [Serializable]
    public class CharacterData
    {
        [Tooltip("Must match the Character Id on the Actor in the scene.")]
        public string id;
        public string name;
        public string role;
        [Tooltip("Bosses can have secrets used on them.")]
        public bool isBoss;
        [StoryRef(StoryRefKind.Color)] public string color;
        [TextArea(1, 3)] public string greeting;
        [Tooltip("Lines said the first time the player talks to this person, before the usual minigame.")]
        [TextArea(1, 3)] public string[] introduction;
        public TopicData[] topics;
        public PingPongProfile pingPong;
    }

    [Serializable]
    public class PingPongProfile
    {
        [Tooltip("0 = hopeless, 1 = returns almost everything.")]
        [Range(0, 1)] public float skill;
        [Tooltip("Trust change when the PLAYER wins. Competitive people may dislike losing (negative).")]
        public int winTrust;
        [Tooltip("Trust change when the PLAYER loses.")]
        public int loseTrust;
        [Tooltip("Chance this person invites the player when an invite check happens (once per day at most).")]
        [Range(0, 1)] public float inviteChance;
        [TextArea(1, 2)] public string inviteLine;
        [Tooltip("Said when the player wins.")]
        [TextArea(1, 2)] public string playerWinsLine;
        [Tooltip("Said when the player loses.")]
        [TextArea(1, 2)] public string playerLosesLine;
    }

    [Serializable]
    public class TopicData
    {
        [Tooltip("The icon the coworker is actually talking about.")]
        [StoryRef(StoryRefKind.Icon)] public string icon;
        [Tooltip("Topic only appears from this day on (use for event-driven dialogue).")]
        public int minDay;
        [Tooltip("Element 0 is the opening line; each later line is said after one more 'Ask more' and should be clearer.")]
        [TextArea(1, 3)] public string[] lines;
        [Tooltip("Said after the player picks the right topic.")]
        [TextArea(1, 3)] public string reply;
        [Tooltip("Said after the player picks the wrong topic.")]
        [TextArea(1, 3)] public string wrongReply;
    }

    [Serializable]
    public class SecretData
    {
        public string id;
        [Tooltip("The boss this secret is about (it can be used on them).")]
        [StoryRef(StoryRefKind.Boss)] public string about;
        [Tooltip("Who reveals it.")]
        [StoryRef(StoryRefKind.Character)] public string source;
        [Tooltip("Trust needed with the source.")]
        public int trustRequired;
        [Tooltip("Secrets that must be known first.")]
        [StoryRef(StoryRefKind.Secret)] public string[] requires;
        public string title;
        [Tooltip("What the source says when revealing it.")]
        [TextArea(2, 5)] public string revealText;
        [Tooltip("What the player says when using it.")]
        [TextArea(2, 5)] public string useText;
        [Tooltip("The boss's reaction.")]
        [TextArea(2, 5)] public string useResponse;
        [StoryRef(StoryRefKind.Effect)] public string effect;
        [Tooltip("Added to the performance score of the review period it's used in (0.1 = +10%).")]
        public float performanceBonus;
    }

    [Serializable]
    public class TaskData
    {
        [TextArea(1, 3)] public string brief;
        public int day;
        [Tooltip("When the build lands on the player's PC that day.")]
        public int hour;
        public int minute;
        [Tooltip("Who assigns it.")]
        [StoryRef(StoryRefKind.Character)] public string from;
        [StoryRef(StoryRefKind.Genre)] public string genre;
    }

    [Serializable]
    public class ChatMessageData
    {
        [TextArea(1, 3)] public string text;
        public int day;
        public int hour;
        public int minute;
        [StoryRef(StoryRefKind.Character)] public string from;
        public ChatReplyData[] replies;
    }

    [Serializable]
    public class ChatReplyData
    {
        public string text;
        [Tooltip("Trust change with the sender.")]
        public int trust;
        [Tooltip("0-1: how good a reply this is; feeds the interaction score.")]
        [Range(0, 1)] public float score;
        [Tooltip("Sender's follow-up (optional).")]
        public string response;
    }

    [Serializable]
    public class LunchEventData
    {
        [TextArea(1, 3)] public string text;
        [Tooltip("Who says it. Leave empty for 'any coworker at the table'. Only used if that person is at the table.")]
        [StoryRef(StoryRefKind.CharacterOrAny)] public string from;
        [Tooltip("Only happens from this day on.")]
        public int minDay;
        [Tooltip("How the player can respond. Trust applies to the speaker; score feeds the interaction score.")]
        public ChatReplyData[] replies;
    }

    [Serializable]
    public class MeetingData
    {
        [Tooltip("Who runs check-ins and reviews.")]
        [StoryRef(StoryRefKind.Character)] public string hostId;
        [Tooltip("Bosses whose secrets can be raised at a review.")]
        [StoryRef(StoryRefKind.Boss)] public string[] attendees;
        [TextArea(2, 4)] public string checkInOpening;
        [TextArea(2, 4)] public string checkInClosing;
        [TextArea(2, 4)] public string reviewOpening;
        [TextArea(2, 4)] public string askForMore;
        [Tooltip("Score bands, lowest first. Each applies from its Min Percent up to the next tier's.")]
        public ReviewTier[] tiers;
        [Tooltip("Added to the verdict when a promotion secret was used.")]
        [TextArea(2, 4)] public string promotedLine;
        [Tooltip("Shown when the player lands in a warning tier twice in a row.")]
        [TextArea(2, 4)] public string firedEnding;
        [Tooltip("Brief for auto-assigned builds on days without authored tasks.")]
        [TextArea(1, 3)] public string unscriptedTaskBrief;
    }

    [Serializable]
    public class ReviewTier
    {
        public string name;
        [Range(0, 100)] public int minPercent;
        [Tooltip("What the host says.")]
        [TextArea(2, 4)] public string line;
        [Tooltip("Landing in a warning tier twice in a row gets the player fired.")]
        public bool warning;
        [Tooltip("Lines other people at the table might add after the host's verdict (one is picked at random).")]
        [TextArea(1, 2)] public string[] reactions;
    }

    [Serializable]
    public class PingPongComment
    {
        [Tooltip("Shout from the sidelines at a coworkers' match. {a} and {b} are replaced with the two players' names.")]
        public string text;
        [Tooltip("Trust change with the first player ({a}).")]
        public int trustA;
        [Tooltip("Trust change with the second player ({b}).")]
        public int trustB;
        [Tooltip("What one of them says back.")]
        public string response;
    }

    public enum StoryRefKind { Character, CharacterOrAny, Boss, Icon, Secret, Genre, Effect, Shape, Color }

    // Draws a string field as a dropdown of valid ids (or a colour picker) in the Story Editor / Inspector.
    public class StoryRefAttribute : PropertyAttribute
    {
        public readonly StoryRefKind Kind;
        public StoryRefAttribute(StoryRefKind kind) => Kind = kind;
    }
}
