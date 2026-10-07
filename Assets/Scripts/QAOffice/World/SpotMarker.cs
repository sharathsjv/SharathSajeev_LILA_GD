using UnityEngine;

namespace QAOffice
{
    // Visible-in-editor marker for empty spots such as meeting seats (arrow = facing).
    public class SpotMarker : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            var p = transform.position + Vector3.up * 0.5f;
            Gizmos.color = new Color(0.3f, 0.8f, 1f);
            Gizmos.DrawWireSphere(p, 0.3f);
            Gizmos.DrawLine(p, p + transform.forward * 0.6f);
        }
    }
}
