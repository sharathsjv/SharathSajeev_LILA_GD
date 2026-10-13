using System.Collections.Generic;
using QAOffice;
using UnityEngine;
using UnityEngine.UI;

namespace Hitman
{
    // The subway ride screen: while the clock skips ahead (and the world carries on), the screen shows a cross-section
    // of the subway car rattling through its tunnel, with passengers sitting on the bench and standing (and the player
    // among them), and the car's LED sign giving the next stop and the time. Lives away from the districts with its
    // own camera; Show/Hide swap it in for the main camera.
    public class SubwayRide : MonoBehaviour
    {
        public static SubwayRide I { get; private set; }

        [SerializeField] Camera rideCamera;
        [Tooltip("The car body: it sways a little, passengers with it.")]
        [SerializeField] Transform car;
        [Tooltip("Tunnel lights rushing past the windows (children move along X and wrap round).")]
        [SerializeField] Transform tunnelLights;
        [SerializeField] float tunnelSpeed = 16f;
        [SerializeField] Vector2 tunnelSpan = new Vector2(-9f, 9f);
        [Tooltip("Where seated passengers' hips go, one per seat (their forward is the way they face).")]
        [SerializeField] Transform[] seats;
        [Tooltip("Where standing passengers stand (their forward is the way they face). The first is the player's.")]
        [SerializeField] Transform[] standSpots;
        [SerializeField] GameObject[] passengerModels;
        public GameObject[] PassengerModels => passengerModels;
        [SerializeField] RuntimeAnimatorController animator;
        [SerializeField] float passengerHeight = 1.75f;
        [SerializeField] Light[] carLights;
        [Tooltip("The overhead grab bar, ends (in this object's space). Standing riders hold it, or a pole if one's close.")]
        [SerializeField] Vector3 barStart = new Vector3(-2.6f, 1.98f, -0.62f);
        [SerializeField] Vector3 barEnd = new Vector3(2.6f, 1.98f, -0.62f);
        [Tooltip("The upright poles (foot of each, in this object's space).")]
        [SerializeField] Vector3[] poles = { new Vector3(1.89f, 0, -0.92f), new Vector3(-1.89f, 0, -0.92f) };
        [SerializeField] float poleGripHeight = 1.3f;
        [Tooltip("Ambient light inside the car (it's lit the same day and night; DayNight takes over again after).")]
        [SerializeField] Color carAmbient = new Color(0.42f, 0.44f, 0.48f);

        static readonly string[] SitPoses = { "Sitting Idle", "Seated Idle", "Sitting Talking", "Sitting Laughing" };
        static readonly string[] StandPoses = { "Idle", "Bored", "Breathing Idle" };    // (a hand free for the bar)

        class Passenger
        {
            public Transform root, hips;
            public Vector3 home;
            public Transform seat;
            public int settle;
        }

        readonly List<Passenger> _people = new List<Passenger>();
        Camera _main;
        RectTransform _sign;
        Text _signText;
        string _destination;
        Vector3 _carHome;
        [System.NonSerialized] float[] _lightBase;
        float _flicker;

        public bool Showing { get; private set; }

        void Awake()
        {
            I = this;
            if (car != null) _carHome = car.localPosition;
            if (rideCamera != null) rideCamera.enabled = false;
            SetVisible(false);
        }

        void SetVisible(bool on)
        {
            foreach (Transform c in transform) if (rideCamera == null || c != rideCamera.transform) c.gameObject.SetActive(on);
            if (rideCamera != null) rideCamera.gameObject.SetActive(true);
        }

        // ---------- showing it ----------

        public void Show(string destination)
        {
            _destination = destination;
            Showing = true;
            SetVisible(true);
            FillCar();
            _main = Game.I.Cam != null ? Game.I.Cam.GetComponent<Camera>() : Camera.main;
            if (_main != null) _main.enabled = false;
            rideCamera.enabled = true;
            rideCamera.orthographicSize = Mathf.Max(2.85f, 5.0f / rideCamera.aspect);    // the whole car on narrow screens
            if (carLights != null && (_lightBase == null || _lightBase.Length != carLights.Length))
            {
                _lightBase = new float[carLights.Length];
                for (int i = 0; i < carLights.Length; i++) _lightBase[i] = carLights[i].intensity;
            }
            MakeSign();
            _sign.gameObject.SetActive(true);
            _sign.SetAsLastSibling();
        }

