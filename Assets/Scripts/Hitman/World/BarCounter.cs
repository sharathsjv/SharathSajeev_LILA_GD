namespace Hitman
{
    // The bar counter during the lunch shift: tap it to pour.
    public class BarCounter : UnityEngine.MonoBehaviour, IActionSource
    {
        public void AddActions(ActionMenu menu)
        {
            if (BarShift.Working) Game.I.UI.BarShift.AddPourActions(menu);
        }
    }
}
