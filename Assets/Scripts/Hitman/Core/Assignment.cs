using UnityEngine;

namespace Hitman
{
    // A contract: the email that arrives on the laptop, and who it's about.
    [CreateAssetMenu(menuName = "Hitman/Assignment", fileName = "Assignment")]
    public class Assignment : ScriptableObject
    {
        public string sender = "Anonymous";
        public string subject;
        [TextArea(4, 10)] public string body;
        public Routine target;
        [Tooltip("Lines the player jots down in their notepad after breakfast on day 1 (they can edit them).")]
        [TextArea(2, 6)] public string startingNotes;
    }
}
