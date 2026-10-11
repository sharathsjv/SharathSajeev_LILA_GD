using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hitman
{
    // Lighting that follows the game clock. The sun moves and changes colour (no real-time shadows: shadows are
    // baked), the sky/ambient colour scales the baked sky layer, and lamps, neon and lit windows fade in at dusk.
    // Static scenery uses the Hitman/Baked shader; characters use the normal URP Lit (sun + ambient + blob shadow).
    public class DayNight : MonoBehaviour
    {
        [Serializable]
        public class Key
        {
            public float hour;
            [Tooltip("Degrees above the horizon (below 0 = the sun is down: soft moon/sky light instead).")]
            public float sunElevation;
            public Color sunColor = Color.white;
            public float sunIntensity = 1;
            [Tooltip("Sky/ambient light: multiplies the baked sky layer, and lights characters.")]
            public Color sky = Color.gray;
            [Tooltip("Street lamps, neon and lit windows (0 = off, 1 = full night). Indoor bulbs are always on.")]
            [Range(0, 1)] public float lamps;
            [Tooltip("How much of the night colour grade to blend in.")]
            [Range(0, 1)] public float night;
            [Tooltip("How much of the golden-hour grade (sunrise/sunset amber) to blend in.")]
            [Range(0, 1)] public float golden;
            [Tooltip("Atmospheric haze: fog towards the sky/sun colour.")]
            [Range(0, 1)] public float haze;

            public Key(float h, float e, Color c, float i, Color s, float l, float n, float g = 0, float z = 0)
            {
                hour = h; sunElevation = e; sunColor = c; sunIntensity = i; sky = s; lamps = l; night = n; golden = g; haze = z;
            }
        }

        [SerializeField] Light sun;
        [Tooltip("The everyday blue noir grade; its strength follows the current area's Noir Strength.")]
        [SerializeField] Volume dayGrade;
        [SerializeField] Volume nightGrade;
        [SerializeField] Volume goldenGrade;
        [Tooltip("The camera's background takes the haze colour, so the void around a scene matches the time of day.")]
        [SerializeField] Camera cam;
        [SerializeField] float hazeDensity = 0.03f;
        [Tooltip("Sky light on characters relative to the scenery (keeps them from looking lit-up against the street).")]
        [SerializeField, Range(0, 1.5f)] float characterSky = 0.75f;
        [SerializeField] float sunriseHour = 6.8f;
        [SerializeField] float sunsetHour = 19.8f;
        [Tooltip("Keyframes by hour; the day starts at 07:00 at golden-hour sunrise and runs past midnight.")]
        [SerializeField] Key[] keys =
        {
            // hour, sun elevation, sun colour, intensity, sky, lamps, night, golden, haze
            new Key(7.0f, 4, new Color(1.00f, 0.68f, 0.32f), 1.3f, new Color(0.55f, 0.42f, 0.28f), 0.25f, 0f, 1f, 1f),
            new Key(8.0f, 14, new Color(1.00f, 0.80f, 0.55f), 1.15f, new Color(0.50f, 0.45f, 0.38f), 0f, 0f, 0.5f, 0.5f),
            new Key(9.5f, 30, new Color(1.00f, 0.93f, 0.82f), 1.2f, new Color(0.47f, 0.48f, 0.52f), 0f, 0f, 0f, 0.15f),
            new Key(13f, 58, new Color(1.00f, 0.97f, 0.92f), 1.25f, new Color(0.48f, 0.50f, 0.56f), 0f, 0f, 0f, 0.1f),
            new Key(17f, 28, new Color(1.00f, 0.90f, 0.75f), 1.1f, new Color(0.48f, 0.46f, 0.46f), 0f, 0f, 0.2f, 0.2f),
            new Key(18.5f, 10, new Color(1.00f, 0.70f, 0.33f), 1.3f, new Color(0.58f, 0.43f, 0.28f), 0.1f, 0f, 1f, 1f),
            new Key(19.5f, 1, new Color(1.00f, 0.50f, 0.22f), 0.8f, new Color(0.42f, 0.30f, 0.22f), 0.6f, 0.2f, 0.8f, 0.8f),
            new Key(20.25f, -4, new Color(0.55f, 0.65f, 0.90f), 0.2f, new Color(0.16f, 0.16f, 0.24f), 0.95f, 0.75f, 0.1f, 0.3f),
            new Key(21f, -10, new Color(0.55f, 0.65f, 0.90f), 0.22f, new Color(0.10f, 0.12f, 0.20f), 1f, 1f, 0f, 0.2f),
            new Key(26f, -10, new Color(0.55f, 0.65f, 0.90f), 0.22f, new Color(0.10f, 0.12f, 0.20f), 1f, 1f, 0f, 0.2f),
        };
        [Tooltip("Brightens the sky light on baked scenery: walls only see half the sky, so they'd be darker than " +
                 "characters standing in front of them.")]
        [SerializeField] float bakedSkyBoost = 2f;
        [Tooltip("The baked sky-occlusion lightmaps, by lightmap index (filled in by the bake tool).")]
        [SerializeField] Texture2D[] skyLightmaps;

        static readonly int SkyId = Shader.PropertyToID("_HitmanSky");
        static readonly int LampsId = Shader.PropertyToID("_HitmanLamps");
        static readonly int SkyMapId = Shader.PropertyToID("_SkyLightmap");

        public float Lamps { get; private set; }

        // Characters can't use lightmaps, so they read light probes: the baked probes hold only the lamp light
        // (baked with a black sky), and every few frames we rebuild them as lamps * lamp factor + today's sky.
        SphericalHarmonicsL2[] _lampProbes;
        SphericalHarmonicsL2[] _probes;
        float _nextProbeUpdate;

        void Start()
        {
            ApplySkyLightmaps();
            var lp = LightmapSettings.lightProbes;
            if (lp != null && lp.count > 0)
            {
                _lampProbes = (SphericalHarmonicsL2[])lp.bakedProbes.Clone();
                _probes = new SphericalHarmonicsL2[_lampProbes.Length];
            }
        }

        // Put the baked probes back as they were (so play mode doesn't leave edited lighting data behind).
        void OnDestroy()
        {
            if (_lampProbes != null && LightmapSettings.lightProbes != null) LightmapSettings.lightProbes.bakedProbes = _lampProbes;
        }

        // Hands each baked renderer its sky lightmap (the lamp layer is Unity's own lightmap).
        public void ApplySkyLightmaps()
        {
            if (skyLightmaps == null || skyLightmaps.Length == 0) return;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if (r.lightmapIndex < 0 || r.lightmapIndex >= skyLightmaps.Length) continue;
                var m = r.sharedMaterial;
                if (m == null || m.shader.name != "Hitman/Baked") continue;
                r.GetPropertyBlock(mpb);
                mpb.SetTexture(SkyMapId, skyLightmaps[r.lightmapIndex]);
                r.SetPropertyBlock(mpb);
            }
        }

        void Update()
        {
            var g = Game.I;
            if (g == null || keys.Length == 0) return;
            float h = g.Minutes / 60f;
            var k = Evaluate(h);

            if (k.sunElevation > 0)
            {
                float t = Mathf.InverseLerp(sunriseHour, sunsetHour, h);
                sun.transform.rotation = Quaternion.Euler(k.sunElevation, 270f + 180f * t, 0);   // east -> south -> west
            }
            else sun.transform.rotation = Quaternion.Euler(55f, 320f, 0);                         // moon / grey sky
            sun.color = k.sunColor;
            sun.intensity = k.sunIntensity;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = k.sky;
            Shader.SetGlobalColor(SkyId, k.sky * bakedSkyBoost);
            Lamps = g.Current is Interior ? 1f : k.lamps;
            Shader.SetGlobalFloat(LampsId, Lamps);
            if (dayGrade != null) dayGrade.weight = g.Current != null ? g.Current.NoirStrength : 1f;
            if (nightGrade != null) nightGrade.weight = k.night;
            if (goldenGrade != null) goldenGrade.weight = k.golden;
            if (_lampProbes != null && Time.unscaledTime >= _nextProbeUpdate)
            {
                _nextProbeUpdate = Time.unscaledTime + 0.25f;
                for (int i = 0; i < _lampProbes.Length; i++)
                {
                    var sh = _lampProbes[i] * (Lamps * Lamps);      // lamps only really count once it's dark
                    sh.AddAmbientLight(k.sky * characterSky);
                    _probes[i] = sh;
                }
                LightmapSettings.lightProbes.bakedProbes = _probes;
            }

            // Haze: fog and background towards a mix of sky and sun colour (amber at golden hour, blue at night).
            var haze = Color.Lerp(k.sky, k.sunColor * Mathf.Clamp01(k.sunIntensity), 0.45f);
            RenderSettings.fog = k.haze > 0.01f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = haze;
            RenderSettings.fogDensity = hazeDensity * k.haze;
            if (cam != null) cam.backgroundColor = haze * 0.6f;
        }

        Key Evaluate(float h)
        {
            if (h <= keys[0].hour) return keys[0];
            for (int i = 1; i < keys.Length; i++)
            {
                if (h > keys[i].hour) continue;
                var a = keys[i - 1];
                var b = keys[i];
                float t = Mathf.InverseLerp(a.hour, b.hour, h);
                return new Key(h, Mathf.Lerp(a.sunElevation, b.sunElevation, t), Color.Lerp(a.sunColor, b.sunColor, t),
                    Mathf.Lerp(a.sunIntensity, b.sunIntensity, t), Color.Lerp(a.sky, b.sky, t),
                    Mathf.Lerp(a.lamps, b.lamps, t), Mathf.Lerp(a.night, b.night, t),
                    Mathf.Lerp(a.golden, b.golden, t), Mathf.Lerp(a.haze, b.haze, t));
            }
            return keys[keys.Length - 1];
        }
    }
}
