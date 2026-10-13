using UnityEngine;

namespace Hitman
{
    // A subway rider's IK (needs the IK pass on in the rider controller): a standing rider's hand on the grab bar or
    // a pole, a seated rider's feet flat on the floor. Points are kept relative to the car so they sway with it.
    [RequireComponent(typeof(Animator))]
    public class RiderGrip : MonoBehaviour
    {
        public Transform car;
        public Vector3 localPoint;
        public AvatarIKGoal hand = AvatarIKGoal.RightHand;
        [Tooltip("Hand facing for the grip, relative to the car.")]
        public Quaternion localRotation = Quaternion.identity;
        public float rotationWeight = 0.5f;
        public bool holding = true;
        [Tooltip("Seated: put both feet on the floor (the car's floor height, in the car's space).")]
        public bool plantFeet;
        public float floorY;

        Animator _anim;

        void Awake() => _anim = GetComponent<Animator>();

        void OnAnimatorIK(int layer)
        {
            if (_anim == null || car == null) return;
            if (plantFeet)
                foreach (var foot in new[] { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot })
                {
                    var p = car.InverseTransformPoint(_anim.GetIKPosition(foot));
                    p.y = floorY + 0.09f;                                          // (ankle height)
                    _anim.SetIKPositionWeight(foot, 1f);
                    _anim.SetIKPosition(foot, car.TransformPoint(p));
                }
            if (!holding) return;
            _anim.SetIKPositionWeight(hand, 1f);
            _anim.SetIKPosition(hand, car.TransformPoint(localPoint));
            _anim.SetIKRotationWeight(hand, rotationWeight);
            _anim.SetIKRotation(hand, car.rotation * localRotation);
        }
    }
}
