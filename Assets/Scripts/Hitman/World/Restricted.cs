using UnityEngine;

namespace Hitman
{
    // A place the player isn't supposed to be. Put it on the Interior.
    //  Security: a guard checks for a pass as you come in (keycard, a suit, or the job ad) and walks you out without one.
    //  Staff only: you have to blend in (use the highlighted spots); hang about and someone calls you out and you're thrown out.
    //  Private: someone's home. Locked without the key, and if they catch you inside they call the cops.
    public class Restricted : MonoBehaviour
    {
        public enum Kind { Security, StaffOnly, Private }

        [SerializeField] Kind kind;
        [Tooltip("Security: the guard. Staff only: unused. Private: unused.")]
        [SerializeField] Watcher guard;
        [Tooltip("Private: the item that unlocks the front door.")]
        [SerializeField] string key = "housekey";
        [Tooltip("Private: whose home it is.")]
        [SerializeField] RoutineCharacter owner;
        [Tooltip("Seconds someone has to watch you not fitting in before they call you out.")]
        [SerializeField] float patience = 5f;

        public Kind Type => kind;
        public Watcher Guard => guard;
        public RoutineCharacter Owner => owner;
        public float Patience => patience;
        public Interior Interior => GetComponent<Interior>();

        public static Restricted Of(Area a) => a != null ? a.GetComponent<Restricted>() : null;

        // Locked doors.
        public static bool MayEnter(Interior into)
        {
            var r = Of(into);
            if (r == null || r.kind != Kind.Private) return true;
            var g = Game.I;
            if (g.S.Has("unlocked." + into.Id)) return true;
            if (g.S.Holding(r.key))
            {
                g.S.Set("unlocked." + into.Id);
                g.UI.Toast("You let yourself in with the key.");
                return true;
            }
            g.UI.Toast("It's locked.");
            return false;
        }
    }
}
