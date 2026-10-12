using System;
using UnityEngine;

namespace Hitman
{
    // Something the player can tap to get a menu of actions (fridge, bed, laptop, subway entrance, taxi stand,
    // drinks, stove, windows...). Each action has an id the game understands (see PlayerController.Perform).
    public class Usable : MonoBehaviour
    {
        [SerializeField] string title;
        [Tooltip("Where the player stands to use it (optional; defaults to the nearest walkable spot).")]
        [SerializeField] Transform standPoint;
        [SerializeField] UseAction[] actions;

        public string Title => title;
        public Transform StandPoint => standPoint;
        public UseAction[] Actions => actions;
    }

    [Serializable]
    public class UseAction
    {
        public string label;
        [Tooltip("laptop, eat, sleep, subway, taxi, notepad, wardrobe, or a custom id handled by the game.")]
        public string id;
        [Tooltip("Extra data for the action, e.g. the food name for 'eat'.")]
        public string arg;
        [Tooltip("Sit down while doing it (laptop, eating at a table).")]
        public bool sit;
        [Tooltip("Optional: after using it, carry on to this spot before the action happens (e.g. take the cereal to the dining table).")]
        public Transform thenGoTo;
    }
}
