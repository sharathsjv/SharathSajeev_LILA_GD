using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // Keeps UI out of notches and rounded corners on phones.
    public class SafeArea : MonoBehaviour
    {
        Rect _last;

        void Update()
        {
            var safe = Screen.safeArea;
            if (safe == _last || Screen.width == 0) return;
            _last = safe;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
