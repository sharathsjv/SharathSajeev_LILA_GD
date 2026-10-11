using System;
using UnityEngine;

namespace QAOffice
{
    // Ping pong match logic, living on the table in the scene. Table frame: origin at the top's centre,
    // +x (the top's right axis) runs from the player's end to the opponent's, +z runs across the width.
    // The player slides along their end of the table and swipes the ball; the closer they are to the
    // ball, the more accurate the shot, and too far away is a miss.
    public class PingPongTable : MonoBehaviour
    {
        public enum Side { Player, Opponent }

        [Header("Scene parts")]
        [Tooltip("The table top. Its X scale is the length, Z scale the width.")]
        [SerializeField] Transform top;
        [SerializeField] Transform playerSpot;
        [SerializeField] Transform opponentSpot;
        [SerializeField] Transform ball;
        [SerializeField] Transform playerPaddle;
        [SerializeField] Transform opponentPaddle;

        [Header("Feel")]
        [Tooltip("How far (across the table) from the ball you can still hit it.")]
        [SerializeField] float reach = 0.55f;
        [Tooltip("How far past the end of the table you can slide.")]
        [SerializeField] float sideSlack = 0.35f;
        [Tooltip("Depth of the hitting window around each end of the table.")]
        [SerializeField] float hitWindow = 0.75f;
        [SerializeField] float arcHeight = 0.45f;
        [Tooltip("The player's body stands this far to one side of their paddle (picked automatically to keep the ball visible to the camera).")]
        [SerializeField] float playerBodyOffset = 0.35f;

        public Transform PlayerSpot => playerSpot;
        public Vector3 Center => top.position;
        public Vector3 BallWorld => ball.position;
        public float TopY => top.position.y + top.lossyScale.y / 2;
        float L => top.lossyScale.x;
        float W => top.lossyScale.z;

        public int PlayerScore { get; private set; }
        public int OpponentScore { get; private set; }
        public bool PlayerServing => _state == State.Held && _server == Side.Player;
        public bool PlayerCanHit => _state == State.Flying && _receiver == Side.Player && _bounced && InWindow(Side.Player);

        public bool InUse => _player != null || _npcWalking;
        public bool IsNpcMatch => _npcMatch && (_player != null || _npcWalking);
        public Actor NpcA => _npcMatch ? (_player ?? _walkA) : null;
        public Actor NpcB => _npcMatch ? (_opponent ?? _walkB) : null;

        public event Action<string> Message;
        public event Action<bool> MatchOver;   // true = player won

        enum State { Idle, Held, Flying, Over }

        State _state = State.Idle;
        Actor _player, _opponent;
        PingPongProfile _profile;
        int _pointsToWin;
        float _baseSpeed;
        float _playerZ, _opponentZ;
        Side _server, _receiver;
        Vector2 _pos, _vel, _landing;
        float _y, _airTime, _airTotal, _waitTimer;
        bool _bounced, _outOnLanding, _opponentSwung;
        float _aiReact;
        float _bodySide = 1;

        // Coworker-vs-coworker game (set dressing): both ends are AI and it ends on a timer.
        bool _npcMatch, _npcWalking;
        Actor _walkA, _walkB;
        int _arrived;
        PingPongProfile _playerSideProfile;
        float _npcTimeLeft;

        // ---------- Frame conversions ----------

        Vector3 World(float x, float z, float height) =>
            new Vector3(top.position.x, TopY + height, top.position.z) + top.right * x + top.forward * z;

        public Vector2 ToLocal(Vector3 world)
        {
            var d = world - top.position;
            return new Vector2(Vector3.Dot(d, top.right), Vector3.Dot(d, top.forward));
        }

        // ---------- Match control ----------

        public void BeginNpcMatch(Actor a, Actor b, float ballSpeed, float seconds)
        {
            if (InUse) return;
            _npcMatch = true;
            _npcWalking = true;
            _walkA = a;
            _walkB = b;
            _arrived = 0;
            _npcTimeLeft = seconds;
            _baseSpeed = ballSpeed;
            a.Hold(true);
            b.Hold(true);
            // Walk over, then start once both are at the table.
            a.WalkTo(playerSpot.position, Arrive);
            b.WalkTo(opponentSpot.position, Arrive);
        }

