using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // Working at the paper company makes the player a coworker: Kevin will chat (once a day). After a couple of
    // chats he invites them round - house 8, door's open - and from then on lets them in when he's home.
    public class KevinChat : MonoBehaviour, IActionSource
    {
        static readonly string[] Lines =
        {
            "You're the new guy? Cool. Don't touch my M&Ms. I count them.",
            "You ever make chili? I make the best chili. Whole office says so. Well. Oscar doesn't.",
            "Hey, you're alright. You should come by my place sometime - house 8, in the suburbs. Door's always open. Poker night's whenever.",
        };

        public void AddActions(ActionMenu menu)
        {
            var g = Game.I;
            var me = GetComponent<RoutineCharacter>();
            if (!g.S.Has("office.hired") || me == null || !me.Alive || !me.IsAwake) return;
            if (g.S.Has($"kevin.chat.{g.Day}")) { menu.Add("Chat with Kevin", () => g.UI.ShowDialog("Kevin", "Shh. I'm on a break. A thinking break.", new List<(string, System.Action)> { ("OK", null) }, transform)); return; }
            menu.Add("Chat with Kevin", () =>
            {
                int n = Mathf.RoundToInt(g.S.Get("kevin.chats"));
                g.S.Set($"kevin.chat.{g.Day}");
                g.S.Put("kevin.chats", n + 1);
                var line = Lines[Mathf.Min(n, Lines.Length - 1)];
                g.UI.ShowDialog("Kevin", line, new List<(string, System.Action)> { ("\"Ha. Sure.\"", null) }, transform);
                if (n + 1 >= 3 && !g.S.Has("kevin.friend"))
                {
                    g.S.Set("kevin.friend");
                    g.Note("kevin.friend", "Kevin invited me to his place: house 8 in the suburbs. He'll let me in when he's home.");
                }
                g.Changed();
            });
        }
    }
}
