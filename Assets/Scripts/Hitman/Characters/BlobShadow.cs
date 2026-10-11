using UnityEngine;

namespace Hitman
{
    // A soft dark blob on the ground under a character or car (there are no real-time shadows).
    // Hidden along with the character's other renderers.
    public class BlobShadow : MonoBehaviour
    {
        [SerializeField] Vector2 size = new Vector2(0.8f, 0.8f);
        [SerializeField] float groundHeight = 0.03f;

        static Material _material;
        Transform _blob;

        public static BlobShadow Attach(GameObject go, Vector2 size)
        {
            var b = go.GetComponent<BlobShadow>();
            if (b == null) b = go.AddComponent<BlobShadow>();
            b.size = size;
            b.Build();
            return b;
        }

        void Awake() => Build();

        void Build()
        {
            if (_blob == null)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "Blob Shadow";
                q.transform.SetParent(transform, false);
                var r = q.GetComponent<Renderer>();
                if (_material == null) _material = Resources.Load<Material>("BlobShadow");
                r.sharedMaterial = _material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                _blob = q.transform;
            }
            _blob.localScale = new Vector3(size.x / Mathf.Max(0.01f, transform.lossyScale.x), size.y / Mathf.Max(0.01f, transform.lossyScale.z), 1);
            Place();
        }

        void LateUpdate() => Place();

        // Flat on the ground under the character, whatever pose the body is in.
        void Place()
        {
            var p = transform.position;
            _blob.SetPositionAndRotation(new Vector3(p.x, p.y + groundHeight, p.z), Quaternion.Euler(90, transform.eulerAngles.y, 0));
        }
    }
}
