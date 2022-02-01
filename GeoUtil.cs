using UnityEngine;

namespace Bulldozer
{
    public static class GeoUtil
    {
        public static int GetLatitudeDegForPosition(Vector3 pos)
        {
            Maths.GetLatitudeLongitude(pos, out int latd, out _, out _, out _, out _, out bool south, out _, out _);
            if (south)
                return -latd;
            return latd;
        }

        public static Vector3 LatLonToPosition(float lat, float lon, float earthRadius)
        {
            var latRad = Mathf.PI / 180 * lat;
            var lonRad = Mathf.PI / 180 * lon;
            var y = Mathf.Sin(latRad);
            var num5 = Mathf.Cos(latRad);
            var num6 = Mathf.Sin(lonRad);
            var num7 = Mathf.Cos(lonRad);
            return new Vector3(num5 * num6, y, num5 * -num7).normalized * earthRadius;
        }
    }
}