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
        public TopicData[] topics;
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
    }

    public enum StoryRefKind { Character, Boss, Icon, Secret, Genre, Effect, Shape, Color }

    // Draws a string field as a dropdown of valid ids (or a colour picker) in the Story Editor / Inspector.
    public class StoryRefAttribute : PropertyAttribute
    {
        public readonly StoryRefKind Kind;
        public StoryRefAttribute(StoryRefKind kind) => Kind = kind;
    }
}
