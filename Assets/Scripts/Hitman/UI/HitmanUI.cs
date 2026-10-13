using System;
using System.Collections;
using System.Collections.Generic;
using QAOffice;
using UnityEngine;
using UnityEngine.UI;

namespace Hitman
{
    // All on-screen UI for the hitman game. Layout lives in the scene; this wires it up and fills dynamic content.
    public class HitmanUI : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] Text dayText;
        [SerializeField] Text timeText;
        [SerializeField] Image hungerFill;
        [SerializeField] Image tiredFill;
        [SerializeField] Button notepadButton;

        [Header("Toast")]
        [SerializeField] GameObject toastBox;
        [SerializeField] Text toastText;

        [Header("Action menu")]
        [SerializeField] RectTransform actionMenu;
        [SerializeField] Text actionTitle;
        [SerializeField] RectTransform actionButtons;

        [Header("Side panel (email / notepad / travel)")]
        [SerializeField] GameObject sidePanel;
        [SerializeField] Text sideTitle;
        [SerializeField] Button sideClose;
        [SerializeField] GameObject emailView;
        [SerializeField] Text emailHeader;
        [SerializeField] Text emailBody;
        [SerializeField] Text emailTarget;
        [SerializeField] Image emailPhoto;
        [SerializeField] GameObject notepadView;
        [SerializeField] InputField notepadInput;
        [SerializeField] GameObject travelView;
        [SerializeField] RectTransform travelButtons;

        [Header("Following")]
        [SerializeField] GameObject followBar;
        [SerializeField] Text followText;
        [SerializeField] Button stopFollowButton;

        [Header("Fast-forward / travel overlay")]
        [SerializeField] GameObject skipOverlay;
        [SerializeField] Image skipTint;
        [SerializeField] RawImage scanlines;
        [SerializeField] RectTransform trackingBar;
        [SerializeField] Text skipLabel;

        [Header("Content")]
        [SerializeField] Assignment assignment;
        [SerializeField] PlayerController player;

        // Everything that takes over taps apart from the little tap menu (tapping elsewhere just closes that).
        public bool BlockingExceptMenu => sidePanel.activeSelf || skipOverlay.activeSelf || _wardrobe.IsOpen || _overlays.Blocking || _computer.IsOpen || _paperView.IsOpen || _notebook.IsOpen || _keyboard.IsOpen;
        public bool MenuOpen => _menu != null && _menu.gameObject.activeSelf;
        public bool Blocking => (_menu != null && _menu.gameObject.activeSelf) || sidePanel.activeSelf || skipOverlay.activeSelf || _wardrobe.IsOpen || _overlays.Blocking || _computer.IsOpen || _paperView.IsOpen || _notebook.IsOpen || _keyboard.IsOpen;

        readonly Queue<string> _toasts = new Queue<string>();
        float _toastTimer;
        bool _emailRead, _notesStarted;
        public BarShift BarShift { get; private set; }
        public WorldMarks Marks { get; private set; }
        public NotebookView Notebook => _notebook;
        public RectTransform NotebookButton => (RectTransform)notepadButton.transform;
        Tutorial _tutorial;
        bool _tape;
        WardrobeUI _wardrobe;
        Overlays _overlays;
        Computer _computer;
        NewspaperView _paperView;
        NotebookView _notebook;
        OnScreenKeyboard _keyboard;
        RectTransform _dynamic, _dynamicContent, _tabs;
        Action _refresh;            // redraws the open dynamic view (pockets change, notes added)
        int _notebookTab;
        Button _tipButton;

        void Awake()
        {
            notepadButton.onClick.AddListener(() => ShowNotebook(0));
            sideClose.onClick.AddListener(CloseSide);
            stopFollowButton.onClick.AddListener(() => player.StopFollow());
            notepadInput.onValueChanged.AddListener(t => _notes = t);
            scanlines.texture = MakeScanlines();
            actionMenu.gameObject.SetActive(false);
            sidePanel.SetActive(false);
            followBar.SetActive(false);
            skipOverlay.SetActive(false);
            toastBox.SetActive(false);
            // Toasts sit to the left, clear of the side panel.
            var trt = (RectTransform)toastBox.transform;
            trt.anchorMin = new Vector2(0.07f, 0.84f);
            trt.anchorMax = new Vector2(0.43f, 0.93f);
            _wardrobe = gameObject.AddComponent<WardrobeUI>();
            _wardrobe.Init((RectTransform)sidePanel.transform.parent);
            _computer = gameObject.AddComponent<Computer>();
            _computer.Init((RectTransform)sidePanel.transform.parent);
            _computer.Closed += () => { if (!_emailRead) { _emailRead = true; if (!Tutorial.Running) Toast("First things first: breakfast. Tap the fridge."); } };
            _computer.EveryClose += SuggestReplay;
            _notebook = gameObject.AddComponent<NotebookView>();
            _notebook.Init((RectTransform)sidePanel.transform.parent);
            _paperView = gameObject.AddComponent<NewspaperView>();
            _paperView.Init((RectTransform)sidePanel.transform.parent);
            _keyboard = gameObject.AddComponent<OnScreenKeyboard>();
            _overlays = gameObject.AddComponent<Overlays>();
            _overlays.Init((RectTransform)sidePanel.transform.parent, notepadButton);
            Marks = gameObject.AddComponent<WorldMarks>();
            Marks.Init((RectTransform)sidePanel.transform.parent);
            // Only three icons on screen (watch, pause, notebook): the clock box goes, the watch shows the time.
            dayText.transform.parent.gameObject.SetActive(false);
            BuildDynamic();
            BuildTipButton();
            StyleLikeTheOS();
            BarShift = gameObject.AddComponent<BarShift>();
            BarShift.Init((RectTransform)sidePanel.transform.parent);
            _tutorial = gameObject.AddComponent<Tutorial>();
            _tutorial.Init((RectTransform)sidePanel.transform.parent);
            _keyboard.Init((RectTransform)sidePanel.transform.parent);      // last, so it sits on top
            sidePanel.transform.parent.gameObject.AddComponent<HardEdges>();  // the same hard-edged look everywhere
            sidePanel.transform.parent.gameObject.AddComponent<TextScale>();  // bigger text everywhere
        }

        void Start()
        {
            Game.I.StateChanged += () =>
            {
                if (sidePanel.activeSelf && _dynamic.gameObject.activeSelf) _refresh?.Invoke();
                if (_notebook.IsOpen && _notebook.Tab != 1) _notebook.Refresh();
            };
            Game.I.DayStarted += OnDayStarted;
            // The title screen first; the first morning starts when they press Play.
            _menu98 = gameObject.AddComponent<MainMenu>();
            _menu98.Init(CanvasRoot);
            _menu98.Show(() => { Game.DeleteSave(); _started = true; if (Game.I.Day == 1) OnDayStarted(1); },
                Game.HasSave ? (Action)(() =>
                {
                    _started = true;
                    _emailRead = true;
                    if (Game.I.ContinueFromSave()) Toast($"Welcome back. Day {Game.I.Day}, {Game.Clock(Game.I.Minutes)}.");
                    else OnDayStarted(1);
                }) : null);
        }

        string _notes = "";

        // The job's done and paid for (the client's email read): there's more than one way to do it - try another.
        void SuggestReplay()
        {
            var st = Game.I.S;
            if (!st.Has("read.payment") || st.Has("replay.suggested")) return;
            st.Set("replay.suggested");
            ShowDialog("Contract complete",
                "Kevin's gone, and you got paid.\n\nThere's more than one way this could have gone. Start over and find another?",
                new List<(string, Action)> { ("Restart", () => Game.I.RestartGame()), ("Keep playing", null) });
        }

        MainMenu _menu98;
        bool _started;

        void OnDayStarted(int day)
        {
            if (!_started) return;                   // (still on the title screen: Play starts the day)
            if (day == 1 && !Game.I.S.Has("tutorial.done")) _tutorial.Begin();
            else if (day == 1 && !_emailRead) ShowEmail();
            else Toast($"Day {day}. {Game.Clock(Game.I.Minutes)}");
        }

        float _pocketsCheck;

        void Update()
        {
            var g = Game.I;
            if (g == null) return;
            // The first time they're carrying something they can use (the newspaper...): how the Pockets tab works.
            _pocketsCheck -= Time.unscaledDeltaTime;
            if (_pocketsCheck <= 0)
            {
                _pocketsCheck = 1f;
                if (_started && !MainMenu.Open && !Tutorial.Running && g.S.Has("tutorial.done") && !g.S.Has("tutorial.pockets") && !g.Busy && !Blocking)
                    foreach (var item in g.S.items)
                    {
                        var acts = new List<(string, Action)>();
                        Contract.I?.AddPocketActions(item, acts);
                        acts.RemoveAll(a => a.Item1.StartsWith("Put it back"));
                        if (acts.Count == 0) continue;
                        _tutorial.BeginPockets(item, acts[0].Item1);
                        break;
                    }
            }
            dayText.text = $"Day {g.Day}";
            timeText.text = g.ClockText;
            hungerFill.fillAmount = 1 - g.Hunger;
            tiredFill.fillAmount = 1 - g.Tiredness;

            if (_toastTimer > 0)
            {
                _toastTimer -= Time.unscaledDeltaTime;
                if (_toastTimer <= 0) toastBox.SetActive(false);
            }
            else if (_toasts.Count > 0)
            {
                toastText.text = _toasts.Dequeue();
                toastBox.SetActive(true);
                toastBox.transform.SetAsLastSibling();
                _toastTimer = 2.6f;
            }

            if (skipOverlay.activeSelf && _tape)
            {
                // Tape fast-forward wobble: scrolling scanlines and a tracking band sweeping down.
                scanlines.uvRect = new Rect(0, Time.unscaledTime * 3f, 1, Screen.height / 4f);
                float y = Mathf.Repeat(Time.unscaledTime * 0.9f, 1.2f) - 0.1f;
                trackingBar.anchorMin = new Vector2(0, 1 - y - 0.06f);
                trackingBar.anchorMax = new Vector2(1, 1 - y);
            }
        }

        public void Toast(string msg) => _toasts.Enqueue(msg);

        // Keep a showing toast in front of anything that keeps itself on top (the tutorial box).
        public void ToastOnTop() { if (toastBox.activeSelf) toastBox.transform.SetAsLastSibling(); }

        // ---------- Action menu ----------

        // ---------- The 90s desktop look for menus and panels ----------

        Text _menuTitle, _sideTitle98;
        RectTransform _menu, _menuRows;

        void StyleLikeTheOS()
        {
            // the tap menu: a grey window with the thing's name in a blue title bar, sized to its rows
            actionMenu.gameObject.SetActive(false);
            var canvas = (RectTransform)actionMenu.parent;
            _menu = Win98.Bevel(UIKit.Rect(canvas, "Menu"), true);
            _menu.anchorMin = _menu.anchorMax = new Vector2(0.5f, 0.5f);
            _menu.pivot = new Vector2(0, 1);
            var v = UIKit.VList(_menu, 0, 6);
            v.padding.top = 54;
            var fit = _menu.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _menu.sizeDelta = new Vector2(440, 100);
            _menuTitle = Win98.TitleBar(_menu, "", 44, 24);
            _menuRows = _menu;
            _menu.gameObject.SetActive(false);
            // the side panel (travel, payphone...)
            Win98.Bevel((RectTransform)sidePanel.transform, true);
            sideTitle.gameObject.SetActive(false);
            sideClose.gameObject.SetActive(false);
            _sideTitle98 = Win98.TitleBar((RectTransform)sidePanel.transform, "", 52, 26, CloseSide);
            var scroll = _dynamicContent.GetComponentInParent<ScrollRect>(true).GetComponent<Image>();
            scroll.color = new Color(0, 0, 0, 0);
        }

        public void ShowActions(string title, List<(string label, Action act)> actions, Vector2 screenPos)
        {
            _menuTitle.text = title;
            // clear the old rows (keep the frame edges and the title bar)
            for (int i = _menuRows.childCount - 1; i >= 0; i--)
            {
                var ch = _menuRows.GetChild(i);
                if (ch.name.StartsWith("Button") || ch.name == "Separator") Destroy(ch.gameObject);
            }
            var actionButtons = _menuRows;
            foreach (var (label, act) in actions)
            {
                var a = act;
                var b = Win98.Button98(actionButtons, label, () => { HideActions(); a(); }, 24, false);
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                b.GetComponentInChildren<Text>().fontStyle = FontStyle.Normal;
                b.Size(58);
            }
            var sep = UIKit.Box(actionButtons, "Separator", Win98.Shadow, false);
            sep.Size(2);
            var c = Win98.Button98(actionButtons, "Cancel", HideActions, 22, false);
            c.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            c.GetComponentInChildren<Text>().fontStyle = FontStyle.Normal;
            c.Size(52);
            var actionMenu = _menu;
            actionMenu.gameObject.SetActive(true);
            actionMenu.SetAsLastSibling();
            // Pop up next to the tap, kept on screen.
            var canvas = (RectTransform)actionMenu.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screenPos, null, out var local);
            Canvas.ForceUpdateCanvases();
            var half = canvas.rect.size / 2;
            var size = actionMenu.rect.size;
            LayoutRebuilder.ForceRebuildLayoutImmediate(actionMenu);
            size = actionMenu.rect.size;
            local.x = Mathf.Clamp(local.x + 30, -half.x + 10, half.x - size.x - 10);
            local.y = Mathf.Clamp(local.y, -half.y + size.y + 10, half.y - 10);
            actionMenu.anchoredPosition = local;
        }

        public void HideActions() { if (_menu != null) _menu.gameObject.SetActive(false); }

        // For the tutorial: what's on screen.
        public string OpenMenuTitle => _menu != null && _menu.gameObject.activeSelf ? _menuTitle.text : null;
        public RectTransform Menu => _menu;
        public Computer Computer => _computer;
        public Overlays Overlays => _overlays;
        public RectTransform CanvasRoot => (RectTransform)sidePanel.transform.parent;

        // ---------- Side panel ----------

        void OpenSide(string title, GameObject view)
        {
            HideActions();
            sideTitle.text = title;
            if (_sideTitle98 != null) _sideTitle98.text = title;
            emailView.SetActive(view == emailView);
            notepadView.SetActive(view == notepadView);
            travelView.SetActive(view == travelView);
            _dynamic.gameObject.SetActive(view == _dynamic.gameObject);
            bool notebook = title == "Notebook";
            _tabs.gameObject.SetActive(notebook);
            // Room for the notebook's tabs above the page.
            float top = notebook ? 0.8f : 0.9f;
            foreach (var v in new[] { notepadView, _dynamic.gameObject })
            {
                var rt = (RectTransform)v.transform;
                rt.anchorMax = new Vector2(rt.anchorMax.x, top);
            }
            sidePanel.SetActive(true);
            sidePanel.transform.SetAsLastSibling();
        }

        void CloseSide()
        {
            bool wasEmail = emailView.activeSelf;
            sidePanel.SetActive(false);
            Game.I.Player.Seated = false;
            if (wasEmail && !_emailRead)
            {
                _emailRead = true;
                Toast("First things first: breakfast. Tap the fridge.");
            }
        }

        // The laptop: the desktop, or straight into the Inbox / the browser.
        public void ShowComputer() { HideActions(); _computer.Show(Computer.App.None); }
        public void ShowEmail() { HideActions(); _computer.Show(Computer.App.Mail); }

        // (old side-panel email, kept for reference)
        public void ShowEmailPanel()
        {
            if (assignment == null) return;
            emailHeader.text = $"<b>From:</b> {assignment.sender}\n<b>Subject:</b> {assignment.subject}";
            emailBody.text = assignment.body;
            var t = assignment.target;
            emailTarget.text = t == null ? "" : $"<b>Name:</b> {t.displayName}\n<b>Information:</b> {t.info}";
            if (emailPhoto != null && t != null && t.photo != null)
            {
                emailPhoto.sprite = t.photo;
                emailPhoto.color = Color.white;
                emailPhoto.type = Image.Type.Simple;
                emailPhoto.preserveAspect = true;
                foreach (var label in emailPhoto.GetComponentsInChildren<Text>()) label.enabled = false;
            }
            if (Contract.I != null)
            {
                var st = Game.I.S;
                if (st.Has("tip.arrived"))
                    emailBody.text += "\n\n<b>Re: Tip</b>\nHe is known to have a few drinks with his breakfast before he reports to work.";
                _tipButton.gameObject.SetActive(!st.Has("tip.bought"));
                _tipButton.SetLabel($"Ask for a tip (${Contract.I.TipPrice} - arrives in 2 hours)");
            }
            OpenSide("Inbox", emailView);
        }

        public void ShowNotepad() => ShowNotebook(1);

        // ---------- Notebook: notes (found out), my notes (typed), pockets ----------

        // The notebook: a yellow legal pad with tabs (0 notes, 1 my notes, 2 pockets).
        public void ShowNotebook(int tab)
        {
            HideActions();
            if (!_notesStarted && assignment != null)
            {
                _notesStarted = true;
                if (string.IsNullOrEmpty(Game.I.S.notepad)) Game.I.S.notepad = assignment.startingNotes;
            }
            _notebook.Open(tab);
        }

        // (the old side-panel notebook)
        void ShowNotebookPanel(int tab)
        {
            _notebookTab = tab;
            if (tab == 1)
            {
                if (!_notesStarted && assignment != null)
                {
                    _notesStarted = true;
                    _notes = assignment.startingNotes;
                }
                notepadInput.text = _notes;
                OpenSide("Notebook", notepadView);
            }
            else OpenDynamic("Notebook", tab == 0 ? (Action)FillNotes : FillPockets);
            for (int i = 0; i < _tabs.childCount; i++)
                _tabs.GetChild(i).GetComponent<Image>().color = i == tab ? UIKit.Accent : UIKit.PanelLight;
        }

        void FillNotes()
        {
            var g = Game.I;
            if (g.S.notes.Count == 0) { Note("Nothing yet. Things you find out (and routines you document) get written down here.\nEvery new note is a checkpoint."); return; }
            for (int i = g.S.notes.Count - 1; i >= 0; i--)
            {
                var n = g.S.notes[i];
                string when = $"{Game.Weekdays[(n.day - 1) % 7].Substring(0, 3)} {Game.Clock(n.minutes)}";
                var idea = n.opening > 0 ? Contract.Idea(n.opening) : null;
                var colour = idea != null ? new Color(0.45f, 0.3f, 0.08f) : n.kind == NoteKind.Tip ? new Color(0.15f, 0.3f, 0.2f) : UIKit.PanelLight;
                var card = UIKit.Box(_dynamicContent, "Note", colour);
                UIKit.VList(card.rectTransform, 6, 14);
                UIKit.Label(card.transform, $"<size=18>{when}</size>", 18, UIKit.Muted).Size(24);
                UIKit.Label(card.transform, n.text, 24, Color.white).Size(Lines(n.text, 24) * 30);
                if (idea != null)
                {
                    var ideaText = UIKit.Label(card.transform, idea, 22, new Color(1f, 0.85f, 0.45f));
                    ideaText.gameObject.SetActive(false);
                    UIKit.Button(card.transform, "Idea!", UIKit.Warn, () => ideaText.gameObject.SetActive(!ideaText.gameObject.activeSelf), 24).Size(56);
                }
            }
        }

        void FillPockets()
        {
            var g = Game.I;
            UIKit.Label(_dynamicContent, $"Cash: ${g.S.cash}", 30, UIKit.Good, TextAnchor.MiddleLeft, FontStyle.Bold).Size(44);
            UIKit.Label(_dynamicContent, $"Pockets ({g.S.items.Count}/{State.PocketSize})", 24, UIKit.Warn, TextAnchor.MiddleLeft, FontStyle.Bold).Size(36);
            if (g.S.items.Count == 0) Note("Empty.");
            foreach (var item in g.S.items.ToArray())
            {
                var it = Items.Get(item);
                var acts = new List<(string, Action)>();
                Contract.I?.AddPocketActions(item, acts);
                bool unpaid = g.S.unpaid.Exists(u => u.EndsWith("|" + item));
                var row = UIKit.Box(_dynamicContent, "Item", UIKit.PanelLight);
                UIKit.VList(row.rectTransform, 6, 12);
                UIKit.Label(row.transform, $"<b>{it.name}</b>{(unpaid ? "  <color=#F2B33D>(not paid for)</color>" : "")}\n<size=20>{it.info}</size>", 24, Color.white).Size(60);
                foreach (var (label, act) in acts)
                {
                    var a = act;
                    UIKit.Button(row.transform, label, UIKit.Accent, () => a(), 22).Size(54);
                }
            }
            var look = g.Look;
            var known = new List<string>();
            foreach (var w in Watcher.All)
            {
                float v = Suspicion.I != null ? Suspicion.I.InfamyWith(w) : 0;
                if (v > 0) known.Add($"{w.DisplayName}: {new string('*', Mathf.CeilToInt(v))}");
            }
            if (known.Count > 0)
            {
                UIKit.Label(_dynamicContent, "Who'd recognise you right now", 24, UIKit.Bad, TextAnchor.MiddleLeft, FontStyle.Bold).Size(36);
                Note(string.Join("\n", known) + "\nChange your top and your hair to be a stranger again.");
            }
        }

        static int Lines(string text, int size) => Mathf.Max(1, Mathf.CeilToInt(text.Length / 34f) + text.Split('\n').Length - 1);

        void Note(string text) => Win98.Text98(_dynamicContent, text, 22, TextAnchor.UpperLeft).Size(Lines(text, 22) * 30);

        // ---------- Dynamic views in the side panel ----------

        void BuildDynamic()
        {
            _dynamic = UIKit.Rect(sidePanel.transform, "Dynamic");
            _dynamic.anchorMin = Vector2.zero;
            _dynamic.anchorMax = new Vector2(1, 0.9f);
            _dynamic.offsetMin = new Vector2(16, 16);
            _dynamic.offsetMax = new Vector2(-16, 0);
            _dynamicContent = UIKit.ScrollList(_dynamic, 12, 12);
            ((RectTransform)_dynamicContent.GetComponentInParent<ScrollRect>(true).transform).Stretch();
            _dynamic.gameObject.SetActive(false);

            _tabs = UIKit.Rect(sidePanel.transform, "Tabs");
            _tabs.anchorMin = new Vector2(0, 0.81f);
            _tabs.anchorMax = new Vector2(1, 0.895f);
            _tabs.offsetMin = new Vector2(16, 0);
            _tabs.offsetMax = new Vector2(-16, 0);
            UIKit.HList(_tabs, 10, 0);
            UIKit.Button(_tabs, "Notes", UIKit.PanelLight, () => ShowNotebook(0), 24);
            UIKit.Button(_tabs, "My notes", UIKit.PanelLight, () => ShowNotebook(1), 24);
            UIKit.Button(_tabs, "Pockets", UIKit.PanelLight, () => ShowNotebook(2), 24);
            _tabs.gameObject.SetActive(false);
        }

        void OpenDynamic(string title, Action fill)
        {
            _refresh = () =>
            {
                UIKit.Clear(_dynamicContent);
                fill();
            };
            _refresh();
            OpenSide(title, _dynamic.gameObject);
            _dynamicContent.GetComponentInParent<ScrollRect>(true).verticalNormalizedPosition = 1;
        }

        Button Row(string text, Color colour, Action act, int size = 24)
        {
            var b = Win98.Button98(_dynamicContent, text, act, size);
            b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
            b.GetComponentInChildren<Text>().fontStyle = FontStyle.Normal;
            b.Size(Mathf.Max(64, Lines(text, size) * 32 + 20));
            return b;
        }

        // ---------- Newspaper ----------

        public void ShowNewspaper() { HideActions(); _paperView.Open(); }

        // (old side-panel newspaper)
        public void ShowNewspaperPanel()
        {
            OpenDynamic("Daily Ledger", () =>
            {
                Note("Tap anything worth remembering to note it.");
                foreach (var a in Contract.I.Paper())
                {
                    var art = a;
                    var t = Row($"<b>{a.headline}</b>\n<size=20>{a.body}</size>", new Color(0.85f, 0.83f, 0.76f), () => Contract.I.NoteArticle(art), 24)
                        .GetComponentInChildren<Text>();
                    t.color = UIKit.Ink;
                    t.GetComponent<Outline>().enabled = false;
                }
            });
        }

        // ---------- Laptop: search ----------

        public void ShowSearch() { HideActions(); _computer.Show(Computer.App.Web); }

        // (old side-panel search)
        public void ShowSearchPanel()
        {
            OpenDynamic("Search the web", () =>
            {
                var box = UIKit.Box(_dynamicContent, "Query", Color.white);
                box.Size(70);
                var input = box.gameObject.AddComponent<InputField>();
                var t = UIKit.Label(box.transform, "", 26, UIKit.Ink);
                t.rectTransform.Stretch(14, 6, 14, 6);
                t.GetComponent<Outline>().enabled = false;
                var ph = UIKit.Label(box.transform, "Search for a name...", 26, UIKit.Muted);
                ph.rectTransform.Stretch(14, 6, 14, 6);
                ph.GetComponent<Outline>().enabled = false;
                input.textComponent = t;
                input.placeholder = ph;
                var results = UIKit.Rect(_dynamicContent, "Results");
                UIKit.VList(results, 10, 0);
                results.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                void Go()
                {
                    UIKit.Clear(results);
                    foreach (var a in Contract.I.Search(input.text))
                    {
                        var art = a;
                        var b = UIKit.Button(results, $"<b>{a.headline}</b>\n<size=20>{a.body}</size>", UIKit.PanelLight, () => Contract.I.NoteArticle(art), 24);
                        b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                        b.Size(Lines(a.headline + a.body, 24) * 30 + 30);
                    }
                }
                UIKit.Button(_dynamicContent, "Search", UIKit.Accent, Go, 26).Size(64);
                input.onEndEdit.AddListener(_ => Go());
            });
        }

        // ---------- Payphone ----------

        public void ShowPhone(Payphone phone)
        {
            var g = Game.I;
            var target = Contract.I.Target.Routine;
            OpenDynamic("Payphone", () =>
            {
                if (phone != null && phone.HasYellowPages)
                {
                    Win98.Text98(_dynamicContent, "Yellow Pages", 26, TextAnchor.MiddleLeft, FontStyle.Bold).Size(36);
                    foreach (var l in Payphone.Book(target))
                    {
                        var entry = l;
                        Row($"{l.name}  <size=20>{l.address}  {l.phone}</size>", UIKit.PanelLight, () =>
                        {
                            if (entry.target) g.Note("yellowpages.target", $"{target.displayName}: {entry.address}. Phone {entry.phone}.");
                            else g.Note("yp." + entry.name, $"{entry.name}: {entry.address}, {entry.phone}.");
                        }, 22);
                    }
                }
                else Note("Somebody's torn the Yellow Pages off this one.");
                Win98.Text98(_dynamicContent, "Call...", 26, TextAnchor.MiddleLeft, FontStyle.Bold).Size(36);
                bool any = false;
                if (g.S.notes.Exists(n => n.id == "yellowpages.target")) { any = true; Row($"{target.displayName} (home)", UIKit.Accent, CallHome); }
                if (g.S.notes.Exists(n => n.id == "jobad" || n.id == "web.work")) { any = true; Row("The paper company, Schrute Towers", UIKit.Accent, CallOffice); }
                if (Contract.I.Initiated && !Contract.I.Reported)
                {
                    any = true;
                    Row("The police - report an incident anonymously", UIKit.Bad, ReportIncident);
                }
                if (!any) Note("You don't have any numbers worth calling yet.");
            });
        }

        void ReportIncident()
        {
            var t = Contract.I.Target;
            ShowDialog("Police (on the phone)", "Police department. What's your emergency?", new List<(string, Action)>
            {
                ($"\"There's a man - {t.Routine.displayName}. Something's happened to him.\" (hang up)", () =>
                {
                    Contract.I.Report();
                    Toast("Reported. Nobody knows it was you.");
                }),
                ("(Hang up)", null),
            });
        }

        void CallHome()
        {
            var g = Game.I;
            var t = Contract.I.Target;
            bool home = t.Alive && t.AreaId != null && t.AreaId.StartsWith("suburbs.house4") && t.IsAwake;
            if (home)
            {
                ShowDialog(t.Routine.displayName + " (on the phone)", "Hello? ...Hello? Who is this? If this is about the chili, I already paid.", null);
                g.Note($"call.home.{g.Day}.{Mathf.FloorToInt(g.Minutes / 60)}", $"Rang Kevin's house at {g.ClockText} - he was home.");
            }
            else ShowDialog("", "It rings and rings. Nobody's home.", null);
        }

        void CallOffice()
        {
            var g = Game.I;
            var t = Contract.I.Target;
            string where;
            if (!t.Alive) where = "Kevin? Oh... you haven't heard.";
            else if (t.AreaId == null) where = "I'm not sure where he is.";
            else if (t.AreaId.StartsWith("business.tower1")) where = t.IsAwake ? "He's at his desk. Shall I put you through?" : "He's... resting his eyes at his desk. Again.";
            else if (t.AreaId == "business.cornerbar") where = "He's out at the Corner Bar. He's always at the Corner Bar.";
            else if (t.AreaId.StartsWith("suburbs.house")) where = "He's gone home for the day.";
            else where = "He's not in the office right now.";
            ShowDialog("Receptionist (on the phone)", $"Paper company, how can I help? Kevin Malone? {where}", null);
            g.Note($"call.office.{g.Day}.{Mathf.FloorToInt(g.Minutes / 60)}", $"Rang his office at {g.ClockText}: \"{where}\"");
        }

        // ---------- Email: the tip ----------

        void BuildTipButton()
        {
            _tipButton = UIKit.Button(emailView.transform, "", UIKit.Warn, () => { Contract.I?.BuyTip(); ShowEmail(); }, 24);
            var rt = (RectTransform)_tipButton.transform;
            rt.anchorMin = new Vector2(0.05f, 0.02f);
            rt.anchorMax = new Vector2(0.95f, 0.1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ---------- Barber ----------

        public void ShowBarber()
        {
            HideActions();
            _wardrobe.Open(null, WardrobeUI.Mode.Barber);
        }

        // ---------- Overlays ----------

        public IEnumerator ShowBusted(string reason)
        {
            CloseAll();
            _toasts.Clear();
            _toastTimer = 0;
            toastBox.SetActive(false);
            return _overlays.ShowBusted(reason);
        }

        void CloseAll()
        {
            HideActions();
            sidePanel.SetActive(false);
            if (_wardrobe.IsOpen) _wardrobe.ForceClose();
            if (_computer.IsOpen) _computer.Close();
            if (_paperView.IsOpen) _paperView.Close();
            if (_notebook.IsOpen) _notebook.Close();
        }

        // A conversation: a speech bubble over `who` (null = over the player, e.g. a voice on the phone).
        public void ShowDialog(string speaker, string line, List<(string, Action)> replies, Transform who = null)
        {
            HideActions();
            _overlays.ShowDialog(speaker, line, replies, who);
        }

        public void ShowContractDone(string name, int pay, bool onTime) => _overlays.ShowContractDone(name, pay, onTime);
        public void Say(Transform who, string text, float seconds = 3.2f) => _overlays.Say(who, text, seconds);
        public void SetNoticed(float share, Transform who) { if (_overlays != null) _overlays.SetNoticed(share, who); }
        public void ShowSpotted(Transform who) => _overlays.ShowSpotted(who);
        public void HideSpotted() => _overlays.HideSpotted();
        public void LastChancePrompt(bool on) => _overlays.LastChancePrompt(on);

        // Called after breakfast on day 1: the player sits and jots down a plan.
        public void OpenNotesAfterBreakfast()
        {
            if (_notesStarted) return;
            ShowNotebook(1);
        }

        public void ShowTravel(bool bySubway)
        {
            var g = Game.I;
            UIKit.Clear(travelButtons);
            foreach (var id in new[] { "shackleton", "business", "suburbs" })
            {
                var d = g.District(id);
                if (d == null || d == g.Current.Map) continue;
                var dest = id;
                int mins = bySubway ? g.SubwayMinutes : g.TaxiMinutes;
                Win98.Button98(travelButtons, $"{d.DisplayName}  ({mins} min)", () =>
                {
                    CloseSide();
                    g.Travel(dest, bySubway);
                }, 28).Size(90);
            }
            OpenSide(bySubway ? "Take the subway to..." : "Taxi to...", travelView);
        }

        // ---------- Wardrobe ----------

        public void ShowWardrobe(Wardrobe w)
        {
            HideActions();
            _wardrobe.Open(w);
        }

        // ---------- Following ----------

        public void SetFollowing(RoutineCharacter who, float distance)
        {
            followBar.SetActive(who != null);
            if (who != null)
                followText.text = $"Following {who.Routine.displayName}  -  {distance:0.0} m\n<size=20>Slide toward / away from them to change the gap</size>";
        }

        // ---------- Fast-forward / travel ----------

        // ---------- fading the screen (between scenes) ----------

        Image _fade;
        Text _fadeLabel;

        // Fade the whole screen to black (to = 1) or back (to = 0) over real time, with an optional caption on the
        // black (where you're going).
        public IEnumerator FadeScreen(float to, float seconds, string label = null)
        {
            if (_fade == null)
            {
                _fade = UIKit.Box(CanvasRoot, "Screen Fade", Color.black, false);
                _fade.rectTransform.Stretch();
                _fade.raycastTarget = true;
                _fadeLabel = UIKit.Label(_fade.transform, "", 34, new Color(0.9f, 0.88f, 0.82f), TextAnchor.MiddleCenter);
                _fadeLabel.rectTransform.Stretch();
                _fade.color = new Color(0, 0, 0, 0);
            }
            if (label != null) _fadeLabel.text = label;
            _fade.gameObject.SetActive(true);
            _fade.transform.SetAsLastSibling();
            float from = _fade.color.a;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                SetFade(Mathf.Lerp(from, to, t / seconds));
                yield return null;
            }
            SetFade(to);
            if (to <= 0) _fade.gameObject.SetActive(false);
        }

        void SetFade(float a)
        {
            _fade.color = new Color(0, 0, 0, a);
            var c = _fadeLabel.color;
            c.a = a;
            _fadeLabel.color = c;
        }

        public void ShowSkip(bool on, string label, bool tape)
        {
            _tape = tape;
            HideActions();
            skipOverlay.SetActive(on);
            if (!on) return;
            skipOverlay.transform.SetAsLastSibling();
            skipLabel.text = tape ? $">> {label}" : label;
            skipTint.color = tape ? new Color(0.05f, 0.08f, 0.12f, 0.18f) : new Color(0, 0, 0, 0.92f);   // (the world shows through)
            scanlines.gameObject.SetActive(tape);
            trackingBar.gameObject.SetActive(tape);
        }

        static Texture2D MakeScanlines()
        {
            var tex = new Texture2D(1, 4) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            tex.SetPixels(new[] { new Color(0, 0, 0, 0.35f), new Color(0, 0, 0, 0f), new Color(1, 1, 1, 0.05f), new Color(0, 0, 0, 0f) });
            tex.Apply();
            return tex;
        }
    }
}
