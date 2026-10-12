using UnityEngine;

namespace Hitman
{
    // Get close enough to someone who's talking and you hear them (speech bubbles). Hearing everything they say
    // during that part of their routine notes what you learned.
    public class Eavesdrop : MonoBehaviour
    {
        [SerializeField] float hearing = 4f;
        [SerializeField] float secondsPerLine = 3.5f;

        RoutineCharacter _talker;
        int _step = -1, _line;
        float _next;

        void Update()
        {
            var g = Game.I;
            if (g == null || g.Busy || !g.ClockRunning) return;
            var me = g.Player.transform.position;
            RoutineCharacter best = null;
            float bestD = hearing;
            foreach (var p in g.People)
            {
                if (!p.Visible || !p.IsAwake || !p.Settled) continue;
                var s = p.Step;
                if (s == null || s.chatter == null || s.chatter.Length == 0) continue;
                float d = (p.transform.position - me).magnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            if (best == null) { _talker = null; return; }
            if (best != _talker || best.StepIndex != _step)
            {
                _talker = best;
                _step = best.StepIndex;
                _line = 0;
                _next = 0;
            }
            if (Time.time < _next) return;
            var step = best.Step;
            if (_line < step.chatter.Length)
            {
                g.UI.Say(best.transform, step.chatter[_line]);
                _line++;
                _next = Time.time + secondsPerLine;
            }
            else if (!string.IsNullOrEmpty(step.overheard))
            {
                g.Note($"heard.{best.Routine.name}.{_step}", step.overheard);
                _next = Time.time + 9999;
            }
        }
    }
}
