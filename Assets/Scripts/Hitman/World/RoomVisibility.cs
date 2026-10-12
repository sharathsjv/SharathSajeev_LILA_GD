using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hitman
{
    // Shows only the room the player is standing in (plus a glimpse through its doorways); the other rooms of
    // this interior are hidden. Things are assigned to rooms either
    //  by name (the apartment):
    //   "<Room>_..."           belongs to that room (e.g. Liv_Couch, Kit_Fridge, Kit_Bulb)
    //   "Lead_<A>_<B>..."      a bit of room A seen from room B (floor past a doorway): shown in either room
    //   "Trim_<A>_<B>..."      a door frame between A and B: shown in either room
    //  or by position (By Position on): anything lying inside a room's area belongs to it; anything spanning
    //  several rooms (the walls between them, a long outer wall) is shown from any of the rooms it touches.
    //  Doorways between rooms get "lead" tiles made automatically (like the apartment's Lead_ pieces): a patch
    //  of the next room's floor just past the doorway, shown from both rooms, so there's something to tap to go
    //  through.
    // Anything else is always shown. People are hidden while they're in a different room from the player.
    public class RoomVisibility : MonoBehaviour
    {
        [Serializable]
        public class Room
        {
            public string tag;
            [Tooltip("Floor area in this interior's local X/Z (x, z, width, depth). Several entries can share a tag.")]
            public Rect area;
        }

        [SerializeField] Room[] rooms;
        [Tooltip("Assign things to rooms by where they are instead of by their names.")]
        [SerializeField] bool byPosition;

        readonly List<(Renderer r, string a, string b)> _renderers = new List<(Renderer, string, string)>();
        readonly List<(Renderer r, HashSet<string> tags)> _spanning = new List<(Renderer, HashSet<string>)>();
        readonly List<(Light l, string a, string b)> _lights = new List<(Light, string, string)>();
        readonly Dictionary<Component, Renderer[]> _people = new Dictionary<Component, Renderer[]>();
        string _current;
        bool _collected;
        Area _area;

        public string CurrentRoom => _current;

        [Tooltip("Rooms smaller than this (m², all of a tag's areas) get the camera to close in on them.")]
        [SerializeField] float smallRoomArea = 30f;

        // The room the player is in, if it's small enough for the camera to close in on: its centre (world) and
        // the orthographic size that fits it with some margin.
        public static bool SmallRoom(Area area, out Vector3 centre, out float size)
        {
            centre = default;
            size = 0;
            var rv = area != null ? area.GetComponent<RoomVisibility>() : null;
            // (big interiors only: the apartment is small enough to see whole)
            if (rv == null || !rv.byPosition || rv._current == null || rv.rooms == null) return false;
            Rect r = default;
            bool any = false;
            float total = 0;
            foreach (var room in rv.rooms)
            {
                if (room.tag != rv._current) continue;
                total += room.area.width * room.area.height;
                r = any ? Rect.MinMaxRect(Mathf.Min(r.xMin, room.area.xMin), Mathf.Min(r.yMin, room.area.yMin),
                                         Mathf.Max(r.xMax, room.area.xMax), Mathf.Max(r.yMax, room.area.yMax)) : room.area;
                any = true;
            }
            if (!any || total > rv.smallRoomArea) return false;
            centre = rv.transform.TransformPoint(new Vector3(r.center.x, 0, r.center.y));
            size = Mathf.Max(3.5f, Mathf.Max(r.width, r.height) * 0.7f + 1.2f);
            return true;
        }

        void Collect()
        {
            _collected = true;
            _area = GetComponent<Area>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponentInParent<CharacterMover>(true) != null) continue;      // people are handled each frame
                if (byPosition)
                {
                    var tags = Touching(r.bounds);
                    if (tags.Count == 1) foreach (var t in tags) _renderers.Add((r, t, t));
                    else if (tags.Count > 1) _spanning.Add((r, tags));
                }
                else if (Owners(r.name, out var a, out var b)) _renderers.Add((r, a, b));
            }
            if (byPosition) MakeLeads();
            foreach (var l in GetComponentsInChildren<Light>(true))
            {
                if (byPosition)
                {
                    var tag = RoomAt(l.transform.position);
                    if (tag != null) _lights.Add((l, tag, tag));
                }
                else if (Owners(l.name, out var a, out var b)) _lights.Add((l, a, b));
            }
        }

        // ---------- Doorway leads ----------

        [Tooltip("How far into the next room the lead tile reaches (m).")]
        [SerializeField] float leadDepth = 1.0f;

        void MakeLeads()
        {
            for (int i = 0; i < rooms.Length; i++)
                for (int j = 0; j < rooms.Length; j++)
                    if (i != j && rooms[i].tag != rooms[j].tag) Doorways(rooms[i], rooms[j]);
        }

        // Openings in the edge where room a meets room b (scanned from a's side; each pair is seen both ways,
        // and each pass lays the tile on b's side of the doorway).
        void Doorways(Room a, Room b)
        {
            Rect ra = a.area, rb = b.area;
            const float touch = 0.06f;
            // a's east / west edge against b
            if (Mathf.Abs(ra.xMax - rb.xMin) < touch) Scan(a, b, true, ra.xMax, Mathf.Max(ra.yMin, rb.yMin), Mathf.Min(ra.yMax, rb.yMax), 1, rb.width);
            if (Mathf.Abs(ra.xMin - rb.xMax) < touch) Scan(a, b, true, ra.xMin, Mathf.Max(ra.yMin, rb.yMin), Mathf.Min(ra.yMax, rb.yMax), -1, rb.width);
            // a's north / south edge against b
            if (Mathf.Abs(ra.yMax - rb.yMin) < touch) Scan(a, b, false, ra.yMax, Mathf.Max(ra.xMin, rb.xMin), Mathf.Min(ra.xMax, rb.xMax), 1, rb.height);
            if (Mathf.Abs(ra.yMin - rb.yMax) < touch) Scan(a, b, false, ra.yMin, Mathf.Max(ra.xMin, rb.xMin), Mathf.Min(ra.xMax, rb.xMax), -1, rb.height);
        }

        // Walk along the shared edge; where you can step straight across (navmesh on both sides, no wall in
        // between) it's a doorway.
        void Scan(Room a, Room b, bool constX, float edge, float s0, float s1, float dir, float roomDepth)
        {
            if (s1 - s0 < 0.5f) return;
            Vector3 L(float along, float off) => constX ? new Vector3(edge + off, 0, along) : new Vector3(along, 0, edge + off);
            const float step = 0.1f;
            float start = float.NaN;
            for (float t = s0 + step; t <= s1; t += step)
            {
                bool open = t < s1 - step / 2 && Passable(L(t, -0.45f * dir), L(t, 0.45f * dir));
                if (open && float.IsNaN(start)) start = t;
                if (!open && !float.IsNaN(start))
                {
                    if (t - start >= 0.5f) Lead(a, b, constX, edge, start - step / 2, t - step / 2, dir, roomDepth);
                    start = float.NaN;
                }
            }
        }

        bool Passable(Vector3 localA, Vector3 localB)
        {
            var wa = transform.TransformPoint(localA);
            var wb = transform.TransformPoint(localB);
            if (!NavMesh.SamplePosition(wa, out var ha, 0.3f, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(wb, out var hb, 0.3f, NavMesh.AllAreas)) return false;
            // Directly connected: walking across is barely longer than stepping straight over (no detour round a wall).
            if (_path == null) _path = new NavMeshPath();
            if (!NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, _path) || _path.status != NavMeshPathStatus.PathComplete) return false;
            float len = 0;
            for (int i = 1; i < _path.corners.Length; i++) len += Vector3.Distance(_path.corners[i - 1], _path.corners[i]);
            return len < 1.6f;
        }

        NavMeshPath _path;

        // A tile of b's floor just past the doorway, shown from a and from b.
        void Lead(Room a, Room b, bool constX, float edge, float s0, float s1, float dir, float roomDepth)
        {
            float depth = Mathf.Min(leadDepth, roomDepth - 0.1f);
            float w = s1 - s0 + 0.3f, mid = (s0 + s1) / 2;
            var centre = constX ? new Vector3(edge + dir * depth / 2, 0, mid) : new Vector3(mid, 0, edge + dir * depth / 2);
            var floor = FloorAt(transform.TransformPoint(centre), out float top);
            if (floor == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Lead_{b.tag}_{a.tag}";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(centre.x, transform.InverseTransformPoint(new Vector3(0, top, 0)).y + 0.012f, centre.z);
            go.transform.localScale = constX ? new Vector3(depth, 0.02f, w) : new Vector3(w, 0.02f, depth);
            go.GetComponent<Collider>().isTrigger = true;                          // tappable, but not in anyone's way
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = floor.sharedMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            _renderers.Add((r, a.tag, b.tag));
        }

        // The floor under a point (the flattest thing there that belongs to this interior).
        Renderer FloorAt(Vector3 world, out float top)
        {
            top = world.y;
            Renderer best = null;
            float bestH = float.MaxValue;
            foreach (var h in Physics.RaycastAll(world + Vector3.up * 3f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!h.collider.transform.IsChildOf(transform) || h.collider.GetComponentInParent<CharacterMover>() != null) continue;
                if (!h.collider.TryGetComponent<Renderer>(out var r) || r.sharedMaterial == null) continue;
                float hgt = h.collider.bounds.size.y;
                if (hgt > 0.3f || hgt >= bestH) continue;
                bestH = hgt;
                best = r;
                top = h.point.y;
            }
            return best;
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

        // The rooms something touches (a little inset so things just touching a wall line don't count).
        HashSet<string> Touching(Bounds world)
        {
            var lo = transform.InverseTransformPoint(world.min);
            var hi = transform.InverseTransformPoint(world.max);
            float x0 = Mathf.Min(lo.x, hi.x) + 0.12f, x1 = Mathf.Max(lo.x, hi.x) - 0.12f;
            float z0 = Mathf.Min(lo.z, hi.z) + 0.12f, z1 = Mathf.Max(lo.z, hi.z) - 0.12f;
            // thin things (walls) keep a little width so a wall on a room's edge still counts as touching it
            if (x1 < x0) { float c = (x0 + x1) / 2; x0 = c - 0.06f; x1 = c + 0.06f; }
            if (z1 < z0) { float c = (z0 + z1) / 2; z0 = c - 0.06f; z1 = c + 0.06f; }
            var box = Rect.MinMaxRect(x0, z0, x1, z1);
            var tags = new HashSet<string>();
            foreach (var room in rooms)
                if (room.area.Overlaps(box, true) || room.area.Contains(box.center)) tags.Add(room.tag);
            return tags;
        }

        string RoomAt(Vector3 world)
        {
            var p = transform.InverseTransformPoint(world);
            foreach (var room in rooms)
                if (room.area.Contains(new Vector2(p.x, p.z))) return room.tag;
            return null;
        }

        bool IsRoom(string tag) => Array.Exists(rooms, r => r.tag == tag);

        void OnEnable() => _current = null;

        void LateUpdate()
        {
            var g = Game.I;
            if (g == null) return;
            if (!_collected) Collect();
            string now = RoomAt(g.Player.transform.position) ?? _current;
            if (now == null && rooms.Length > 0) now = rooms[0].tag;
            if (now != _current)
            {
                _current = now;
                foreach (var (r, a, b) in _renderers) if (r != null) r.enabled = a == now || b == now;
                foreach (var (r, tags) in _spanning) if (r != null) r.enabled = tags.Contains(now);
                foreach (var (l, a, b) in _lights) if (l != null) l.enabled = a == now || b == now;
            }
            if (byPosition) People(g);
        }

        // Staff working here and anyone whose routine has them in here: shown only in the player's room.
        void People(Game g)
        {
            foreach (var sm in GetComponentsInChildren<StaffMember>())
                if (sm.OnDuty) ShowIfHere(sm, sm.transform.position);
            if (_area == null) return;
            foreach (var p in g.People)
                if (p.Visible && p.AreaId == _area.Id) ShowIfHere(p, p.transform.position);
        }

        void ShowIfHere(Component who, Vector3 pos)
        {
            if (!_people.TryGetValue(who, out var rs)) _people[who] = rs = who.GetComponentsInChildren<Renderer>(true);
            var room = RoomAt(pos);
            bool show = room == null || room == _current;
            foreach (var r in rs) if (r != null && r.enabled != show) r.enabled = show;
        }
    }
}
