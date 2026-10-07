using System.Linq;
using UnityEditor;
using UnityEngine;

namespace QAOffice.EditorTools
{
    // Dropdowns for id references and a colour picker for hex colours.
    [CustomPropertyDrawer(typeof(StoryRefAttribute))]
    public class StoryRefDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var kind = ((StoryRefAttribute)attribute).Kind;
            EditorGUI.BeginProperty(position, label, property);
            if (kind == StoryRefKind.Color)
            {
                ColorUtility.TryParseHtmlString(property.stringValue, out var c);
                EditorGUI.BeginChangeCheck();
                c = EditorGUI.ColorField(position, label, c, true, false, false);
                if (EditorGUI.EndChangeCheck()) property.stringValue = "#" + ColorUtility.ToHtmlStringRGB(c);
            }
            else
            {
                var options = Options(kind);
                if (options == null)
                {
                    property.stringValue = EditorGUI.TextField(position, label, property.stringValue);
                }
                else
                {
                    string current = property.stringValue ?? "";
                    var list = options.ToList();
                    int index = list.IndexOf(current);
                    var display = list.Select(o => new GUIContent(o)).ToList();
                    if (index < 0)
                    {
                        display.Insert(0, new GUIContent(string.IsNullOrEmpty(current) ? "(choose...)" : $"{current}  (missing!)"));
                        list.Insert(0, current);
                        index = 0;
                    }
                    int picked = EditorGUI.Popup(position, label, index, display.ToArray());
                    if (picked != index) property.stringValue = list[picked];
                }
            }
            EditorGUI.EndProperty();
        }

        static string[] Options(StoryRefKind kind)
        {
            var d = StoryEditorWindow.Current;
            switch (kind)
            {
                case StoryRefKind.Genre: return BugCatalog.PoolByGenre.Keys.Append("random").ToArray();
                case StoryRefKind.Effect: return new[] { "promotion", "bonus", "none" };
                case StoryRefKind.Shape: return Icons.ShapeNames;
            }
            if (d == null) return null;
            switch (kind)
            {
                case StoryRefKind.Character: return d.characters?.Select(c => c.id).ToArray();
                case StoryRefKind.Boss: return d.characters?.Where(c => c.isBoss).Select(c => c.id).ToArray();
                case StoryRefKind.Icon: return d.icons?.Select(i => i.id).ToArray();
                case StoryRefKind.Secret: return d.secrets?.Select(s => s.id).ToArray();
            }
            return null;
        }
    }
}