        void Arrive()
        {
            if (++_arrived < 2 || !_npcWalking) return;
            _npcWalking = false;
            _playerSideProfile = _walkA.Data.pingPong;
            Begin(_walkA, _walkB, _walkB.Data.pingPong, int.MaxValue, _baseSpeed);
            _bodySide = 0;
        }

        public void Begin(Actor player, Actor opponent, PingPongProfile profile, int pointsToWin, float ballSpeed)
        {
            _player = player;
            _opponent = opponent;
            _profile = profile;
            _pointsToWin = Mathf.Max(1, pointsToWin);
            _baseSpeed = Mathf.Max(0.5f, ballSpeed);
            PlayerScore = OpponentScore = 0;
            _playerZ = _opponentZ = 0;
            player.Hold(true);
            opponent.Hold(true);
            ball.gameObject.SetActive(true);
            playerPaddle.gameObject.SetActive(true);
            opponentPaddle.gameObject.SetActive(true);
            _server = Side.Player;
            _bodySide = PickBodySide();
            Hold();
        }

        // Which side of the paddle the player's body should stand on so it doesn't hide the ball:
        // the side that pushes the body furthest from the ball in screen space.
        float PickBodySide()
        {
            var cam = Camera.main;
            if (cam == null) return 1;
            var ballAtEnd = World(-L / 2, 0, 0.2f);
            float Separation(float side)
            {
                var body = playerSpot.position + top.forward * (side * playerBodyOffset) + Vector3.up * 0.9f;
                return Mathf.Abs(cam.WorldToScreenPoint(ballAtEnd).x - cam.WorldToScreenPoint(body).x);
            }
            return Separation(1) >= Separation(-1) ? 1 : -1;
        }

        public void End()
        {
            if (_npcWalking)
            {
                _walkA.Hold(false);
                _walkB.Hold(false);
            }
            _npcWalking = false;
            _npcMatch = false;
            _walkA = _walkB = null;
            _state = State.Idle;
            ball.gameObject.SetActive(false);
            playerPaddle.gameObject.SetActive(false);
            opponentPaddle.gameObject.SetActive(false);
            if (_opponent != null) _opponent.Hold(false);
            if (_player != null) _player.Hold(false);
            _player = _opponent = null;
        }

        void Hold()
        {
            _state = State.Held;
            _bounced = false;
            _waitTimer = IsAI(_server) ? 1.1f : 0;
            Message?.Invoke(_server == Side.Player ? "Your serve - swipe the ball" : $"{_opponent.Data.name} serves");
        }

        // ---------- Player input ----------

        public void PlayerSlide(float dz)
        {
            float limit = W / 2 + sideSlack;
            _playerZ = Mathf.Clamp(_playerZ + dz, -limit, limit);
        }

        // dir: swipe direction in table space; power 0..1 from swipe speed.
        public void PlayerSwing(Vector2 dir, float power)
        {
            if (PlayerServing)
            {
                Shoot(Side.Player, dir, power, 1f);
                return;
            }
            if (_state != State.Flying || _receiver != Side.Player || !_bounced || !InWindow(Side.Player)) return;
            float dz = Mathf.Abs(_playerZ - _pos.y);
            if (dz > reach)
            {
                Message?.Invoke("Too far!");
                return;
            }
            Shoot(Side.Player, dir, power, 1f - dz / reach);
        }

        // ---------- Shots ----------

