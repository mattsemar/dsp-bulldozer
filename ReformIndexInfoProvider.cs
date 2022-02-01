using System;
using System.Collections.Concurrent;
using static Bulldozer.Log;

namespace Bulldozer
{
    /// <summary>
    /// Provides mapping from reform indexes to lat / lan values
    /// </summary>
    public class ReformIndexInfoProvider
    {
        private const int LatitudesPerPass = 4;
        private ConcurrentDictionary<int, LatLon> _llLookup = new();
        private ConcurrentDictionary<int, LatLon> _llModLookup = new();
        private PlatformSystem platformSystem;
        private bool _lookupsCreated;
        private float _minLatLookupWorkItem = -90f;
        private int _planetId;

        public ReformIndexInfoProvider(PlatformSystem platformSystem)
        {
            this.platformSystem = platformSystem;
            _planetId = platformSystem.planet.id;
        }

        public int PlanetId => _planetId;
        public bool Initted => _lookupsCreated;

        private void SetInitValues(PlatformSystem platformSystem, int planetId)
        {
            _llLookup.Clear();
            _llModLookup.Clear();
            _lookupsCreated = false;
            _planetId = planetId;
            this.platformSystem = platformSystem;
            _minLatLookupWorkItem = -90f;
        }

        public void PlanetChanged(PlanetData planetData)
        {
            if (planetData == null)
            {
                SetInitValues(null, -1);
            }
            else
            {
                if (_planetId != platformSystem.planet.id)
                {
                    SetInitValues(planetData.factory?.platformSystem, planetData.id);
                }
                else
                {
                    Debug("planet did not actually change");
                }
            }
        }

        public LatLon GetForIndex(int index)
        {
            if (!_lookupsCreated)
            {
                return null;
            }

            _llLookup.TryGetValue(index, out var result);
            return result;
        }

        public LatLon GetForModIndex(int index)
        {
            if (!_lookupsCreated)
            {
                return null;
            }

            _llModLookup.TryGetValue(index, out var result);
            return result;
        }

        public void DoInitWork(PlanetData planetData)
        {
            if (planetData == null)
            {
                return;
            }

            if (planetData.id != _planetId)
            {
                PlanetChanged(planetData);
            }

            if (_lookupsCreated)
                return;
            if (platformSystem == null)
            {
                Warn($"plat system null for planet {planetData.id}");
                return;
            }

            int posCounter = 0;
            var endLat = Math.Min(_minLatLookupWorkItem + LatitudesPerPass, 90);
            var planetRawData = platformSystem.planet.data;
            for (var lat = _minLatLookupWorkItem; lat < endLat; lat += 0.2f)
            {
                for (var longi = -180f; longi < 180; longi += 0.2f)
                {
                    posCounter++;
                    var pos = GeoUtil.LatLonToPosition(_minLatLookupWorkItem, longi, platformSystem.planet.realRadius);
                    var reformIndexForPosition = platformSystem.GetReformIndexForPosition(pos);
                    LatLon latLon = LatLon.FromCoords(_minLatLookupWorkItem, longi);
                    if (reformIndexForPosition > -1)
                    {
                        _llLookup[reformIndexForPosition] = latLon;
                    }

                    var currentDataIndex = planetRawData.QueryIndex(pos);
                    _llModLookup[currentDataIndex] = latLon;

                    // _llModLookup[modIndex] = latLon;
                }

                _minLatLookupWorkItem = lat;
            }

            if (_minLatLookupWorkItem >= 89)
            {
                _lookupsCreated = true;
            }

            if (_lookupsCreated)
                Debug($"found positions for {_llLookup.Count} indices. Looked at {posCounter} positions on last pass {_lookupsCreated} {Initted}");
        }
    }
}