using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace Bulldozer
{
    [Flags]
    public enum GuideMarkTypes
    {
        None = 0,
        Equator = 1,
        Meridian = 2,
        Tropic = 4,
        Segment = 8
    }

    public class GuideMarker
    {
        public static ManualLogSource logger;
        private static readonly float[] TropicLats = { 86f + 11f / 60f, 84.5f, 82.5f, 79.1f, 75.25f, 70.0f + 11.0f / 60f, 64.75f, 55f + 29f / 60f, 46.75f, 28.75f };

        public static void AddGuideMarks(PlatformSystem platformSystem, GuideMarkTypes types)
        {
            if (types == GuideMarkTypes.None)
            {
                return;
            }

            if ((types & GuideMarkTypes.Tropic) == GuideMarkTypes.Tropic)
            {
                try
                {
                    PaintTropics(platformSystem);
                }
                catch (Exception e)
                {
                    logger.LogWarning($"failed to paint tropics {e.Message}");
                    logger.LogWarning(e.StackTrace);
                }
            }

            if ((types & GuideMarkTypes.Tropic) == GuideMarkTypes.Tropic)
            {
                PaintTropics(platformSystem);
            }

            if ((types & GuideMarkTypes.Meridian) == GuideMarkTypes.Meridian)
            {
                PaintMeridians(platformSystem);
            }

            if ((types & GuideMarkTypes.Equator) == GuideMarkTypes.Equator)
            {
                PaintEquator(platformSystem);
            }
        }

        private static void PaintEquator(PlatformSystem platformSystem)
        {
            // equator stripe
            var coordLineOffset = GetCoordLineOffset(platformSystem.planet);
            List<int> indexes = new List<int>();
            for (var lon = -179.9f; lon < 180; lon += 0.25f)
            {
                for (var latOffset = -1; latOffset < 1; latOffset++)
                {
                    var position = LatLonToPosition(0f + latOffset * coordLineOffset, lon, platformSystem.planet.radius);

                    var reformIndexForPosition = platformSystem.GetReformIndexForPosition(position);
                    if (reformIndexForPosition >= platformSystem.reformData.Length || reformIndexForPosition < 0)
                    {
                        logger.LogWarning($"reformIndex = {reformIndexForPosition} is out of bounds, apparently");
                        continue;
                    }

                    indexes.Add(reformIndexForPosition);
                }
            }

            indexes.Sort();
            InterpolateMissingIndexes(indexes);
            foreach (var equatorIndex in indexes)
            {
                var actual = Math.Max(0, equatorIndex);
                try
                {
                    platformSystem.SetReformType(actual, 1);
                    platformSystem.SetReformColor(actual, 7);
                }
                catch (Exception e)
                {
                    logger.LogWarning($"exception while setting reform at index {equatorIndex} max={platformSystem.reformData.Length} {e.Message}");
                }
            }
        }

        private static void PaintMeridians(PlatformSystem platformSystem)
        {
            var planetRadius = platformSystem.planet.radius;
            var coordLineOffset = GetCoordLineOffset(platformSystem.planet);

            var indexesToPaint = new List<int>();
            for (var lat = -90.0f; lat < 90; lat += coordLineOffset)
            {
                for (var meridianOffset = 0; meridianOffset < 4; meridianOffset++)
                {
                    var lonOffsetMin = -1;
                    // this is all to handle a bug where the 4th meridian line would be too skinny near the poles
                    if (Math.Abs(lat) > TropicLats[5])
                    {
                        lonOffsetMin = -3;
                    }
                    
                    if (Math.Abs(lat) > TropicLats[5])
                    {
                        lonOffsetMin = -3;
                    }
                    if (Math.Abs(lat) > TropicLats[2])
                    {
                        lonOffsetMin -= 2;
                    }
                    if (Math.Abs(lat) > TropicLats[1])
                    {
                        lonOffsetMin -= 5;
                    }
                    var lonOffsetMax = 2;

                    HashSet<int> actualIndexes = new HashSet<int>();
                    for (var lonOffset = lonOffsetMin; lonOffset < lonOffsetMax; lonOffset++)
                    {
                        var lon = coordLineOffset * lonOffset + meridianOffset * 90f;
                        var position = LatLonToPosition(lat, lon, planetRadius);

                        var reformIndexForPosition = platformSystem.GetReformIndexForPosition(position);
                        indexesToPaint.Add(reformIndexForPosition);
                        actualIndexes.Add(reformIndexForPosition);
                    }
                }
            }

            indexesToPaint.Sort();
            InterpolateMissingIndexes(indexesToPaint);
            foreach (var meridianIndex in indexesToPaint)
            {
                var actualIndex = Math.Max(0, meridianIndex);
                try
                {
                    platformSystem.SetReformType(actualIndex, 1);
                    platformSystem.SetReformColor(actualIndex, 12);
                }
                catch (Exception e)
                {
                    logger.LogWarning($"exception while setting reform at index {actualIndex} max={platformSystem.reformData.Length} {e.Message}");
                }
            }
        }

        private static void PaintTropics(PlatformSystem platformSystem)
        {

            var signs = new[] { 1, -1 };
            var indexes = new List<int>();
            foreach (var latInDegrees in TropicLats)
            {
                foreach (var sign in signs)
                {
                    for (var lon = -179.9f; lon < 180; lon += 0.25f)
                    {
                        var position = LatLonToPosition(latInDegrees * sign, lon, platformSystem.planet.radius);

                        var reformIndexForSegment = platformSystem.GetReformIndexForPosition(position);
                        if (reformIndexForSegment < 0)
                            continue;
                        indexes.Add(reformIndexForSegment);
                    }
                }
            }

            InterpolateMissingIndexes(indexes);
            foreach (var ndx in indexes)
            {
                platformSystem.SetReformType(ndx, 1);
                platformSystem.SetReformColor(ndx, 2);
            }
        }

        public static float GetCoordLineOffset(PlanetData planet, float baseRate = 0.25f)
        {
            var result = baseRate;
            if (planet.radius > 201)
            {
                // so if we step by 0.25 for a 200 radius planet, step by 0.125 for a 400 radius planet
                result /= (planet.radius / 200f);
            }

            return result;
        }

        public static void InterpolateMissingIndexes(List<int> indexes)
        {
            var tempNewIndexes = new List<int>();
            for (int i = 1; i < indexes.Count; i++)
            {
                var curIndex = indexes[i];
                var prevIndex = indexes[i - 1];
                if (prevIndex != curIndex - 1 && curIndex - prevIndex < 8)
                {
                    // fill in
                    for (int j = prevIndex + 1; j < curIndex; j++)
                    {
                        tempNewIndexes.Add(j);
                    }
                }
            }

            indexes.AddRange(tempNewIndexes);
        }

        public static Vector3 LatLonToPosition(float lat, float lon, float earthRadius)
        {
            var latRad = Math.PI / 180 * lat;
            var lonRad = Math.PI / 180 * lon;
            var x = (float)(Math.Cos(latRad) * Math.Cos(lonRad));
            var z = (float)(Math.Cos(latRad) * Math.Sin(lonRad));
            var y = (float)Math.Sin(latRad);
            return new Vector3(x, y, z).normalized * earthRadius;
        }
    }
}