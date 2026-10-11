using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace QAOffice
{
    // A person placed in the scene. Leave Character Id empty for the player; otherwise it must match a
    // character id in story.json. Home is wherever the actor is placed in the scene.
    // To use a rigged model (e.g. Mixamo), put it as a child and disable the Body renderers; an Animator
    // found in children receives a "Speed" float if it has that parameter.
    public class Actor : MonoBehaviour
    {
        [SerializeField] string characterId;
        [SerializeField] TextMesh label;
        [SerializeField] float walkSpeed = 2.2f;
        [Tooltip("Chance, each idle check (every 8-16 s), to wander over to a coworker's desk.")]
        [SerializeField, Range(0, 1)] float wanderChance = 0.4f;
        [Tooltip("Ground speed (units/s) at which the walk animation's feet don't slide; the walk plays faster or slower to match actual speed.")]
        [SerializeField] float walkAnimSpeed = 1.55f;

        public string CharacterId => characterId;
        public CharacterData Data { get; private set; }
        public bool IsPlayer => string.IsNullOrEmpty(characterId);
        public Vector3 Home { get; private set; }
        public Quaternion HomeRotation { get; private set; }
        public bool Held { get; private set; }

        Vector3? _target;
        readonly Queue<Vector3> _path = new Queue<Vector3>();
        static NavMeshPath _navPath;
        Action _onArrive;
        Animator _animator;
        bool _hasSpeedParam, _hasWorkingParam, _hasWalkRateParam;
        float _idleTimer;
        bool _visiting;
        Office _office;

        public void Bind(Office office, CharacterData data)
        {
            _office = office;
            Data = data;
            Home = transform.position;
            HomeRotation = transform.rotation;
            _idleTimer = UnityEngine.Random.Range(4f, 12f);
            if (label != null) label.text = data == null ? "You" : data.name;

            _animator = GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                foreach (var p in _animator.parameters)
                {
                    if (p.name == "Speed") _hasSpeedParam = true;
                    if (p.name == "Working") _hasWorkingParam = true;
                    if (p.name == "WalkRate") _hasWalkRateParam = true;
                }
                // A rigged model (e.g. Mixamo) is present: hide the placeholder capsule but keep its collider for taps.
                var body = transform.Find("Body");
                if (body != null)
                    foreach (var r in body.GetComponentsInChildren<Renderer>()) r.enabled = false;
            }
        }

        // Walks around obstacles using the office NavMesh. Never cuts through walls: if the spot can't be
        // reached, the character stops at the closest point the path gets to.
        public void WalkTo(Vector3 target, Action onArrive = null)
        {
            _onArrive = onArrive;
            _path.Clear();
            _navPath ??= new NavMeshPath();
            bool haveStart = NavMesh.SamplePosition(transform.position, out var from, 1.5f, NavMesh.AllAreas) ||
                             NavMesh.SamplePosition(transform.position, out from, 4f, NavMesh.AllAreas);
            if (haveStart && NavMesh.SamplePosition(target, out var to, 2f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, _navPath) &&
                _navPath.status != NavMeshPathStatus.PathInvalid)
            {
                foreach (var corner in _navPath.corners) _path.Enqueue(new Vector3(corner.x, Home.y, corner.z));
                // Finish on the exact spot (e.g. their seat at a desk) only when the path really reaches it.
                if (_navPath.status == NavMeshPathStatus.PathComplete && (to.position - target).sqrMagnitude < 1.0f * 1.0f)
                    _path.Enqueue(new Vector3(target.x, Home.y, target.z));
            }
            else if (!haveStart)
            {
                // No walkable area anywhere near (e.g. no NavMesh at all): fall back to a straight line.
                _path.Enqueue(new Vector3(target.x, Home.y, target.z));
            }
            if (_path.Count == 0)
            {
                _target = null;
                var cb = _onArrive;
                _onArrive = null;
                cb?.Invoke();
                return;
            }
            _target = _path.Dequeue();
        }

        public void Teleport(Vector3 pos)
        {
            _target = null;
            _path.Clear();
            _onArrive = null;
            _visiting = false;
            transform.position = new Vector3(pos.x, Home.y, pos.z);
        }

        public void GoHomeInstant()
        {
            Teleport(Home);
            transform.rotation = HomeRotation;
        }

        public void Hold(bool hold)
        {
            Held = hold;
            if (hold)
            {
                _target = null;
                _path.Clear();
                _onArrive = null;
            }
            else if (!IsPlayer && (transform.position - Home).sqrMagnitude > 0.01f)
            {
                _visiting = false;
                WalkTo(Home, () => transform.rotation = HomeRotation);
            }
        }

        public void Face(Vector3 point)
        {
            var d = point - transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
        }

        void Update()
        {
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
                    if (_path.Count > 0)
                    {
                        _target = _path.Dequeue();
                        if (_hasSpeedParam) _animator.SetFloat("Speed", moved);
                        return;
                    }
                    _target = null;
                    var cb = _onArrive;
                    _onArrive = null;
                    cb?.Invoke();
                }
            }
            if (_hasSpeedParam) _animator.SetFloat("Speed", moved);
            if (_hasWalkRateParam && moved > 0.1f) _animator.SetFloat("WalkRate", Mathf.Clamp(moved / walkAnimSpeed, 0.5f, 3f));
            if (_hasWorkingParam) _animator.SetBool("Working", IsWorking());

            if (IsPlayer || Data == null || Held || _target != null || GameManager.I == null || !GameManager.I.ClockRunning) return;
            Wander();
        }

        // Sitting at their own desk with nothing else going on: everyone (player included) types away.
        bool IsWorking() =>
            _target == null && !Held && (transform.position - Home).sqrMagnitude <= 0.04f;

        // Set dressing: wander over to a coworker's desk, chat for a bit, come back.
        void Wander()
        {
            _idleTimer -= Time.deltaTime;
            if (_idleTimer > 0) return;
            if (_visiting)
            {
                _visiting = false;
                WalkTo(Home, () => transform.rotation = HomeRotation);
                _idleTimer = UnityEngine.Random.Range(8f, 16f);
                return;
            }
            _idleTimer = UnityEngine.Random.Range(8f, 16f);
            if (UnityEngine.Random.value > wanderChance) return;
            var other = _office.RandomOtherNpc(this);
            if (other == null) return;
            var spot = other.Home + other.HomeRotation * new Vector3(UnityEngine.Random.Range(-0.8f, 0.8f), 0, -0.9f);
            _visiting = true;
            _idleTimer = UnityEngine.Random.Range(5f, 9f);
            WalkTo(spot, () => Face(other.transform.position));
        }
    }
}
