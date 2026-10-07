using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace QAOffice
{
    // Taps on the diorama: coworker = go talk, your desk = use the computer, floor = walk there.
    public class WorldInput : MonoBehaviour
    {
        readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

        void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            var gm = GameManager.I;
            if (!gm.ClockRunning || gm.UI.Modal) return;

            var pos = pointer.position.ReadValue();
            if (OverUI(pos)) return;

            var office = gm.Office;
            if (!Physics.Raycast(office.Cam.ScreenPointToRay(pos), out var hit, 200f)) return;

            var actor = hit.collider.GetComponentInParent<Actor>();
            if (actor != null)
            {
                if (!actor.IsPlayer) office.Approach(actor);
                return;
            }
            var it = hit.collider.GetComponentInParent<Interactable>();
            if (it == null) return;
            if (it.kind == Interactable.Kind.PlayerDesk) office.GoToDesk();
            else office.WalkPlayer(hit.point);
        }

        bool OverUI(Vector2 pos)
        {
            if (EventSystem.current == null) return false;
            _uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pos }, _uiHits);
            return _uiHits.Count > 0;
        }
    }
}
