using UnityEngine;

namespace QAOffice
{
    // Browsers can't lock orientation, so when a phone is held upright, cover the game with a
    // "rotate your phone" panel and pause the clock until it's landscape again.
    public class OrientationOverlay : MonoBehaviour
    {
        [SerializeField] GameObject panel;

        bool _pausedClock;

        void Update()
        {
            bool portrait = Screen.height > Screen.width;
            if (panel.activeSelf != portrait)
            {
                panel.SetActive(portrait);
                if (portrait) transform.SetAsLastSibling();
            }

            var g = GameManager.I;
            if (g == null) return;
            if (portrait && g.ClockRunning)
            {
                g.ClockRunning = false;
                _pausedClock = true;
            }
            else if (!portrait && _pausedClock)
            {
                g.ClockRunning = true;
                _pausedClock = false;
            }
        }
    }
}
