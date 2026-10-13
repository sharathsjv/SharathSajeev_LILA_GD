using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

namespace Hitman
{
    // A space the player can be in: a street map (District) or the inside of a building (Interior).
    // Only the area the player is in is active.
    public abstract class Area : MonoBehaviour
    {
        [SerializeField] protected string id;
        [SerializeField] protected string displayName;
        [SerializeField] protected NavMeshSurface navSurface;
        [Tooltip("Camera look-at point when nothing else is being followed.")]
        [SerializeField] protected Transform cameraHome;
        [Header("Camera here")]
        [Tooltip("Which way the camera faces (0 = looking north/+Z, 90 = east/+X, 270 = west). On streets, pick it so " +
                 "the main street runs roughly left-right across the screen.")]
        [SerializeField] protected float cameraYaw;
        [Tooltip("How steeply it looks down (90 = straight down). 0 = the camera's default.")]
        [SerializeField] protected float cameraPitch;
        [Tooltip("Zoom (orthographic size; smaller = closer). 0 = the camera's default.")]
        [SerializeField] protected float cameraSize;
        [Tooltip("How far the camera sits back from the player. 0 = the camera's default (far away).")]
        [SerializeField] protected float cameraDistance;
        [Tooltip("Street view: the camera stays on the far side of the street from the player and swings round " +
                 "when they cross. The street runs along Z through X = streetCenterX.")]
        [SerializeField] protected bool switchSides;
        [SerializeField] protected float streetCenterX;
        [Tooltip("The camera is kept within this distance of the street's centre line, so the buildings behind it " +
                 "are cut away.")]
        [SerializeField] protected float streetHalfWidth = 7f;
        [Tooltip("Street view: the camera's rail runs this far out from the centre line, on the player's side (the " +
                 "middle of the sidewalk keeps them centred there; 0 = down the middle of the street).")]
        [SerializeField] protected float railOffset;
        [Tooltip("Street view: the road's half width. Small props (lamps, trees, benches...) on the sidewalk on the " +
                 "camera's side, past this, are hidden along with the buildings there (0 = keep them).")]
        [SerializeField] protected float roadHalfWidth;
        [Tooltip("How strongly the blue noir grade applies here (1 = full, for grim areas; lower for the cleaner districts).")]
        [SerializeField, Range(0, 1)] protected float noirStrength = 1f;
        [Tooltip("At night: multiplies the sky/ambient light here (1 = as the day/night keys say).")]
        [SerializeField, Range(0.5f, 4)] protected float nightSky = 1f;
        [Tooltip("At night: multiplies the street lamps, neon and lit windows here.")]
        [SerializeField, Range(0.5f, 3)] protected float nightLamps = 1f;
        [Tooltip("Keep the camera on Camera Home (the middle of the room) instead of following the player, so the " +
                 "whole floor stays in view.")]
        [SerializeField] protected bool fixedCamera;

        public string Id => id;
        public string DisplayName => displayName;
        public Transform CameraHome => cameraHome;
        public float CameraYaw => cameraYaw;
        public float CameraPitch => cameraPitch;
        public float CameraSize => cameraSize;
        public float CameraDistance => cameraDistance;
        public bool SwitchSides => switchSides;
        public float StreetCenterX => streetCenterX;
        public float StreetHalfWidth => streetHalfWidth;
        public float RailOffset => railOffset;
        public float NoirStrength => noirStrength;
        public float NightSky => nightSky;

        [Tooltip("Streets: where the player's part of the district ends (local X/Z: x, z, width, depth; the doc map's " +
                 "\"do not let the player go past this point\"). The player can't walk past it and the camera stays inside. " +
                 "Zero size = no limit. Passers-by and cars still come and go beyond it.")]
        [SerializeField] protected Rect playerBounds;

        public bool Bounded => playerBounds.width > 0 && playerBounds.height > 0;

        // A point pulled back inside the player's bounds (margin: how far inside).
        public Vector3 ClampToBounds(Vector3 world, float margin = 0)
        {
            if (!Bounded) return world;
            var l = transform.InverseTransformPoint(world);
            l.x = Mathf.Clamp(l.x, playerBounds.xMin + margin, playerBounds.xMax - margin);
            l.z = Mathf.Clamp(l.z, playerBounds.yMin + margin, playerBounds.yMax - margin);
            return transform.TransformPoint(l);
        }
        public float NightLamps => nightLamps;
        public bool FixedCamera => fixedCamera && cameraHome != null;

        // ---------- street cutaway ----------
        // With the camera low inside the street, the buildings on its own side would be sliced by the camera.
        // Everything beyond the street edge on the camera's side is hidden (the ground stays), and so are the small
        // props on the camera's sidewalk, which the camera would otherwise slice through.
        List<Renderer> _west, _east;
        int _cutSide;

        public void Cutaway(int cameraSide)
        {
            if (!switchSides) return;
            if (_west == null || _west.Count + _east.Count == 0)
            {
                CollectCutaway();
                _cutSide = 0;                                   // (re)apply to the freshly collected pieces
            }
            if (cameraSide == _cutSide) return;
            _cutSide = cameraSide;
            foreach (var r in _west) if (r != null) r.enabled = cameraSide >= 0;   // camera on the west (-X) side hides the west
            foreach (var r in _east) if (r != null) r.enabled = cameraSide <= 0;
        }

        static readonly string[] SidewalkProps =
            { "Prop_Lamp", "Prop_Tree", "Prop_Bench", "Prop_Hydrant", "Prop_Bollard", "Prop_TrafficLight", "Phone booth", "Station_" };

        bool IsProp(Transform t)
        {
            for (; t != null && t != transform; t = t.parent)
                foreach (var p in SidewalkProps) if (t.name.StartsWith(p)) return true;
            return false;
        }

        void CollectCutaway()
        {
            _west = new List<Renderer>();
            _east = new List<Renderer>();
            float edge = streetHalfWidth;
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                var n = r.name;
                if (n.StartsWith("Gnd_") || n.StartsWith("NoCol_") || r.GetComponentInParent<StreetLife>() != null) continue;
                // By the piece's centre (signs and canopies stick out past the facade), from the mesh asset's
                // bounds (renderer bounds are empty while the area is switched off).
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var c = transform.InverseTransformPoint(r.transform.TransformPoint(mf.sharedMesh.bounds.center)).x - streetCenterX;
                if (c < -edge || (roadHalfWidth > 0 && c < -roadHalfWidth && IsProp(r.transform))) _west.Add(r);
                else if (c > edge || (roadHalfWidth > 0 && c > roadHalfWidth && IsProp(r.transform))) _east.Add(r);
            }
        }
        // The street map this area belongs to (itself for a District); subways and taxis work per district.
        public abstract District Map { get; }

        bool _built;

        public void Enter()
        {
            gameObject.SetActive(true);
            if (!_built && navSurface != null)
            {
                navSurface.BuildNavMesh();
                _built = true;
            }
        }

        public void Leave() => gameObject.SetActive(false);
    }
}
