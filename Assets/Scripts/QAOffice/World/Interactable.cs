using UnityEngine;

namespace QAOffice
{
    // Marks clickable world props.
    public class Interactable : MonoBehaviour
    {
        public enum Kind { PlayerDesk, Floor, PingPongTable }
        public Kind kind;
    }
}
