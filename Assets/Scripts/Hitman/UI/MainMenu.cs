using System;
using QAOffice;
using UnityEngine;
using UnityEngine.UI;

namespace Hitman
{
    // The title screen: the Everyday Objects logo and a Play button, in the middle. Nothing runs until Play.
    public class MainMenu : MonoBehaviour
    {
        public static bool Open { get; private set; }

        RectTransform _root;
        Action _play, _continue;
        Button _playButton, _continueButton;

        public void Init(RectTransform parent)
        {
            _root = UIKit.Box(parent, "Main Menu", new Color32(16, 18, 24, 255), false).rectTransform.Stretch();
            _root.GetComponent<Image>().raycastTarget = true;                 // (nothing behind it can be tapped)
            var logoTex = Resources.Load<Texture2D>("HitmanLogo");
            if (logoTex != null)
            {
                var logo = UIKit.Rect(_root, "Logo");
                var img = logo.gameObject.AddComponent<RawImage>();
                img.texture = logoTex;
                img.raycastTarget = false;
                logo.anchorMin = logo.anchorMax = new Vector2(0.5f, 0.6f);
                float h = 380f, w = h * logoTex.width / logoTex.height;
                logo.sizeDelta = new Vector2(w, h);
            }
            var play = Win98.Button98(_root, "Play", () => _play?.Invoke(), 40);
            var pr = (RectTransform)play.transform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.24f);
            pr.sizeDelta = new Vector2(320, 96);
            _playButton = play;
            // Continue: back to the last note written in an earlier session (only when there is one)
            var cont = Win98.Button98(_root, "Continue", () => _continue?.Invoke(), 40);
            var cr = (RectTransform)cont.transform;
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.27f);
            cr.sizeDelta = new Vector2(320, 96);
            _continueButton = cont;
            _root.gameObject.SetActive(false);
        }

        public void Show(Action play, Action cont = null)
        {
            bool canContinue = cont != null;
            _continueButton.gameObject.SetActive(canContinue);
            _playButton.SetLabel(canContinue ? "New game" : "Play");
            ((RectTransform)_playButton.transform).anchorMin = ((RectTransform)_playButton.transform).anchorMax = new Vector2(0.5f, canContinue ? 0.14f : 0.24f);
            _continue = () =>
            {
                _root.gameObject.SetActive(false);
                Open = false;
                Game.I.ClockRunning = true;
                cont?.Invoke();
            };
            _play = () =>
            {
                _root.gameObject.SetActive(false);
                Open = false;
                Game.I.ClockRunning = true;
                play?.Invoke();
            };
            Open = true;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
        }

        void LateUpdate()
        {
            if (!Open) return;
            Game.I.ClockRunning = false;                                       // the city waits for you
            _root.SetAsLastSibling();
        }
    }
}
