using UnityEngine;

namespace Hitman
{
    // Orthographic camera. Each area sets its own direction, tilt, zoom and distance (see Area's "Camera here").
    // Follows the player; when following a target it frames both of them, zooming out as the gap grows.
    // On streets set to switch sides, it sits low on the far side of the street from the player and swings
    // round when they cross, staying inside the street so the buildings behind it are cut away.
    [RequireComponent(typeof(Camera))]
    public class HitmanCamera : MonoBehaviour
    {
        [Tooltip("Default distance back from the player for areas that don't set their own.")]
        [SerializeField] float distance = 40f;
        [Tooltip("Default tilt for areas that don't set their own (90 = straight down).")]
        [SerializeField] float pitch = 50f;
        [Tooltip("Default zoom for areas that don't set their own.")]
        [SerializeField] float normalSize = 6f;
        [SerializeField] float minPairSize = 4f;
        [SerializeField] float maxPairSize = 11f;
        [SerializeField] float smoothTime = 0.3f;
        [Tooltip("How quickly the camera swings round when the player crosses the street.")]
        [SerializeField] float swingTime = 0.5f;
        [Tooltip("How far past the street's centre line the player has to go before the camera swings round.")]
        [SerializeField] float crossMargin = 0.6f;
        [Tooltip("Near clip plane. Keep it small and positive: URP works out fog depth from it on an orthographic " +
                 "camera, and a negative value floods the whole view with fog.")]
        [SerializeField] float nearPlane = 0.01f;
        [Tooltip("Streets: how far past the street's half width (onto the sidewalks) the camera stays on its rail " +
                 "down the middle of the street before it starts following the player sideways (down side roads).")]
        [SerializeField] float railMargin = 2.5f;

        Camera _cam;
        Transform _a, _b;
        Vector3 _vel;
        float _sizeVel;
        float _size, _pitch, _yaw, _dist;
        Area _area;
        int _side = 1;
        float _yawNow, _yawVel;
        float _pitchNow, _pitchVel;
        Transform _showcase;
        float _showcaseShift;
        Transform _spotA, _spotB;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _size = normalSize;
            _pitch = pitch;
            _dist = distance;
        }

        // Use this area's camera setup (call before Snap when changing area).
        public void Apply(Area a)
        {
            if (_cam == null) Awake();
            _area = a;
            _pitch = a.CameraPitch > 0 ? a.CameraPitch : pitch;
            _yaw = a.CameraYaw;
            _size = a.CameraSize > 0 ? a.CameraSize : normalSize;
            _dist = a.CameraDistance > 0 ? a.CameraDistance : distance;
            _yawNow = TargetYaw();
            _pitchNow = _pitch;
            transform.rotation = Quaternion.Euler(_pitch, _yawNow, 0);
        }

        // Close-up of someone (the wardrobe): low and near, with them on the left of the screen and room on the
        // right for a panel taking up the given share of the screen. Pass null to go back to normal.
        // inFront: look at them from the front, whichever way they face (the barber's chair, facing the mirror),
        // instead of turning them to the camera; anything in between (the mirror, its wall) is hidden meanwhile.
        public void Showcase(Transform who, float panelShare = 0.45f, bool inFront = false)
        {
            _showcase = who;
            _showcaseShift = panelShare;
            _showcaseFront = who != null && inFront;
            if (!_showcaseFront) ShowHidden();
        }

        bool _showcaseFront;
        readonly System.Collections.Generic.List<Renderer> _hidden = new System.Collections.Generic.List<Renderer>();

