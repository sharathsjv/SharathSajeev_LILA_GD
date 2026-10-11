using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // Top-left: weekday with the time below. Top-right: job title. Bottom-right: notebook icon button.
    public class Hud : View
    {
        [SerializeField] Text dayText;
        [SerializeField] Text timeText;
        [SerializeField] Text jobTitle;
        [SerializeField] Text hint;
        [Tooltip("Optional shortcut to the work PC (hidden by default; tapping the desk opens it).")]
        [SerializeField] Button computerButton;
        [SerializeField] Button notebookButton;
        [SerializeField] Button pauseButton;

        public override void Init()
        {
            if (computerButton != null) computerButton.onClick.AddListener(() => G.Office.GoToDesk());
            notebookButton.onClick.AddListener(() => G.UI.Notebook.Open());
            pauseButton.onClick.AddListener(() => G.UI.Pause.Open());
        }

        void Update()
        {
            var g = G;
            if (g == null) return;
            dayText.text = g.Day == 0 ? "" : GameManager.Weekday(g.Day);
            timeText.text = g.Day == 0 ? "" : g.ClockText;
            jobTitle.text = g.JobTitle;
            if (computerButton != null)
            {
                int n = g.UnreadCount + g.OpenTaskCount;
                computerButton.SetLabel(n > 0 ? $"Computer ({n})" : "Computer");
            }
            hint.enabled = g.ClockRunning && !g.UI.Modal;
            // Conversations, lunch and the PC use the screen edges; get out of their way.
            bool show = !g.UI.Modal;
            if (notebookButton.gameObject.activeSelf != show) notebookButton.gameObject.SetActive(show);
            // Pause is available whenever the game is running (the PC has its own in the Start menu).
            bool pause = g.Day > 0 && !g.UI.Screens.IsOpen && !g.UI.Computer.IsOpen && !g.UI.Pause.IsOpen;
            if (pauseButton.gameObject.activeSelf != pause) pauseButton.gameObject.SetActive(pause);
        }
    }
}
