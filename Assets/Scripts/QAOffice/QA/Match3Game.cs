using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace QAOffice
{
    // Match-3: tap a tile, then an adjacent tile, to swap. Lines of 3+ clear and the board refills.
    public class Match3Game : MiniGame
    {
        const int Cols = 7, Rows = 5, Types = 5, Hole = 99;
        static readonly string[] Shapes = { "circle", "square", "triangle", "diamond", "star" };
        static readonly Color[] Colors =
        {
            UIKit.Hex("#E5534B"), UIKit.Hex("#4F8EF7"), UIKit.Hex("#3FBF7F"), UIKit.Hex("#F2B33D"), UIKit.Hex("#B07CF2"),
        };

        readonly int[,] _grid = new int[Cols, Rows];
        readonly Image[,] _cells = new Image[Cols, Rows];
        Vector2 _origin;
        float _cell;
        Vector2Int? _selected;
        int _lockedColumn;
        float _wait;
        System.Action _next;

        protected override void Setup()
        {
            _cell = Mathf.Min(W / (Cols + 0.5f), H * 0.86f / Rows);
            _origin = new Vector2((W - _cell * Cols) / 2, (H - _cell * Rows) / 2 - H * 0.03f);
            _lockedColumn = Rng.Next(Cols);

            var board = Sprite("Board", new Color(0, 0, 0, 0.3f), new Vector2(_cell * Cols, _cell * Rows), UIKit.Rounded);
            board.type = Image.Type.Sliced;
            Place(board, _origin);

            for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows; y++)
            {
                var img = Sprite($"Tile {x},{y}", Color.white, Vector2.one * _cell * 0.8f);
                Place(img, _origin + new Vector2(x + 0.1f, y + 0.1f) * _cell);
                _cells[x, y] = img;
                int t;
                do t = Rng.Next(Types); while (MakesLine(x, y, t));
                _grid[x, y] = t;
            }
            Redraw();
            if (!HasMove()) Shuffle();
        }

        bool MakesLine(int x, int y, int t) =>
            (x >= 2 && _grid[x - 1, y] == t && _grid[x - 2, y] == t) ||
            (y >= 2 && _grid[x, y - 1] == t && _grid[x, y - 2] == t);

        protected override void Press(Vector2 pos)
        {
            if (_next != null) return;
            var c = Vector2Int.FloorToInt((pos - _origin) / _cell);
            if (c.x < 0 || c.y < 0 || c.x >= Cols || c.y >= Rows) return;
            // Controls bug: one column silently ignores taps.
            if (Spec.Has(BugType.Match3LockedColumn) && c.x == _lockedColumn) return;
            if (_grid[c.x, c.y] == Hole) return;

            if (_selected is Vector2Int s && (Mathf.Abs(s.x - c.x) + Mathf.Abs(s.y - c.y)) == 1)
            {
                _selected = null;
                Swap(s, c);
                Redraw();
                if (FindMatches().Count > 0) Later(0.15f, Resolve);
                else Later(0.25f, () => { Swap(s, c); Redraw(); });
            }
            else _selected = c;
            Redraw();
        }

        protected override void Step(float dt)
        {
            if (_next == null) return;
            _wait -= dt;
            if (_wait > 0) return;
            var n = _next;
            _next = null;
            n();
        }

        void Later(float delay, System.Action a)
        {
            _wait = delay;
            _next = a;
        }

        void Swap(Vector2Int a, Vector2Int b)
        {
            (_grid[a.x, a.y], _grid[b.x, b.y]) = (_grid[b.x, b.y], _grid[a.x, a.y]);
        }

        HashSet<Vector2Int> FindMatches()
        {
            var found = new HashSet<Vector2Int>();
            for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Cols - 2; x++)
            {
                int t = _grid[x, y];
                if (t >= 0 && t != Hole && _grid[x + 1, y] == t && _grid[x + 2, y] == t)
                    for (int k = 0; k < 3; k++) found.Add(new Vector2Int(x + k, y));
            }
            for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows - 2; y++)
            {
                int t = _grid[x, y];
                if (t >= 0 && t != Hole && _grid[x, y + 1] == t && _grid[x, y + 2] == t)
                    for (int k = 0; k < 3; k++) found.Add(new Vector2Int(x, y + k));
            }
            return found;
        }

        void Resolve()
        {
            var matches = FindMatches();
            if (matches.Count == 0)
            {
                if (!HasMove()) Shuffle();
                return;
            }
            foreach (var m in matches) _grid[m.x, m.y] = -1;
            SetScore(Score + (Spec.Has(BugType.Match3ScoreSubtracts) ? -10 : 10) * matches.Count);
            Redraw();
            Later(0.2f, () =>
            {
                Collapse();
                Redraw();
                Later(0.2f, Resolve);
            });
        }

        void Collapse()
        {
            for (int x = 0; x < Cols; x++)
            {
                int write = 0;
                for (int y = 0; y < Rows; y++)
                    if (_grid[x, y] != -1) _grid[x, write++] = _grid[x, y];
                for (int y = write; y < Rows; y++)
                    // Spawning bug: some refills come in as dead holes.
                    _grid[x, y] = Spec.Has(BugType.Match3RefillHoles) && Rng.NextDouble() < 0.25 ? Hole : Rng.Next(Types);
            }
        }

        bool HasMove()
        {
            for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows; y++)
            foreach (var d in new[] { Vector2Int.right, Vector2Int.up })
            {
                var a = new Vector2Int(x, y);
                var b = a + d;
                if (b.x >= Cols || b.y >= Rows) continue;
                Swap(a, b);
                bool ok = FindMatches().Count > 0;
                Swap(a, b);
                if (ok) return true;
            }
            return false;
        }

        void Shuffle()
        {
            for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows; y++)
                _grid[x, y] = Rng.Next(Types);
            Redraw();
            Later(0.2f, Resolve);
        }

        void Redraw()
        {
            for (int x = 0; x < Cols; x++)
            for (int y = 0; y < Rows; y++)
            {
                var img = _cells[x, y];
                int t = _grid[x, y];
                img.enabled = t != -1;
                if (t == -1) continue;
                if (t == Hole)
                {
                    img.sprite = Icons.Shape("square");
                    img.color = new Color(0, 0, 0, 0.55f);
                }
                else
                {
                    // Visual bug: tile type 4 is drawn as type 0.
                    int look = Spec.Has(BugType.Match3LookalikeTiles) && t == 4 ? 0 : t;
                    img.sprite = Icons.Shape(Shapes[look]);
                    img.color = Colors[look];
                }
                bool sel = _selected is Vector2Int s && s.x == x && s.y == y;
                img.rectTransform.localScale = Vector3.one * (sel ? 1.15f : 1f);
            }
        }
    }
}