        void Shoot(Side hitter, Vector2 dir, float power, float quality)
        {
            float sign = hitter == Side.Player ? 1 : -1;
            float startX = hitter == Side.Player ? Mathf.Min(_pos.x, -L / 2 + 0.05f) : Mathf.Max(_pos.x, L / 2 - 0.05f);
            if (_state == State.Held) _pos = new Vector2(sign * -(L / 2 - 0.05f), hitter == Side.Player ? _playerZ : _opponentZ);
            else _pos.x = startX;

            dir = dir.sqrMagnitude < 0.0001f ? new Vector2(sign, 0) : dir.normalized;
            if (dir.x * sign < 0.3f) dir = new Vector2(0.3f * sign, dir.y).normalized; // must go forward-ish
            float depth = Mathf.Lerp(0.12f, 0.46f, power) * L;
            float targetX = sign * depth;
            float targetZ = _pos.y + dir.y / dir.x * (targetX - _pos.x);

            float error = (1 - quality) * 0.45f + Mathf.Max(0, power - 0.85f) * 2f;
            targetX += UnityEngine.Random.Range(-error, error) * 0.5f;
            targetZ += UnityEngine.Random.Range(-error, error);

            _landing = new Vector2(targetX, targetZ);
            float speed = _baseSpeed * Mathf.Lerp(0.8f, 1.5f, power);
            _vel = (_landing - _pos).normalized * speed;
            _airTotal = Vector2.Distance(_pos, _landing) / speed;
            _airTime = 0;
            _y = 0.25f;
            _bounced = false;
            _outOnLanding = Mathf.Abs(targetZ) > W / 2 || Mathf.Abs(targetX) > L / 2 || targetX * sign <= 0;
            _receiver = hitter == Side.Player ? Side.Opponent : Side.Player;
            _state = State.Flying;
            _opponentSwung = false;
            _aiReact = 0.15f;
            Message?.Invoke("");
        }

        bool InWindow(Side side)
        {
            float end = side == Side.Player ? -L / 2 : L / 2;
            return Mathf.Abs(_pos.x - end) <= hitWindow;
        }

        // ---------- Simulation ----------

        bool IsAI(Side side) => side == Side.Opponent || _npcMatch;

        PingPongProfile ProfileOf(Side side) => side == Side.Player ? _playerSideProfile : _profile;

        void Update()
        {
            if (_player == null) return;
            if (_npcMatch)
            {
                _npcTimeLeft -= Time.deltaTime;
                if (_npcTimeLeft <= 0 && _state != State.Flying)
                {
                    End();
                    return;
                }
            }

            switch (_state)
            {
                case State.Held:
                    _pos = new Vector2(_server == Side.Player ? -(L / 2 - 0.05f) : L / 2 - 0.05f, _server == Side.Player ? _playerZ : _opponentZ);
                    _y = 0.3f + Mathf.Abs(Mathf.Sin(Time.time * 4)) * 0.15f;
                    if (IsAI(_server))
                    {
                        _waitTimer -= Time.deltaTime;
                        if (_waitTimer <= 0) AIShoot(_server);
                    }
                    break;

                case State.Flying:
                    Fly(Time.deltaTime);
                    break;

                case State.Idle:
                    _waitTimer -= Time.deltaTime;
                    if (_waitTimer <= 0 && _state != State.Over) Hold();
                    break;
            }

            MoveAI(Side.Opponent, Time.deltaTime);
            if (_npcMatch) MoveAI(Side.Player, Time.deltaTime);
            PlaceActors();
            ball.position = World(_pos.x, _pos.y, _y);
        }

