using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // Ambient passers-by and traffic for one district. People appear just off-screen, walk a sidewalk and
    // vanish once they're past the camera; cars drive the road lanes and brake for anyone in front of them.
    // Lives on the district, so it only runs while the player is in that district.
    public class StreetLife : MonoBehaviour
    {
        // Scales every district's pedestrian and car counts (set from Game's Street Density).
        public static float Density = 1f;

        [Header("Passers-by")]
        [SerializeField] int pedestrianCount = 8;
        [SerializeField] GameObject[] pedestrianModels;
        [SerializeField] RuntimeAnimatorController animator;
        [SerializeField] float walkSpeed = 1.6f;
        [Tooltip("Everyone is scaled to about this height (the source models come in at different sizes).")]
        [SerializeField] float pedestrianHeight = 1.75f;
        [Tooltip("How far either side of the sidewalk's centre line people may walk.")]
        [SerializeField] float sidewalkSpread = 1f;

        [Header("Traffic")]
        [SerializeField] int carCount = 4;
        [SerializeField] float carSpeed = 6f;
        [SerializeField] Color[] carColors = { new Color(0.8f, 0.2f, 0.2f), new Color(0.2f, 0.4f, 0.8f), new Color(0.9f, 0.9f, 0.9f), new Color(0.15f, 0.15f, 0.15f), new Color(0.9f, 0.7f, 0.2f) };
        [SerializeField] Material carTemplate;
        [Tooltip("Car models to pick from (built in Blender, facing +Z). Empty = simple box cars.")]
        [SerializeField] GameObject[] carModels;
        [SerializeField] GameObject[] taxiModels;
        [Tooltip("Share of the traffic that's taxis (lots in the Business District).")]
        [SerializeField, Range(0, 1)] float taxiShare = 0.15f;

        class Walker
        {
            public Transform t;
            public Animator anim;
            public Vector3 from, to;
            public Vector3 at;          // where they are along their line (they step round things off it)
            public float detour;
            public float wait;
            public float speed;
            public bool active;
        }

        class Car
        {
            public Transform t;
            public Lane lane;
            public float s;      // distance along lane
            public float speed;
            public float wait;
            public bool active;
        }

        readonly List<Lane> _sidewalks = new List<Lane>();
        readonly List<Lane> _roads = new List<Lane>();
        readonly List<Walker> _walkers = new List<Walker>();
        readonly List<Car> _cars = new List<Car>();
        Camera _cam;
        bool _built;

        void OnEnable()
        {
            if (Game.I == null) return;   // scene still loading; the game enables the district again on entry
            if (!_built) Build();
            Prewarm();
        }

        void Build()
        {
            _built = true;
            _cam = GameCamera();
            foreach (var l in GetComponentsInChildren<Lane>(true)) (l.LaneKind == Lane.Kind.Road ? _roads : _sidewalks).Add(l);
            var area = GetComponentInParent<Area>();
            if (area != null)
                foreach (var r in area.GetComponentsInChildren<Renderer>(true))
                    foreach (var o in Obstacles)
                        if (r.name.StartsWith(o))
                        {
                            var b = r.bounds;
                            b.Expand(new Vector3(0.7f, 10f, 0.7f));                  // (a body's width round it; any height)
                            _obstacles.Add(b);
                        }
            if (_sidewalks.Count > 0 && pedestrianModels != null && pedestrianModels.Length > 0)
                for (int i = 0; i < Mathf.RoundToInt(pedestrianCount * Density); i++) _walkers.Add(MakeWalker(i));
            if (_roads.Count > 0)
                for (int i = 0; i < Mathf.RoundToInt(carCount * Density); i++) _cars.Add(MakeCar(i));
        }

        // Fill the street straight away when the player arrives, instead of it starting empty.
        void Prewarm()
        {
            foreach (var w in _walkers)
            {
                if (Random.value > 0.75f) { w.active = false; w.t.gameObject.SetActive(false); w.wait = Random.Range(0f, 4f); continue; }
                SpawnWalker(w, Random.Range(0f, 0.9f));
            }
            foreach (var c in _cars)
            {
                c.active = false;
                c.t.gameObject.SetActive(false);
                c.wait = Random.Range(1f, 6f);
                if (Random.value > 0.7f) continue;
                var lane = _roads[Random.Range(0, _roads.Count)];
                float s = Random.Range(0f, Vector3.Distance(lane.A, lane.B) * 0.9f);
                bool clash = false;
                foreach (var o in _cars) if (o.active && o.lane == lane && Mathf.Abs(o.s - s) < 7f) clash = true;
                if (!clash) SpawnCar(c, lane, s);
            }
        }

        // ---------- Passers-by ----------

        Walker MakeWalker(int i)
        {
            var model = Instantiate(pedestrianModels[i % pedestrianModels.Length], transform);
            model.name = "Passer-by " + i;
            float h = 0;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) h = Mathf.Max(h, r.bounds.max.y - model.transform.position.y);
            if (h > 0.1f) model.transform.localScale *= pedestrianHeight * Random.Range(0.94f, 1.05f) / h;
            var anim = model.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.runtimeAnimatorController = animator;
                // Some people text or carry shopping as they walk.
                var lib = AnimLibrary.I;
                if (lib != null && lib.walkClip != null && i % 3 != 0)
                {
                    var walk = lib.Get(i % 3 == 1 ? "Walking While Texting" : "Walking With Shopping Bag");
                    if (walk != null)
                    {
                        var o = new AnimatorOverrideController(animator);
                        o[lib.walkClip] = walk;
                        anim.runtimeAnimatorController = o;
                    }
                }
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullCompletely;
            }
            BlobShadow.Attach(model, new Vector2(0.7f, 0.7f));
            var w = new Walker { t = model.transform, anim = anim, wait = Random.Range(0f, 6f) };
            model.SetActive(false);
            return w;
        }

        void SpawnWalker(Walker w, float? at = null)
        {
            var lane = _sidewalks[Random.Range(0, _sidewalks.Count)];
            bool reverse = lane.BothWays && Random.value < 0.5f;
            var a = reverse ? lane.B : lane.A;
            var b = reverse ? lane.A : lane.B;
            // Start somewhere off-screen along the lane, heading the length of it.
            // Staggered a little so people spawned together don't walk in lockstep.
            float startT = at ?? Mathf.Max(0, OffScreenStart(a, b) - Random.Range(0f, 0.15f));
            var side = Vector3.Cross(Vector3.up, (b - a).normalized) * Random.Range(-sidewalkSpread, sidewalkSpread);
            w.speed = walkSpeed * Random.Range(0.85f, 1.15f);
            w.from = Vector3.Lerp(a, b, startT) + side;
            w.to = b + side;
            w.at = w.from;
            w.detour = Detour(w.at, (b - a).normalized);
            w.t.position = w.at + Vector3.Cross(Vector3.up, (b - a).normalized) * w.detour;
            w.t.rotation = Quaternion.LookRotation(b - a);
            w.t.gameObject.SetActive(true);
            w.active = true;
        }

        // Pick the furthest-back point on the lane that's still off-screen (or the start if all of it is).
        float OffScreenStart(Vector3 a, Vector3 b)
        {
            float best = 0;
            for (float t = 0; t <= 0.9f; t += 0.1f)
            {
                if (OnScreen(Vector3.Lerp(a, b, t), 0.05f)) break;
                best = t;
            }
            return best;
        }

        // Things on the sidewalk passers-by walk round rather than through.
        static readonly string[] Obstacles = { "Station_Stairs", "Prop_Stoops_", "Prop_Bench", "Phone booth" };
        readonly List<Bounds> _obstacles = new List<Bounds>();

        // How far to the side (along the lane's left) to step to keep p clear of the obstacles (0 = clear): the
        // smallest step that's clear of all of them.
        float Detour(Vector3 p, Vector3 dir)
        {
            var left = Vector3.Cross(Vector3.up, dir);
            if (Clear(p)) return 0;
            for (float k = 0.1f; k < 4f; k += 0.1f)
            {
                if (Clear(p + left * k)) return k;
                if (Clear(p - left * k)) return -k;
            }
            return 0;
        }

        bool Clear(Vector3 p)
        {
            foreach (var b in _obstacles) if (b.Contains(new Vector3(p.x, b.center.y, p.z))) return false;
            return true;
        }

        // The game's camera, even while it's switched off (the subway ride screen has its own): Camera.main is null then.
        static Camera GameCamera() => Game.I != null && Game.I.Cam != null ? Game.I.Cam.GetComponent<Camera>() : Camera.main;

        bool OnScreen(Vector3 p, float margin)
        {
            var v = _cam.WorldToViewportPoint(p);
            return v.z > 0 && v.x > -margin && v.x < 1 + margin && v.y > -margin && v.y < 1 + margin;
        }

        void Update()
        {
            if (_cam == null) _cam = GameCamera();
            float dt = Time.deltaTime;

            foreach (var w in _walkers)
            {
                if (!w.active)
                {
                    w.wait -= dt;
                    if (w.wait <= 0) SpawnWalker(w);
                    continue;
                }
                var pos = w.at = Vector3.MoveTowards(w.at, w.to, w.speed * dt);
                var dir = (w.to - w.from).normalized;
                // Step round the subway stairs, stoops, benches and phone booths (looking a little ahead).
                float need = Detour(pos + dir * 2.0f, dir);
                if (Mathf.Abs(Detour(pos, dir)) > Mathf.Abs(need)) need = Detour(pos, dir);
                w.detour = Mathf.MoveTowards(w.detour, need, 2.5f * dt);
                w.t.position = pos + Vector3.Cross(Vector3.up, dir) * w.detour;
                if (w.anim != null)
                {
                    w.anim.SetFloat("Speed", w.speed);
                    w.anim.SetFloat("WalkRate", w.speed / 1.55f);
                }
                bool done = (pos - w.to).sqrMagnitude < 0.01f;
                // Gone once they've walked out of view (and aren't heading back into it).
                bool pastCamera = !OnScreen(pos, 0.15f) && Vector3.Dot(w.to - pos, CameraGroundCenter() - pos) < 0;
                if (done || pastCamera)
                {
                    w.active = false;
                    w.t.gameObject.SetActive(false);
                    w.wait = Random.Range(1f, 5f);
                }
            }

            foreach (var c in _cars) UpdateCar(c, dt);
        }

        Vector3 CameraGroundCenter()
        {
            var ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            return new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var d) ? ray.GetPoint(d) : _cam.transform.position;
        }

        // ---------- Traffic ----------

        Car MakeCar(int i)
        {
            var root = new GameObject("Car " + i).transform;
            root.SetParent(transform, false);
            bool taxi = taxiModels != null && taxiModels.Length > 0 && Random.value < taxiShare;
            var pool = taxi ? taxiModels : carModels;
            if (pool != null && pool.Length > 0)
            {
                var body = Instantiate(pool[Random.Range(0, pool.Length)], root, false);
                foreach (var c in body.GetComponentsInChildren<Collider>()) Destroy(c);
                BlobShadow.Attach(root.gameObject, new Vector2(2.0f, 4.6f));
                root.gameObject.SetActive(false);
                return new Car { t = root, wait = Random.Range(0f, 8f) };
            }
            var color = carColors[Random.Range(0, carColors.Length)];
            Part(root, new Vector3(0, 0.45f, 0), new Vector3(1.5f, 0.6f, 3.4f), color);
            Part(root, new Vector3(0, 0.95f, -0.2f), new Vector3(1.3f, 0.5f, 1.8f), color * 0.85f);
            Part(root, new Vector3(0, 0.96f, 0.72f), new Vector3(1.2f, 0.42f, 0.05f), new Color(0.2f, 0.25f, 0.3f));
            foreach (var z in new[] { -1.1f, 1.1f })
            foreach (var x in new[] { -0.72f, 0.72f })
                Part(root, new Vector3(x, 0.25f, z), new Vector3(0.18f, 0.5f, 0.5f), new Color(0.08f, 0.08f, 0.08f));
            BlobShadow.Attach(root.gameObject, new Vector2(2.0f, 3.9f));
            root.gameObject.SetActive(false);
            return new Car { t = root, wait = Random.Range(0f, 8f) };
        }

        void Part(Transform root, Vector3 pos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            if (carTemplate != null) r.material = new Material(carTemplate) { color = color };
            else r.material.color = color;
        }

        void SpawnCar(Car c, Lane lane, float s)
        {
            c.lane = lane;
            c.s = s;
            c.speed = carSpeed * Random.Range(0.8f, 1.2f);
            c.active = true;
            var dir = (lane.B - lane.A).normalized;
            c.t.SetPositionAndRotation(lane.A + dir * s, Quaternion.LookRotation(dir));
            c.t.gameObject.SetActive(true);
        }

        void UpdateCar(Car c, float dt)
        {
            if (!c.active)
            {
                c.wait -= dt;
                if (c.wait > 0) return;
                SpawnCar(c, _roads[Random.Range(0, _roads.Count)], 0);
            }
            var a = c.lane.A;
            var b = c.lane.B;
            float len = Vector3.Distance(a, b);
            var dir = (b - a) / Mathf.Max(0.01f, len);
            var pos = a + dir * c.s;
            // Brake for people (or another car) just ahead.
            bool blocked = Physics.SphereCast(pos + Vector3.up * 0.8f - dir * 0.5f, 0.6f, dir, out var hit, 4.5f) &&
                           hit.collider.GetComponentInParent<CharacterMover>() != null;
            if (!blocked)
                foreach (var other in _cars)
                    if (other != c && other.active && other.lane == c.lane && other.s > c.s && other.s - c.s < 5f) { blocked = true; break; }
            if (!blocked) c.s += c.speed * dt;
            c.t.position = a + dir * c.s;
            c.t.rotation = Quaternion.LookRotation(dir);
            if (c.s >= len)
            {
                c.active = false;
                c.t.gameObject.SetActive(false);
                c.wait = Random.Range(1f, 6f);
            }
        }
    }
}
