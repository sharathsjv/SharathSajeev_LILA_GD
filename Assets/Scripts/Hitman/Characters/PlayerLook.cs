using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    public enum Slot { Hair, Top, Bottoms, Shoes }

    // What the player is wearing (saved between sessions).
    [Serializable]
    public class Outfit
    {
        public const int Own = -1, None = -2;
        [Tooltip("Index into PlayerLook's characters.")]
        public int who = -1;
        [Tooltip("Per Slot: Own, None (hair only) or the character to take that part from.")]
        public int[] style = { Own, Own, Own, Own };
        [Tooltip("Per Slot: 0 = the original colour, else an index into PlayerLook.Colours(slot).")]
        public int[] colour = { 0, 0, 0, 0 };

        public Outfit Copy() => new Outfit { who = who, style = (int[])style.Clone(), colour = (int[])colour.Clone() };
    }

    // The player's look: which character they are, whose hair / top / bottoms / shoes they wear, and dye colours.
    // Parts taken from another character are re-skinned onto the player's own skeleton (all the Mixamo rigs share
    // bone names), so they follow the animation; differently built bodies can poke through each other's clothes.
    [RequireComponent(typeof(CharacterMover))]
    public class PlayerLook : MonoBehaviour
    {
        [Serializable]
        public class Character
        {
            public string name;
            public GameObject model;
            [Tooltip("Built on a woman's body (tops and bottoms only swap between the same build).")]
            public bool feminine;
            // The bodies have no skin under their own clothes (no forearms under long sleeves, no shins under
            // trousers), so a swapped top or bottoms has to reach at least as far down the limb as the bare skin
            // starts. Both are 0..1 along each limb segment (upper arm, forearm, thigh, shin) from the joint down.
            // Filled in by the component's "Measure coverage" menu.
            [Tooltip("Per segment: where the bare body's skin starts (1 = no skin on it).")]
            public float[] skin = new float[Segments];
            [Tooltip("Per Slot x segment: how far down the segment that part reaches (1 = all of it).")]
            public float[] reach = new float[4 * Segments];
            [Tooltip("Shoulder joint width as a share of height. Tops only swap between similar shoulders: some " +
                     "bodies are broad on narrow joints (Remy), and anything else's top sits inside them.")]
            public float shoulders;
            [Tooltip("Their top is a suit jacket (counts as a suit, e.g. for building security).")]
            public bool suit;
        }

        const int Segments = 4;   // upper arm, forearm, thigh, shin
        static readonly int[][] SlotSegments = { new int[0], new[] { 0, 1 }, new[] { 2, 3 }, new int[0] };

        [SerializeField] Character[] characters;
        [SerializeField] Shader dyeShader;

        // Index 0 of each list is "original" (no dye); colours are as they'd look on a white garment.
        public static readonly Color[] HairColours =
        {
            Color.white, C(0.10f, 0.09f, 0.09f), C(0.25f, 0.16f, 0.10f), C(0.45f, 0.30f, 0.18f), C(0.55f, 0.22f, 0.12f),
            C(0.80f, 0.42f, 0.18f), C(0.90f, 0.78f, 0.50f), C(0.92f, 0.90f, 0.85f), C(0.60f, 0.60f, 0.62f),
            C(0.20f, 0.40f, 0.85f), C(0.95f, 0.45f, 0.70f),
        };
        public static readonly Color[] ClothColours =
        {
            Color.white, C(0.12f, 0.12f, 0.13f), C(0.30f, 0.30f, 0.32f), C(0.62f, 0.62f, 0.64f), C(0.95f, 0.95f, 0.93f),
            C(0.12f, 0.18f, 0.38f), C(0.20f, 0.45f, 0.85f), C(0.10f, 0.55f, 0.55f), C(0.20f, 0.50f, 0.25f),
            C(0.45f, 0.45f, 0.20f), C(0.78f, 0.65f, 0.45f), C(0.42f, 0.27f, 0.16f), C(0.50f, 0.10f, 0.15f),
            C(0.85f, 0.15f, 0.15f), C(0.95f, 0.50f, 0.15f), C(0.95f, 0.82f, 0.25f), C(0.95f, 0.55f, 0.70f),
            C(0.45f, 0.25f, 0.65f),
        };
        public static Color[] Colours(Slot s) => s == Slot.Hair ? HairColours : ClothColours;
        static Color C(float r, float g, float b) => new Color(r, g, b);

        const string SaveKey = "hitman.outfit";

        public Character[] Characters => characters;
        public Outfit Current => _outfit.Copy();
        public int SceneCharacter => _sceneWho;
        public event Action Changed;

        // What people remember the player by (infamy): their top and their hair, each with its dye.
        public string TopKey => PartKey(Slot.Top);
        public string HairKey => PartKey(Slot.Hair);
        string PartKey(Slot s)
        {
            int i = (int)s;
            int from = _outfit.style[i] == Outfit.Own ? _outfit.who : _outfit.style[i];
            return $"{from}/{_outfit.style[i] == Outfit.None}/{_outfit.colour[i]}";
        }

        // Wearing a suit jacket (Joe's), e.g. to walk past security at Schrute Towers.
        public bool WearingSuit
        {
            get
            {
                int t = _outfit.style[(int)Slot.Top];
                int from = t == Outfit.Own ? _outfit.who : t;
                return from >= 0 && from < characters.Length && characters[from].suit;
            }
        }

        public int IndexOf(string characterName)
        {
            for (int i = 0; i < characters.Length; i++) if (characters[i].name == characterName) return i;
            return -1;
        }

        Transform _model;
        int _sceneWho = -1, _builtWho = -1;
        float _height;                                  // the player stays this tall whoever they are
        float[] _sourceHeight;
        Outfit _outfit = new Outfit();
        readonly Dictionary<Renderer, Material[]> _originals = new Dictionary<Renderer, Material[]>();
        readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();
        readonly List<GameObject> _added = new List<GameObject>();
        readonly Dictionary<(Material, Color), Material> _dyed = new Dictionary<(Material, Color), Material>();
        Material _dyeMat;

        void Awake()
        {
            _model = GetComponentInChildren<Animator>().transform;
            _sourceHeight = new float[characters.Length];
            // Which of the characters the scene's player model is.
            var mesh = _model.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh;
            for (int i = 0; i < characters.Length && _sceneWho < 0; i++)
                foreach (var r in characters[i].model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (r.sharedMesh == mesh) { _sceneWho = i; break; }
            if (_sceneWho < 0) { Debug.LogWarning("PlayerLook: the player's model isn't in the character list."); _sceneWho = 0; }
            _builtWho = _sceneWho;
            _height = SourceHeight(_sceneWho) * _model.localScale.y;
            _outfit.who = _sceneWho;
            Collect();
        }

        // ---------- What's available ----------

        public static Slot? SlotOf(string rendererName)
        {
            var n = rendererName;
            if (n.Contains("Hair")) return Slot.Hair;
            if (n.Contains("Shirt") || n.Contains("Sweater") || n.Contains("Collar") || n.Contains("Top") || n.Contains("Suit") || n.Contains("Tie"))
                return Slot.Top;
            if (n.Contains("Pants") || n.Contains("Bottom") || n.Contains("Belt")) return Slot.Bottoms;
            if (n.Contains("Shoe") || n.Contains("Sneaker") || n.Contains("Heel")) return Slot.Shoes;
            return null;
        }

        IEnumerable<SkinnedMeshRenderer> Parts(int who, Slot s)
        {
            foreach (var r in characters[who].model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (SlotOf(r.name) == s) yield return r;
        }

        public bool HasPart(int who, Slot s) { foreach (var _ in Parts(who, s)) return true; return false; }

        // A character modelled as one piece (clothes and body in a single mesh) can't swap clothes.
        public bool OnePiece(int who) => !HasPart(who, Slot.Top) && !HasPart(who, Slot.Bottoms) && !HasPart(who, Slot.Shoes);

        // Can `who` wear `from`'s part? Tops and bottoms only swap between the same build of body, must reach down
        // the arms / legs to where `who`'s skin starts (or there'd be missing arms and legs), and tops need
        // similar shoulders.
        public bool Fits(int who, int from, Slot s)
        {
            if (s != Slot.Top && s != Slot.Bottoms) return true;
            if (characters[who].feminine != characters[from].feminine) return false;
            float a = characters[who].shoulders, b = characters[from].shoulders;
            if (s == Slot.Top && a > 0 && b > 0 && Mathf.Abs(a / b - 1) > 0.08f) return false;
            var skin = characters[who].skin;
            var reach = characters[from].reach;
            if (skin == null || skin.Length < Segments || reach == null || reach.Length < 4 * Segments) return true;
            foreach (int seg in SlotSegments[(int)s])
                if (reach[(int)s * Segments + seg] < skin[seg] - 0.05f) return false;
            return true;
        }

        // ---------- Wearing ----------

        public void Wear(Outfit o, bool save = true)
        {
            o = o.Copy();
            if (o.who < 0 || o.who >= characters.Length) o.who = _sceneWho;
            if (o.who != _builtWho) Rebuild(o.who);

            foreach (var go in _added) Destroy(go);
            _added.Clear();
            foreach (var kv in _originals)
            {
                kv.Key.enabled = true;
                kv.Key.sharedMaterials = kv.Value;
            }

            var used = new HashSet<Material>();
            bool onePiece = OnePiece(o.who);
            for (int s = 0; s < 4; s++)
            {
                var slot = (Slot)s;
                if (o.style[s] == o.who || (o.style[s] >= 0 && (o.style[s] >= characters.Length || !HasPart(o.style[s], slot) || !Fits(o.who, o.style[s], slot))))
                    o.style[s] = Outfit.Own;
                if (o.colour[s] < 0 || o.colour[s] >= Colours(slot).Length) o.colour[s] = 0;
                if (onePiece && slot != Slot.Hair) { o.style[s] = Outfit.Own; o.colour[s] = 0; continue; }
                Color? dye = o.colour[s] > 0 ? Colours(slot)[o.colour[s]] : (Color?)null;

                if (o.style[s] == Outfit.Own)
                {
                    bool any = false;
                    foreach (var r in BaseParts(slot)) { any = true; if (!Trim(r.name, BaseParts(slot))) Dye(r, dye, used, null); }
                    // Hair that's part of the body mesh (one-piece characters): dye just its hair material.
                    if (!any && slot == Slot.Hair) foreach (var r in _originals.Keys) Dye(r, dye, used, "hair");
                    continue;
                }
                foreach (var r in BaseParts(slot)) r.enabled = false;
                if (o.style[s] >= 0)
                {
                    var parts = new List<SkinnedMeshRenderer>(Parts(o.style[s], slot));
                    foreach (var src in parts)
                    {
                        var r = Graft(src, o.style[s], slot);
                        if (!Trim(src.name, parts)) Dye(r, dye, used, null);
                    }
                }
            }
            foreach (var key in new List<(Material, Color)>(_dyed.Keys))
            {
                var m = _dyed[key];
                if (used.Contains(m)) continue;
                if (m.mainTexture is RenderTexture rt) { rt.Release(); Destroy(rt); }
                Destroy(m);
                _dyed.Remove(key);
            }

            _outfit = o;
            if (save) Save();
            Changed?.Invoke();
        }

        void Save()
        {
            try
            {
                PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_outfit));
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("PlayerLook: couldn't save the outfit: " + e.Message); }
        }

        // Pieces that keep their own colour when the garment is dyed: a tie, a collar, a belt, and the shirt
        // under a suit jacket.
        static bool Trim<T>(string name, IEnumerable<T> outfit) where T : Renderer
        {
            if (name.Contains("Tie") || name.Contains("Collar") || name.Contains("Belt")) return true;
            if (!name.Contains("Shirt")) return false;
            foreach (var r in outfit) if (r.name.Contains("Suit")) return true;
            return false;
        }

        IEnumerable<Renderer> BaseParts(Slot s)
        {
            foreach (var r in _originals.Keys) if (SlotOf(r.name) == s) yield return r;
        }

        // Swap the whole body for another character, keeping the animator and the player's height.
        void Rebuild(int who)
        {
            var old = _model;
            var oldAnim = old.GetComponent<Animator>();
            var m = Instantiate(characters[who].model, transform);
            m.name = "Model";
            m.transform.localPosition = Vector3.zero;
            m.transform.localRotation = Quaternion.identity;
            m.transform.localScale = Vector3.one * (_height / SourceHeight(who));
            if (!m.TryGetComponent<Animator>(out var anim)) anim = m.AddComponent<Animator>();
            anim.runtimeAnimatorController = oldAnim.runtimeAnimatorController;
            anim.applyRootMotion = oldAnim.applyRootMotion;
            anim.cullingMode = oldAnim.cullingMode;
            var template = old.GetComponentInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in m.GetComponentsInChildren<SkinnedMeshRenderer>(true)) CopySettings(template, r);

            old.SetParent(null);                       // out of the way before the mover looks for the new animator
            old.gameObject.SetActive(false);
            Destroy(old.gameObject);
            _model = m.transform;
            _builtWho = who;
            Collect();
            GetComponent<CharacterMover>().BindAnimator();
        }

        void Collect()
        {
            _originals.Clear();
            foreach (var r in _model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) _originals[r] = r.sharedMaterials;
            _bones.Clear();
            foreach (var t in _model.GetComponentsInChildren<Transform>(true))
                if (!t.name.StartsWith("~") && t.GetComponent<Renderer>() == null) _bones[Strip(t.name)] = t;
        }

        // Re-skin another character's part onto our skeleton.
        SkinnedMeshRenderer Graft(SkinnedMeshRenderer src, int from, Slot slot)
        {
            var go = new GameObject($"{src.name} ({characters[from].name})");
            go.transform.SetParent(_model, false);
            var r = go.AddComponent<SkinnedMeshRenderer>();
            CopySettings(_model.GetComponentInChildren<SkinnedMeshRenderer>(true), r);
            r.sharedMesh = src.sharedMesh;
            r.sharedMaterials = src.sharedMaterials;
            // The source model may be built at a different size (Remy is about twice as big): scale its offsets
            // from each bone to match our body.
            float k = SourceHeight(_builtWho) / SourceHeight(from) * BoneScale(from) / BoneScale(_builtWho);
            bool loose = slot == Slot.Top || slot == Slot.Bottoms;
            var bones = new Transform[src.bones.Length];
            for (int i = 0; i < bones.Length; i++) bones[i] = Bone(src.bones[i], loose ? k * LooseFit(src.bones[i].name) : k);
            r.bones = bones;
            r.rootBone = Bone(src.rootBone != null ? src.rootBone : src.bones[0], k);
            r.updateWhenOffscreen = true;
            _added.Add(go);
            return r;
        }

        // Clothes cut for someone slimmer let skin poke through, worst on the upper arms (the bodies differ most
        // in the shoulders): worn a little loose, and looser on the arms.
        static float LooseFit(string bone)
        {
            var n = Strip(bone);
            if (n.EndsWith("ForeArm")) return 1.12f;
            if (n.EndsWith("Arm")) return 1.2f;
            return 1.05f;
        }

        static void CopySettings(SkinnedMeshRenderer from, SkinnedMeshRenderer to)
        {
            if (from == null || from == to) return;
            to.shadowCastingMode = from.shadowCastingMode;
            to.receiveShadows = from.receiveShadows;
            to.lightProbeUsage = from.lightProbeUsage;
            to.reflectionProbeUsage = from.reflectionProbeUsage;
            to.renderingLayerMask = from.renderingLayerMask;
            to.quality = from.quality;
        }

        // Our bone with the same name (or the nearest named ancestor's, for extra bones we don't have).
        Transform Bone(Transform src, float k)
        {
            for (var b = src; b != null; b = b.parent)
                if (_bones.TryGetValue(Strip(b.name), out var t)) return Mathf.Abs(k - 1) < 0.03f ? t : Proxy(t, k);
            return _model;
        }

        static Transform Proxy(Transform bone, float k)
        {
            var name = "~x" + k.ToString("0.000");
            var p = bone.Find(name);
            if (p != null) return p;
            p = new GameObject(name).transform;
            p.SetParent(bone, false);
            p.localScale = Vector3.one * k;
            return p;
        }

        static string Strip(string n)
        {
            int i = n.IndexOf(':');
            return i >= 0 ? n.Substring(i + 1) : n;
        }

        float BoneScale(int who)
        {
            foreach (var t in characters[who].model.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith("Hips")) return t.lossyScale.x;
            return 1;
        }

        // Height of a character's model as imported (measured once on a throwaway copy, like the street crowds).
        float SourceHeight(int who)
        {
            if (_sourceHeight[who] > 0) return _sourceHeight[who];
            var probe = Instantiate(characters[who].model, new Vector3(0, -500, 0), Quaternion.identity);
            float h = 0;
            foreach (var r in probe.GetComponentsInChildren<Renderer>()) h = Mathf.Max(h, r.bounds.max.y - probe.transform.position.y);
            probe.SetActive(false);
            Destroy(probe);
            return _sourceHeight[who] = h > 0.1f ? h : 1.8f;
        }

