using System.Collections.Generic;
using QAOffice;
using UnityEngine;
using UnityEngine.UI;

namespace Hitman
{
    // Every panel and button in the hitman UI gets the same hard-edged, bevelled 90s look: rounded boxes are
    // squared off, and buttons and panels get a raised bevel in their own colour. Runs over the canvas now and
    // then so UI built later (wardrobe options, menus, notes) is covered too.
    public class HardEdges : MonoBehaviour
    {
        readonly HashSet<Image> _done = new HashSet<Image>();
        float _next;

        void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            var rounded = UIKit.Rounded;
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                if (img == null || _done.Contains(img)) continue;
                _done.Add(img);
                if (img.sprite != rounded) continue;
                img.sprite = null;
                img.type = Image.Type.Simple;
                bool frame = img.GetComponent<Button>() != null || IsPanel(img.name);
                if (frame && img.transform.Find("Edge") == null) Win98.Bevel(img.rectTransform, true, img.color);
            }
        }

        static bool IsPanel(string n) =>
            n.Contains("Panel") || n.Contains("Box") || n.Contains("Toast") || n.Contains("Tab") || n.Contains("Card") ||
            n.Contains("Following") || n.Contains("Clock") || n == "Note" || n == "Item";
    }
}
