using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // A wardrobe the player can change clothes at (Usable action id "wardrobe"). Its doors swing open while
    // they're choosing. Each door hinges on the edge furthest from the wardrobe's middle and opens outwards.
    public class Wardrobe : MonoBehaviour
    {
        [Serializable]
        public class Door
        {
            [Tooltip("The door and anything fixed to it (handle, knob).")]
            public Transform[] parts;
        }

        [Tooltip("The wardrobe's body (used to find which way is out and where the hinges are).")]
        [SerializeField] Renderer body;
        [SerializeField] Door[] doors;
        [SerializeField] float openAngle = 100f;
        [SerializeField] float swingTime = 0.45f;

        static readonly List<Wardrobe> All = new List<Wardrobe>();

        Vector3[][] _pos;
        Quaternion[][] _rot;
        Vector3[] _hinge;
        float[] _sign;
        Coroutine _swing;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static Wardrobe Nearest(Vector3 p)
        {
            Wardrobe best = null;
            float bestD = float.MaxValue;
            foreach (var w in All)
            {
                float d = (w.transform.position - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = w; }
            }
            return best;
        }

        public void Open(bool open)
        {
            if (doors == null || doors.Length == 0 || body == null) return;
            if (_pos == null)
            {
                _pos = new Vector3[doors.Length][];
                _rot = new Quaternion[doors.Length][];
                _hinge = new Vector3[doors.Length];
                _sign = new float[doors.Length];
                var b = body.bounds;
                for (int i = 0; i < doors.Length; i++)
                {
                    _pos[i] = Array.ConvertAll(doors[i].parts, t => t.position);
                    _rot[i] = Array.ConvertAll(doors[i].parts, t => t.rotation);
                    var r = doors[i].parts.Length > 0 && doors[i].parts[0] != null ? doors[i].parts[0].GetComponent<Renderer>() : null;
                    if (r == null) continue;
                    // Out = from the body's middle toward the door; the hinge is the door's side edge away from the middle.
                    var db = r.bounds;
                    bool alongX = db.size.x >= db.size.z;          // the door's width runs along this axis
                    var side = alongX ? new Vector3(Mathf.Sign(db.center.x - b.center.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(db.center.z - b.center.z));
                    var front = alongX ? new Vector3(0, 0, Mathf.Sign(db.center.z - b.center.z)) : new Vector3(Mathf.Sign(db.center.x - b.center.x), 0, 0);
                    _hinge[i] = db.center + Vector3.Scale(side, db.extents) + Vector3.Scale(front, db.extents);
                    // Turn the free edge (toward the middle) out toward the front.
                    _sign[i] = Vector3.Cross(-side, front).y > 0 ? 1 : -1;
                }
            }
            if (_swing != null) StopCoroutine(_swing);
            _swing = StartCoroutine(Swing(open));
        }

        float _t;   // 0 = shut, 1 = open

        IEnumerator Swing(bool open)
        {
            float goal = open ? 1 : 0;
            while (!Mathf.Approximately(_t, goal))
            {
                _t = Mathf.MoveTowards(_t, goal, Time.deltaTime / swingTime);
                Pose(Mathf.SmoothStep(0, 1, _t));
                yield return null;
            }
            _swing = null;
        }

        void Pose(float k)
        {
            for (int i = 0; i < doors.Length; i++)
            {
                var parts = doors[i].parts;
                if (_sign[i] == 0) continue;
                for (int j = 0; j < parts.Length; j++)
                {
                    parts[j].SetPositionAndRotation(_pos[i][j], _rot[i][j]);
                    parts[j].RotateAround(_hinge[i], Vector3.up, _sign[i] * openAngle * k);
                }
            }
        }
    }
}
