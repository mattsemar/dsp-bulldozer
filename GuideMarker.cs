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
        MinorMeridian = 8,
        Pole = 16
    }

    public class GuideMarker
    {
        public static ManualLogSource logger;

        private static readonly float[] TropicLats = { 86f + 11f / 60f, 84.5f, 82.5f, 79.1f, 75.25f, 70.0f + 11.0f / 60f, 64.75f, 55f + 29f / 60f, 46.75f, 28.75f };

        // Map GuideMarkType to PluginConfigVariable in a switch statement. For whatever reason having a staticly initted Dictionary resulted in null refs.
        // That might be because of the order that things get instantiated 
        private static int GetCustomColorIndex(GuideMarkTypes guideMarkType)
        {
            switch (guideMarkType)
            {
                case GuideMarkTypes.Equator: return PluginConfig.guideLinesEquatorColor.Value;
                case GuideMarkTypes.Meridian: return PluginConfig.guideLinesMeridianColor.Value;
                case GuideMarkTypes.MinorMeridian: return PluginConfig.guideLinesMinorMeridianColor.Value;
                case GuideMarkTypes.Tropic: return PluginConfig.guideLinesTropicColor.Value;
                case GuideMarkTypes.Pole: return PluginConfig.guideLinesPoleColor.Value;
                default:
                    Log.logger.LogWarning($"no defined plugin config to get custom color for GuideMarkType {guideMarkType}");
                    return 7;
            }
        }
        
        public static void AddGuideMarks(PlatformSystem platformSystem, GuideMarkTypes types)
        {
            if (types == GuideMarkTypes.None)
            {
                return;
            }

            if ((types & GuideMarkTypes.Tropic) == GuideMarkTypes.Tropic)
            {
                PaintTropics(platformSystem);
            }

            if ((types & GuideMarkTypes.MinorMeridian) == GuideMarkTypes.MinorMeridian)
            {
                PaintMinorMeridians(platformSystem);
            }

            if ((types & GuideMarkTypes.Meridian) == GuideMarkTypes.Meridian)
            {
                PaintMeridians(platformSystem);
            }

            if ((types & GuideMarkTypes.Equator) == GuideMarkTypes.Equator)
            {
                PaintEquator(platformSystem);
            }

            if ((types & GuideMarkTypes.Pole) == GuideMarkTypes.Pole)
            {
                PaintPoles(platformSystem);
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
            var colorIndex = GetCustomColorIndex(GuideMarkTypes.Equator);

            Console.WriteLine($"custom color index for equator {colorIndex}");
            foreach (var equatorIndex in indexes)
            {
                var actual = Math.Max(0, equatorIndex);
                try
                {
                    platformSystem.SetReformType(actual, 1);
                    platformSystem.SetReformColor(actual, colorIndex);
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
            var tropicLatitudes = Math.Abs(platformSystem.planet.radius - 200f) < 0.01f ? TropicLats : GetTropicLatitudes(platformSystem);
            for (var lat = -90.0f; lat < 90; lat += coordLineOffset)
            {
                for (var meridianOffset = 0; meridianOffset < 4; meridianOffset++)
                {
                    var lonOffsetMin = -1;
                    // this is all to handle a bug where the 4th meridian line would be too skinny near the poles
                    if (Math.Abs(lat) > tropicLatitudes[5])
                    {
                        lonOffsetMin = -3;
                    }

                    if (Math.Abs(lat) > tropicLatitudes[4])
                    {
                        lonOffsetMin = -3;
                    }

                    if (Math.Abs(lat) > tropicLatitudes[2])
                    {
                        lonOffsetMin -= 2;
                    }

                    if (Math.Abs(lat) > tropicLatitudes[1])
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
            var customColor = GetCustomColorIndex(GuideMarkTypes.Meridian);
            foreach (var meridianIndex in indexesToPaint)
            {
                var actualIndex = Math.Max(0, meridianIndex);
                try
                {
                    platformSystem.SetReformType(actualIndex, 1);
                    platformSystem.SetReformColor(actualIndex, customColor);
                }
                catch (Exception e)
                {
                    logger.LogWarning($"exception while setting reform at index {actualIndex} max={platformSystem.reformData.Length} {e.Message}");
                }
            }
        }

        private static void PaintMinorMeridians(PlatformSystem platformSystem)
        {
            var planetRadius = platformSystem.planet.radius;
            var coordLineOffset = GetCoordLineOffset(platformSystem.planet);

            var indexesToPaint = new List<int>();
            var interval = PluginConfig.minorMeridianInterval.Value;
            var tropicLatitudes = Math.Abs(platformSystem.planet.radius - 200f) < 0.01f ? TropicLats : GetTropicLatitudes(platformSystem);
            for (var lat = -90.0f; lat < 90; lat += coordLineOffset)
            {
                if (Math.Abs(lat) > Math.Abs(tropicLatitudes[3]))
                    continue;
                for (var meridianOffset = -180; meridianOffset < 180; meridianOffset += interval)
                {
                    var lonOffsetMin = -1;

                    var lonOffsetMax = 2;

                    HashSet<int> actualIndexes = new HashSet<int>();
                    for (var lonOffset = lonOffsetMin; lonOffset < lonOffsetMax; lonOffset++)
                    {
                        var lon = coordLineOffset * lonOffset + meridianOffset;
                        var position = LatLonToPosition(lat, lon, planetRadius);

                        var reformIndexForPosition = platformSystem.GetReformIndexForPosition(position);

                        indexesToPaint.Add(reformIndexForPosition);
                        actualIndexes.Add(reformIndexForPosition);
                    }
                }
            }

            indexesToPaint.Sort();
            InterpolateMissingIndexes(indexesToPaint);
            var customColor = GetCustomColorIndex(GuideMarkTypes.MinorMeridian);
            foreach (var meridianIndex in indexesToPaint)
            {
                var actualIndex = Math.Max(0, meridianIndex);
                try
                {
                    platformSystem.SetReformType(actualIndex, 1);
                    platformSystem.SetReformColor(actualIndex, customColor);
                }
                catch (Exception e)
                {
                    logger.LogWarning($"exception while setting reform at index {actualIndex} max={platformSystem.reformData.Length} {e.Message}");
                }
            }
        }

        private static void PaintTropics(PlatformSystem platformSystem)
        {
            var latitudes = Math.Abs(platformSystem.planet.radius - 200f) < 0.01f ? TropicLats : GetTropicLatitudes(platformSystem);
            var signs = new[] { 1, -1 };
            var indexes = new List<int>();
            var coordLineOffset = GetCoordLineOffset(platformSystem.planet);
            foreach (var latInDegrees in latitudes)
            {
                foreach (var sign in signs)
                {
                    for (var lon = -179.9f; lon < 180; lon += coordLineOffset)
                    {
                        var position = LatLonToPosition(latInDegrees * sign, lon, platformSystem.planet.radius);

                        var reformIndexForSegment = platformSystem.GetReformIndexForPosition(position);
                        if (reformIndexForSegment >= 0)
                        {
                            indexes.Add(reformIndexForSegment);
                        }
                    }
                }
            }

            foreach (var ndx in indexes)
            {
                platformSystem.SetReformType(ndx, 1);
                platformSystem.SetReformColor(ndx, GetCustomColorIndex(GuideMarkTypes.Tropic));
            }
        }

        private static void PaintPoles(PlatformSystem platformSystem)
        {
            var tropicLatitudes = Math.Abs(platformSystem.planet.radius - 200f) < 0.01f ? TropicLats : GetTropicLatitudes(platformSystem);
            // poles will be anything in the first two tropics
            var indexes = new List<int>();
            var coordLineOffset = GetCoordLineOffset(platformSystem.planet);
            for (var lat = -90.0f; lat < 90; lat += coordLineOffset)
            {
                if (Math.Abs(lat) <= Math.Abs(tropicLatitudes[0]) + coordLineOffset)
                    continue;
                for (var lon = -179.9f; lon < 180; lon += coordLineOffset)
                {
                    var position = LatLonToPosition(lat, lon, platformSystem.planet.radius);

                    var reformIndexForSegment = platformSystem.GetReformIndexForPosition(position);
                    if (reformIndexForSegment >= 0)
                    {
                        indexes.Add(reformIndexForSegment);
                    }
                }
            }

            var color = GetCustomColorIndex(GuideMarkTypes.Pole);
            foreach (var ndx in indexes)
            {
                platformSystem.SetReformType(ndx, 1);
                platformSystem.SetReformColor(ndx, color);
            }
        }

        private static float[] GetTropicLatitudes(PlatformSystem platformSystem)
        {
            var lastLen = 0;
            var result = new List<float>();
            var coordLineOffset = GetCoordLineOffset(platformSystem.planet);
            for (var lat = -89.9f; lat < 90; lat += coordLineOffset)
            {
                var position = LatLonToPosition(lat, 0, platformSystem.planet.radius);
                position.Normalize();

                double latitude = Mathf.Asin(position.y);
                var latitudeIndex = (float)(latitude / 6.28318548202515) * platformSystem.segment;
                var longitudeSegmentCount = PlatformSystem.DetermineLongitudeSegmentCount(Mathf.FloorToInt(Mathf.Abs(latitudeIndex)), platformSystem.segment);
                if (lastLen != longitudeSegmentCount)
                {
                    logger.LogDebug($"at lat = {lat} length is {longitudeSegmentCount}, previous len was {lastLen}");
                    result.Add(lat);
                }

                lastLen = longitudeSegmentCount;
            }

            return result.ToArray();
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