#if UNITY_EDITOR
        // Works out how far each character's tops / bottoms reach down the limbs and where their bare skin starts,
        // from which bones the vertices follow. (Editor only: the meshes aren't readable in builds.)
        [ContextMenu("Measure coverage")]
        void MeasureCoverage()
        {
            UnityEditor.Undo.RecordObject(this, "Measure coverage");
            foreach (var c in characters) MeasureCoverageOf(c);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // Fills in each character's skin / reach (see Character).
        void MeasureCoverageOf(Character c)
        {
            var root = c.model.transform;
            float height = 0;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) height = Mathf.Max(height, r.sharedMesh.bounds.max.y);
            var l = FindBone(root, "LeftArm");
            var rt = FindBone(root, "RightArm");
            c.shoulders = l != null && rt != null && height > 0 ? Vector3.Distance(l.position, rt.position) / height : 0;
            c.skin = new float[Segments];
            c.reach = new float[4 * Segments];
            var bodySeen = new bool[Segments];
            for (int seg = 0; seg < Segments; seg++) c.skin[seg] = 0;
            var slotReach = new float[4, Segments];
            for (int sl = 0; sl < 4; sl++) for (int seg = 0; seg < Segments; seg++) slotReach[sl, seg] = 1;
            var slotSeen = new bool[4];
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var slot = SlotOf(r.name);
                Limbs(root, r, out var minT, out var maxT, out var deeper, out var any);
                for (int seg = 0; seg < Segments; seg++)
                {
                    if (slot == null)
                    {
                        // Skin starts where the body's vertices on this segment start, or after it if it has none.
                        float start = any[seg] ? minT[seg] : deeper[seg] ? 1 : 0;
                        c.skin[seg] = Mathf.Max(c.skin[seg], start);
                    }
                    else
                    {
                        float r2 = deeper[seg] ? 1 : any[seg] ? maxT[seg] : 0;
                        int sl = (int)slot.Value;
                        slotReach[sl, seg] = slotSeen[sl] ? Mathf.Max(slotReach[sl, seg], r2) : r2;
                    }
                }
                if (slot != null) slotSeen[(int)slot.Value] = true;
            }
            for (int sl = 0; sl < 4; sl++) for (int seg = 0; seg < Segments; seg++) c.reach[sl * Segments + seg] = slotSeen[sl] ? slotReach[sl, seg] : 0;
        }

        // Where this mesh's vertices sit along each limb segment (worst of left and right): the lowest and highest
        // point on it (0 = top joint, 1 = bottom joint), and whether it carries on further down the limb.
        static void Limbs(Transform root, SkinnedMeshRenderer r, out float[] minT, out float[] maxT, out bool[] deeper, out bool[] any)
        {
            minT = new float[Segments];
            maxT = new float[Segments];
            deeper = new bool[Segments];
            any = new bool[Segments];
            var verts = r.sharedMesh.vertices;
            var weights = r.sharedMesh.boneWeights;
            var sideMin = new float[2, Segments];
            var sideMax = new float[2, Segments];
            var sideAny = new bool[2, Segments];
            var sideDeeper = new bool[2, Segments];
            for (int sd = 0; sd < 2; sd++) for (int seg = 0; seg < Segments; seg++) { sideMin[sd, seg] = 1; sideMax[sd, seg] = 0; }
            for (int i = 0; i < verts.Length; i++)
            {
                var bone = r.bones[weights[i].boneIndex0];
                var n = Strip(bone.name);
                int side = n.StartsWith("Left") ? 0 : n.StartsWith("Right") ? 1 : -1;
                if (side < 0) continue;
                int pos = ChainPos(n, out bool leg);
                if (pos < 0) continue;
                int first = leg ? 2 : 0;
                // Further down than a segment counts as covering all of it.
                for (int k = 0; k < pos && k < 2; k++) sideDeeper[side, first + k] = true;
                if (pos > 1) continue;
                int seg = first + pos;
                var child = FindBone(root, (side == 0 ? "Left" : "Right") + (leg ? (pos == 0 ? "Leg" : "Foot") : (pos == 0 ? "ForeArm" : "Hand")));
                if (child == null) continue;
                var p0 = root.InverseTransformPoint(bone.position);
                var d = root.InverseTransformPoint(child.position) - p0;
                float t = Mathf.Clamp01(Vector3.Dot(verts[i] - p0, d) / d.sqrMagnitude);
                sideMin[side, seg] = Mathf.Min(sideMin[side, seg], t);
                sideMax[side, seg] = Mathf.Max(sideMax[side, seg], t);
                sideAny[side, seg] = true;
            }
            for (int seg = 0; seg < Segments; seg++)
            {
                any[seg] = sideAny[0, seg] || sideAny[1, seg];
                deeper[seg] = sideDeeper[0, seg] && sideDeeper[1, seg];
                minT[seg] = Mathf.Max(sideAny[0, seg] ? sideMin[0, seg] : 0, sideAny[1, seg] ? sideMin[1, seg] : 0);
                maxT[seg] = Mathf.Min(sideAny[0, seg] ? sideMax[0, seg] : 1, sideAny[1, seg] ? sideMax[1, seg] : 1);
            }
        }

        // Position down the arm (Arm 0, ForeArm 1, Hand/fingers 2) or leg (UpLeg 0, Leg 1, Foot/Toe 2).
        static int ChainPos(string n, out bool leg)
        {
            n = n.StartsWith("Left") ? n.Substring(4) : n.StartsWith("Right") ? n.Substring(5) : n;
            leg = true;
            if (n.StartsWith("UpLeg")) return 0;
            if (n.StartsWith("Leg")) return 1;
            if (n.StartsWith("Foot") || n.StartsWith("Toe")) return 2;
            leg = false;
            if (n.StartsWith("ForeArm")) return 1;
            if (n.StartsWith("Arm")) return 0;
            if (n.StartsWith("Hand")) return 2;
            return -1;
        }

        static Transform FindBone(Transform root, string stripped)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (Strip(t.name) == stripped) return t;
            return null;
        }
