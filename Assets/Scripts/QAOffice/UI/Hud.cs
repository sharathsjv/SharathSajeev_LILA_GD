using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    public class Hud : View
    {
        [SerializeField] Text clock, jobTitle, hint;
        [SerializeField] Button computerButton, notebookButton;

        public override void Init()
        {
            computerButton.onClick.AddListener(() => G.Office.GoToDesk());
            notebookButton.onClick.AddListener(() => G.UI.Notebook.Open());
        }

        void Update()
        {
            var g = G;
            if (g == null) return;
            clock.text = g.Day == 0 ? "" : $"Day {g.Day}   {g.ClockText}";
            jobTitle.text = g.JobTitle;
            int n = g.UnreadCount + g.OpenTaskCount;
            computerButton.SetLabel(n > 0 ? $"Computer ({n})" : "Computer");
            hint.enabled = g.ClockRunning && !g.UI.Modal;
        }
    }
}
