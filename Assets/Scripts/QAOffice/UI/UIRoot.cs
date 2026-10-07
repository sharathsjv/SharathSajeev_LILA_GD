using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // A screen/panel laid out in the scene. Init() hooks up its buttons at runtime.
    public abstract class View : MonoBehaviour
    {
        protected GameManager G => GameManager.I;
        protected RectTransform Root => (RectTransform)transform;
        public bool IsOpen => gameObject.activeSelf;

        public virtual void Init() { }

        public virtual void Close() => gameObject.SetActive(false);
    }

    public class UIRoot : MonoBehaviour
    {
        [SerializeField] Hud hud;
        [SerializeField] ComputerView computer;
        [SerializeField] ConversationView conversation;
        [SerializeField] NotebookView notebook;
        [SerializeField] ScreensView screens;
        [SerializeField] Image toastBox;
        [SerializeField] Text toastText;

        public Hud Hud => hud;
        public ComputerView Computer => computer;
        public ConversationView Conversation => conversation;
        public NotebookView Notebook => notebook;
        public ScreensView Screens => screens;

        public bool Modal => computer.IsOpen || conversation.IsOpen || notebook.IsOpen || screens.IsOpen;

        readonly Queue<string> _toasts = new Queue<string>();
        float _toastTimer;

        public void Init()
        {
            var input = FindAnyObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (input != null && input.actionsAsset == null) input.AssignDefaultActions();
            foreach (var v in new View[] { hud, computer, conversation, notebook, screens }) v.Init();
            hud.gameObject.SetActive(true);
            computer.gameObject.SetActive(false);
            conversation.gameObject.SetActive(false);
            notebook.gameObject.SetActive(false);
            screens.gameObject.SetActive(false);
            toastBox.gameObject.SetActive(false);
        }

        public void CloseAll()
        {
            computer.Close();
            conversation.Close();
            notebook.Close();
        }

        public void Toast(string msg) => _toasts.Enqueue(msg);

        public void ClearToasts()
        {
            _toasts.Clear();
            _toastTimer = 0;
            toastBox.gameObject.SetActive(false);
        }

        void Update()
        {
            if (_toastTimer > 0)
            {
                toastBox.transform.SetAsLastSibling(); // stay above views opened after the toast
                _toastTimer -= Time.deltaTime;
                if (_toastTimer <= 0) toastBox.gameObject.SetActive(false);
                return;
            }
            if (_toasts.Count == 0) return;
            toastText.text = _toasts.Dequeue();
            toastBox.gameObject.SetActive(true);
            _toastTimer = 2.2f;
        }
    }
}