        public void Hide()
        {
            Showing = false;
            rideCamera.enabled = false;
            if (_main != null) _main.enabled = true;
            if (_sign != null) _sign.gameObject.SetActive(false);
            foreach (var p in _people) if (p.root != null) Destroy(p.root.gameObject);
            _people.Clear();
            SetVisible(false);
        }

        // A different crowd every ride: a few people on the bench, a few standing, and the player standing.
        void FillCar()
        {
            foreach (var p in _people) if (p.root != null) Destroy(p.root.gameObject);
            _people.Clear();
            var lib = AnimLibrary.I;
            // the player, as they're dressed now
            var model = Game.I.Player.transform.Find("Model");
            if (model != null && standSpots.Length > 0)
                Add(Instantiate(model.gameObject), standSpots[0], null, "Idle", false);
            if (passengerModels == null || passengerModels.Length == 0) return;
            var seatList = new List<Transform>(seats);
            Shuffle(seatList);
            int sitting = Random.Range(2, Mathf.Min(5, seatList.Count + 1));
            // (each model once before any repeats)
            var models = new List<GameObject>();
            GameObject Next()
            {
                if (models.Count == 0) { models.AddRange(passengerModels); Shuffle(models); }
                var m = models[models.Count - 1];
                models.RemoveAt(models.Count - 1);
                return m;
            }
            for (int i = 0; i < sitting; i++)
                Add(Instantiate(Next()), seatList[i], seatList[i], SitPoses[Random.Range(0, SitPoses.Length)], true);
            var spots = new List<Transform>(standSpots);
            spots.RemoveAt(0);
            Shuffle(spots);
            int standing = Random.Range(1, Mathf.Min(4, spots.Count + 1));
            for (int i = 0; i < standing; i++)
                Add(Instantiate(Next()), spots[i], null, StandPoses[Random.Range(0, StandPoses.Length)], true);
        }

