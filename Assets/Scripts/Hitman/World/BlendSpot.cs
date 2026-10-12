using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // Somewhere the player can blend in: a free desk to type at, the water cooler, the coffee machine, a bar stool.
    // Reflex vision highlights these. Needs a Usable with a "blend" action on the same object.
    [RequireComponent(typeof(Usable))]
    public class BlendSpot : MonoBehaviour
    {
        [Tooltip("What the player looks like they're doing, e.g. \"Type at the desk\".")]
        [SerializeField] string doing = "Blend in";
        [Tooltip("Where the player stands or sits (defaults to the Usable's stand point).")]
        [SerializeField] Transform stand;
        [SerializeField] bool sit;
        [Tooltip("Pose the player holds here (AnimLibrary name). Empty = typing when sitting, idle when standing.")]
        [SerializeField] string pose;

        public static readonly List<BlendSpot> All = new List<BlendSpot>();

        public string Doing => doing;
        public bool Sit => sit;
        public string Pose => pose;
        public Transform Stand
        {
            get
            {
                if (stand != null) return stand;
                var sp = GetComponent<Usable>().StandPoint;
                return sp != null ? sp : transform;
            }
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);
    }
}
