using UnityEngine;

namespace QAOffice
{
    // Outline for world-space TextMesh labels (names, area signs): dark copies of the text drawn just
    // behind it at small offsets. Copies follow the main text's string and visibility.
    [RequireComponent(typeof(TextMesh))]
    public class OutlinedText : MonoBehaviour
    {
        [SerializeField] Color outlineColor = new Color(0.07f, 0.08f, 0.12f, 1f);
        [Tooltip("Outline width in world units.")]
        [SerializeField] float thickness = 0.035f;

        TextMesh _main;
        MeshRenderer _mainRenderer;
        TextMesh[] _copies;
        MeshRenderer[] _copyRenderers;

        void Start()
        {
            _main = GetComponent<TextMesh>();
            _mainRenderer = GetComponent<MeshRenderer>();
            const int n = 8;
            _copies = new TextMesh[n];
            _copyRenderers = new MeshRenderer[n];
            var scale = transform.lossyScale.x == 0 ? 1 : transform.lossyScale.x;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                var go = new GameObject("Outline");
                go.transform.SetParent(transform, false);
                // Local space follows the billboard: x/y are screen-aligned, +z is away from the camera.
                go.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0.5f) * (thickness / scale) + new Vector3(0, 0, 0.01f);
                var t = go.AddComponent<TextMesh>();
                t.font = _main.font;
                t.fontSize = _main.fontSize;
                t.fontStyle = _main.fontStyle;
                t.characterSize = _main.characterSize;
                t.anchor = _main.anchor;
                t.alignment = _main.alignment;
                t.lineSpacing = _main.lineSpacing;
                t.color = outlineColor;
                t.text = _main.text;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = _mainRenderer.sharedMaterial;
                _copies[i] = t;
                _copyRenderers[i] = r;
            }
        }

        void LateUpdate()
        {
            if (_copies == null) return;
            bool visible = _mainRenderer.enabled;
            for (int i = 0; i < _copies.Length; i++)
            {
                if (_copies[i].text != _main.text) _copies[i].text = _main.text;
                if (_copyRenderers[i].enabled != visible) _copyRenderers[i].enabled = visible;
            }
        }
    }
}
