using System.Collections.Generic;

namespace Hitman
{
    // Everything the player can carry. Ids are what Usable actions, shops and set-ups refer to.
    public static class Items
    {
        public class Item
        {
            public string id, name, info;
            public int price;
        }

        static readonly Dictionary<string, Item> All = new Dictionary<string, Item>();

        static Items()
        {
            Add("poison", "Rat poison", 6, "Odourless. Keep away from food.");
            Add("food", "Groceries", 3, "Put it in the fridge at home: two meals.");
            Add("water", "Bottle of water", 1, "");
            Add("newspaper", "Newspaper", 1, "Today's Daily Ledger.");
            Add("coffee", "Coffee", 2, "");
            Add("keycard", "Schrute Towers keycard", 0, "Gets you past security.");
            Add("housekey", "Kevin's house key", 0, "Was under the plant pot.");
            Add("mop", "Wet mop", 0, "From the utilities cupboard.");
            Add("takeout", "Kevin's takeout", 0, "Large chili, extra cheese.");
            Add("takeout.poisoned", "Kevin's takeout (poisoned)", 0, "Large chili, extra rat poison.");
        }

        static void Add(string id, string name, int price, string info) =>
            All[id] = new Item { id = id, name = name, price = price, info = info };

        public static Item Get(string id) => id != null && All.TryGetValue(id, out var i) ? i : new Item { id = id, name = id, info = "" };
        public static string Name(string id) => Get(id).name;
        public static int Price(string id) => Get(id).price;
    }
}
