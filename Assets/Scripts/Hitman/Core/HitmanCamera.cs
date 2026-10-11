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

        Camera _cam;
        Transform _a, _b;
        Vector3 _vel;
        float _sizeVel;
        float _size, _pitch, _yaw, _dist;
        Area _area;
        int _side = 1;
        float _yawNow, _yawVel;

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
            transform.rotation = Quaternion.Euler(_pitch, _yawNow, 0);
        }

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
            transform.rotation = Quaternion.Euler(_pitch, _yawNow, 0);
            transform.position = Goal(a.position);
            _cam.orthographicSize = _size;
        }

        void LateUpdate()
        {
            if (_a == null) return;
            Vector3 focus;
            float size;
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
            if (_area != null && _area.FixedCamera && _b == null) focus = _area.CameraHome.position;
            UpdateSide(_a.position, false);
            _yawNow = Mathf.SmoothDampAngle(_yawNow, TargetYaw(), ref _yawVel, swingTime);
            transform.rotation = Quaternion.Euler(_pitch, _yawNow, 0);
            transform.position = Vector3.SmoothDamp(transform.position, Goal(focus), ref _vel, smoothTime);
            _cam.orthographicSize = Mathf.SmoothDamp(_cam.orthographicSize, size, ref _sizeVel, smoothTime);
        }

        bool Street => _area != null && _area.SwitchSides;

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

        float TargetYaw() => _side > 0 ? _yaw : _yaw + 180f;

        Vector3 Goal(Vector3 focus)
        {
            var goal = focus + Vector3.up - transform.forward * _dist;
            if (Street)
            {
                var local = _area.transform.InverseTransformPoint(goal);
                local.x = Mathf.Clamp(local.x, _area.StreetCenterX - _area.StreetHalfWidth, _area.StreetCenterX + _area.StreetHalfWidth);
                goal = _area.transform.TransformPoint(local);
            }
            return goal;
        }
    }
}
