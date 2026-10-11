using UnityEngine;

namespace Hitman
{
    // A straight path for street life: a sidewalk for passers-by or one direction of traffic for cars.
    // Drag the two end points (children "A" and "B") to fit the street.
    public class Lane : MonoBehaviour
    {
        public enum Kind { Sidewalk, Road }

        [SerializeField] Kind kind;
        [SerializeField] Transform a;
        [SerializeField] Transform b;
        [Tooltip("Sidewalks: people walk both ways. Roads: traffic only goes A to B (add another lane for the other way).")]
        [SerializeField] bool bothWays = true;

        public Kind LaneKind => kind;
        public Vector3 A => a.position;
        public Vector3 B => b.position;
        public bool BothWays => kind == Kind.Sidewalk && bothWays;

        void OnDrawGizmos()
        {
            if (a == null || b == null) return;
            Gizmos.color = kind == Kind.Road ? new Color(1f, 0.8f, 0.2f) : new Color(0.4f, 1f, 0.6f);
            Gizmos.DrawLine(a.position + Vector3.up * 0.1f, b.position + Vector3.up * 0.1f);
            Gizmos.DrawWireSphere(b.position + Vector3.up * 0.1f, 0.25f);
        }
    }
}
