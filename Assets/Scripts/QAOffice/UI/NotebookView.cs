using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    public class NotebookView : View
    {
        [SerializeField] RectTransform list;
        [SerializeField] Button closeButton;

        public override void Init() => closeButton.onClick.AddListener(Close);

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            UIKit.Clear(list);

            Section("People");
            foreach (var c in G.Story.characters)
            {
                var row = UIKit.Rect(list, c.name);
                row.Size(70);
                var name = UIKit.Label(row, $"{c.name}  <color=#666a7a><size=24>{c.role}</size></color>", 32, UIKit.Hex(c.color), TextAnchor.MiddleLeft, FontStyle.Bold);
                name.rectTransform.Anchor(0, 0, 0.55f, 1);
                var bar = UIKit.Bar(row, UIKit.Good, 22);
                bar.fillAmount = (float)G.Trust(c.id) / G.S.maxTrust;
                ((RectTransform)bar.transform.parent).Anchor(0.56f, 0.3f, 0.86f, 0.7f);
                var num = UIKit.Label(row, $"{G.Trust(c.id)}/{G.S.maxTrust}", 26, UIKit.Ink, TextAnchor.MiddleRight);
                num.rectTransform.Anchor(0.87f, 0, 1, 1);
            }

            Section("Secrets");
            int unknown = 0;
            foreach (var s in G.Story.secrets)
            {
                if (!G.Known.Contains(s.id)) { unknown++; continue; }
                string state = G.Used.Contains(s.id) ? "used" : $"usable on {G.Character(s.about).name}";
                UIKit.Label(list, $"<b>{s.title}</b>  <color=#666a7a>({state}, from {G.Character(s.source).name})</color>\n{s.revealText}", 26, UIKit.Ink);
            }
            UIKit.Label(list, unknown > 0 ? $"{unknown} secret(s) still out there. Build trust to hear more." : "You know every secret in the building.", 24, UIKit.Muted);
        }

        void Section(string text)
        {
            var t = UIKit.Label(list, text.ToUpperInvariant(), 26, UIKit.Accent, TextAnchor.LowerLeft, FontStyle.Bold);
            t.Size(50);
        }
    }
}
