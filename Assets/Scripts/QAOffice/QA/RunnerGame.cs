using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // Endless runner: tap to jump over obstacles, grab coins.
    public class RunnerGame : MiniGame
    {
        class Thing
        {
            public Image Img;
            public Vector2 Pos, Size;
            public bool Coin;
        }

        readonly List<Thing> _things = new List<Thing>();
        Image _player, _ground;
        Text _banner;
        Vector2 _playerSize;
        float _px, _py, _vy, _groundY;
        float _speed, _gravity, _jumpV;
        float _spawnTimer, _crashTimer, _distance;

        protected override void Setup()
        {
            _groundY = H * 0.18f;
            _playerSize = Vector2.one * H * 0.11f;
            _px = W * 0.15f;
            _py = _groundY;
            _speed = W * (0.42f + 0.3f * Spec.Speed);
            _gravity = H * (3.4f + 1.6f * Spec.Gravity);
            _jumpV = Mathf.Sqrt(2 * _gravity * H * 0.36f);

            _ground = Sprite("Ground", Spec.Background * 1.8f, new Vector2(W, _groundY));
            _player = Sprite("Player", Spec.PlayerColor, _playerSize, Icons.Shape("square"));
            _banner = UIKit.Label(Area, "", 44, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            _banner.rectTransform.Stretch();
            _spawnTimer = 1f;
        }

        protected override void Press(Vector2 pos)
        {
            if (_crashTimer > 0 || _py > _groundY + 1f) return;
            if (Spec.Has(BugType.RunnerDroppedJumps) && Rng.NextDouble() < 0.45) return;
            _vy = _jumpV;
        }

        protected override void Step(float dt)
        {
            if (_crashTimer > 0)
            {
                _crashTimer -= dt;
                if (_crashTimer <= 0) _banner.text = "";
                return;
            }

            _vy -= _gravity * dt;
            _py += _vy * dt;
            if (_py <= _groundY) { _py = _groundY; _vy = 0; }
            Place(_player, new Vector2(_px, _py));

            // Visual bug: the runner blinks out for a moment every couple of seconds.
            if (Spec.Has(BugType.RunnerPlayerFlicker)) _player.enabled = Elapsed % 2.3f > 0.4f;

            _distance += _speed * dt;
            if (Mathf.FloorToInt(_distance / W) > 0)
            {
                _distance -= W;
                SetScore(Score + 1);
            }

            _spawnTimer -= dt;
            if (_spawnTimer <= 0)
            {
                Spawn();
                _spawnTimer = Rand(0.9f, 1.7f) - 0.45f * Spec.Density;
            }

            var playerRect = new Rect(_px, _py, _playerSize.x, _playerSize.y);
            for (int i = _things.Count - 1; i >= 0; i--)
            {
                var t = _things[i];
                t.Pos.x -= _speed * dt;
                Place(t.Img, t.Pos);
                if (t.Pos.x < -t.Size.x)
                {
                    Remove(i);
                    continue;
                }
                if (!playerRect.Overlaps(new Rect(t.Pos, t.Size))) continue;
                if (t.Coin)
                {
                    SetScore(Score + (Spec.Has(BugType.RunnerCoinsSubtract) ? -10 : 10));
                    Remove(i);
                }
                else if (!Spec.Has(BugType.RunnerNoCollision))
                {
                    Crash();
                    return;
                }
            }
        }

        void Spawn()
        {
            float x = W;
            // Spawning bug: some obstacles appear out of thin air mid-screen.
            if (Spec.Has(BugType.RunnerPopInObstacles) && Rng.NextDouble() < 0.35) x = W * Rand(0.45f, 0.6f);

            var size = new Vector2(_playerSize.x * Rand(0.6f, 0.9f), _playerSize.y * Rand(0.7f, 1.5f));
            Add(new Vector2(x, _groundY), size, false, Spec.HazardColor);

            if (Rng.NextDouble() < 0.6)
            {
                var coinSize = _playerSize * 0.5f;
                float cx = x + Rand(W * 0.12f, W * 0.22f);
                Add(new Vector2(cx, _groundY + _playerSize.y * Rand(0.3f, 2.4f)), coinSize, true, Spec.PickupColor);
            }
        }

        void Add(Vector2 pos, Vector2 size, bool coin, Color color)
        {
            var img = Sprite(coin ? "Coin" : "Obstacle", color, size, Icons.Shape(coin ? "circle" : "square"));
            Place(img, pos);
            _things.Add(new Thing { Img = img, Pos = pos, Size = size, Coin = coin });
        }

        void Remove(int i)
        {
            Destroy(_things[i].Img.gameObject);
            _things.RemoveAt(i);
        }

        void Crash()
        {
            for (int i = _things.Count - 1; i >= 0; i--) Remove(i);
            _banner.text = "CRASH!";
            _crashTimer = 1f;
            _vy = 0;
            _py = _groundY;
            SetScore(0);
        }
    }
}
