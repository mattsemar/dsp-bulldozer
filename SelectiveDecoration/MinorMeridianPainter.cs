using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bulldozer.SelectiveDecoration
{
    public class MinorMeridianPainter : ISelectivePlanetDecorator
    {
        private readonly HashSet<LatLon> _minorMeridianPoints = new();

        private static DecorationConfig _minorMeridianConfig;
        private readonly ReformIndexInfoProvider infoProvider;

        public MinorMeridianPainter(ReformIndexInfoProvider reformIndexInfoProvider)
        {
            infoProvider = reformIndexInfoProvider;
            _minorMeridianConfig = new DecorationConfig(PluginConfig.guideLinesMinorMeridianColor.Value);
            InitMeridianLongitudes();
        }

        public DecorationConfig GetDecorationForLocation(LatLon location)
        {
            // This check probably isn't needed now that the lines aren't thick and misaligned, but I left the logic intact just in case.
            //if (DistanceFromMajorMeridian(location.Long) < 5)
            //    return DecorationConfig.None;

            if (_minorMeridianPoints.Contains(location))
                return _minorMeridianConfig;

            return DecorationConfig.None;
        }

        private int DistanceFromMajorMeridian(float lon)
        {
            return lon switch
            {
                >= -5 and <= 5 => (int)Mathf.Abs(lon),
                >= 85 and <= 95 => (int)Mathf.Abs(90 - lon),
                >= 175 => (int)Mathf.Abs(180 - lon),
                <= -85 and >= -95 => (int)Mathf.Abs(-90 - lon),
                _ => (int)Mathf.Abs(-180 - lon)
            };
        }

        private void InitMeridianLongitudes()
        {
            var interval = PluginConfig.minorMeridianInterval.Value;
            var segment = infoProvider.platformSystem.segment;
            var precision = ReformIndexInfoProvider.latLonPrecision;
            var latitudeCount = infoProvider.platformSystem.latitudeCount;
            var latDegIncrement = (90 * 2.0f) / latitudeCount;

            for (float lat = latDegIncrement / 2; lat <= SelectiveDecorationBuilder.POLE_LATITUDE_START; lat += latDegIncrement)
			{
                var latSegment = Mathf.FloorToInt(lat * latitudeCount / 180f) / 5;
                var lonDivisions = PlatformSystem.DetermineLongitudeSegmentCount(latSegment, segment) * 5;
                var lonStep = 360f / lonDivisions;

                // arbitrary value of just over half a tile so that if the meridian is right between two tiles it gets both
                // this could be used to adjust thickness of the line and might be better as a config option in the future
                var proximityThreshold = lonStep * 0.501f; 

                for (float meridianLon = 0f; meridianLon <= 180f; meridianLon += interval)
                {
                    var minLon = (Mathf.Round((meridianLon - proximityThreshold) / lonStep) + 0.5) * lonStep;
                    var maxLon = (Mathf.Round((meridianLon + proximityThreshold) / lonStep) - 0.5) * lonStep;

                    for(var tileLon = minLon; tileLon <= maxLon; tileLon += lonStep)
					{
                        _minorMeridianPoints.Add(LatLon.FromCoords(lat, tileLon, precision));
                        _minorMeridianPoints.Add(LatLon.FromCoords(-lat, tileLon, precision));
                        _minorMeridianPoints.Add(LatLon.FromCoords(lat, -tileLon, precision));
                        _minorMeridianPoints.Add(LatLon.FromCoords(-lat, -tileLon, precision));
                    }
                }
            }

            Log.Debug($"Marked {_minorMeridianPoints.Count} tiles as minor meridians. Average thickness: {_minorMeridianPoints.Count / Mathf.Floor(360f / interval) / latitudeCount}");
        }

        public string ActionSummary()
        {
            return $"Minor Meridians (interval = {PluginConfig.minorMeridianInterval.Value}°)";
        }
    }
}