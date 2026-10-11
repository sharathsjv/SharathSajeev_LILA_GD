using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // Pause menu: freezes the whole game (clock, people, minigames) until resumed.
    public class PauseView : View
    {
        [SerializeField] Button resumeButton;
        [SerializeField] Button restartButton;
        [SerializeField] Text restartLabel;

        bool _confirmRestart;

        public override void Init()
        {
            resumeButton.onClick.AddListener(Close);
            restartButton.onClick.AddListener(() =>
            {
                // Ask once before throwing the run away.
                if (!_confirmRestart)
                {
                    _confirmRestart = true;
                    restartLabel.text = "Tap again to restart";
                    return;
                }
                SceneFlow.Restart();
            });
        }

        public void Open()
        {
            _confirmRestart = false;
            restartLabel.text = "Restart game";
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            G.SetPaused(true);
        }

        public override void Close()
        {
            if (G != null && G.Paused) G.SetPaused(false);
            base.Close();
        }
    }
}
