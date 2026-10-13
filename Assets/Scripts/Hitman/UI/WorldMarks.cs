using System.Collections.Generic;
using QAOffice;
using UnityEngine;
using UnityEngine.UI;
using static Hitman.Win98;

namespace Hitman
{
    // Little signs drawn over the world, under the rest of the UI:
    //  - Interaction icons (a setting in the pause menu): a tiny marker over everything the player can tap for
    //    something to do - things, doors, people.
    //  - How the player is doing: comic growl lines out of the stomach when hungry, a "Z!" over the head when sleepy.
    public class WorldMarks : MonoBehaviour
    {
        const string IconsPref = "hitman.interactIcons";
        const float IconRange = 22f;

        RectTransform _root, _layer, _icons, _info;
        readonly List<RectTransform> _pool = new List<RectTransform>();
        readonly List<Component> _things = new List<Component>();
        Area _scannedFor;
        float _rescan;

        RectTransform _growl, _sleepy;
        readonly List<Image> _waves = new List<Image>();
        Text _z;

        public bool IconsOn { get; private set; }
        // The tutorial shows them off whatever the player's state.
        public bool ShowHungry { get; set; }
        public bool ShowSleepy { get; set; }

        public void Init(RectTransform root)
        {
            _root = root;
            _layer = UIKit.Rect(_root, "World Marks").Stretch();
            _layer.SetAsFirstSibling();                                  // under every panel, dialog and HUD button
            _icons = UIKit.Rect(_layer, "Interaction Icons").Stretch();
            BuildNeeds();
            BuildInfo();
            try { IconsOn = PlayerPrefs.GetInt(IconsPref, 0) == 1; } catch { IconsOn = false; }
        }

        // ---------- Interaction icons ----------

        public void SetIcons(bool on)
        {
            IconsOn = on;
            try { PlayerPrefs.SetInt(IconsPref, on ? 1 : 0); PlayerPrefs.Save(); } catch { }
            _scannedFor = null;
            if (on) ShowInfo();
        }

