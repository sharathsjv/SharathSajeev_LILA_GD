using System;
using System.Collections.Generic;
using QAOffice;
using UnityEngine;
using UnityEngine.UI;
using static Hitman.Win98;

namespace Hitman
{
    // The laptop: a late-90s desktop (sky wallpaper, icons, taskbar with Start and a clock) with two programs in
    // grey bevelled windows: the Inbox (contract, tip, payment) and the web browser (search). Built in code.
    public class Computer : MonoBehaviour
    {
        public enum App { None, Mail, Web, Crew }

        RectTransform _root, _screen, _desk, _window, _startMenu;
        Text _clock;
        string _selected;
        bool _firstClose = true;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;
        public bool MailOpen => IsOpen && _window != null && _window.name == "Window Inbox - Mail";
        public RectTransform InboxIcon => _desk != null ? (RectTransform)_desk.Find("Icon Inbox") : null;
        public RectTransform StartButton { get; private set; }
        public RectTransform StartMenu => _startMenu;
        public RectTransform AcceptButton
        {
            get
            {
                if (!MailOpen) return null;
                foreach (var b in _window.GetComponentsInChildren<Button>()) if (b.name == "Button Accept the job") return (RectTransform)b.transform;
                return null;
            }
        }
        public event Action Closed;

        public void Init(RectTransform parent)
        {
            // the room is dark; the screen sits inside a chunky beige CRT bezel
            var bg = UIKit.Box(parent, "Computer", new Color(0.06f, 0.06f, 0.08f), false);
            _root = bg.rectTransform.Stretch();
            var bezel = Bevel(UIKit.Rect(_root, "Monitor"), true, new Color(0.86f, 0.83f, 0.74f));
            bezel.anchorMin = new Vector2(0.035f, 0.02f);
            bezel.anchorMax = new Vector2(0.965f, 0.98f);
            bezel.offsetMin = bezel.offsetMax = Vector2.zero;
            var brand = UIKit.Label(bezel, "TOSHIRO 15\"", 18, new Color(0.4f, 0.38f, 0.32f), TextAnchor.MiddleCenter, FontStyle.Bold);
            brand.GetComponent<Outline>().enabled = false;
            brand.rectTransform.anchorMin = new Vector2(0.4f, 0);
            brand.rectTransform.anchorMax = new Vector2(0.6f, 0);
            brand.rectTransform.offsetMin = new Vector2(0, 4);
            brand.rectTransform.offsetMax = new Vector2(0, 30);
            var led = UIKit.Box(bezel, "Power", new Color(0.2f, 0.95f, 0.3f), false);
            led.rectTransform.anchorMin = led.rectTransform.anchorMax = new Vector2(0.94f, 0);
            led.rectTransform.sizeDelta = new Vector2(34, 12);
            led.rectTransform.anchoredPosition = new Vector2(0, 17);
            led.gameObject.AddComponent<Button>().onClick.AddListener(Close);      // the power button turns it off
            var glass = Sunken(UIKit.Rect(bezel, "Glass"));
            glass.Stretch(34, 34, 34, 30);
            _screen = UIKit.Rect(glass, "Screen").Stretch(4, 4, 4, 4);
            _screen.gameObject.AddComponent<RectMask2D>();
            var wall = UIKit.Rect(_screen, "Wallpaper").Stretch(0, 56, 0, 0);
            wall.gameObject.AddComponent<RawImage>().texture = Sky();
            _desk = UIKit.Rect(_screen, "Desktop").Stretch(0, 56, 0, 0);

            // icons down the left, like everyone's desktop
            var icons = new (string label, Texture2D icon, Action open)[]
            {
                ("Inbox", IconMail(), () => Open(App.Mail)),
                ("Internet Explorer", IconGlobe(), () => Open(App.Web)),
                ("CrewSpace", IconCrew(), () => Open(App.Crew)),
            };
            for (int i = 0; i < icons.Length; i++)
            {
                var (label, tex, open) = icons[i];
                var b = UIKit.Rect(_desk, "Icon " + label);
                b.anchorMin = b.anchorMax = new Vector2(0, 1);
                b.pivot = new Vector2(0, 1);
                b.sizeDelta = new Vector2(150, 140);
                b.anchoredPosition = new Vector2(24, -24 - i * 150);
                var hit = b.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);
                var btn = b.gameObject.AddComponent<Button>();
                var o = open;
                btn.onClick.AddListener(() => { HideStart(); o(); });
                var img = UIKit.Rect(b, "Image");
                img.anchorMin = img.anchorMax = new Vector2(0.5f, 1);
                img.pivot = new Vector2(0.5f, 1);
                img.sizeDelta = new Vector2(72, 72);
                img.anchoredPosition = new Vector2(0, -6);
                var ri = img.gameObject.AddComponent<RawImage>();
                ri.texture = tex;
                ri.raycastTarget = false;
                var t = UIKit.Label(b, label, 22, Color.white, TextAnchor.UpperCenter);
                t.rectTransform.Anchor(0, 0, 1, 0.42f);
            }

