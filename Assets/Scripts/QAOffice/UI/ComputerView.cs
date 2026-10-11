using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // The work PC: a desktop with app icons (QA Tester, Chat) whose corner badges count pending work,
    // app windows, and a taskbar whose start menu has a power button that logs you off.
    public class ComputerView : View
    {
        enum App { None, Tasks, Chat }

        [Header("Desktop")]
        [SerializeField] Button qaIcon;
        [SerializeField] GameObject qaBadge;
        [SerializeField] Text qaBadgeText;
        [SerializeField] Button chatIcon;
        [SerializeField] GameObject chatBadge;
        [SerializeField] Text chatBadgeText;

        [Header("App window")]
        [SerializeField] GameObject window;
        [SerializeField] Text windowTitle;
        [SerializeField] Button windowClose;
        [SerializeField] RectTransform _content;

        [Header("Taskbar")]
        [SerializeField] Button startButton;
        [SerializeField] Button taskbarApp;
        [SerializeField] Text taskbarClock;
        [SerializeField] GameObject startMenu;
        [SerializeField] Text startMenuUser;
        [SerializeField] Button powerButton;
        [SerializeField] Button pauseMenuButton;

        App _app = App.None;
        QASessionView _session;

        public override void Init()
        {
            qaIcon.onClick.AddListener(() => Show(App.Tasks));
            chatIcon.onClick.AddListener(() => Show(App.Chat));
            windowClose.onClick.AddListener(() => Show(App.None));
            startButton.onClick.AddListener(() => startMenu.SetActive(!startMenu.activeSelf));
            taskbarApp.onClick.AddListener(() => { if (_app != App.None) Show(_app); });
            powerButton.onClick.AddListener(Close);
            if (pauseMenuButton != null) pauseMenuButton.onClick.AddListener(() => { startMenu.SetActive(false); G.UI.Pause.Open(); });
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            startMenu.SetActive(false);
            Show(App.None); // start on the desktop
        }

        public override void Close()
        {
            EndSession();
            startMenu.SetActive(false);
            base.Close();
        }

        void Update()
        {
            SetBadge(qaBadge, qaBadgeText, G.OpenTaskCount);
            SetBadge(chatBadge, chatBadgeText, G.UnreadCount);
            taskbarClock.text = $"{G.ClockText}\n{GameManager.Weekday(G.Day)}";
            startMenuUser.text = $"You\n<size=22><color=#9AA0B4>{G.JobTitle}</color></size>";
        }

        static void SetBadge(GameObject badge, Text text, int count)
        {
            bool show = count > 0;
            if (badge.activeSelf != show) badge.SetActive(show);
            if (show) text.text = count > 9 ? "9+" : count.ToString();
        }

        void Show(App app)
        {
            EndSession();
            startMenu.SetActive(false);
            _app = app;
            window.SetActive(app != App.None);
            taskbarApp.gameObject.SetActive(app != App.None);
            if (app == App.None) return;
            windowTitle.text = app == App.Tasks ? "QA Tester - Assigned builds" : "Chat - #qa-team";
            taskbarApp.SetLabel(app == App.Tasks ? "QA Tester" : "Chat");
            UIKit.Clear(_content);
            if (app == App.Tasks) BuildTasks();
            else BuildChat();
        }

        void Refresh() => Show(_app);

        void EndSession()
        {
            if (_session != null) Destroy(_session.gameObject);
            _session = null;
        }

        // ---- Tasks ----

        void BuildTasks()
        {
            var list = UIKit.ScrollList(_content);
            ((RectTransform)list.parent.parent).Stretch();
            var header = UIKit.Label(list, $"Builds assigned for {GameManager.Weekday(G.Day)}", 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            header.Size(60);

            foreach (var task in G.TasksToday)
            {
                var row = UIKit.Box(list, "Task", UIKit.PanelLight);
                row.Size(150);
                var from = G.Character(task.Data.from);
                var text = UIKit.Label(row.transform,
                    $"<b>From {from.name}</b>  <color=#9AA0B4>({Genre(task.Data.genre)})</color>\n{task.Data.brief}", 28, Color.white);
                text.supportRichText = true;
                text.rectTransform.Anchor(0, 0, 0.72f, 1);
                text.rectTransform.offsetMin = new Vector2(20, 10);
                if (task.Done)
                {
                    var r = task.Result;
                    var status = UIKit.Label(row.transform, $"Filed\n{Mathf.RoundToInt(r.Score * 100)}%", 30,
                        r.Score >= 0.6f ? UIKit.Good : r.Score >= 0.35f ? UIKit.Warn : UIKit.Bad, TextAnchor.MiddleCenter, FontStyle.Bold);
                    status.rectTransform.Anchor(0.74f, 0, 1, 1);
                }
                else
                {
                    var t = task;
                    var start = UIKit.Button(row.transform, "Start test", UIKit.Good, () => StartSession(t));
                    start.GetComponent<RectTransform>().Anchor(0.75f, 0.2f, 0.98f, 0.8f);
                }
            }
            UIKit.Label(list, "Tip: flag anything that looks wrong while playing. You'll sort the flags into categories afterwards. Writing up a report takes " +
                              $"{G.S.reportCostMinutes} minutes of your day.", 24, UIKit.Muted);
        }

        static string Genre(string g) => g == "runner" ? "Endless runner" : g == "match3" ? "Match-3" : "Surprise genre";

        void StartSession(TaskState task)
        {
            startMenu.SetActive(false);
            UIKit.Clear(_content);
            windowTitle.text = "QA Tester - Testing build";
            var rt = UIKit.Rect(_content, "QA Session").Stretch();
            _session = rt.gameObject.AddComponent<QASessionView>();
            _session.Begin(task, () =>
            {
                _session = null;
                Show(App.Tasks);
            });
        }

        // ---- Chat ----

        void BuildChat()
        {
            var list = UIKit.ScrollList(_content);
            ((RectTransform)list.parent.parent).Stretch();
            var header = UIKit.Label(list, "#qa-team", 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            header.Size(60);

            bool any = false;
            foreach (var chat in G.ChatsSoFar)
            {
                any = true;
                var from = G.Character(chat.Data.from);
                var msg = UIKit.Label(list, $"<color={from.color}><b>{from.name}</b></color> <color=#9AA0B4>Day {chat.Data.day} {chat.Data.hour:00}:{chat.Data.minute:00}</color>\n{chat.Data.text}", 28, Color.white);
                msg.supportRichText = true;

                if (chat.Answered)
                {
                    var reply = chat.Data.replies[chat.Chosen];
                    var you = UIKit.Label(list, $"<color=#4F8EF7><b>You</b></color>\n{reply.text}", 28, Color.white);
                    you.supportRichText = true;
                    if (!string.IsNullOrEmpty(reply.response))
                    {
                        var resp = UIKit.Label(list, $"<color={from.color}><b>{from.name}</b></color>\n{reply.response}", 28, Color.white);
                        resp.supportRichText = true;
                    }
                }
                else if (chat.Data.day == G.Day)
                {
                    var row = UIKit.Rect(list, "Replies");
                    row.Size(90);
                    UIKit.HList(row, 12, 0);
                    for (int i = 0; i < chat.Data.replies.Length; i++)
                    {
                        int idx = i;
                        var c = chat;
                        UIKit.Button(row, chat.Data.replies[i].text, UIKit.PanelLight, () =>
                        {
                            G.AnswerChat(c, idx);
                            Refresh();
                        }, 24);
                    }
                }
                else UIKit.Label(list, "(no reply sent)", 24, UIKit.Muted);

                var spacer = UIKit.Rect(list, "Spacer");
                spacer.Size(16);
            }
            if (!any) UIKit.Label(list, "No messages yet.", 28, UIKit.Muted);
        }
    }
}
