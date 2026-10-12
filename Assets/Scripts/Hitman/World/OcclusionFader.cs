using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // Isometric see-through: tall things between the camera and the player (or whoever they're following)
    // fade to a transparent copy of their material, and come back when the view is clear again.
    [RequireComponent(typeof(Camera))]
    public class OcclusionFader : MonoBehaviour
    {
        [Tooltip("A transparent URP Lit material used as the template for faded copies (keeps the shader variant in builds).")]
        [SerializeField] Material fadeTemplate;
        [SerializeField, Range(0, 1)] float fadedAlpha = 0.18f;
        [Tooltip("Only things at least this tall get faded (walls, buildings), not furniture.")]
        [SerializeField] float minHeight = 1.4f;
        [SerializeField] float probeRadius = 0.6f;
        [Tooltip("Objects (by name prefix) that are always see-through, e.g. the elevated subway overhead.")]
        [SerializeField] string[] alwaysFaded = { "Rail_Deck", "Rail_Platform" };
        [SerializeField, Range(0, 1)] float alwaysFadedAlpha = 0.25f;

        // Anything whose underside is above this is overhead: faded when in the way, and taps pass through it.
        public const float OverheadHeight = 2.2f;

        static readonly Dictionary<Renderer, Material[]> _original = new Dictionary<Renderer, Material[]>();

        // True while this collider's building is see-through (taps should go through it).
        public static bool IsFaded(Collider c) => c != null && c.TryGetComponent<Renderer>(out var r) && _original.ContainsKey(r);

        void OnDestroy() => _original.Clear();
        readonly Dictionary<Material, Material> _fadedCopies = new Dictionary<Material, Material>();
        readonly HashSet<Renderer> _hitThisFrame = new HashSet<Renderer>();
        readonly List<Renderer> _restore = new List<Renderer>();
        readonly RaycastHit[] _hits = new RaycastHit[32];
        readonly HashSet<Renderer> _permanent = new HashSet<Renderer>();
        readonly Dictionary<Material, Material> _permanentCopies = new Dictionary<Material, Material>();
        Area _fadedArea;

        // The always-see-through objects of the area the player is in.
        void FadePermanent(Area area)
        {
            _fadedArea = area;
            if (area == null) return;
            foreach (var r in area.GetComponentsInChildren<Renderer>(true))
            {
                if (_permanent.Contains(r)) continue;
                bool match = false;
                foreach (var p in alwaysFaded) if (r.name.StartsWith(p)) { match = true; break; }
                if (!match) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Faded(mats[i], alwaysFadedAlpha, _permanentCopies);
                r.sharedMaterials = mats;
                _permanent.Add(r);
            }
        }

        void LateUpdate()
        {
            var g = Game.I;
            if (g == null || fadeTemplate == null) return;
            if (g.Current != _fadedArea) FadePermanent(g.Current);
            _hitThisFrame.Clear();
            Probe(g.Player.transform.position);
            var follow = g.Player.GetComponent<PlayerController>().Following;
            if (follow != null && follow.Visible) Probe(follow.transform.position);

            foreach (var r in _hitThisFrame)
                if (!_original.ContainsKey(r))
                {
                    var mats = r.sharedMaterials;
                    _original[r] = mats;
                    var faded = new Material[mats.Length];
                    for (int i = 0; i < mats.Length; i++) faded[i] = Faded(mats[i]);
                    r.sharedMaterials = faded;
                }

            _restore.Clear();
            foreach (var kv in _original)
                if (kv.Key == null || !_hitThisFrame.Contains(kv.Key)) _restore.Add(kv.Key);
            foreach (var r in _restore)
            {
                if (r != null) r.sharedMaterials = _original[r];
                _original.Remove(r);
            }
        }

        void Probe(Vector3 target)
        {
            var aim = target + Vector3.up * 1f;
            var from = transform.position;
            var dir = aim - from;
            int n = Physics.SphereCastNonAlloc(from, probeRadius, dir.normalized, _hits, dir.magnitude - 0.8f);
            for (int i = 0; i < n; i++)
            {
                var col = _hits[i].collider;
                if (col.GetComponentInParent<CharacterMover>() != null) continue;
                // Doors (and anything else you can use) never fade: they have to stay visible and tappable.
                if (col.GetComponentInParent<Usable>() != null) continue;
                // Tall things (walls, buildings) and anything overhead (the elevated subway, fire escapes).
                if (col.bounds.size.y < minHeight && col.bounds.min.y < OverheadHeight) continue;
                foreach (var r in col.GetComponents<Renderer>()) if (!_permanent.Contains(r)) _hitThisFrame.Add(r);
            }
        }

        Material Faded(Material original) => Faded(original, fadedAlpha, _fadedCopies);

        Material Faded(Material original, float alpha, Dictionary<Material, Material> cache)
        {
            if (original == null) return null;
            if (cache.TryGetValue(original, out var m)) return m;
            m = new Material(fadeTemplate) { name = original.name + " (faded)" };
            var c = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.color;
            c.a = alpha;
            m.SetColor("_BaseColor", c);
            if (original.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", original.GetTexture("_BaseMap"));
                m.SetTextureScale("_BaseMap", original.GetTextureScale("_BaseMap"));
            }
            cache[original] = m;
            return m;
        }
    }
}
