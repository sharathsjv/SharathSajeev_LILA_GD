using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // Shows only the room the player is standing in (plus a glimpse through its doorways); the other rooms of
    // this interior are hidden. Things are assigned to rooms by name:
    //   "<Room>_..."           belongs to that room (e.g. Liv_Couch, Kit_Fridge, Kit_Bulb)
    //   "Lead_<A>_<B>..."      a bit of room A seen from room B (floor past a doorway): shown in either room
    //   "Trim_<A>_<B>..."      a door frame between A and B: shown in either room
    // Anything else is always shown.
    public class RoomVisibility : MonoBehaviour
    {
        [Serializable]
        public class Room
        {
            public string tag;
            [Tooltip("Floor area in this interior's local X/Z (x, z, width, depth).")]
            public Rect area;
        }

        [SerializeField] Room[] rooms;

        readonly List<(Renderer r, string a, string b)> _renderers = new List<(Renderer, string, string)>();
        readonly List<(Light l, string a, string b)> _lights = new List<(Light, string, string)>();
        string _current;

        public string CurrentRoom => _current;

        void Awake()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (Owners(r.name, out var a, out var b)) _renderers.Add((r, a, b));
            foreach (var l in GetComponentsInChildren<Light>(true))
                if (Owners(l.name, out var a, out var b)) _lights.Add((l, a, b));
        }

        bool Owners(string name, out string a, out string b)
        {
            a = b = null;
            var parts = name.Split('_');
            if (parts.Length >= 3 && (parts[0] == "Lead" || parts[0] == "Trim") && IsRoom(parts[1]) && IsRoom(parts[2]))
            {
                a = parts[1];
                b = parts[2];
                return true;
            }
            if (parts.Length >= 2 && IsRoom(parts[0]))
            {
                a = b = parts[0];
                return true;
            }
            return false;
        }

        bool IsRoom(string tag) => Array.Exists(rooms, r => r.tag == tag);

        void OnEnable() => _current = null;

        void LateUpdate()
        {
            var g = Game.I;
            if (g == null) return;
            var p = transform.InverseTransformPoint(g.Player.transform.position);
            string now = _current;
            foreach (var room in rooms)
                if (room.area.Contains(new Vector2(p.x, p.z))) { now = room.tag; break; }
            if (now == null && rooms.Length > 0) now = rooms[0].tag;
            if (now == _current) return;
            _current = now;
            foreach (var (r, a, b) in _renderers) if (r != null) r.enabled = a == now || b == now;
            foreach (var (l, a, b) in _lights) if (l != null) l.enabled = a == now || b == now;
        }
    }
}
