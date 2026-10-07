using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // The work PC "OS": a Tasks app (QA builds) and a Chat app (office messenger).
    public class ComputerView : View
    {
        enum App { Tasks, Chat }

        App _app = App.Tasks;
        [SerializeField] RectTransform _body, _content;
        [SerializeField] Text _titleBar;
        [SerializeField] Button _tasksTab, _chatTab, _logoff;
        QASessionView _session;

        public override void Init()
        {
            _tasksTab.onClick.AddListener(() => Show(App.Tasks));
            _chatTab.onClick.AddListener(() => Show(App.Chat));
            _logoff.onClick.AddListener(Close);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Show(G.UnreadCount > 0 && G.OpenTaskCount == 0 ? App.Chat : _app);
        }

        public override void Close()
        {
            if (_session != null) Destroy(_session.gameObject);
            _session = null;
            _body.gameObject.SetActive(true);
            base.Close();
        }

        void Update()
        {
            _titleBar.text = $"QA-OS 3.1   |   {G.JobTitle}   |   Day {G.Day}  {G.ClockText}";
            _tasksTab.SetLabel(G.OpenTaskCount > 0 ? $"Tasks ({G.OpenTaskCount})" : "Tasks");
            _chatTab.SetLabel(G.UnreadCount > 0 ? $"Chat ({G.UnreadCount})" : "Chat");
        }

        void Show(App app)
        {
            _app = app;
            _tasksTab.image.color = app == App.Tasks ? UIKit.Accent : UIKit.PanelLight;
            _chatTab.image.color = app == App.Chat ? UIKit.Accent : UIKit.PanelLight;
            UIKit.Clear(_content);
            if (app == App.Tasks) BuildTasks();
            else BuildChat();
        }

        void Refresh() => Show(_app);

        // ---- Tasks ----

        void BuildTasks()
        {
            var list = UIKit.ScrollList(_content);
            ((RectTransform)list.parent.parent).Stretch();
            var header = UIKit.Label(list, $"Builds assigned for Day {G.Day}", 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
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
            _body.gameObject.SetActive(false);
            var rt = UIKit.Rect(_body.parent, "QA Session");
            rt.Anchor(0, 0, 1, 0.9f);
            _session = rt.gameObject.AddComponent<QASessionView>();
            _session.Begin(task, () =>
            {
                Destroy(rt.gameObject);
                _session = null;
                _body.gameObject.SetActive(true);
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
