using UnityEngine;

namespace Hitman
{
    // One of the three street maps (Shackleton Ave, Business District, Suburbs), connected by subway and taxi.
    public class District : Area
    {
        [Tooltip("Where you come out when arriving by subway.")]
        [SerializeField] Place subwayExit;
        [Tooltip("Where the taxi drops you off.")]
        [SerializeField] Place taxiStop;

        public Place SubwayExit => subwayExit;
        public Place TaxiStop => taxiStop;
        public override District Map => this;
    }
}