        // The marker itself: a small yellow ring with a dot, outlined so it reads over anything.
        static Sprite _icon;
        public static Sprite Icon()
        {
            if (_icon != null) return _icon;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var yellow = new Color(1f, 0.85f, 0.15f);
            var ink = new Color(0.08f, 0.08f, 0.1f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                    Color c = Color.clear;
                    if (d < 31) c = ink;                                    // outline
                    if (d < 27) c = yellow;                                 // ring
                    if (d < 19) c = ink;
                    if (d < 16) c = new Color(1, 1, 1, 0);                  // see-through middle
                    if (d < 8) c = yellow;                                  // dot
                    if (d < 8 && d > 6.5f) c = Color.Lerp(yellow, ink, 0.5f);
                    c.a *= Mathf.Clamp01(31.5f - d);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return _icon = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        void Collect(Game g)
        {
            _things.Clear();
            _scannedFor = g.Current;
            _rescan = 1f;
            var dummy = new ActionMenu(null, null, "");
            foreach (var u in FindObjectsByType<Usable>(FindObjectsSortMode.None))
            {
                if (u.GetComponentInParent<Area>() != g.Current) continue;
                bool any = false;
                if (u.Actions != null) foreach (var a in u.Actions) if (!PlayerController.Hidden(a)) { any = true; break; }
                if (!any) foreach (var src in u.GetComponents<IActionSource>()) { src.AddActions(dummy); any = dummy.Items.Count > 0; dummy.Items.Clear(); if (any) break; }
                if (any) _things.Add(u);
            }
            foreach (var s in FindObjectsByType<Setup>(FindObjectsSortMode.None))
            {
                if (s.GetComponent<Usable>() != null || s.GetComponentInParent<RoutineCharacter>() != null) continue;
                if (s.GetComponentInParent<Area>() != g.Current) continue;
                s.AddActions(dummy);
                if (dummy.Items.Count > 0) _things.Add(s);
                dummy.Items.Clear();
            }
            if (g.People != null) foreach (var p in g.People) _things.Add(p);
        }

        // Where to put the icon (over the top of it), or null if it can't be seen right now.
        static Vector3? Anchor(Component c, Game g)
        {
            if (c is RoutineCharacter rc)
            {
                if (!rc.Visible || !rc.Alive || rc.AreaId != g.Current.Id) return null;
                // (the target only once the player has tapped them: don't give away who it is)
                if (Contract.I != null && rc == Contract.I.Target && !g.S.Has("target.tapped")) return null;
                return rc.transform.position + Vector3.up * 2.0f;
            }
            Bounds b = default;
            bool found = false, anyRenderer = false;
            foreach (var r in c.GetComponentsInChildren<Renderer>())
            {
                anyRenderer = true;
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (found) b.Encapsulate(r.bounds); else { b = r.bounds; found = true; }
            }
            if (!found)
            {
                if (anyRenderer) return null;                               // in a room that's hidden right now
                var col = c.GetComponentInChildren<Collider>();
                if (col == null || !col.enabled) return null;
                b = col.bounds;
            }
            return new Vector3(b.center.x, Mathf.Min(b.max.y, b.min.y + 2.6f) + 0.15f, b.center.z);
        }

        void UpdateIcons(Game g, Camera cam, bool show)
        {
            int used = 0;
            if (show && IconsOn)
            {
                _rescan -= Time.unscaledDeltaTime;
                if (_scannedFor != g.Current || _rescan <= 0) Collect(g);
                var me = g.Player.transform.position;
                foreach (var c in _things)
                {
                    if (c == null) continue;
                    var at = Anchor(c, g);
                    if (at == null || (at.Value - me).sqrMagnitude > IconRange * IconRange) continue;
                    var sp = cam.WorldToScreenPoint(at.Value);
                    if (sp.z <= 0 || sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height) continue;
                    if (used == _pool.Count) _pool.Add(NewIcon());
                    var rt = _pool[used++];
                    rt.gameObject.SetActive(true);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_icons, sp, null, out var local);
                    rt.anchoredPosition = local + Vector2.up * (2f * Mathf.Sin(Time.unscaledTime * 3f + local.x * 0.01f));
                }
            }
            for (int i = used; i < _pool.Count; i++) if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
        }

        RectTransform NewIcon()
        {
            var rt = UIKit.Rect(_icons, "Icon");
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Icon();
            img.raycastTarget = false;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(20, 20);
            return rt;
        }

        // Turned on: a word on what the icon means.
        void BuildInfo()
        {
            _info = UIKit.Box(_root, "Icons Info", new Color(0, 0, 0, 0.55f), false).rectTransform.Stretch();
            _info.GetComponent<Image>().raycastTarget = true;
            var win = Bevel(UIKit.Rect(_info, "Window"), true);
            win.anchorMin = new Vector2(0.3f, 0.36f);
            win.anchorMax = new Vector2(0.7f, 0.66f);
            win.offsetMin = win.offsetMax = Vector2.zero;
            TitleBar(win, "Interaction icons", 40, 22);
            var icon = UIKit.Rect(win, "Icon");
            icon.gameObject.AddComponent<Image>().sprite = Icon();
            icon.anchorMin = icon.anchorMax = new Vector2(0, 1);
            icon.pivot = new Vector2(0, 1);
            icon.sizeDelta = new Vector2(64, 64);
            icon.anchoredPosition = new Vector2(28, -72);
            var t = Text98(win, "From now on, this symbol appears above everything you can interact with - things, doors and people. " +
                                "Tap it to see what you can do.\n\nYou can turn it off any time in the pause menu.", 24, TextAnchor.UpperLeft);
            t.rectTransform.Stretch(112, 80, 24, 60);
            var ok = Button98(win, "OK", () => _info.gameObject.SetActive(false), 24);
            var ort = (RectTransform)ok.transform;
            ort.anchorMin = ort.anchorMax = new Vector2(0.5f, 0);
            ort.pivot = new Vector2(0.5f, 0);
            ort.sizeDelta = new Vector2(140, 52);
            ort.anchoredPosition = new Vector2(0, 16);
            _info.gameObject.SetActive(false);
        }

        void ShowInfo()
        {
            _info.gameObject.SetActive(true);
            _info.SetAsLastSibling();
        }

        // ---------- Hungry / sleepy ----------

        static Sprite _arc;
        // One comic "growl" line: a short arc bulging to the right.
        static Sprite Arc()
        {
            if (_arc != null) return _arc;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var centre = new Vector2(-2, n / 2f);
            const float radius = 44, half = 8.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f) - centre;
                    float ang = Mathf.Abs(Mathf.Atan2(p.y, p.x)) * Mathf.Rad2Deg;
                    float d = Mathf.Abs(p.magnitude - radius);
                    float a = Mathf.Clamp01(half + 0.5f - d) * Mathf.Clamp01((40f - ang) / 4f);
                    // dark edge, white core
                    var c = d < half - 3.5f ? Color.white : new Color(0.08f, 0.08f, 0.1f);
                    c.a = a;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return _arc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        void BuildNeeds()
        {
            _growl = UIKit.Rect(_layer, "Hungry");
            _growl.anchorMin = _growl.anchorMax = new Vector2(0.5f, 0.5f);
            _growl.sizeDelta = new Vector2(10, 10);
            foreach (int side in new[] { -1, 1 })
                for (int i = 0; i < 3; i++)
                {
                    var rt = UIKit.Rect(_growl, "Wave");
                    var img = rt.gameObject.AddComponent<Image>();
                    img.sprite = Arc();
                    img.raycastTarget = false;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    float s = 24 + i * 10;
                    rt.sizeDelta = new Vector2(s * 0.6f, s);
                    rt.anchoredPosition = new Vector2(side * (24 + i * 10), 0);
                    rt.localScale = new Vector3(side, 1, 1);
                    _waves.Add(img);
                }
            _growl.gameObject.SetActive(false);

            _sleepy = UIKit.Rect(_layer, "Sleepy");
            _sleepy.anchorMin = _sleepy.anchorMax = new Vector2(0.5f, 0.5f);
            _sleepy.sizeDelta = new Vector2(80, 50);
            _z = UIKit.Label(_sleepy, "Z<size=24>z</size>!", 38, new Color(0.75f, 0.85f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            _z.rectTransform.Stretch();
            _z.raycastTarget = false;
            var o = _z.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.08f, 0.08f, 0.1f);
            o.effectDistance = new Vector2(2, -2);
            _sleepy.gameObject.SetActive(false);
        }

        void UpdateNeeds(Game g, Camera cam, bool show)
        {
            var player = g.Player.transform;
            bool hungry = show && (ShowHungry || g.Hunger > 0.6f);
            bool sleepy = show && (ShowSleepy || g.Tiredness > 0.6f);

            // Hungry: every few seconds the stomach growls - lines ripple out from it on both sides.
            float t = Time.unscaledTime;
            float phase = Mathf.Repeat(t, ShowHungry ? 1.8f : 3.2f);
            bool growling = hungry && phase < 1.4f;
            if (_growl.gameObject.activeSelf != growling) _growl.gameObject.SetActive(growling);
            if (growling)
            {
                Put(_growl, cam, player.position + Vector3.up * 1.0f, 0);
                _growl.anchoredPosition += new Vector2(Mathf.Sin(t * 60f) * 1.5f, 0);   // a little rumble
                for (int k = 0; k < _waves.Count; k++)
                {
                    int i = k % 3;
                    float local = phase - i * 0.18f;                               // inner line first, then outward
                    float a = local < 0 ? 0 : Mathf.Clamp01(local * 6f) * Mathf.Clamp01((1.2f - local) * 3f);
                    _waves[k].color = new Color(1, 1, 1, a);
                }
            }

            // Sleepy: a Z! bobbing over the head.
            if (_sleepy.gameObject.activeSelf != sleepy) _sleepy.gameObject.SetActive(sleepy);
            if (sleepy)
            {
                Put(_sleepy, cam, player.position + Vector3.up * 2.05f, 18);
                _sleepy.anchoredPosition += new Vector2(Mathf.Sin(t * 1.3f) * 4f, Mathf.Sin(t * 2.2f) * 5f);
                _sleepy.localRotation = Quaternion.Euler(0, 0, 10f * Mathf.Sin(t * 1.3f));
            }
        }

        void Put(RectTransform rt, Camera cam, Vector3 world, float up)
        {
            var sp = cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, sp, null, out var local);
            rt.anchoredPosition = local + Vector2.up * up;
        }

        // ---------- Per frame ----------

        void LateUpdate()
        {
            var g = Game.I;
            if (g == null || g.Current == null) return;
            var cam = g.Cam != null ? g.Cam.GetComponent<Camera>() : null;
            if (cam == null) return;
            bool show = cam.isActiveAndEnabled && !MainMenu.Open && !g.Busy && !g.UI.BlockingExceptMenu;
            _layer.SetAsFirstSibling();
            UpdateIcons(g, cam, show);
            UpdateNeeds(g, cam, show || ShowHungry || ShowSleepy);
            if (_info.gameObject.activeSelf) _info.SetAsLastSibling();
        }
    }
}
