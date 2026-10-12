using System.Collections;
using UnityEngine;

namespace Hitman
{
    // Small one-off actions used from the tap menus.
    public static class Actions
    {
        // At home: eat from the fridge (needs groceries).
        public static void Eat(string what)
        {
            var g = Game.I;
            if (g.Current != null && g.Current.Id == "shackleton.home")
            {
                float meals = g.S.Get("fridge.meals");
                if (meals <= 0)
                {
                    g.UI.Toast(g.S.Holding("food") ? "The fridge is empty. Put your groceries in it first." :
                        "The fridge is empty. Buy groceries at the Shop n Save next door.");
                    return;
                }
                g.S.Put("fridge.meals", meals - 1);
            }
            g.Eat(string.IsNullOrEmpty(what) ? "something" : what);
        }

        // "meal" actions: pay for a meal and eat it there (arg "Burger:5").
        public static void Meal(string arg)
        {
            var parts = (arg ?? "a meal:5").Split(':');
            int price = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 5;
            if (!Game.I.Spend(price)) return;
            Game.I.Eat(parts[0]);
        }

        public static void StockFridge()
        {
            var g = Game.I;
            int n = g.S.Count("food");
            if (n == 0) return;
            g.S.items.RemoveAll(i => i == "food");
            g.S.Put("fridge.meals", g.S.Get("fridge.meals") + 2 * n);
            g.UI.Toast($"Fridge stocked: {g.S.Get("fridge.meals")} meals.");
            g.Changed();
        }

        // Pay for something and put it in the pockets straight away (newspaper machine, coffee counter).
        public static void Buy(string item)
        {
            var g = Game.I;
            if (g.S.Full) { g.UI.Toast("Your pockets are full (5 things)."); return; }
            if (!g.Spend(Items.Price(item))) return;
            g.Give(item);
        }

        // The suit shop: buy the suit and change into it there and then (it's in the wardrobe after that).
        public static void BuySuit(int price)
        {
            var g = Game.I;
            var look = g.Look;
            if (g.S.Has("owns.suit")) { g.UI.Toast("You already own the suit. It's in your wardrobe."); return; }
            if (!g.Spend(price)) return;
            g.S.Set("owns.suit");
            int joe = -1;
            for (int i = 0; i < look.Characters.Length; i++) if (look.Characters[i].suit) joe = i;
            if (joe >= 0)
            {
                var o = look.Current;
                foreach (var slot in new[] { Slot.Top, Slot.Bottoms })
                {
                    g.S.AddClothes(slot, joe, 0);
                    if (look.Fits(o.who, joe, slot)) { o.style[(int)slot] = joe; o.colour[(int)slot] = 0; }
                }
                look.Wear(o);
            }
            g.UI.Toast("Sharp. You look like you work in a tower now.");
            g.Changed();
        }

        // Hang about by the subway for half an hour, watching who comes and goes.
        public static void Loiter(Usable at)
        {
            var g = Game.I;
            g.StartCoroutine(LoiterRoutine(at));
        }

        static IEnumerator LoiterRoutine(Usable at)
        {
            var g = Game.I;
            bool done = false;
            var district = g.Current.Map;
            string seen = null;
            g.FastForward(30, "Loitering by the subway", () => done = true);
            var target = Contract.I != null ? Contract.I.Target : null;
            while (!done)
            {
                if (seen == null && target != null && target.Alive && !target.InTransit && target.AreaId == district.Id && district.SubwayExit != null &&
                    (target.transform.position - district.SubwayExit.transform.position).magnitude < 12f)
                    seen = Game.Clock(g.Minutes);
                yield return null;
            }
            if (seen != null)
                g.Note($"loiter.{district.Id}.{seen.Substring(0, 2)}",
                    $"Saw {target.Routine.displayName} at the {district.DisplayName} subway around {seen}.");
            else g.UI.Toast("Nobody you're interested in came by.");
        }
    }
}
