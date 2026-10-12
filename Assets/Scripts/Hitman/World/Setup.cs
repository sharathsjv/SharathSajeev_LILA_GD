using UnityEngine;

namespace Hitman
{
    // Marks a world object (or person) that takes part in the contract: the mop bucket, the coffee machine,
    // Kevin's windows, the bartender... The Contract decides what you can do with it right now.
    public class Setup : MonoBehaviour, IActionSource
    {
        public enum Kind
        {
            BarPlate, Bartender, MopBucket, MensRoomFloor, CoffeeMachine, PlantPot, Stove, Window, KevinsDoor,
            DeliveryPerson, ForgottenCard, Guard, Receptionist
        }

        [SerializeField] Kind kind;
        [Tooltip("Window: starts open.")]
        [SerializeField] bool startsOpen;
        [Tooltip("Window: the glass pane (hidden while open).")]
        [SerializeField] Renderer pane;

        public Kind Type => kind;
        public bool StartsOpen => startsOpen;
        public Renderer Pane => pane;

        public void Init(Kind k) => kind = k;

        public void AddActions(ActionMenu menu)
        {
            if (Contract.I != null) Contract.I.AddActions(this, menu);
        }
    }
}
