using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // Adds actions to the tap menu that depend on what's going on (poison his food, close the window, ask for a
    // job...). Put it on the same object as a Usable, or on a person.
    public interface IActionSource
    {
        void AddActions(ActionMenu menu);
    }

    public class ActionMenu
    {
        public readonly List<(string label, Action act)> Items = new List<(string, Action)>();
        public string Title;
        readonly PlayerController _player;
        readonly Transform _at;

        public ActionMenu(PlayerController player, Transform at, string title)
        {
            _player = player;
            _at = at;
            Title = title;
        }

        // Done right where the player is (talking from across a room, reading something in the pockets).
        public void Add(string label, Action act) => Items.Add((label, act));

        // Walk over to it first (to `stand` if given, else next to the tapped thing), then do it.
        public void AddWalk(string label, Action act, Transform stand = null, bool sit = false) =>
            Items.Add((label, () => _player.GoThen(stand != null ? stand : _at, stand != null, act, sit)));
    }
}
