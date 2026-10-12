using System.Collections.Generic;
using QAOffice;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hitman
{
    // A small keyboard in the 90s desktop style that pops up whenever a text box is selected (web search,
    // CrewSpace search, My notes). On phones it replaces the system keyboard; on a computer you can type either way.
    public class OnScreenKeyboard : MonoBehaviour
    {
        static readonly string[] Rows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL'", "ZXCVBNM,.?" };

        RectTransform _root;
        InputField _target, _ignore;
        bool _shift = true;
        readonly List<(Text t, char c)> _letters = new List<(Text, char)>();

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public void Init(RectTransform parent)
        {
            _root = Win98.Bevel(UIKit.Rect(parent, "Keyboard"), true);
            _root.anchorMin = new Vector2(0.18f, 0.01f);
            _root.anchorMax = new Vector2(0.82f, 0.4f);
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            Win98.TitleBar(_root, "Keyboard", 40, 22, Hide);
            var keys = UIKit.Rect(_root, "Keys").Stretch(10, 10, 10, 54);
            UIKit.VList(keys, 6, 0);
            foreach (var row in Rows)
            {
                var r = Row(keys);
                foreach (var ch in row) Key(r, ch.ToString(), ch);
            }
            var last = Row(keys);
            Special(last, "Shift", () => { _shift = !_shift; Relabel(); }, 1.4f);
            Special(last, "Space", () => Type(' '), 4f);
            Special(last, "Back", Backspace, 1.4f);
            Special(last, "Enter", Enter, 1.4f);
            Relabel();
            _root.gameObject.SetActive(false);
        }

        RectTransform Row(RectTransform parent)
        {
            var r = UIKit.Rect(parent, "Row");
            UIKit.HList(r, 6, 0);
            return r;
        }

        void Key(RectTransform row, string label, char c)
        {
            var b = Win98.Button98(row, label, () => Type(c), 24);
            if (char.IsLetter(c)) _letters.Add((b.GetComponentInChildren<Text>(), c));
        }

        void Special(RectTransform row, string label, System.Action act, float weight)
        {
            var b = Win98.Button98(row, label, () => act(), 22);
            b.Size(-1, -1, weight);
        }

        void Relabel()
        {
            foreach (var (t, c) in _letters) t.text = (_shift ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c)).ToString();
        }

        void Update()
        {
            var es = EventSystem.current;
            if (es == null) return;
            var sel = es.currentSelectedGameObject;
            var field = sel != null ? sel.GetComponent<InputField>() : null;
            if (field == null) _ignore = null;                       // deselected: the next tap on it opens the keyboard again
            if (field != null && field != _target && field != _ignore && field.gameObject.activeInHierarchy) Show(field);
            if (IsOpen && (_target == null || !_target.gameObject.activeInHierarchy)) Hide();
        }

        void Show(InputField f)
        {
            _target = f;
            // no system keyboard on phones: this one types instead
            if (Application.isMobilePlatform) { f.readOnly = true; f.shouldHideMobileInput = true; }
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            _shift = string.IsNullOrEmpty(f.text) || f.text.EndsWith(". ") || f.text.EndsWith("\n");
            Relabel();
        }

        public void Hide()
        {
            if (_target != null && _target.readOnly) _target.onEndEdit?.Invoke(_target.text);
            if (_target != null && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == _target.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            _ignore = _target;
            _target = null;
            if (_root != null) _root.gameObject.SetActive(false);
        }

        void Type(char c)
        {
            if (_target == null) return;
            if (char.IsLetter(c)) c = _shift ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
            Set(_target.text + c);
            if (_shift && char.IsLetter(c)) { _shift = false; Relabel(); }
        }

        void Backspace()
        {
            if (_target == null || string.IsNullOrEmpty(_target.text)) return;
            Set(_target.text.Substring(0, _target.text.Length - 1));
        }

        void Enter()
        {
            if (_target == null) return;
            if (_target.lineType == InputField.LineType.MultiLineNewline) { Set(_target.text + "\n"); _shift = true; Relabel(); }
            else
            {
                _target.onEndEdit?.Invoke(_target.text);
                Hide();
            }
        }

        // Setting the text fires onValueChanged as if typed; keep the caret at the end.
        void Set(string text)
        {
            _target.text = text;
            _target.caretPosition = text.Length;
        }
    }
}