        // Hide whatever stands between the camera and them (checked along a few lines from their knees to their head).
        void HideBetween(Vector3 camPos)
        {
            ShowHidden();
            if (_area == null) return;
            foreach (var r in _area.GetComponentsInChildren<Renderer>())
            {
                if (r.transform.IsChildOf(_showcase) || !r.enabled) continue;
                var b = r.bounds;
                foreach (var h in new[] { 0.5f, 1.0f, 1.4f })
                {
                    var from = _showcase.position + Vector3.up * h;
                    if (b.Contains(from)) break;                                // (their chair)
                    var to = camPos - from;
                    if (b.IntersectRay(new Ray(from, to.normalized), out var d) && d < to.magnitude)
                    {
                        r.enabled = false;
                        _hidden.Add(r);
                        break;
                    }
                }
            }
        }

        void ShowHidden()
        {
            foreach (var r in _hidden) if (r != null) r.enabled = true;
            _hidden.Clear();
        }

        // Spotted: close in on whoever spotted the player and the player (null to go back to normal).
        public void Spotlight(Transform watcher, Transform player, float minSize = 1.8f)
        {
            _spotA = watcher;
            _spotB = player;
            _spotMin = minSize;
        }

        float _spotMin = 1.8f;

        public void Follow(Transform a, Transform b = null)
        {
            _a = a;
            _b = b;
        }

        public void Snap(Transform a)
        {
            _a = a;
            _b = null;
            if (_cam == null) Awake();
            if (_area != null && _area.FixedCamera) a = _area.CameraHome;
            UpdateSide(a.position, true);
            _yawNow = TargetYaw();
            _pitchNow = _pitch;
            transform.rotation = Quaternion.Euler(_pitch, _yawNow, 0);
            transform.position = Goal(OnRail(a.position));
            _cam.orthographicSize = _size;
            _cam.nearClipPlane = NearPlane();
        }

        void LateUpdate()
        {
            if (_a == null) return;
            UpdateSide(_a.position, false);
            Vector3 focus;
            float size;
            bool smallRoom = false;
            if (_b != null)
            {
                focus = (_a.position + _b.position) / 2;
                float gap = Vector3.Distance(_a.position, _b.position);
                size = Mathf.Clamp(gap * 0.75f + 2.5f, minPairSize, maxPairSize);
            }
            else
            {
                focus = _a.position;
                size = _size;
            }
            if (_area != null && _area.FixedCamera && _b == null)
            {
                focus = _area.CameraHome.position;
                // In a small side room (washroom, cupboard) close in on that room.
                if (RoomVisibility.SmallRoom(_area, out var roomCentre, out var roomSize)) { focus = roomCentre; size = roomSize; smallRoom = true; }
            }
            if (_showcase == null && _spotB == null) focus = OnRail(focus);
            if (_area != null) focus = _area.ClampToBounds(focus);           // never past the district's limits
            float pitchGoal = _pitch;
            if (_spotB != null)
            {
                var other = _spotA != null ? _spotA : _spotB;
                focus = (other.position + _spotB.position) / 2 + Vector3.up * 0.3f;
                size = Mathf.Clamp(Vector3.Distance(other.position, _spotB.position) * 0.45f + 1.4f, _spotMin, Mathf.Max(3.5f, _spotMin));
            }
            if (_showcase != null)
            {
                size = ShowcaseSize;
                pitchGoal = ShowcasePitch;
                // Centre to the right of them so they stand in the middle of the space left of the panel.
                focus = _showcase.position + Vector3.up * (ShowcaseSize - 1.25f)   // (Goal adds a metre)
                        + transform.right * (_showcaseShift * size * _cam.aspect);
            }
            // A change of view (other side of the street, the wardrobe close-up, being spotted, a small room) is a
            // straight cut, not a swing; only following the player as they walk is smoothed.
            int mode = _side + (_showcase != null ? 10 : 0) + (_spotB != null ? 100 : 0) + (smallRoom ? 1000 : 0);
            if (mode != _mode)
            {
                _mode = mode;
                _yawNow = TargetYaw();
                _pitchNow = pitchGoal;
                _yawVel = _pitchVel = _sizeVel = 0;
                _vel = Vector3.zero;
                transform.rotation = Quaternion.Euler(_pitchNow, _yawNow, 0);
                transform.position = Goal(focus);
                _cam.orthographicSize = size;
                _cam.nearClipPlane = NearPlane();
                if (_showcaseFront) HideBetween(transform.position);
                return;
            }
            // Real time, so the camera keeps moving smoothly in slow motion.
            float dt = Time.unscaledDeltaTime;
            float zoomTime = _spotB != null ? 0.25f : smoothTime;
            _yawNow = Mathf.SmoothDampAngle(_yawNow, TargetYaw(), ref _yawVel, swingTime, Mathf.Infinity, dt);
            _pitchNow = Mathf.SmoothDamp(_pitchNow, pitchGoal, ref _pitchVel, smoothTime, Mathf.Infinity, dt);
            transform.rotation = Quaternion.Euler(_pitchNow, _yawNow, 0);
            transform.position = Vector3.SmoothDamp(transform.position, Goal(focus), ref _vel, zoomTime, Mathf.Infinity, dt);
            _cam.orthographicSize = Mathf.SmoothDamp(_cam.orthographicSize, size, ref _sizeVel, zoomTime, Mathf.Infinity, dt);
            _cam.nearClipPlane = NearPlane();
            if (_showcaseFront) HideBetween(Goal(focus));
        }

