using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // A named spot in the world (e.g. "suburbs.house4.couch"). Routines and travel refer to places by id.
    // The arrow (forward) is the way a character faces when they arrive.
    public class Place : MonoBehaviour
    {
        [SerializeField] string id;
        public string Id => id;
        public Area Area => GetComponentInParent<Area>(true);

        static readonly Dictionary<string, Place> All = new Dictionary<string, Place>();

        public static Place Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (All.TryGetValue(id, out var p) && p != null) return p;
            // Inactive districts don't run Awake, so fall back to a search (cached afterwards).
            foreach (var place in Resources.FindObjectsOfTypeAll<Place>())
                if (place.id == id && place.gameObject.scene.IsValid()) { All[id] = place; return place; }
            return null;
        }

        void OnDrawGizmos()
        {
            var p = transform.position + Vector3.up * 0.05f;
            Gizmos.color = new Color(1f, 0.6f, 0.2f);
            Gizmos.DrawWireSphere(p, 0.25f);
            Gizmos.DrawLine(p, p + transform.forward * 0.5f);
        }
    }
}
