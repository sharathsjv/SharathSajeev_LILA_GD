using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
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
        [Tooltip("Children are lunch seats: the first is the player's, the rest go to companions. The parent's position is where the camera looks.")]
        [SerializeField] Transform lunchSeats;
        [SerializeField] PingPongTable pingPong;
        [Tooltip("Walkable area, rebuilt from the scene's colliders when the game starts, so moving furniture just works.")]
        [SerializeField] NavMeshSurface navSurface;

        public Actor Player { get; private set; }
        public readonly List<Actor> Npcs = new List<Actor>();
        public Camera Cam => cam;
        public CameraRig Rig => cam != null && cam.TryGetComponent<CameraRig>(out var rig) ? rig : null;

        Actor _approaching;

        public void Init(StoryData story)
        {
            if (cam == null) cam = Camera.main;
            if (navSurface != null) navSurface.BuildNavMesh();
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

        // World-space name labels and signs; hidden while the camera is zoomed into a conversation.
        public void SetLabelsVisible(bool visible)
        {
            foreach (var b in GetComponentsInChildren<Billboard>(true))
                if (b.TryGetComponent<MeshRenderer>(out var r)) r.enabled = visible;
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

        readonly List<Actor> _atLunch = new List<Actor>();

        public PingPongTable PingPong => pingPong;

        public void GoToPingPongTable()
        {
            if (pingPong == null) return;
            if (pingPong.IsNpcMatch)
            {
                // Tapping a coworkers' game: shout something from the sidelines.
                GameManager.I.UI.PingPong.ShowComments();
                return;
            }
            CancelApproach();
            Player.WalkTo(pingPong.PlayerSpot.position, () =>
            {
                if (GameManager.I.ClockRunning) GameManager.I.UI.PingPong.ShowOpponentPicker();
            });
        }

        public Vector3 MeetingFocus
        {
            get
            {
                if (meetingSeats == null || meetingSeats.childCount == 0) return transform.position;
                var sum = Vector3.zero;
                foreach (Transform seat in meetingSeats) sum += seat.position;
                return sum / meetingSeats.childCount;
            }
        }

        public Vector3 LunchFocus => lunchSeats != null ? lunchSeats.position : Vector3.zero;

        public void GatherForLunch(List<Actor> companions)
        {
            CancelApproach();
            _atLunch.Clear();
            _atLunch.Add(Player);
            _atLunch.AddRange(companions);
            for (int i = 0; i < _atLunch.Count && lunchSeats != null && i < lunchSeats.childCount; i++)
            {
                var seat = lunchSeats.GetChild(i);
                _atLunch[i].Hold(true);
                _atLunch[i].Teleport(seat.position);
                _atLunch[i].transform.rotation = seat.rotation;
            }
        }

        public void ReturnFromLunch()
        {
            foreach (var a in _atLunch) a.Hold(false); // NPCs walk back to their desks
            _atLunch.Clear();
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