        int _mode = int.MinValue;

        const float ShowcaseSize = 1.2f, ShowcasePitch = 16f, ShowcaseDistance = 2.5f;

        // The wardrobe close-up stands 2.5 m from the player: anything behind the camera there would fill the screen.
        float NearPlane() => _showcase != null ? Mathf.Max(0.1f, nearPlane) : nearPlane;

        bool Street => _area != null && _area.SwitchSides;

        // Streets: the camera is on a rail down the street (down the sidewalk on the player's side, if the area sets
        // an offset). It slides along with the player but never towards the buildings (while they're on the street or its sidewalks; down a side road it follows them).
        Vector3 OnRail(Vector3 focus)
        {
            if (!Street) return focus;
            var local = _area.transform.InverseTransformPoint(focus);
            float across = local.x - _area.StreetCenterX;
            if (Mathf.Abs(across) > _area.StreetHalfWidth + railMargin) return focus;
            local.x = _area.StreetCenterX + _side * _area.RailOffset;
            return _area.transform.TransformPoint(local);
        }

        // Which side of the street the player is on (+1 = the +X side): the camera looks at them from the other side.
        void UpdateSide(Vector3 player, bool force)
        {
            if (!Street) { _side = 1; return; }
            float x = _area.transform.InverseTransformPoint(player).x - _area.StreetCenterX;
            if (force) _side = x >= 0 ? 1 : -1;
            else if (x > crossMargin) _side = 1;
            else if (x < -crossMargin) _side = -1;
            _area.Cutaway(_side > 0 ? -1 : 1);         // the camera sits on the opposite side to the player
        }

        float TargetYaw() => _showcaseFront ? _showcase.eulerAngles.y + 180f : _side > 0 ? _yaw : _yaw + 180f;

        Vector3 Goal(Vector3 focus)
        {
            // Close-ups stand just in front of them, so walls and rooms behind the camera aren't in the shot.
            var goal = focus + Vector3.up - transform.forward * (_showcase != null ? ShowcaseDistance : _dist);
            if (Street)
            {
                var local = _area.transform.InverseTransformPoint(goal);
                float x = Mathf.Clamp(local.x, _area.StreetCenterX - _area.StreetHalfWidth, _area.StreetCenterX + _area.StreetHalfWidth);
                var back = _area.transform.InverseTransformDirection(transform.forward);
                if (_area.RailOffset > 0 && Mathf.Abs(back.x) > 0.01f)
                    // Slide in along the view instead, so the framing stays put (it's orthographic).
                    goal += transform.forward * ((x - local.x) / back.x);
                else
                {
                    local.x = x;
                    goal = _area.transform.TransformPoint(local);
                }
            }
            return goal;
        }
    }
}
