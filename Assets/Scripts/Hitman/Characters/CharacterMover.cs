using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Hitman
{
    // Walks a character along the district NavMesh and drives the shared animator
    // (Speed, WalkRate, Working = seated pose). Used by the player and by NPC avatars.
    [DefaultExecutionOrder(-100)]     // undoes last frame's seat shift before anything else looks at the body
    public class CharacterMover : MonoBehaviour
    {
        [SerializeField] float walkSpeed = 2.2f;
        [SerializeField] float walkAnimSpeed = 1.55f;

        public bool Moving => _target != null;
        public bool Seated { get; set; }
        public float WalkSpeed { get => walkSpeed; set => walkSpeed = value; }

        readonly Queue<Vector3> _path = new Queue<Vector3>();
        static NavMeshPath _navPath;
        Vector3? _target;
        Action _onArrive;
        Animator _animator;
        bool _speedParam, _rateParam, _workParam;

        // ---- sitting on the actual seat ----
        // The seated (typing) pose puts the hips behind where the character stands, so on its own it floats
        // behind chairs. While seated, the body is shifted so the hips sit on the nearest seat.
        static readonly string[] SeatWords = { "Chair", "Stool", "Couch", "Sofa", "Booth", "Bench", "Seat" };
        static readonly Collider[] _near = new Collider[24];
        Transform _hips, _body;
        Vector3? _seat;             // world point the hips rest on
        bool _wasSitting;
        float _nextSeatLook;
        Vector3 _seatFrom;
        Vector3 _shift;             // applied in LateUpdate, removed again at the start of the next frame

        void Awake() => BindAnimator();

        public void BindAnimator()
        {
            _animator = GetComponentInChildren<Animator>();
            _body = _animator != null ? _animator.transform : null;
            _hips = null;
            if (_animator != null && _animator.isHuman) _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            if (_hips == null && _body != null)
                foreach (var t in _body.GetComponentsInChildren<Transform>()) if (t.name.EndsWith("Hips")) { _hips = t; break; }
            _speedParam = _rateParam = _workParam = false;
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            foreach (var p in _animator.parameters)
            {
                if (p.name == "Speed") _speedParam = true;
                if (p.name == "WalkRate") _rateParam = true;
                if (p.name == "Working") _workParam = true;
            }
        }

        public void WalkTo(Vector3 target, Action onArrive = null)
        {
            Seated = false;
            _onArrive = onArrive;
            _path.Clear();
            _navPath ??= new NavMeshPath();
            bool haveStart = NavMesh.SamplePosition(transform.position, out var from, 1.5f, NavMesh.AllAreas) ||
                             NavMesh.SamplePosition(transform.position, out from, 4f, NavMesh.AllAreas);
            if (haveStart && NavMesh.SamplePosition(target, out var to, 2f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _navPath) &&
                _navPath.status != NavMeshPathStatus.PathInvalid)
            {
                foreach (var c in _navPath.corners) _path.Enqueue(Flat(c));
                if (_navPath.status == NavMeshPathStatus.PathComplete && (to.position - target).sqrMagnitude < 1f)
                    _path.Enqueue(Flat(target));
            }
            else if (!haveStart) _path.Enqueue(Flat(target));

            if (_path.Count == 0) { Arrive(); return; }
            _target = _path.Dequeue();
        }

        public void Stop()
        {
            _path.Clear();
            _target = null;
            _onArrive = null;
        }

        public void Teleport(Vector3 pos, Quaternion? rot = null)
        {
            Stop();
            transform.position = pos;
            if (rot.HasValue) transform.rotation = rot.Value;
        }

        public void Face(Vector3 point)
        {
            var d = point - transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
        }

        Vector3 Flat(Vector3 p) => new Vector3(p.x, transform.position.y, p.z);

        void Arrive()
        {
            _target = null;
            var cb = _onArrive;
            _onArrive = null;
            cb?.Invoke();
        }

        void LateUpdate()
        {
            if (_body == null || _hips == null || _body == transform) return;
            bool sitting = Seated && _target == null;
            // Look for the seat on sitting down, and keep looking while there isn't one: characters often sit
            // down while their room is switched off (no colliders yet) and only appear later.
            bool moved = (transform.position - _seatFrom).sqrMagnitude > 0.09f;
            if (sitting && (!_wasSitting || moved || (!_seat.HasValue && Time.time >= _nextSeatLook)))
            {
                _seat = FindSeat();
                _seatFrom = transform.position;
                _nextSeatLook = Time.time + 0.5f;
            }
            if (!sitting) _seat = null;
            _wasSitting = sitting;
            if (!sitting || !_seat.HasValue) { _shift = Vector3.zero; return; }
            // Hips error against the seat, smoothed (the typing animation sways a little).
            _shift = Vector3.Lerp(_shift, _seat.Value - _hips.position, 0.5f);
            _body.position += _shift;
        }

        void UndoShift()
        {
            if (_body != null && _shift != Vector3.zero) _body.position -= _shift;
        }

        Vector3? FindSeat()
        {
            var origin = transform.position + Vector3.up * 0.45f;
            int n = Physics.OverlapSphereNonAlloc(origin, 0.9f, _near);
            Collider best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = _near[i];
                if (c.transform.IsChildOf(transform) || c.name.Contains("Back") || c.name.Contains("Leg")) continue;
                bool seat = false;
                foreach (var w in SeatWords) if (c.name.Contains(w)) { seat = true; break; }
                if (!seat) continue;
                float d = (c.bounds.ClosestPoint(origin) - origin).sqrMagnitude;   // (mesh colliders can't do ClosestPoint)
                d += 0.01f * (c.bounds.center - origin).sqrMagnitude;               // ties: the one we're most inside
                if (d < bestD) { bestD = d; best = c; }
            }
            if (best == null) return null;
            // Where on it: as close to where we stand as fits (couches, benches), else its middle (chairs).
            var b = best.bounds;
            var p = transform.position;
            float sx = Mathf.Max(0, b.extents.x - 0.25f), sz = Mathf.Max(0, b.extents.z - 0.25f);
            var xz = new Vector3(Mathf.Clamp(p.x, b.center.x - sx, b.center.x + sx), 0, Mathf.Clamp(p.z, b.center.z - sz, b.center.z + sz));
            float top = b.max.y;
            if (best.Raycast(new Ray(new Vector3(xz.x, b.max.y + 0.2f, xz.z), Vector3.down), out var hit, b.size.y + 0.5f)) top = hit.point.y;
            return new Vector3(xz.x, top + 0.07f, xz.z);
        }

        void Update()
        {
            UndoShift();
            float moved = 0;
            if (_target is Vector3 t)
            {
                var pos = transform.position;
                var next = Vector3.MoveTowards(pos, t, walkSpeed * Time.deltaTime);
                moved = (next - pos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                Face(t);
                transform.position = next;
                if ((next - t).sqrMagnitude < 0.0004f)
                {
                    if (_path.Count > 0) _target = _path.Dequeue();
                    else Arrive();
                }
            }
            if (_animator == null) return;
            if (_speedParam) _animator.SetFloat("Speed", moved);
            if (_rateParam && moved > 0.1f) _animator.SetFloat("WalkRate", Mathf.Clamp(moved / walkAnimSpeed, 0.5f, 3f));
            if (_workParam) _animator.SetBool("Working", Seated && _target == null);
        }
    }
}
