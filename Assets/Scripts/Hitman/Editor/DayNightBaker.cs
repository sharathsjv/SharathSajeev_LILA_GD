using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hitman.EditorTools
{
    // Hitman > Bake Day-Night Lighting: the two-pass bake the day/night cycle needs (see DayNight.cs).
    //   1. Sky layer: only a flat white sky, all baked lights off -> saved as Assets/Hitman/Lighting/Sky_<n>.png
    //   2. Lamp layer: the baked lights (street lamps, neon, wall bulbs, room bulbs), black sky -> Unity's own lightmaps
    // Both bakes pack objects the same way, so the sky maps line up with the lamp lightmaps.
    // Re-run after changing a level's layout or its lights.
    public static class DayNightBaker
    {
        const string Folder = "Assets/Hitman/Lighting";
        static int _stage;
        static double _startedAt;
        static bool _sawRunning;
        static Color _ambient;
        static AmbientMode _ambientMode;

        [MenuItem("Hitman/Bake Day-Night Lighting")]
        public static void Bake()
        {
            if (Lightmapping.isRunning || _stage != 0) { Debug.LogWarning("[Hitman] A bake is already running."); return; }
            _ambient = RenderSettings.ambientLight;
            _ambientMode = RenderSettings.ambientMode;
            _stage = 1;
            SetLights(false);
            SetSky(Color.white);
            Debug.Log("[Hitman] Baking the sky layer...");
            StartBake("sky");
            EditorApplication.update += Tick;
        }

        static void StartBake(string what)
        {
            _sawRunning = false;
            _startedAt = EditorApplication.timeSinceStartup;
            if (!Lightmapping.BakeAsync()) Fail("couldn't start the " + what + " bake");
        }

        // Watches the baker and moves on to the next pass once the current one has finished.
        static void Tick()
        {
            if (_stage == 0) { EditorApplication.update -= Tick; return; }
            if (Lightmapping.isRunning) { _sawRunning = true; return; }
            if (!_sawRunning && EditorApplication.timeSinceStartup - _startedAt < 3) return;
            if (_stage == 1)
            {
                int n = SaveSkyMaps();
                _stage = 2;
                SetLights(true);
                SetSky(Color.black);
                Debug.Log($"[Hitman] Saved {n} sky lightmaps. Baking the lamp layer...");
                StartBake("lamp");
            }
            else if (_stage == 2) Finish();
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            _stage = 0;
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientLight = _ambient;
            var dn = Object.FindAnyObjectByType<DayNight>();
            if (dn != null)
            {
                var so = new SerializedObject(dn);
                var maps = so.FindProperty("skyLightmaps");
                maps.arraySize = LightmapSettings.lightmaps.Length;
                for (int i = 0; i < maps.arraySize; i++)
                    maps.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/Sky_{i}.png");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Hitman] Day-night lighting baked.");
        }

        static void Fail(string why)
        {
            EditorApplication.update -= Tick;
            _stage = 0;
            SetLights(true);
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientLight = _ambient;
            Debug.LogError("[Hitman] Day-night bake failed: " + why);
        }

        static void SetLights(bool on)
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
                if (l.lightmapBakeType == LightmapBakeType.Baked) l.enabled = on;
        }

        static void SetSky(Color c)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = c;
            RenderSettings.ambientIntensity = 1f;
        }

        // Copies each baked lightmap into a small greyscale PNG (the sky's reach, 0..1).
        static int SaveSkyMaps()
        {
            Directory.CreateDirectory(Folder);
            var maps = LightmapSettings.lightmaps;
            for (int i = 0; i < maps.Length; i++)
            {
                string src = AssetDatabase.GetAssetPath(maps[i].lightmapColor);
                var ti = (TextureImporter)AssetImporter.GetAtPath(src);
                bool readable = ti.isReadable;
                var comp = ti.textureCompression;
                ti.isReadable = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.SaveAndReimport();
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(src);
                var px = t.GetPixels();
                for (int k = 0; k < px.Length; k++)
                {
                    float v = Mathf.Clamp01((px[k].r + px[k].g + px[k].b) / 3f);
                    px[k] = new Color(v, v, v, 1);
                }
                var outTex = new Texture2D(t.width, t.height, TextureFormat.RGB24, false, true);
                outTex.SetPixels(px);
                outTex.Apply();
                string dst = $"{Folder}/Sky_{i}.png";
                File.WriteAllBytes(dst, outTex.EncodeToPNG());
                Object.DestroyImmediate(outTex);
                ti.isReadable = readable;
                ti.textureCompression = comp;
                ti.SaveAndReimport();

                AssetDatabase.ImportAsset(dst);
                var di = (TextureImporter)AssetImporter.GetAtPath(dst);
                di.sRGBTexture = false;
                di.mipmapEnabled = false;
                di.wrapMode = TextureWrapMode.Clamp;
                di.textureCompression = TextureImporterCompression.Compressed;
                di.SaveAndReimport();
            }
            return maps.Length;
        }
    }
}
