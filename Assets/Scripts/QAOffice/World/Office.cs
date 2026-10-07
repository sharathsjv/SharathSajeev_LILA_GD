using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QAOffice
{
    // The office as laid out in the scene. Finds every Actor under it on startup; move desks, people and
    // meeting seats around in the scene to change the layout.
    public class Office : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [Tooltip("Children of this transform are the seats people teleport to for the performance meeting.")]
        [SerializeField] Transform meetingSeats;

        public Actor Player { get; private set; }
        public readonly List<Actor> Npcs = new List<Actor>();
        public Camera Cam => cam;

        Actor _approaching;

        public void Init(StoryData story)
        {
            if (cam == null) cam = Camera.main;
            foreach (var actor in GetComponentsInChildren<Actor>(true))
            {
                if (actor.IsPlayer)
                {
                    if (Player != null) Debug.LogWarning($"[QAOffice] More than one player Actor; using {Player.name}.", actor);
                    else Player = actor;
                    actor.Bind(this, null);
                    continue;
                }
                var data = story.characters.FirstOrDefault(c => c.id == actor.CharacterId);
                if (data == null)
                {
                    Debug.LogWarning($"[QAOffice] Actor '{actor.name}' has character id '{actor.CharacterId}', which isn't in story.json. Disabling it.", actor);
                    actor.gameObject.SetActive(false);
                    continue;
                }
                actor.Bind(this, data);
                Npcs.Add(actor);
            }
            foreach (var c in story.characters)
                if (Npcs.All(n => n.Data.id != c.id))
                    Debug.LogWarning($"[QAOffice] Character '{c.id}' from story.json has no Actor in the scene.");
            if (Player == null) Debug.LogError("[QAOffice] No player Actor (an Actor with an empty Character Id) under Office.");
        }

        public Actor Npc(string id) => Npcs.FirstOrDefault(n => n.Data.id == id);

        public Actor RandomOtherNpc(Actor self)
        {
            var others = Npcs.Where(n => n != self && !n.Data.isBoss).ToList();
            return others.Count == 0 ? null : others[Random.Range(0, others.Count)];
        }

        // ---- Player actions ----

        public void Approach(Actor npc)
        {
            CancelApproach();
            _approaching = npc;
            npc.Hold(true);
            var dir = Player.transform.position - npc.transform.position;
            dir.y = 0;
            var spot = npc.transform.position + (dir.sqrMagnitude < 0.01f ? -npc.transform.forward : dir.normalized) * 1f;
            Player.WalkTo(spot, () =>
            {
                _approaching = null;
                Player.Face(npc.transform.position);
                npc.Face(Player.transform.position);
                if (GameManager.I.ClockRunning) GameManager.I.UI.Conversation.Open(npc);
                else npc.Hold(false);
            });
        }

        public void GoToDesk()
        {
            CancelApproach();
            Player.WalkTo(Player.Home, () =>
            {
                Player.transform.rotation = Player.HomeRotation;
                if (GameManager.I.ClockRunning) GameManager.I.UI.Computer.Open();
            });
        }

        public void WalkPlayer(Vector3 point)
        {
            CancelApproach();
            Player.WalkTo(point);
        }

        void CancelApproach()
        {
            if (_approaching != null) _approaching.Hold(false);
            _approaching = null;
        }

        public void ResetPositions()
        {
            CancelApproach();
            Player.GoHomeInstant();
            foreach (var n in Npcs)
            {
                n.Hold(false);
                n.GoHomeInstant();
            }
        }

        public void GatherForMeeting()
        {
            CancelApproach();
            if (meetingSeats == null || meetingSeats.childCount == 0)
            {
                Debug.LogWarning("[QAOffice] No meeting seats set on Office.");
                return;
            }
            var people = new List<Actor> { Player };
            people.AddRange(Npcs);
            for (int i = 0; i < people.Count; i++)
            {
                var seat = meetingSeats.GetChild(i % meetingSeats.childCount);
                people[i].Hold(true);
                people[i].Teleport(seat.position);
                people[i].transform.rotation = seat.rotation;
            }
        }
    }
}
