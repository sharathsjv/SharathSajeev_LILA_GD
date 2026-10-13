using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hitman
{
    // Makes all the game's text bigger (playtesters found it too small), without touching every label: each Text
    // under the canvas grows by `scale`, and is allowed to shrink back towards its old size wherever the bigger
    // text wouldn't fit its box (best fit). Picks up text created later too (menus, notes, bubbles).
    public class TextScale : MonoBehaviour
    {
        public float scale = 1.25f;

        readonly HashSet<Text> _done = new HashSet<Text>();
        static readonly List<Transform> Excluded = new List<Transform>();

        // UI that lays its text out on its own grid (the notebook's ruled lines) sizes its own text instead.
        public static void Exclude(Transform root) { if (root != null && !Excluded.Contains(root)) Excluded.Add(root); }

        static bool Skipped(Transform t)
        {
            foreach (var r in Excluded) if (r != null && t.IsChildOf(r)) return true;
            return false;
        }
        float _next;

        void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.25f;
            foreach (var t in GetComponentsInChildren<Text>(true))
            {
                if (!_done.Add(t) || Skipped(t.transform)) continue;
                int was = t.resizeTextForBestFit ? t.resizeTextMaxSize : t.fontSize;
                int min = t.resizeTextForBestFit ? t.resizeTextMinSize : was;
                int now = Mathf.RoundToInt(was * scale);
                // Best fit only shrinks text that wraps and truncates: text in a real box gets that, so it shrinks to fit
                // instead of spilling out (labels with no box of their own keep overflowing, as they were built to).
                var size = t.rectTransform.rect.size;
                if (size.y >= was * 0.9f && t.verticalOverflow == VerticalWrapMode.Overflow) t.verticalOverflow = VerticalWrapMode.Truncate;
                if (size.x >= was * 2f && t.horizontalOverflow == HorizontalWrapMode.Overflow) t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = Mathf.Max(1, min);
                t.resizeTextMaxSize = now;
                t.fontSize = now;
            }
        }
    }
}
