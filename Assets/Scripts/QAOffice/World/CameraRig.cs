using UnityEngine;

namespace QAOffice
{
    // Smoothly zooms the orthographic office camera onto a point (e.g. a conversation) and back.
    // The camera's placement in the scene is the default view.
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        [SerializeField] float smoothTime = 0.35f;
        [SerializeField] float distance = 40f;

        Camera _cam;
        Vector3 _homePos;
        float _homeSize;
        Vector3 _targetPos;
        float _targetSize;
        Vector3 _vel;
        float _sizeVel;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _homePos = _targetPos = transform.position;
            _homeSize = _targetSize = _cam.orthographicSize;
        }

        public void Focus(Vector3 worldPoint, float orthoSize)
        {
            _targetPos = worldPoint - transform.forward * distance;
            _targetSize = orthoSize;
        }

        public void Release()
        {
            _targetPos = _homePos;
            _targetSize = _homeSize;
        }

        void LateUpdate()
        {
            transform.position = Vector3.SmoothDamp(transform.position, _targetPos, ref _vel, smoothTime);
            _cam.orthographicSize = Mathf.SmoothDamp(_cam.orthographicSize, _targetSize, ref _sizeVel, smoothTime);
        }
    }
}
