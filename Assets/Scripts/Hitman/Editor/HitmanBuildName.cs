using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Hitman.EditorTools
{
    // The office game and the hitman game share this project: a build of the Hitman scene goes out as
    // "Everyday Objects" on its own web page template, and the office game's settings are put back afterwards.
    public class HitmanBuildName : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        const string Name = "Everyday Objects";
        const string Template = "PROJECT:Hitman";

        public int callbackOrder => 0;

        static string _name, _template;

        static bool IsHitman(BuildReport report)
        {
            var output = (report.summary.outputPath ?? "").Replace('\\', '/').TrimEnd('/');
            if (output.EndsWith("hitman-web") || output.Contains("/hitman")) return true;
            foreach (var f in EditorBuildSettingsScenesFor(report)) if (f.EndsWith("Hitman.unity")) return true;
            return false;
        }

        static string[] EditorBuildSettingsScenesFor(BuildReport report)
        {
            var active = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
            if (active != null && active.overrideGlobalScenes)
            {
                var list = new System.Collections.Generic.List<string>();
                foreach (var s in active.scenes) if (s.enabled) list.Add(s.path);
                return list.ToArray();
            }
            var all = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled) all.Add(s.path);
            return all.ToArray();
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            _name = null;
            if (report.summary.platform != BuildTarget.WebGL || !IsHitman(report)) return;
            _name = PlayerSettings.productName;
            _template = PlayerSettings.WebGL.template;
            PlayerSettings.productName = Name;
            PlayerSettings.WebGL.template = Template;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (_name == null) return;
            PlayerSettings.productName = _name;
            PlayerSettings.WebGL.template = _template;
            _name = null;
        }
    }
}