            // the taskbar
            var bar = Bevel(UIKit.Rect(_screen, "Taskbar"), true);
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(1, 0);
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = new Vector2(0, 56);
            var start = Button98(bar, "Start", ToggleStart, 24, true);
            StartButton = (RectTransform)start.transform;
            var srt = (RectTransform)start.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0, 0.5f);
            srt.pivot = new Vector2(0, 0.5f);
            srt.sizeDelta = new Vector2(120, 44);
            srt.anchoredPosition = new Vector2(6, 0);
            var logo = UIKit.Box(start.transform, "Logo", new Color32(0, 120, 60, 255), false);
            logo.rectTransform.anchorMin = logo.rectTransform.anchorMax = new Vector2(0, 0.5f);
            logo.rectTransform.sizeDelta = new Vector2(22, 22);
            logo.rectTransform.anchoredPosition = new Vector2(20, 0);
            logo.raycastTarget = false;
            start.GetComponentInChildren<Text>().rectTransform.offsetMin = new Vector2(34, 0);
            var tray = Sunken(UIKit.Rect(bar, "Tray"));
            tray.anchorMin = tray.anchorMax = new Vector2(1, 0.5f);
            tray.pivot = new Vector2(1, 0.5f);
            tray.sizeDelta = new Vector2(130, 42);
            tray.anchoredPosition = new Vector2(-6, 0);
            _clock = Text98(tray, "", 22, TextAnchor.MiddleCenter);
            _clock.rectTransform.Stretch();

            // the Start menu
            _startMenu = Bevel(UIKit.Rect(_screen, "Start Menu"), true);
            _startMenu.anchorMin = _startMenu.anchorMax = Vector2.zero;
            _startMenu.pivot = Vector2.zero;
            _startMenu.sizeDelta = new Vector2(340, 260);
            _startMenu.anchoredPosition = new Vector2(4, 58);
            var side = UIKit.Box(_startMenu, "Side", Navy, false);
            side.rectTransform.Anchor(0, 0, 0, 1);
            side.rectTransform.offsetMin = new Vector2(4, 4);
            side.rectTransform.offsetMax = new Vector2(44, -4);
            var items = UIKit.Rect(_startMenu, "Items").Stretch(50, 6, 6, 6);
            UIKit.VList(items, 2, 0);
            MenuItem(items, "Inbox", () => Open(App.Mail));
            MenuItem(items, "Internet Explorer", () => Open(App.Web));
            MenuItem(items, "CrewSpace", () => Open(App.Crew));
            MenuItem(items, "Log Off", Close);
            _startMenu.gameObject.SetActive(false);

