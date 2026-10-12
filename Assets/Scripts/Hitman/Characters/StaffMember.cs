using UnityEngine;

namespace Hitman
{
    // Someone who works somewhere (bartender, cashier, guard, office worker, barber): stands or sits at their post
    // during working hours, optionally wandering between a few spots (the bartender pops into the kitchen).
    [RequireComponent(typeof(CharacterMover))]
    public class StaffMember : MonoBehaviour
    {
        [SerializeField] string displayName = "Staff";
        [Tooltip("Spots they move between; they start at the first. Empty = stay put.")]
        [SerializeField] Transform[] posts;
        [Tooltip("Pose held at each post (AnimLibrary names, same order as Posts; empty = idle / typing when seated).")]
        [SerializeField] string[] postPoses;
        [Tooltip("Real seconds at each post.")]
        [SerializeField] Vector2 stay = new Vector2(20, 40);
        [SerializeField] bool sitAtFirstPost;
        [Tooltip("Working hours (24h). Outside them they're not there.")]
        [SerializeField] Vector2 hours = new Vector2(0, 24);

        CharacterMover _mover;
        int _at;
        float _leaveAt;
        bool _here = true;

        public string DisplayName => displayName;
        // At work right now (inside their working hours).
        public bool OnDuty => _here;

        void Awake()
        {
            _mover = GetComponent<CharacterMover>();
            if (string.IsNullOrEmpty(displayName)) displayName = name;
        }

        void OnEnable()
        {
            if (posts != null && posts.Length > 0 && posts[0] != null)
            {
                _at = 0;
                _mover.Teleport(posts[0].position, posts[0].rotation);
                _mover.Seated = sitAtFirstPost;
                PoseAt(0);
            }
            _leaveAt = Time.time + Random.Range(stay.x, stay.y);
        }

        void PoseAt(int i)
        {
            var pose = postPoses != null && i < postPoses.Length ? postPoses[i] : null;
            if (string.IsNullOrEmpty(pose)) _mover.ClearPose();
            else _mover.SetPose(pose);
        }

        void Update()
        {
            var g = Game.I;
            if (g == null) return;
            float h = (g.Minutes / 60f) % 24f;
            bool here = h >= hours.x && h < hours.y;
            if (here != _here)
            {
                _here = here;
                foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = here;
                foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = here;
            }
            if (!here || posts == null || posts.Length < 2 || _mover.Moving || Time.time < _leaveAt) return;
            _at = (_at + 1) % posts.Length;
            var to = posts[_at];
            _mover.WalkTo(to.position, () =>
            {
                transform.rotation = to.rotation;
                _mover.Seated = sitAtFirstPost && _at == 0;
                PoseAt(_at);
                _leaveAt = Time.time + Random.Range(stay.x, stay.y);
            });
        }
    }
}
