using System.Collections.Generic;
using UnityEngine;

namespace Hitman
{
    // A public telephone (in cafes and on the street). Some have a Yellow Pages hanging under them - which ones is
    // rolled when the game starts. From any of them you can ring numbers you've found.
    [RequireComponent(typeof(Usable))]
    public class Payphone : MonoBehaviour
    {
        [Tooltip("Chance this phone has a Yellow Pages.")]
        [SerializeField, Range(0, 1)] float yellowPagesChance = 0.6f;

        static int _seed = -1;
        bool? _hasBook;

        public bool HasYellowPages
        {
            get
            {
                if (_hasBook == null)
                {
                    if (_seed < 0) _seed = Random.Range(0, 100000);
                    var p = transform.position;
                    var rng = new System.Random(_seed ^ Mathf.RoundToInt(p.x * 13 + p.z * 7919));
                    _hasBook = rng.NextDouble() < yellowPagesChance;
                }
                return _hasBook.Value;
            }
        }

        public class Listing
        {
            public string name, address, phone;
            public bool target;
        }

        static List<Listing> _book;

        static readonly string[] First = { "Arthur", "Beth", "Carl", "Dolores", "Ed", "Fran", "Gus", "Hal", "Irene", "Jack", "Lou", "Marge", "Ned", "Opal", "Pete", "Rita", "Stan", "Vera", "Walt" };
        static readonly string[] Last = { "Abbott", "Barnes", "Kowalski", "Kramer", "Malloy", "Marsh", "Martin", "Mason", "Miller", "Moretti", "Murphy", "Nolan", "Price", "Quinn", "Reyes", "Ruiz", "Shaw", "Walsh" };
        static readonly string[] Streets = { "Shackleton Ave", "Kensington", "Ridge Rd", "Elm St", "Harbor Blvd", "4th St" };

        // The directory, made up once per game; the target is always in it.
        public static List<Listing> Book(Routine target)
        {
            if (_book != null) return _book;
            var rng = new System.Random(_seed < 0 ? 1 : _seed);
            _book = new List<Listing>();
            var used = new HashSet<string>();
            while (_book.Count < 14)
            {
                var n = $"{Last[rng.Next(Last.Length)]}, {First[rng.Next(First.Length)]}";
                if (!used.Add(n)) continue;
                _book.Add(new Listing { name = n, address = $"{rng.Next(2, 980)} {Streets[rng.Next(Streets.Length)]}", phone = $"555-{rng.Next(1000, 9999)}" });
            }
            if (target != null)
            {
                var parts = target.displayName.Split(' ');
                var name = parts.Length > 1 ? $"{parts[parts.Length - 1]}, {parts[0]}" : target.displayName;
                _book.Add(new Listing { name = name, address = target.address, phone = target.phone, target = true });
            }
            _book.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return _book;
        }
    }
}