            _root.gameObject.SetActive(false);
        }

        public void Show(App app)
        {
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            HideStart();
            if (app == App.Mail)
            {
                // open on the newest unread message
                _selected = null;
                foreach (var m in Mails()) if (!Game.I.S.Has("read." + m.id)) { _selected = m.id; break; }
            }
            if (app != App.None) Open(app);
            else CloseWindow();
        }

        public void Close()
        {
            HideStart();
            CloseWindow();
            _root.gameObject.SetActive(false);
            Game.I.Player.Seated = false;
            if (_firstClose)
            {
                _firstClose = false;
                Closed?.Invoke();
            }
        }

        void Update()
        {
            if (!IsOpen || Game.I == null) return;
            int m = Mathf.FloorToInt(Game.I.Minutes) % (24 * 60);
            int h = m / 60 % 12;
            _clock.text = $"{(h == 0 ? 12 : h)}:{m % 60:00} {(m / 60 < 12 ? "AM" : "PM")}";
        }

        void ToggleStart() => _startMenu.gameObject.SetActive(!_startMenu.gameObject.activeSelf);
        void HideStart() { if (_startMenu != null) _startMenu.gameObject.SetActive(false); }

        void MenuItem(RectTransform parent, string label, Action act)
        {
            var b = Button98(parent, label, () => { HideStart(); act(); }, 24, false);
            b.Size(60);
            b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
        }

        // ---------- Windows ----------

        void CloseWindow()
        {
            if (_window != null) Destroy(_window.gameObject);
            _window = null;
        }

        void Open(App app)
        {
            if (app == App.Mail) OpenMail();
            else if (app == App.Web) OpenWeb();
            else if (app == App.Crew) OpenCrew();
        }

        // A grey window with a blue title bar and a close box; returns the area inside.
        RectTransform Window(string title, Vector4 anchors)
        {
            CloseWindow();
            _window = Bevel(UIKit.Rect(_desk, "Window " + title), true);
            _window.anchorMin = new Vector2(anchors.x, anchors.y);
            _window.anchorMax = new Vector2(anchors.z, anchors.w);
            _window.offsetMin = _window.offsetMax = Vector2.zero;
            var titleBar = UIKit.Rect(_window, "Title");
            titleBar.anchorMin = new Vector2(0, 1);
            titleBar.anchorMax = Vector2.one;
            titleBar.offsetMin = new Vector2(6, -50);
            titleBar.offsetMax = new Vector2(-6, -6);
            var grad = titleBar.gameObject.AddComponent<RawImage>();
            grad.texture = TitleGradient();
            var t = UIKit.Label(titleBar, title, 24, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            t.rectTransform.Stretch(12, 0, 60, 0);
            t.GetComponent<Outline>().enabled = false;
            var x = Button98(titleBar, "X", CloseWindow, 22, true);
            var xr = (RectTransform)x.transform;
            xr.anchorMin = xr.anchorMax = new Vector2(1, 0.5f);
            xr.pivot = new Vector2(1, 0.5f);
            xr.sizeDelta = new Vector2(40, 34);
            xr.anchoredPosition = new Vector2(-5, 0);
            var inside = UIKit.Rect(_window, "Inside");
            inside.Stretch(8, 8, 8, 56);
            return inside;
        }

        // ---------- Inbox ----------

        class Mail
        {
            public string id, from, subject, received, body;
            public Action<RectTransform> extra;
        }

        List<Mail> Mails()
        {
            var g = Game.I;
            var st = g.S;
            var c = Contract.I;
            var a = c != null ? c.Assignment : null;
            var list = new List<Mail>();
            if (a != null)
                list.Add(new Mail
                {
                    id = "contract", from = a.sender, subject = a.subject, received = "Mon 7:00 AM",
                    body = a.body + (a.target != null ? $"\n\nName: {a.target.displayName}\nInformation: {a.target.info}" : ""),
                    extra = area =>
                    {
                        if (!st.Has("job.accepted"))
                        {
                            var acc = Button98(area, "Accept the job", () => { c.Accept(); OpenMail(); }, 24, true);
                            acc.Size(60);
                            return;
                        }
                        Text98(area, "<i>You accepted the job.</i>", 22, TextAnchor.MiddleLeft).Size(36);
                        if (st.Has("tip.bought") || st.Has("contract.done")) return;
                        var b = Button98(area, $"Reply: \"Send me the tip\" (${c.TipPrice})", () => { c.BuyTip(); OpenMail(); }, 22, true);
                        b.Size(56);
                    },
                });
            if (st.Has("tip.arrived"))
                list.Add(new Mail
                {
                    id = "tip", from = a != null ? a.sender : "Anonymous", subject = "Re: Tip", received = When(st.Get("tip.received", -1)),
                    body = "Hint: He is known to have a few drinks with his breakfast before he reports to work.",
                });
            if (st.Has("paid"))
                list.Add(new Mail
                {
                    id = "payment", from = a != null ? a.sender : "Anonymous", subject = "Saw it in the paper", received = When((c.StoryDay - 1) * 1440 + 7 * 60),
                    body = $"I saw the story in this morning's paper. Nice work - it looks like an accident.\n\n" +
                           $"I've wired ${Mathf.RoundToInt(st.Get("paid.amount"))} to your account." +
                           (st.Get("paid.amount") < 400 ? "\n\nIt took longer than I wanted, so that's all you get." : ""),
                });
            list.Reverse();          // newest first
            return list;
        }

        static string When(float absMinutes)
        {
            if (absMinutes < 0) return "";
            int day = Mathf.FloorToInt(absMinutes / 1440) + 1;
            int m = Mathf.FloorToInt(absMinutes) % 1440;
            int h = m / 60 % 12;
            return $"{Game.Weekdays[(day - 1) % 7].Substring(0, 3)} {(h == 0 ? 12 : h)}:{m % 60:00} {(m / 60 < 12 ? "AM" : "PM")}";
        }

        public int Unread
        {
            get
            {
                int n = 0;
                foreach (var m in Mails()) if (!Game.I.S.Has("read." + m.id)) n++;
                return n;
            }
        }

        void OpenMail()
        {
            var st = Game.I.S;
            var mails = Mails();
            if (_selected == null || !mails.Exists(m => m.id == _selected)) _selected = mails.Count > 0 ? mails[0].id : null;
            var w = Window("Inbox - Mail", new Vector4(0.12f, 0.06f, 0.95f, 0.96f));
            // the message list, down the left
            var listBox = Sunken(UIKit.Rect(w, "List"));
            listBox.anchorMin = Vector2.zero;
            listBox.anchorMax = new Vector2(0.34f, 1);
            listBox.offsetMin = listBox.offsetMax = Vector2.zero;
            var white = UIKit.Box(listBox, "White", Color.white, false);
            white.rectTransform.Stretch(3, 3, 3, 3);
            var header = Button98(white.transform, "Inbox", null, 20, true);
            var hr = (RectTransform)header.transform;
            hr.anchorMin = new Vector2(0, 1);
            hr.anchorMax = Vector2.one;
            hr.offsetMin = new Vector2(0, -42);
            hr.offsetMax = Vector2.zero;
            header.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            var rows = UIKit.Rect(white.transform, "Rows");
            rows.Stretch(0, 0, 0, 44);
            UIKit.VList(rows, 0, 0);
            foreach (var m in mails)
            {
                var mail = m;
                bool sel = m.id == _selected, unread = !st.Has("read." + m.id);
                // unread: bold, an envelope, and a pale yellow highlight; the open one is navy
                var row = UIKit.Box(rows, "Row", sel ? Navy : unread ? new Color32(255, 247, 200, 255) : Color.white, false);
                row.Size(78).flexibleHeight = 0;
                var btn = row.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() => { _selected = mail.id; OpenMail(); });
                var t = Text98(row.transform, $"{(unread ? "\u2709 " : "")}{m.from}   <size=17>{m.received}</size>\n{m.subject}", 20, TextAnchor.MiddleLeft,
                               unread ? FontStyle.Bold : FontStyle.Normal);
                t.color = sel ? Color.white : Color.black;
                t.rectTransform.Stretch(10, 2, 6, 2);
                var line = UIKit.Box(row.transform, "Line", new Color(0, 0, 0, 0.12f), false);
                line.rectTransform.Anchor(0, 0, 1, 0);
                line.rectTransform.sizeDelta = new Vector2(0, 1);
            }
            // the selected message
            var view = Sunken(UIKit.Rect(w, "Message"));
            view.anchorMin = new Vector2(0.35f, 0);
            view.anchorMax = Vector2.one;
            view.offsetMin = view.offsetMax = Vector2.zero;
            var page = UIKit.Box(view, "Page", Color.white, false);
            page.rectTransform.Stretch(3, 3, 3, 3);
            var sel2 = mails.Find(x => x.id == _selected);
            if (sel2 == null) { Text98(page.transform, "No messages.", 24, TextAnchor.UpperLeft).rectTransform.Stretch(16, 16, 16, 16); return; }
            st.Set("read." + sel2.id);
            var content = UIKit.ScrollList(page.transform, 10, 18);
            ((RectTransform)content.GetComponentInParent<ScrollRect>(true).transform).Stretch();
            content.GetComponentInParent<ScrollRect>(true).GetComponent<Image>().color = Color.white;
            ScrollBar98(content.GetComponentInParent<ScrollRect>(true));          // there's more below: a scrollbar says so
            Text98(content, $"<b>From:</b> {sel2.from}\n<b>Subject:</b> {sel2.subject}\n<b>Received:</b> {sel2.received}", 22, TextAnchor.UpperLeft);
            var rule = UIKit.Box(content, "Rule", Win98.Shadow, false);
            rule.Size(2);
            Text98(content, sel2.body, 24, TextAnchor.UpperLeft);          // (sizes itself to the wrapped text)
            if (sel2.id == "contract" && Contract.I != null && Contract.I.Assignment.target != null && Contract.I.Assignment.target.photo != null)
            {
                var ph = UIKit.Rect(content, "Photo");
                ph.gameObject.AddComponent<Image>().sprite = Contract.I.Assignment.target.photo;
                ph.GetComponent<Image>().preserveAspect = true;
                ph.Size(180, 180, 0);
            }
            sel2.extra?.Invoke(content);
        }

        // ---------- Web ----------

        void OpenWeb()
        {
            var w = Window("Internet Explorer", new Vector4(0.12f, 0.06f, 0.95f, 0.96f));
            var barRow = UIKit.Rect(w, "Address");
            barRow.anchorMin = new Vector2(0, 1);
            barRow.anchorMax = Vector2.one;
            barRow.offsetMin = new Vector2(0, -60);
            barRow.offsetMax = Vector2.zero;
            var label = Text98(barRow, "Search", 22, TextAnchor.MiddleLeft);
            label.rectTransform.Anchor(0, 0, 0, 1);
            label.rectTransform.offsetMin = new Vector2(6, 0);
            label.rectTransform.offsetMax = new Vector2(90, 0);
            var field = Sunken(UIKit.Rect(barRow, "Field"));
            field.Stretch(96, 8, 130, 8);
            var fw = UIKit.Box(field, "White", Color.white, false);
            fw.rectTransform.Stretch(3, 3, 3, 3);
            var input = fw.gameObject.AddComponent<InputField>();
            var it = Text98(fw.transform, "", 24, TextAnchor.MiddleLeft);
            it.rectTransform.Stretch(10, 2, 10, 2);
            var ph = Text98(fw.transform, "Type a name...", 24, TextAnchor.MiddleLeft);
            ph.rectTransform.Stretch(10, 2, 10, 2);
            ph.color = Win98.Shadow;
            input.textComponent = it;
            input.placeholder = ph;
            var page = Sunken(UIKit.Rect(w, "Page"));
            page.Stretch(0, 0, 0, 66);
            var white = UIKit.Box(page, "White", Color.white, false);
            white.rectTransform.Stretch(3, 3, 3, 3);
            var results = UIKit.ScrollList(white.transform, 14, 20);
            ((RectTransform)results.GetComponentInParent<ScrollRect>(true).transform).Stretch();
            results.GetComponentInParent<ScrollRect>(true).GetComponent<Image>().color = Color.white;
            Text98(results, "Welcome to the Internet. Search for a name, a business or a place.", 24, TextAnchor.UpperLeft).Size(40);
            void Go()
            {
                UIKit.Clear(results);
                Text98(results, $"Results for \"{input.text}\" - tap one to note it:", 22, TextAnchor.UpperLeft).Size(36);
                foreach (var a in Contract.I.Search(input.text))
                {
                    var art = a;
                    var box = UIKit.Rect(results, "Result");
                    box.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
                    box.gameObject.AddComponent<Button>().onClick.AddListener(() => Contract.I.NoteArticle(art));
                    var t = Text98(box, $"<color=#0000EE><b>{a.headline}</b></color>\n{a.body}", 23, TextAnchor.UpperLeft);
                    t.rectTransform.Stretch();
                    box.Size(Mathf.Max(70, (a.body.Length / 75 + 2) * 30));
                }
            }
            var go = Button98(barRow, "Go", Go, 22, true);
            var gr = (RectTransform)go.transform;
            gr.anchorMin = gr.anchorMax = new Vector2(1, 0.5f);
            gr.pivot = new Vector2(1, 0.5f);
            gr.sizeDelta = new Vector2(110, 44);
            gr.anchoredPosition = new Vector2(-6, 0);
            input.onEndEdit.AddListener(_ => Go());
        }

        // ---------- CrewSpace: the little social network ----------

        static readonly Color CrewBlue = new Color32(40, 90, 160, 255);

        void OpenCrew()
        {
            var w = Window("CrewSpace - Internet Explorer", new Vector4(0.12f, 0.04f, 0.95f, 0.97f));
            var page = Sunken(UIKit.Rect(w, "Page"));
            page.Stretch();
            var white = UIKit.Box(page, "White", new Color(0.95f, 0.96f, 0.98f), false);
            white.rectTransform.Stretch(3, 3, 3, 3);
            // header: logo, search
            var head = UIKit.Box(white.transform, "Header", CrewBlue, false).rectTransform;
            head.anchorMin = new Vector2(0, 1);
            head.anchorMax = Vector2.one;
            head.offsetMin = new Vector2(0, -74);
            head.offsetMax = Vector2.zero;
            var logo = UIKit.Label(head, "CrewSpace", 36, Color.white, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            logo.rectTransform.Stretch(20, 0, 0, 0);
            logo.GetComponent<Outline>().enabled = false;
            var field = UIKit.Box(head, "Search", Color.white, false).rectTransform;
            field.anchorMin = new Vector2(0.45f, 0.2f);
            field.anchorMax = new Vector2(0.97f, 0.8f);
            field.offsetMin = field.offsetMax = Vector2.zero;
            var input = field.gameObject.AddComponent<InputField>();
            var it = Text98(field, "", 22, TextAnchor.MiddleLeft);
            it.rectTransform.Stretch(12, 2, 12, 2);
            var ph = Text98(field, "Search people...", 22, TextAnchor.MiddleLeft);
            ph.rectTransform.Stretch(12, 2, 12, 2);
            ph.color = Win98.Shadow;
            input.textComponent = it;
            input.placeholder = ph;
            // the feed
            var area = UIKit.Rect(white.transform, "Feed").Stretch(0, 0, 0, 78);
            var feed = UIKit.ScrollList(area, 10, 16);
            ((RectTransform)feed.GetComponentInParent<ScrollRect>(true).transform).Stretch();
            feed.GetComponentInParent<ScrollRect>(true).GetComponent<Image>().color = new Color(0, 0, 0, 0);
            void Fill(string query)
            {
                UIKit.Clear(feed);
                var q = (query ?? "").Trim().ToLowerInvariant();
                int n = 0;
                Text98(feed, q.Length == 0 ? "Latest Pings - tap one to note it" : $"Pings from people matching \"{query}\"", 20, TextAnchor.MiddleLeft, FontStyle.Bold).Size(30);
                foreach (var p in Contract.I.Pings())
                {
                    if (q.Length > 0 && !p.name.ToLowerInvariant().Contains(q) && !p.handle.ToLowerInvariant().Contains(q)) continue;
                    // the target only turns up if you go looking for him
                    if (q.Length == 0 && p.handle == "kevin_m") continue;
                    if (++n > 40) break;
                    var ping = p;
                    var card = UIKit.Box(feed, "Ping", Color.white);
                    card.gameObject.AddComponent<Button>().onClick.AddListener(() => Contract.I.NotePing(ping));
                    var h = UIKit.HList(card.rectTransform, 14, 12);
                    h.childForceExpandWidth = false;
                    h.childForceExpandHeight = false;
                    h.childAlignment = TextAnchor.UpperLeft;
                    var av = UIKit.Box(card.transform, "Avatar", p.colour, false);
                    av.Size(56, 56, 0);
                    var ini = UIKit.Label(av.transform, p.name.Substring(0, 1), 30, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                    ini.rectTransform.Stretch();
                    var col = UIKit.Rect(card.transform, "Body");
                    UIKit.VList(col, 2, 0);
                    col.Size(-1, -1, 1);
                    Text98(col, $"<b>{p.name}</b> <color=#707070>@{p.handle} - {Contract.PingTime(p.at)}</color>", 21, TextAnchor.UpperLeft);
                    Text98(col, p.text, 23, TextAnchor.UpperLeft);
                }
                if (n == 0) Text98(feed, "No Pings found.", 22, TextAnchor.MiddleLeft).Size(40);
            }
            input.onEndEdit.AddListener(Fill);
            Fill("");
        }

        static Texture2D IconCrew()
        {
            var t = Blank(32);
            Fill(t, 3, 3, 28, 28, CrewBlue);
            for (int y = 0; y < 32; y++)                                   // a white speech bubble
                for (int x = 0; x < 32; x++)
                    if (new Vector2((x - 15.5f) / 10f, (y - 17f) / 7f).sqrMagnitude < 1) t.SetPixel(x, y, Color.white);
            for (int k = 0; k < 4; k++) Fill(t, 9 + k, 8 + k, 10 + k, 9 + k, Color.white);
            t.Apply();
            return t;
        }

        // ---------- Win98 widgets ----------

        // ---------- Pixel art ----------

        static Texture2D Px(int n) => new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };

        static void Fill(Texture2D t, int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) t.SetPixel(x, y, c);
        }

        static Texture2D Blank(int n)
        {
            var t = Px(n);
            Fill(t, 0, 0, n - 1, n - 1, new Color(0, 0, 0, 0));
            return t;
        }

        static Texture2D IconMail()
        {
            var t = Blank(32);
            Fill(t, 3, 7, 28, 24, new Color32(250, 250, 240, 255));
            Fill(t, 3, 7, 28, 7, Color.gray); Fill(t, 3, 24, 28, 24, Color.gray);
            Fill(t, 3, 7, 3, 24, Color.gray); Fill(t, 28, 7, 28, 24, Color.gray);
            for (int i = 0; i < 13; i++) { t.SetPixel(3 + i, 24 - i, Color.gray); t.SetPixel(28 - i, 24 - i, Color.gray); }
            Fill(t, 22, 2, 30, 8, new Color32(240, 200, 0, 255));
            t.Apply();
            return t;
        }

        static Texture2D IconGlobe()
        {
            var t = Blank(32);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                    if (d > 13) continue;
                    bool land = Mathf.PerlinNoise(x * 0.18f, y * 0.18f) > 0.55f;
                    t.SetPixel(x, y, land ? new Color32(40, 160, 60, 255) : new Color32(40, 110, 220, 255));
                }
            Fill(t, 2, 14, 29, 15, new Color32(250, 200, 30, 255));
            t.Apply();
            return t;
        }

        // Blue sky with soft white clouds.
        static Texture2D Sky()
        {
            const int w = 256, h = 144;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var sky = Color.Lerp(new Color32(150, 190, 235, 255), new Color32(60, 120, 210, 255), y / (float)h);
                    float c = Mathf.PerlinNoise(x * 0.025f + 3, y * 0.05f) * 0.6f + Mathf.PerlinNoise(x * 0.07f, y * 0.12f + 7) * 0.4f;
                    c = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.48f, 0.75f, c));
                    t.SetPixel(x, y, Color.Lerp(sky, Color.white, c * 0.9f));
                }
            t.Apply();
            return t;
        }
    }
}
