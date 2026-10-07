using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace QAOffice.EditorTools
{
    // QA Office > Story Editor: edit everything in story.json (settings, characters & dialogue, secrets,
    // tasks, chats, reviews, icons) with dropdowns for references and a validation report.
    public class StoryEditorWindow : EditorWindow
    {
        const string StoryPath = "Assets/Content/story.json";

        static readonly string[] Tabs = { "Settings", "Characters", "Secrets", "Tasks", "Chats", "Reviews", "Icons" };
        static readonly string[] Props = { "settings", "characters", "secrets", "tasks", "chats", "meeting", "icons" };
        static readonly string[] Help =
        {
            "Timing, costs and scoring. Changes apply the next time you press Play.",
            "Everyone in the office. Each id must match the Character Id of an Actor in the Office scene. Topics are what the trust minigame is about: line 0 opens, each further line answers one 'Ask more'.",
            "Secrets are revealed by a source once trust is high enough and every required secret is known. Use 'promotion' on the last secret of each path.",
            "Builds assigned per day. Days without tasks here get auto-assigned random builds (see Settings).",
            "Messages that arrive in the work chat at a set day and time. Each reply changes trust with the sender and the interaction score.",
            "Check-in and review dialogue. A review picks the highest tier whose Min Percent is at or below the player's score.",
            "Topic icons for the trust minigame: a shape, a colour and a label.",
        };

        public static StoryData Current { get; private set; }

        [SerializeField] int tab;
        [SerializeField] Vector2 scroll;
        StoryBuffer _buffer;
        SerializedObject _so;
        List<(MessageType type, string text)> _issues = new List<(MessageType, string)>();
        bool _showIssues = true;

        [MenuItem("QA Office/Story Editor")]
        public static void Open() => GetWindow<StoryEditorWindow>("Story Editor").Show();

        void OnEnable()
        {
            saveChangesMessage = "story.json has unsaved changes. Save them?";
            Load();
            Undo.undoRedoPerformed += OnUndo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            if (_buffer != null) DestroyImmediate(_buffer);
            Current = null;
        }

        void OnUndo()
        {
            if (_so == null) return;
            _so.Update();
            hasUnsavedChanges = true;
            Repaint();
        }

        void Load()
        {
            if (_buffer != null) DestroyImmediate(_buffer);
            _buffer = CreateInstance<StoryBuffer>();
            _buffer.hideFlags = HideFlags.DontSave; // not HideAndDontSave: that includes NotEditable, which greys out every field
            _buffer.data = File.Exists(StoryPath) ? JsonUtility.FromJson<StoryData>(File.ReadAllText(StoryPath)) : new StoryData();
            _so = new SerializedObject(_buffer);
            Current = _buffer.data;
            hasUnsavedChanges = false;
            Validate();
        }

        public override void SaveChanges()
        {
            Save();
            base.SaveChanges();
        }

        void Save()
        {
            _so.ApplyModifiedProperties();
            File.WriteAllText(StoryPath, JsonUtility.ToJson(_buffer.data, true));
            AssetDatabase.ImportAsset(StoryPath);
            hasUnsavedChanges = false;
            Validate();
            ShowNotification(new GUIContent(_issues.Any(i => i.type == MessageType.Error) ? "Saved (with errors - see below)" : "Saved"));
        }

        void OnGUI()
        {
            if (_so == null || _buffer == null) Load();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(StoryPath + (hasUnsavedChanges ? "  *" : ""), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton)) Validate();
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton) &&
                    (!hasUnsavedChanges || EditorUtility.DisplayDialog("Reload story.json?", "Discard unsaved changes?", "Discard", "Cancel")))
                    Load();
                using (new EditorGUI.DisabledScope(!hasUnsavedChanges))
                    if (GUILayout.Button("Save", EditorStyles.toolbarButton)) Save();
            }

            tab = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(26));
            EditorGUILayout.HelpBox(Help[tab], MessageType.None);
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("Saved changes take effect the next time the game starts.", MessageType.Info);

            _so.Update();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var prop = _so.FindProperty("data." + Props[tab]);
            if (prop.isArray) EditorGUILayout.PropertyField(prop, new GUIContent(Tabs[tab]), true);
            else DrawChildren(prop);
            EditorGUILayout.EndScrollView();
            if (_so.ApplyModifiedProperties())
            {
                hasUnsavedChanges = true;
                Current = _buffer.data;
            }

            DrawIssues();
        }

        static void DrawChildren(SerializedProperty prop)
        {
            var it = prop.Copy();
            var end = it.GetEndProperty();
            if (!it.NextVisible(true)) return;
            while (!SerializedProperty.EqualContents(it, end))
            {
                EditorGUILayout.PropertyField(it, true);
                if (!it.NextVisible(false)) break;
            }
        }

        void DrawIssues()
        {
            int errors = _issues.Count(i => i.type == MessageType.Error);
            int warnings = _issues.Count(i => i.type == MessageType.Warning);
            EditorGUILayout.Space(4);
            _showIssues = EditorGUILayout.Foldout(_showIssues,
                _issues.Count == 0 ? "Validation: no problems found" : $"Validation: {errors} error(s), {warnings} warning(s)", true);
            if (!_showIssues) return;
            foreach (var (type, text) in _issues.Take(12)) EditorGUILayout.HelpBox(text, type);
            if (_issues.Count > 12) EditorGUILayout.LabelField($"...and {_issues.Count - 12} more.");
        }

        // ---------- Validation ----------

        void Validate()
        {
            _issues.Clear();
            var d = _buffer.data;
            void Err(string m) => _issues.Add((MessageType.Error, m));
            void Warn(string m) => _issues.Add((MessageType.Warning, m));

            var chars = d.characters ?? new CharacterData[0];
            var icons = d.icons ?? new IconData[0];
            var secrets = d.secrets ?? new SecretData[0];
            var charIds = new HashSet<string>(chars.Select(c => c.id));
            var iconIds = new HashSet<string>(icons.Select(i => i.id));
            var secretIds = new HashSet<string>(secrets.Select(s => s.id));

            foreach (var dup in chars.GroupBy(c => c.id).Where(g => g.Count() > 1)) Err($"Characters: id '{dup.Key}' is used {dup.Count()} times.");
            foreach (var dup in icons.GroupBy(c => c.id).Where(g => g.Count() > 1)) Err($"Icons: id '{dup.Key}' is used {dup.Count()} times.");
            foreach (var dup in secrets.GroupBy(c => c.id).Where(g => g.Count() > 1)) Err($"Secrets: id '{dup.Key}' is used {dup.Count()} times.");
            if (icons.Length < 4) Err("Icons: the trust minigame needs at least 4 icons (1 answer + 3 decoys).");

            foreach (var c in chars)
            {
                var topics = c.topics ?? new TopicData[0];
                if (!topics.Any(t => t.minDay <= 1)) Err($"{c.name}: needs at least one topic available from Day 1.");
                foreach (var t in topics)
                {
                    if (!iconIds.Contains(t.icon)) Err($"{c.name}: topic icon '{t.icon}' doesn't exist.");
                    if (t.lines == null || t.lines.Length < 2) Warn($"{c.name}: topic '{t.icon}' has fewer than 2 lines, so 'Ask more' can't add clues.");
                }
            }

            int maxTrust = d.settings?.maxTrust ?? 10;
            foreach (var s in secrets)
            {
                var about = chars.FirstOrDefault(c => c.id == s.about);
                if (about == null) Err($"Secret '{s.id}': 'about' character '{s.about}' doesn't exist.");
                else if (!about.isBoss) Warn($"Secret '{s.id}': '{s.about}' isn't a boss, so it can't be used on them.");
                if (!charIds.Contains(s.source)) Err($"Secret '{s.id}': source '{s.source}' doesn't exist.");
                foreach (var r in s.requires ?? new string[0])
                    if (!secretIds.Contains(r)) Err($"Secret '{s.id}': requires unknown secret '{r}'.");
                if (s.trustRequired > maxTrust) Err($"Secret '{s.id}': needs trust {s.trustRequired}, above max trust {maxTrust}.");
                if (s.effect != "promotion" && s.effect != "bonus" && s.effect != "none") Err($"Secret '{s.id}': unknown effect '{s.effect}'.");
            }
            if (!secrets.Any(s => s.effect == "promotion")) Warn("Secrets: no secret has the 'promotion' effect, so nobody can be promoted.");
            foreach (var s in secrets.Where(s => DependsOn(s.id, s.id, secrets, new HashSet<string>())))
                Err($"Secret '{s.id}': its requirements loop back to itself.");

            var genres = new HashSet<string>(BugCatalog.PoolByGenre.Keys) { "random" };
            foreach (var t in d.tasks ?? new TaskData[0])
            {
                if (!charIds.Contains(t.from)) Err($"Task on Day {t.day}: 'from' character '{t.from}' doesn't exist.");
                if (!genres.Contains(t.genre)) Err($"Task on Day {t.day}: unknown genre '{t.genre}'.");
            }
            foreach (var c in d.chats ?? new ChatMessageData[0])
            {
                if (!charIds.Contains(c.from)) Err($"Chat on Day {c.day} {c.hour:00}:{c.minute:00}: sender '{c.from}' doesn't exist.");
                if (c.replies == null || c.replies.Length == 0) Err($"Chat on Day {c.day} {c.hour:00}:{c.minute:00}: needs at least one reply.");
                if (d.settings != null && (c.hour < d.settings.dayStartHour || c.hour >= d.settings.dayEndHour))
                    Warn($"Chat on Day {c.day} {c.hour:00}:{c.minute:00}: outside working hours, it'll never arrive.");
            }

            var m = d.meeting;
            if (m != null)
            {
                if (!charIds.Contains(m.hostId)) Err($"Reviews: host '{m.hostId}' doesn't exist.");
                foreach (var a in m.attendees ?? new string[0])
                    if (!charIds.Contains(a)) Err($"Reviews: attendee '{a}' doesn't exist.");
                var tiers = m.tiers ?? new ReviewTier[0];
                if (tiers.Length == 0) Err("Reviews: needs at least one tier.");
                else
                {
                    if (tiers.Min(t => t.minPercent) != 0) Err("Reviews: the lowest tier must start at 0%.");
                    for (int i = 1; i < tiers.Length; i++)
                        if (tiers[i].minPercent <= tiers[i - 1].minPercent) Warn("Reviews: tiers should be listed lowest Min Percent first.");
                }
            }

            // Cross-check with the open scene, if it's the Office scene.
            var actors = FindObjectsByType<Actor>(FindObjectsInactive.Include);
            if (actors.Length > 0)
            {
                var sceneIds = new HashSet<string>(actors.Where(a => !a.IsPlayer).Select(a => a.CharacterId));
                foreach (var c in chars.Where(c => !sceneIds.Contains(c.id)))
                    Warn($"{c.name}: no Actor with Character Id '{c.id}' in the open scene, so they won't appear.");
                foreach (var id in sceneIds.Where(id => !charIds.Contains(id)))
                    Warn($"Scene Actor with Character Id '{id}' has no matching character here.");
            }
        }

        static bool DependsOn(string target, string current, SecretData[] all, HashSet<string> seen)
        {
            var s = all.FirstOrDefault(x => x.id == current);
            if (s?.requires == null) return false;
            foreach (var r in s.requires)
            {
                if (r == target) return true;
                if (seen.Add(r) && DependsOn(target, r, all, seen)) return true;
            }
            return false;
        }
    }
}
