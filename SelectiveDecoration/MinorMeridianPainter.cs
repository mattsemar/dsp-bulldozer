using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bulldozer.SelectiveDecoration
{
    public class MinorMeridianPainter : ISelectivePlanetDecorator
    {
        private readonly HashSet<int> _minorMeridianLongitudes = new();

        private static DecorationConfig _minorMeridianConfig;


        public MinorMeridianPainter()
        {
            _minorMeridianConfig = new DecorationConfig(PluginConfig.guideLinesMinorMeridianColor.Value);
            InitMeridianLongitudes();
        }

        private LatLon _lastRequest = LatLon.Empty;
        private bool _lastResult;

        public DecorationConfig GetDecorationForLocation(LatLon location)
        {
            if (Math.Abs(location.Lat) > SelectiveDecorationBuilder.POLE_LATITUDE_START)
                return DecorationConfig.None;
            if (DistanceFromMajorMeridian(location.Long) < 5)
                return DecorationConfig.None;
            if (location.Long < 0)
            {
                if (_minorMeridianLongitudes.Contains(Mathf.CeilToInt(location.Long)))
                {
                    _lastRequest = location;
                    _lastResult = true;
                    return _minorMeridianConfig;
                }
            }
            else if (_minorMeridianLongitudes.Contains(Mathf.FloorToInt(location.Long)))
            {
                _lastRequest = location;
                _lastResult = true;
                return _minorMeridianConfig;
            }

            // before returning false see if there's a point in between the last request and this one that would work
            if (_lastRequest.RawLat() == location.RawLat() && !_lastResult)
            {
                var delta = Mathf.Abs(_lastRequest.Long - location.Long) / 3.0f;
                for (int j = 1; j <= 3; j++)
                {
                    var inBetween = Mathf.MoveTowards(_lastRequest.Long, location.Long, delta * j);

                    if (_minorMeridianLongitudes.Contains((int)inBetween))
                    {
                        _lastRequest = location;
                        _lastResult = true;
                        return _minorMeridianConfig;
                    }
                }
            }

            _lastRequest = location;
            _lastResult = false;

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
            for (int i = 0; i < 180; i += interval)
            {
                _minorMeridianLongitudes.Add(i);
            }


            for (int i = 0 - interval; i > -180; i -= interval)
            {
                _minorMeridianLongitudes.Add(i);
            }

            var meridianVals = string.Join(",", _minorMeridianLongitudes);
            Log.Debug($"minor meridians for {interval} are {meridianVals}");
        }

        public string ActionSummary()
        {
            return $"Minor Meridians (interval = {PluginConfig.minorMeridianInterval.Value}°)";
        }
    }
}