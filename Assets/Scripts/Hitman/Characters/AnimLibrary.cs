using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // The Mixamo clips the hitman characters can play by name (poses held while doing something, and one-off
    // actions). Lives in Resources/HitmanAnims; names are the Mixamo names, e.g. "Sitting Drinking".
    [CreateAssetMenu(menuName = "Hitman/Anim Library", fileName = "HitmanAnims")]
    public class AnimLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string name;
            public AnimationClip clip;
        }

        [Tooltip("The placeholder clip in the controller's Pose state (swapped for the pose being held).")]
        public AnimationClip posePlaceholder;
        [Tooltip("The placeholder clip in the controller's Action state (swapped for the one-off action).")]
        public AnimationClip actionPlaceholder;
        [Tooltip("The placeholder clip in the Walk state (swapped for walk variants, e.g. walking while texting).")]
        public AnimationClip walkClip;
        public List<Entry> clips = new List<Entry>();

        static AnimLibrary _i;
        Dictionary<string, AnimationClip> _map;

        public static AnimLibrary I => _i != null ? _i : (_i = Resources.Load<AnimLibrary>("HitmanAnims"));

        public AnimationClip Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_map == null)
            {
                _map = new Dictionary<string, AnimationClip>();
                foreach (var e in clips) if (e.clip != null) _map[e.name] = e.clip;
            }
            if (_map.TryGetValue(name, out var c)) return c;
            Debug.LogWarning($"[Hitman] No animation called '{name}'.");
            return null;
        }
    }
}
