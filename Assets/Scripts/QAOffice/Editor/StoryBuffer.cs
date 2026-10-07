using UnityEngine;

namespace QAOffice.EditorTools
{
    // In-memory holder so the Story Editor can draw story.json with Unity's property UI (undo included).
    public class StoryBuffer : ScriptableObject
    {
        public StoryData data;
    }
}
