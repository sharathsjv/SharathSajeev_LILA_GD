using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QAOffice
{
    public enum BugCategory { None, Collision, Controls, Scoring, Visual, Spawning }

    public enum BugType
    {
        // Runner
        RunnerNoCollision, RunnerDroppedJumps, RunnerCoinsSubtract, RunnerPlayerFlicker, RunnerPopInObstacles,
        // Match-3
        Match3LockedColumn, Match3ScoreSubtracts, Match3LookalikeTiles, Match3RefillHoles,
    }

    public static class BugCatalog
    {
        public static readonly Dictionary<string, BugType[]> PoolByGenre = new Dictionary<string, BugType[]>
        {
            { "runner", new[] { BugType.RunnerNoCollision, BugType.RunnerDroppedJumps, BugType.RunnerCoinsSubtract, BugType.RunnerPlayerFlicker, BugType.RunnerPopInObstacles } },
            { "match3", new[] { BugType.Match3LockedColumn, BugType.Match3ScoreSubtracts, BugType.Match3LookalikeTiles, BugType.Match3RefillHoles } },
        };

        public static BugCategory Category(BugType b)
        {
            switch (b)
            {
                case BugType.RunnerNoCollision: return BugCategory.Collision;
                case BugType.RunnerDroppedJumps:
                case BugType.Match3LockedColumn: return BugCategory.Controls;
                case BugType.RunnerCoinsSubtract:
                case BugType.Match3ScoreSubtracts: return BugCategory.Scoring;
                case BugType.RunnerPlayerFlicker:
                case BugType.Match3LookalikeTiles: return BugCategory.Visual;
                default: return BugCategory.Spawning;
            }
        }

        public static string Describe(BugType b)
        {
            switch (b)
            {
                case BugType.RunnerNoCollision: return "Obstacles had no collision - the runner passed straight through.";
                case BugType.RunnerDroppedJumps: return "Some jump inputs were silently dropped.";
                case BugType.RunnerCoinsSubtract: return "Collecting coins lowered the score.";
                case BugType.RunnerPlayerFlicker: return "The runner sprite flickered out of view.";
                case BugType.RunnerPopInObstacles: return "Obstacles popped in mid-screen instead of scrolling in.";
                case BugType.Match3LockedColumn: return "One column of tiles ignored input.";
                case BugType.Match3ScoreSubtracts: return "Making matches lowered the score.";
                case BugType.Match3LookalikeTiles: return "Two different tile types were drawn identically.";
                case BugType.Match3RefillHoles: return "The board refilled with empty holes.";
            }
            return b.ToString();
        }
    }

    // One procedurally generated build handed to the player to test.
    public class BuildSpec
    {
        public string Genre;
        public string Title;
        public int Seed;
        public List<BugType> Bugs = new List<BugType>();
        public Color Background, PlayerColor, HazardColor, PickupColor;
        public float Speed;      // genre-specific tuning, 0..1 range mapped by the game
        public float Gravity;
        public float Density;

        static readonly string[] TitleA = { "Super", "Mega", "Turbo", "Hyper", "Pixel", "Neon", "Ultra", "Tiny", "Cosmic", "Retro" };
        static readonly string[] TitleB = { "Dash", "Blast", "Gems", "Jumper", "Crush", "Quest", "Rush", "Pop", "Hop", "Drop" };
        static readonly string[] TitleC = { "Deluxe", "2", "Remix", "HD", "Legends", "Saga", "Zero", "Plus", "Origins", "Go" };

        public static BuildSpec Generate(string genre, int seed)
        {
            var rng = new System.Random(seed);
            if (genre == "random" || !BugCatalog.PoolByGenre.ContainsKey(genre))
                genre = BugCatalog.PoolByGenre.Keys.ElementAt(rng.Next(BugCatalog.PoolByGenre.Count));

            var spec = new BuildSpec { Genre = genre, Seed = seed };
            spec.Title = $"{TitleA[rng.Next(TitleA.Length)]} {TitleB[rng.Next(TitleB.Length)]} {TitleC[rng.Next(TitleC.Length)]}";

            float hue = (float)rng.NextDouble();
            spec.Background = Color.HSVToRGB(hue, 0.35f, 0.25f);
            spec.PlayerColor = Color.HSVToRGB((hue + 0.5f) % 1, 0.6f, 0.95f);
            spec.HazardColor = Color.HSVToRGB((hue + 0.15f) % 1, 0.75f, 0.9f);
            spec.PickupColor = new Color(1f, 0.85f, 0.2f);
            spec.Speed = (float)rng.NextDouble();
            spec.Gravity = (float)rng.NextDouble();
            spec.Density = (float)rng.NextDouble();

            // 0 bugs 20%, 1 bug 35%, 2 bugs 30%, 3 bugs 15%: clean builds make "Ship" a real decision.
            double roll = rng.NextDouble();
            int count = roll < 0.2 ? 0 : roll < 0.55 ? 1 : roll < 0.85 ? 2 : 3;
            var pool = BugCatalog.PoolByGenre[genre].OrderBy(_ => rng.Next()).ToList();
            spec.Bugs.AddRange(pool.Take(count));
            return spec;
        }

        public bool Has(BugType b) => Bugs.Contains(b);
    }

    public class BugFlag
    {
        public float Time;
        public BugCategory Category = BugCategory.None;
    }

    public class QAResult
    {
        public int Planted, Found, FalseReports;
        public bool Shipped, VerdictCorrect;
        public float Score; // 0..1
        public List<BugType> Missed = new List<BugType>();
        public List<BugType> Caught = new List<BugType>();

        public static QAResult Grade(BuildSpec spec, List<BugFlag> flags, bool shipped)
        {
            var r = new QAResult { Planted = spec.Bugs.Count, Shipped = shipped };
            var remaining = new List<BugType>(spec.Bugs);
            foreach (var f in flags)
            {
                if (f.Category == BugCategory.None) continue;
                int idx = remaining.FindIndex(b => BugCatalog.Category(b) == f.Category);
                if (idx >= 0)
                {
                    r.Caught.Add(remaining[idx]);
                    remaining.RemoveAt(idx);
                    r.Found++;
                }
                else r.FalseReports++;
            }
            r.Missed = remaining;
            r.VerdictCorrect = shipped == (spec.Bugs.Count == 0);
            float detection = r.Planted == 0 ? 1f : (float)r.Found / r.Planted;
            float accuracy = Mathf.Clamp01(detection - 0.2f * r.FalseReports);
            r.Score = 0.75f * accuracy + 0.25f * (r.VerdictCorrect ? 1f : 0f);
            return r;
        }
    }
}
