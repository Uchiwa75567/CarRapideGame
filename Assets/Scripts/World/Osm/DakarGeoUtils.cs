using UnityEngine;

namespace CarRapide.World.Osm
{
    public static class DakarGeoUtils
    {
        public const double OriginLatitude = 14.6825;
        public const double OriginLongitude = -17.4425;

        private const double MetersPerDegreeLatitude = 111_320.0;

        public static Vector3 ToWorld(double latitude, double longitude, float y = 0f)
        {
            double metersPerDegreeLongitude = MetersPerDegreeLatitude *
                                              System.Math.Cos(OriginLatitude * System.Math.PI / 180.0);

            float x = (float)((longitude - OriginLongitude) * metersPerDegreeLongitude);
            float z = (float)((latitude - OriginLatitude) * MetersPerDegreeLatitude);

            return new Vector3(x, y, z);
        }
    }
}
