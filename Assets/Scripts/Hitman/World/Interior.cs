using UnityEngine;

namespace Hitman
{
    // The inside of a building, built as its own space. You get in through the door on the street
    // (outsideDoor, in the district) and come out the same way (insideDoor, in here).
    // Upper floors are interiors too, reached from another interior by elevator or stairs instead of from the
    // street: set Parent, and where you board (in the parent) and arrive (in here).
    public class Interior : Area
    {
        [SerializeField] District district;
        [Tooltip("Spot on the street in front of the door.")]
        [SerializeField] Place outsideDoor;
        [Tooltip("Spot just inside the door.")]
        [SerializeField] Place insideDoor;

        [Header("Reached from inside another building (elevator / stairs)")]
        [Tooltip("Leave empty for interiors entered from the street.")]
        [SerializeField] Interior parent;
        [Tooltip("Where you arrive in here (in front of the elevator, top of the stairs).")]
        [SerializeField] Place linkHere;
        [Tooltip("Where you board in the parent interior.")]
        [SerializeField] Place linkInParent;
        [Tooltip("Shown during the transition, e.g. \"Elevator\" or \"Stairs\".")]
        [SerializeField] string linkLabel = "Elevator";

        public override District Map => parent != null ? parent.Map : district;
        public Place OutsideDoor => outsideDoor;
        public Place InsideDoor => insideDoor;
        public Interior Parent => parent;
        public Place LinkHere => linkHere;
        public Place LinkInParent => linkInParent;
        public string LinkLabel => linkLabel;

        // The area you step into on your way out: the parent floor, or the street.
        public Area Outside => parent != null ? (Area)parent : district;
    }
}
