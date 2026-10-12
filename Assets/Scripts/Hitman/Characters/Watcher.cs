using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // Someone who can see the player: routine characters, staff, office workers. Used for witnesses (poisoning
    // in front of someone fails the mission), restricted areas (being called out) and infamy.
    public class Watcher : MonoBehaviour
    {
        [Tooltip("Who remembers the player (infamy is kept per person). Defaults to the object's name.")]
        [SerializeField] string personId;
        [SerializeField] float viewDistance = 9f;
        [SerializeField, Range(10, 180)] float fieldOfView = 130f;
        [SerializeField] float eyeHeight = 1.6f;

        public static readonly List<Watcher> All = new List<Watcher>();
        static readonly RaycastHit[] Hits = new RaycastHit[12];

        RoutineCharacter _routine;
        StaffMember _staff;
        Renderer _renderer;

        public string PersonId => string.IsNullOrEmpty(personId) ? name : personId;
        public string DisplayName => _routine != null ? _routine.Routine.displayName : _staff != null ? _staff.DisplayName : name;
        public RoutineCharacter Routine => _routine;

        void Awake()
        {
            _routine = GetComponent<RoutineCharacter>();
            _staff = GetComponent<StaffMember>();
            _renderer = GetComponentInChildren<Renderer>(true);
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        // Awake, here and shown (not asleep, passed out, in the shower or dead).
        public bool Present
        {
            get
            {
                if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return false;
                if (_routine != null) return _routine.Visible && _routine.IsAwake;
                if (_staff != null) return _staff.OnDuty;           // (may be hidden by the room the player is in)
                return _renderer == null || _renderer.enabled;
            }
        }

        public bool CanSee(Vector3 point, float range = -1)
        {
            if (!Present) return false;
            var eye = transform.position + Vector3.up * eyeHeight;
            var target = point + Vector3.up * 1.2f;
            var to = target - eye;
            float max = range > 0 ? range : viewDistance;
            if (to.magnitude > max) return false;
            var flat = new Vector3(to.x, 0, to.z);
            if (flat.sqrMagnitude > 0.25f && Vector3.Angle(transform.forward, flat) > fieldOfView / 2) return false;
            // Anything solid in between (walls, partitions) blocks the view; people and the player don't.
            int n = Physics.RaycastNonAlloc(eye, to.normalized, Hits, to.magnitude - 0.3f);
            for (int i = 0; i < n; i++)
            {
                var c = Hits[i].collider;
                if (c.isTrigger || c.transform.IsChildOf(transform)) continue;
                if (c.GetComponentInParent<CharacterMover>() != null || c.GetComponentInParent<Watcher>() != null) continue;
                if (c.bounds.size.y < 1.2f) continue;            // tables, counters: you can see over them
                return false;
            }
            return true;
        }

        // The first person who can see this spot (except the ones given), or null.
        public static Watcher Seeing(Vector3 point, params Watcher[] except)
        {
            foreach (var w in All)
            {
                if (System.Array.IndexOf(except, w) >= 0) continue;
                if (w.CanSee(point)) return w;
            }
            return null;
        }

        public static Watcher Of(RoutineCharacter rc) => rc != null ? rc.GetComponent<Watcher>() : null;
    }
}