        static void Shuffle<T>(List<T> l)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (l[i], l[j]) = (l[j], l[i]); }
        }

        void Add(GameObject go, Transform spot, Transform seat, string pose, bool scale)
        {
            go.name = "Rider";
            go.transform.SetParent(car != null ? car : transform, false);
            go.transform.SetPositionAndRotation(spot.position, spot.rotation);
            go.transform.localScale = Vector3.one;
            if (scale)
            {
                float h = 0;
                foreach (var r in go.GetComponentsInChildren<Renderer>()) h = Mathf.Max(h, r.bounds.max.y - go.transform.position.y);
                if (h > 0.1f) go.transform.localScale *= passengerHeight * Random.Range(0.94f, 1.05f) / h;
            }
            var anim = go.GetComponentInChildren<Animator>();
            Transform hips = null;
            if (anim != null)
            {
                var lib = AnimLibrary.I;
                var o = new AnimatorOverrideController(animator);
                var clip = lib != null ? lib.Get(pose) : null;
                if (clip != null) o[lib.posePlaceholder] = clip;
                anim.runtimeAnimatorController = o;
                anim.applyRootMotion = false;
                anim.updateMode = AnimatorUpdateMode.UnscaledTime;            // (the world is fast-forwarding)
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.SetBool("Posing", clip != null);
                anim.Update(0);
                if (clip != null) anim.Play("Pose", 0, Random.value);         // straight into it, not all in step
                if (anim.isHuman) hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            }
            if (seat == null && anim != null && anim.isHuman) Grip(anim, spot);
            if (seat != null && anim != null && anim.isHuman)
            {
                var g = anim.gameObject.AddComponent<RiderGrip>();
                g.car = car != null ? car : transform;
                g.holding = false;
                g.plantFeet = true;
                g.floorY = g.car.InverseTransformPoint(transform.position).y;
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _people.Add(new Passenger { root = go.transform, hips = hips, home = go.transform.localPosition, seat = seat });
        }

        // A hand on the nearest upright pole if there's one within reach, else on the grab bar overhead.
        void Grip(Animator anim, Transform spot)
        {
            var at = transform.InverseTransformPoint(spot.position);
            var right = transform.InverseTransformDirection(spot.right);
            Vector3 point;
            Vector3? pole = null;
            foreach (var p in poles)
                if (new Vector2(p.x - at.x, p.z - at.z).magnitude < 0.75f) pole = p;
            if (pole is Vector3 pl) point = pl + Vector3.up * poleGripHeight;
            else
            {
                float x = Mathf.Clamp(at.x + right.x * 0.22f, Mathf.Min(barStart.x, barEnd.x) + 0.05f, Mathf.Max(barStart.x, barEnd.x) - 0.05f);
                point = new Vector3(x, barStart.y, barStart.z);
            }
            var g = anim.gameObject.AddComponent<RiderGrip>();
            g.car = car != null ? car : transform;
            g.localPoint = g.car.InverseTransformPoint(transform.TransformPoint(point));
            g.hand = Vector3.Dot(right, point - at) >= 0 ? AvatarIKGoal.RightHand : AvatarIKGoal.LeftHand;
            g.rotationWeight = 0;
        }

        // ---------- the ride ----------

        void LateUpdate()
        {
            if (!Showing) return;
            float t = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            RenderSettings.fog = false;                                          // (the street haze is for the streets)
            RenderSettings.ambientLight = carAmbient;                            // lit the same day or night
            if (car != null)
            {
                car.localPosition = _carHome + new Vector3(0, Mathf.Sin(t * 9f) * 0.006f + Mathf.Sin(t * 2.3f) * 0.01f, 0);
                car.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.7f) * 0.25f);
            }
            if (tunnelLights != null)
                foreach (Transform l in tunnelLights)
                {
                    var p = l.localPosition;
                    p.x -= tunnelSpeed * dt;
                    if (p.x < tunnelSpan.x) p.x += tunnelSpan.y - tunnelSpan.x;
                    l.localPosition = p;
                }
            // now and then the lights dip
            if (_lightBase != null && _lightBase.Length == carLights.Length)
            {
                if (_flicker <= 0 && Random.value < dt * 0.25f) _flicker = Random.Range(0.08f, 0.2f);
                _flicker -= dt;
                for (int i = 0; i < carLights.Length; i++) carLights[i].intensity = _lightBase[i] * (_flicker > 0 ? 0.45f : 1f);
            }
            // seated passengers: put their hips on the seat (the sitting clips sit them behind where they stand)
            // (worked out over the first few frames and then held, so the feet can be planted where the body is)
            foreach (var p in _people)
            {
                if (p.root == null || p.seat == null || p.hips == null || p.settle >= 4) continue;
                p.settle++;
                p.root.position += p.seat.position - p.hips.position;
            }
            if (_signText != null)
                _signText.text = $"<color=#ff9a3c>(7)</color>   NEXT STOP: {_destination.ToUpper()}      {Game.Clock(Game.I.Minutes)}";
        }

        // The car's LED sign, across the top of the screen (like the doc's reference).
        void MakeSign()
        {
            if (_sign != null) return;
            var root = Game.I.UI.CanvasRoot;
            _sign = UIKit.Box(root, "Subway Sign", new Color(0.04f, 0.04f, 0.05f, 0.94f), false).rectTransform;
            _sign.anchorMin = new Vector2(0.2f, 1);
            _sign.anchorMax = new Vector2(0.8f, 1);
            _sign.pivot = new Vector2(0.5f, 1);
            _sign.sizeDelta = new Vector2(0, 74);
            _sign.anchoredPosition = new Vector2(0, -18);
            _signText = UIKit.Label(_sign, "", 34, new Color(0.72f, 0.84f, 1f), TextAnchor.MiddleCenter);
            _signText.rectTransform.Stretch(16, 4, 16, 4);
            _signText.supportRichText = true;
            _signText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _signText.verticalOverflow = VerticalWrapMode.Overflow;
            _signText.fontStyle = FontStyle.Bold;
        }
    }
}
