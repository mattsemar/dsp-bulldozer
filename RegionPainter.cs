using System;
using System.Collections.Generic;
using UnityEngine;
using static Bulldozer.Log;

namespace Bulldozer
{
    public class RegionPainter
    {
        private Dictionary<int, LatLon> _llLookup = new();
        private readonly PlatformSystem platformSystem;
        private bool _lookupsCreated;
        private float _minLatLookupWorkItem = -90f;

        public RegionPainter(PlatformSystem platformSystem)
        {
            this.platformSystem = platformSystem;
        }

        private void ComputeLookups()
        {
            DateTime start = DateTime.Now;
            int posCounter = 0;
            Debug($"Resuming init at {_minLatLookupWorkItem}");
            for (var lat = _minLatLookupWorkItem; lat < 90; lat += 0.2f)
            {
                for (var longi = -180f; longi < 180; longi += 0.2f)
                {
                    posCounter++;
                    var pos = LatLonToPosition(lat, longi, platformSystem.planet.realRadius);
                    var reformIndexForPosition = platformSystem.GetReformIndexForPosition(pos);
                    if (reformIndexForPosition > -1)
                    {
                        LatLon latLon = LatLon.FromCoords(lat, longi);
                        _llLookup[reformIndexForPosition] = latLon;
                    }
                }
            }

            Debug($"found positions for {_llLookup.Count} indices in {(DateTime.Now - start).TotalSeconds} seconds. Looked at {posCounter} positions");
            _lookupsCreated = true;
        }


        public void PaintRegions()
        {
            if (!PluginConfig.enableRegionColor.Value)
                return;
            if (RegionalColors.Instance.Count == 0)
                return;
            if (!_lookupsCreated)
                ComputeLookups();

            var actionBuild = GameMain.mainPlayer?.controller.actionBuild;
            if (actionBuild == null)
            {
                return;
            }

            RegionalColors.Instance.Save();

            // reform brush type of 7 is foundation with no decoration
            // brush type 2 is decorated, but not painted
            // 1 seems to be paint mode
            var brushType = 1;
            switch (PluginConfig.foundationDecorationMode.Value)
            {
                case FoundationDecorationMode.Tool:
                    brushType = actionBuild.reformTool.brushType;
                    break;
                case FoundationDecorationMode.Paint:
                    brushType = 1;
                    break;
                case FoundationDecorationMode.Decorate:
                    brushType = 2;
                    break;
                case FoundationDecorationMode.Clear:
                    brushType = 7;
                    break;
                default:
                    Warn($"unexpected brush type requested {PluginConfig.foundationDecorationMode.Value}");
                    break;
            }

            var reformCount = platformSystem.maxReformCount;
            for (var index = 0; index < reformCount; ++index)
            {
                if (!_llLookup.ContainsKey(index))
                {
                    Warn($"No lookup for position at index {index}");
                    continue;
                }

                var latLon = _llLookup[index];
                var regionColorConfig = RegionalColors.Instance.GetForPosition(latLon.Lat, latLon.Long);
                if (regionColorConfig == null)
                {
                    continue;
                }

                platformSystem.SetReformType(index, brushType);
                platformSystem.SetReformColor(index, regionColorConfig.colorIndex);
            }
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

        public void DoInitWork()
        {
            if (_lookupsCreated)
                return;

            DateTime start = DateTime.Now;
            int posCounter = 0;
            var endLat = Math.Min( _minLatLookupWorkItem + 4, 90);
            for (var lat = _minLatLookupWorkItem; lat < endLat; lat += 0.2f)
            {
                for (var longi = -180f; longi < 180; longi += 0.2f)
                {
                    posCounter++;
                    var pos = LatLonToPosition(_minLatLookupWorkItem, longi, platformSystem.planet.realRadius);
                    var reformIndexForPosition = platformSystem.GetReformIndexForPosition(pos);
                    if (reformIndexForPosition > -1)
                    {
                        // _positionLookups[reformIndexForPosition] = pos;
                        LatLon latLon = LatLon.FromCoords(_minLatLookupWorkItem, longi);
                        _llLookup[reformIndexForPosition] = latLon;
                    }
                }

                _minLatLookupWorkItem = lat;
            }

            if (_minLatLookupWorkItem >= 89)
            {
                Debug($" lat work done at {_minLatLookupWorkItem}");
                _lookupsCreated = true;
            }

            Debug($"found positions for {_llLookup.Count} indices in {(DateTime.Now - start).TotalSeconds} seconds. Looked at {posCounter} positions");
        }
    }

    internal class LatLon : IEquatable<LatLon>
    {
        private int lat;
        private int lng;

        public int Lat
        {
            get => lat;
            set => lat = value;
        }

        public int Long
        {
            get => lng;
            set => lng = value;
        }

        private static Dictionary<int, Dictionary<int, LatLon>> _poolLatToLonToInstance = new();

        public static LatLon FromCoords(double lat, double lon)
        {
            int newLat = lat < 0 ? Mathf.CeilToInt((float)lat) : Mathf.FloorToInt((float)lat);
            int newLon = lon < 0 ? Mathf.CeilToInt((float)lon) : Mathf.FloorToInt((float)lon);
            if (!_poolLatToLonToInstance.TryGetValue(newLat, out var lonLookup))
            {
                lonLookup = new Dictionary<int, LatLon>();
                _poolLatToLonToInstance[newLat] = lonLookup;
            }

            if (!lonLookup.TryGetValue(newLon, out var inst))
            {
                lonLookup[newLon] = inst = new LatLon { lat = newLat, lng = newLon };
            }

            return inst;
        }

        public bool Equals(LatLon other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return lat == other.lat && lng == other.lng;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((LatLon)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (lat * 397) ^ lng;
            }
        }
    }
}