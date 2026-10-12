using System;
using UnityEngine;

namespace Hitman
{
    public enum Activity { Stand, Sit, Work, Drink, Eat, Sleep, PassedOut, Hidden }

    // A person's daily routine: at each time they head to a place and do an activity there.
    // Steps with Options pick one option per day at random by weight (e.g. 75% couch, 25% bed).
    [CreateAssetMenu(menuName = "Hitman/Routine", fileName = "Routine")]
    public class Routine : ScriptableObject
    {
        public string displayName;
        [Tooltip("Portrait (shown on the contract email, yellow pages, etc.).")]
        public Sprite photo;
        [TextArea(1, 3)] public string info;
        public string address;
        public string phone;
        [Tooltip("Pose they hold when standing about (AnimLibrary name, e.g. \"Drunk Idle\"). Empty = breathing idle.")]
        public string standPose;
        [Tooltip("Walk cycle (AnimLibrary name, e.g. \"Walking With Shopping Bag\"). Empty = normal walk.")]
        public string walk;
        [Tooltip("Steps in time order. Before the first step of the day, they're wherever the last step left them.")]
        public RoutineStep[] steps;
    }

    [Serializable]
    public class RoutineStep
    {
        [Tooltip("24h time, e.g. 08:45")]
        public string time;
        [Tooltip("Place id, e.g. suburbs.house4.couch. Ignored when Options are set.")]
        public string place;
        public Activity activity;
        [Tooltip("Optional: pick one of these per day instead (weights don't need to add up to 1).")]
        public RoutineOption[] options;
        [Tooltip("Pose held during this step (AnimLibrary name, e.g. \"Drinking\"). Empty = picked from the activity.")]
        public string pose;
        [Tooltip("Designer note, not shown in game.")]
        [TextArea(1, 2)] public string note;
        [Tooltip("What the player writes down when they watch this step and tap Document (empty = can't be documented).")]
        [TextArea(1, 3)] public string documented;
        [Tooltip("Elimination situation number from the design doc (1-5), 0 = none. Documented openings are highlighted in the notebook.")]
        public int opening;
        [Tooltip("What they say during this step, overheard when the player is close enough.")]
        public string[] chatter;
        [Tooltip("Noted in the notebook when the player overhears this step's chatter (empty = nothing to note).")]
        [TextArea(1, 2)] public string overheard;

        public float Minutes
        {
            get
            {
                var parts = (time ?? "0:0").Split(':');
                int h = parts.Length > 0 && int.TryParse(parts[0], out var hh) ? hh : 0;
                int m = parts.Length > 1 && int.TryParse(parts[1], out var mm) ? mm : 0;
                return h * 60 + m;
            }
        }
    }

    [Serializable]
    public class RoutineOption
    {
        public float weight = 1;
        public string place;
        public Activity activity;
    }
}