        void Fly(float dt)
        {
            _pos += _vel * dt;
            if (!_bounced)
            {
                _airTime += dt;
                float t = Mathf.Clamp01(_airTime / _airTotal);
                _y = Mathf.Lerp(0.25f, 0f, t) + arcHeight * 4 * t * (1 - t);
                if (t >= 1)
                {
                    _bounced = true;
                    _airTime = 0;
                    if (_outOnLanding)
                    {
                        Point(_receiver, "Out!");
                        return;
                    }
                }
            }
            else
            {
                _airTime += dt;
                float u = _airTime * _vel.magnitude / 1.4f;
                _y = arcHeight * 0.8f * 4 * u * (1 - u);
                if (_y < 0) _y = 0;
            }

            // AI returns once the bounced ball is well inside its window.
            float recvEnd = _receiver == Side.Player ? -L / 2 : L / 2;
            if (IsAI(_receiver) && _bounced && !_opponentSwung && (_pos.x - recvEnd) * Mathf.Sign(recvEnd) >= -hitWindow * 0.35f)
            {
                _opponentSwung = true;
                float z = _receiver == Side.Player ? _playerZ : _opponentZ;
                if (Mathf.Abs(z - _pos.y) <= reach && UnityEngine.Random.value < Mathf.Lerp(0.5f, 0.95f, ProfileOf(_receiver).skill))
                {
                    AIShoot(_receiver);
                    return;
                }
            }

            // Ball left the receiver's window without a return.
            float end = _receiver == Side.Player ? -L / 2 : L / 2;
            if (_bounced && (_pos.x - end) * Mathf.Sign(end) > hitWindow * 0.6f)
                Point(_receiver == Side.Player ? Side.Opponent : Side.Player, _receiver == Side.Player ? "Missed!" : "Point!");
        }

        void AIShoot(Side side)
        {
            var profile = ProfileOf(side);
            float sign = side == Side.Player ? 1 : -1;   // direction toward the other end
            float aimZ = UnityEngine.Random.Range(-W / 2 * 0.8f, W / 2 * 0.8f);
            float tx = sign * UnityEngine.Random.Range(0.15f, 0.42f) * L;
            float myZ = side == Side.Player ? _playerZ : _opponentZ;
            var from = _state == State.Held ? new Vector2(-sign * (L / 2 - 0.05f), myZ) : _pos;
            var dir = new Vector2(tx, aimZ) - from;
            float power = Mathf.Lerp(0.35f, 0.75f, profile.skill) * UnityEngine.Random.Range(0.85f, 1.1f);
            float quality = UnityEngine.Random.value < (1 - profile.skill) * 0.2f ? 0.1f : 0.9f;
            Shoot(side, dir, power, quality);
        }

        void MoveAI(Side side, float dt)
        {
            float end = side == Side.Player ? -L / 2 : L / 2;
            float target = 0;
            if (_state == State.Flying && _receiver == side && Mathf.Abs(_vel.x) > 0.01f)
            {
                _aiReact -= dt;
                // Predict where the ball crosses this end.
                target = _pos.y + _vel.y * ((end - _pos.x) / _vel.x);
                if (_aiReact > 0) return;
            }
            float speed = Mathf.Lerp(0.8f, 2.4f, ProfileOf(side).skill);
            float limit = W / 2 + sideSlack;
            if (side == Side.Player) _playerZ = Mathf.Clamp(Mathf.MoveTowards(_playerZ, target, speed * dt), -limit, limit);
            else _opponentZ = Mathf.Clamp(Mathf.MoveTowards(_opponentZ, target, speed * dt), -limit, limit);
        }

        void PlaceActors()
        {
            _player.transform.position = playerSpot.position + top.forward * (_playerZ + _bodySide * playerBodyOffset);
            _player.transform.rotation = Quaternion.LookRotation(top.right);
            _opponent.transform.position = opponentSpot.position + top.forward * _opponentZ;
            _opponent.transform.rotation = Quaternion.LookRotation(-top.right);
            playerPaddle.position = World(-L / 2 - 0.2f, _playerZ, 0.2f);
            opponentPaddle.position = World(L / 2 + 0.2f, _opponentZ, 0.2f);
        }

        void Point(Side winner, string message)
        {
            if (winner == Side.Player) PlayerScore++;
            else OpponentScore++;
            Message?.Invoke(message);

            if (PlayerScore >= _pointsToWin || OpponentScore >= _pointsToWin)
            {
                _state = State.Over;
                MatchOver?.Invoke(PlayerScore > OpponentScore);
                return;
            }
            if ((PlayerScore + OpponentScore) % 2 == 0) _server = _server == Side.Player ? Side.Opponent : Side.Player;
            _state = State.Idle;
            _waitTimer = 1.2f;
            _y = 0;
        }
    }
}
