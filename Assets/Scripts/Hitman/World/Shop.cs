using UnityEngine;

namespace Hitman
{
    // A shop interior: take things off the shelves ("take:<item>" actions), pay at the till ("pay"). Walking out
    // with something you haven't paid for gets the cops called.
    public class Shop : MonoBehaviour
    {
        [SerializeField] string shopName = "Shop";
        public string ShopName => shopName;

        static Shop Here => Game.I.Current != null ? Game.I.Current.GetComponent<Shop>() : null;
        static string Id(Shop s) => s.GetComponent<Area>().Id;

        public static void TakeFromShelf(string item)
        {
            var g = Game.I;
            var shop = Here;
            if (shop == null) { g.Give(item); return; }
            if (!g.Give(item, true)) return;
            g.S.unpaid.Add($"{Id(shop)}|{item}");
            g.UI.Toast($"{Items.Name(item)} (${Items.Price(item)}). Pay at the till on your way out.");
        }

        static int Owed(Shop shop, out int count)
        {
            int total = 0;
            count = 0;
            foreach (var u in Game.I.S.unpaid)
            {
                var parts = u.Split('|');
                if (parts[0] != Id(shop)) continue;
                total += Items.Price(parts[1]);
                count++;
            }
            return total;
        }

        public static void PayAtTill()
        {
            var g = Game.I;
            var shop = Here;
            if (shop == null) return;
            int total = Owed(shop, out int count);
            if (count == 0) { g.UI.Toast("Nothing to pay for."); return; }
            if (g.S.cash < total)
            {
                g.UI.ShowDialog("Cashier", $"That's ${total}. You've only got ${g.S.cash}.", new System.Collections.Generic.List<(string, System.Action)>
                {
                    ("Put it all back", () => PutBack(shop)),
                }, Cashier(shop));
                return;
            }
            g.Spend(total);
            g.S.unpaid.RemoveAll(u => u.StartsWith(Id(shop) + "|"));
            g.UI.Toast($"Paid ${total}. Thanks, come again.");
            g.Changed();
        }

        static Transform Cashier(Shop shop)
        {
            foreach (var s in shop.GetComponentsInChildren<StaffMember>()) return s.transform;
            return null;
        }

        static void PutBack(Shop shop)
        {
            var g = Game.I;
            foreach (var u in g.S.unpaid.FindAll(x => x.StartsWith(Id(shop) + "|"))) g.S.items.Remove(u.Split('|')[1]);
            g.S.unpaid.RemoveAll(u => u.StartsWith(Id(shop) + "|"));
            g.UI.Toast("You put everything back.");
            g.Changed();
        }

        // On the way out: anything unpaid is shoplifting.
        public static bool CanLeave()
        {
            var shop = Here;
            if (shop == null) return true;
            Owed(shop, out int count);
            if (count == 0) return true;
            Game.I.Fail($"You walked out of {shop.shopName} without paying. They called the cops.");
            return false;
        }
    }
}
