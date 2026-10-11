using System;
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

        public bool Blocking => actionMenu.gameObject.activeSelf || sidePanel.activeSelf || skipOverlay.activeSelf;

        readonly Queue<string> _toasts = new Queue<string>();
        float _toastTimer;
        bool _emailRead, _notesStarted;
        bool _tape;

        void Awake()
        {
            notepadButton.onClick.AddListener(ShowNotepad);
            sideClose.onClick.AddListener(CloseSide);
            stopFollowButton.onClick.AddListener(() => player.StopFollow());
            notepadInput.onValueChanged.AddListener(t => _notes = t);
            scanlines.texture = MakeScanlines();
            actionMenu.gameObject.SetActive(false);
            sidePanel.SetActive(false);
            followBar.SetActive(false);
            skipOverlay.SetActive(false);
            toastBox.SetActive(false);
        }

        void Start()
        {
            Game.I.DayStarted += OnDayStarted;
            if (Game.I.Day == 1) OnDayStarted(1);   // Game.Start may have run first
        }

        string _notes = "";

        void OnDayStarted(int day)
        {
            if (day == 1 && !_emailRead) ShowEmail();
            else Toast($"Day {day}. {Game.Clock(Game.I.Minutes)}");
        }

        void Update()
        {
            var g = Game.I;
            if (g == null) return;
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

        // ---------- Action menu ----------

        public void ShowActions(string title, List<(string label, Action act)> actions, Vector2 screenPos)
        {
            actionTitle.text = title;
            UIKit.Clear(actionButtons);
            foreach (var (label, act) in actions)
            {
                var a = act;
                UIKit.Button(actionButtons, label, UIKit.PanelLight, () => { HideActions(); a(); }, 26).Size(70);
            }
            UIKit.Button(actionButtons, "Cancel", UIKit.Muted, HideActions, 24).Size(60);
            actionMenu.gameObject.SetActive(true);
            actionMenu.SetAsLastSibling();
            // Pop up next to the tap, kept on screen.
            var canvas = (RectTransform)actionMenu.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screenPos, null, out var local);
            Canvas.ForceUpdateCanvases();
            var half = canvas.rect.size / 2;
            var size = actionMenu.rect.size;
            local.x = Mathf.Clamp(local.x + 30, -half.x + 10, half.x - size.x - 10);
            local.y = Mathf.Clamp(local.y, -half.y + size.y + 10, half.y - 10);
            actionMenu.anchoredPosition = local;
        }

        public void HideActions() => actionMenu.gameObject.SetActive(false);

        // ---------- Side panel ----------

        void OpenSide(string title, GameObject view)
        {
            HideActions();
            sideTitle.text = title;
            emailView.SetActive(view == emailView);
            notepadView.SetActive(view == notepadView);
            travelView.SetActive(view == travelView);
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

        public void ShowEmail()
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
            OpenSide("Inbox", emailView);
        }

        public void ShowNotepad()
        {
            if (!_notesStarted && assignment != null)
            {
                _notesStarted = true;
                _notes = assignment.startingNotes;
            }
            notepadInput.text = _notes;
            OpenSide("Notepad", notepadView);
        }

        // Called after breakfast on day 1: the player sits and jots down a plan.
        public void OpenNotesAfterBreakfast()
        {
            if (_notesStarted) return;
            ShowNotepad();
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
                UIKit.Button(travelButtons, $"{d.DisplayName}  ({mins} min)", UIKit.Accent, () =>
                {
                    CloseSide();
                    g.Travel(dest, bySubway);
                }, 28).Size(90);
            }
            OpenSide(bySubway ? "Take the subway to..." : "Taxi to...", travelView);
        }

        // ---------- Following ----------

        public void SetFollowing(RoutineCharacter who, float distance)
        {
            followBar.SetActive(who != null);
            if (who != null)
                followText.text = $"Following {who.Routine.displayName}  -  {distance:0.0} m\n<size=20>Slide toward / away from them to change the gap</size>";
        }

        // ---------- Fast-forward / travel ----------

        public void ShowSkip(bool on, string label, bool tape)
        {
            _tape = tape;
            HideActions();
            skipOverlay.SetActive(on);
            if (!on) return;
            skipOverlay.transform.SetAsLastSibling();
            skipLabel.text = tape ? $">> {label}" : label;
            skipTint.color = tape ? new Color(0.05f, 0.08f, 0.12f, 0.45f) : new Color(0, 0, 0, 0.92f);
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