#endif

        // ---------- Dye ----------

        void Dye(Renderer r, Color? colour, HashSet<Material> used, string onlyMaterialsNamed)
        {
            if (colour == null) return;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || (onlyMaterialsNamed != null && !mats[i].name.ToLowerInvariant().Contains(onlyMaterialsNamed))) continue;
                mats[i] = Dyed(mats[i], colour.Value);
                used.Add(mats[i]);
            }
            r.sharedMaterials = mats;
        }

        // A copy of the material with its texture recoloured on the GPU (cached while it's being worn).
        Material Dyed(Material src, Color c)
        {
            var key = (src, c);
            if (_dyed.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(src) { name = src.name + " (dyed)" };
            var tex = src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : src.mainTexture;
            if (tex == null || dyeShader == null)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            }
            else
            {
                if (_dyeMat == null) _dyeMat = new Material(dyeShader);
                var rt = new RenderTexture(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    useMipMap = true, autoGenerateMips = true, wrapMode = tex.wrapMode, filterMode = FilterMode.Trilinear,
                    name = tex.name + " (dyed)",
                };
                _dyeMat.SetColor("_Dye", c);
                var was = RenderTexture.active;
                Graphics.Blit(tex, rt, _dyeMat);
                RenderTexture.active = was;          // (Blit leaves rt active, which warns when it's released later)
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", rt);
                m.mainTexture = rt;
            }
            _dyed[key] = m;
            return m;
        }
    }
}
