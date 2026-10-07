using System;
using UnityEngine;

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

        public string CharacterId => characterId;
        public CharacterData Data { get; private set; }
        public bool IsPlayer => string.IsNullOrEmpty(characterId);
        public Vector3 Home { get; private set; }
        public Quaternion HomeRotation { get; private set; }
        public bool Held { get; private set; }

        Vector3? _target;
        Action _onArrive;
        Animator _animator;
        bool _hasSpeedParam;
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
                foreach (var p in _animator.parameters)
                    if (p.name == "Speed") _hasSpeedParam = true;
        }

        public void WalkTo(Vector3 target, Action onArrive = null)
        {
            _target = new Vector3(target.x, Home.y, target.z);
            _onArrive = onArrive;
        }

        public void Teleport(Vector3 pos)
        {
            _target = null;
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
                    _target = null;
                    var cb = _onArrive;
                    _onArrive = null;
                    cb?.Invoke();
                }
            }
            if (_hasSpeedParam) _animator.SetFloat("Speed", moved);

            if (IsPlayer || Data == null || Held || _target != null || GameManager.I == null || !GameManager.I.ClockRunning) return;
            Wander();
        }

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
