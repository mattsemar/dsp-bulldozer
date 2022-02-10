using System;
using System.Collections.Generic;
using UnityEngine;
using static Bulldozer.Log;

namespace Bulldozer
{
    /// <summary>
    /// Provides mapping from reform indexes to lat / lan values
    /// </summary>
    public class ReformIndexInfoProvider
    {
        private const int LatitudesPerPass = 10;
        private readonly Dictionary<int, LatLon> _llLookup = new();
        private readonly Dictionary<int, LatLon> _llModLookup = new();
        private readonly HashSet<LatLon> _tropicsLatitudes = new();
        private readonly LatLon[] _equatorLatitudes = new LatLon[2];
        private readonly HashSet<LatLon> _meridians = new();
        public PlatformSystem platformSystem;
        private bool _lookupsCreated;
        private float _latLookupWorkItemIndex = -89.9f;
        private int _initUpdateCounter;
        private int _planetId;
        private int prevLength = -1;

        public ReformIndexInfoProvider(PlatformSystem platformSystem)
        {
            this.platformSystem = platformSystem;
            _planetId = platformSystem.planet.id;
        }

        public int PlanetId => _planetId;
        public bool Initted => _lookupsCreated;

        private void SetInitValues(PlatformSystem newPlatformSystem, int planetId)
        {
            _llLookup.Clear();
            _llModLookup.Clear();
            _tropicsLatitudes.Clear();
            _equatorLatitudes[0] = LatLon.Empty;
            _equatorLatitudes[1] = LatLon.Empty;
            _meridians.Clear();
            _lookupsCreated = false;
            _planetId = planetId;
            platformSystem = newPlatformSystem;
            _latLookupWorkItemIndex = -89.9f;
            _initUpdateCounter = 0;
        }

        public void PlanetChanged(PlanetData planetData)
        {
            if (planetData == null)
            {
                Debug($"planet changed, initting");
                SetInitValues(null, -1);
            }
            else
            {
                Debug("Index provider detected planet changed");
                SetInitValues(planetData.factory?.platformSystem, planetData.id);
            }
        }

        public LatLon GetForIndex(int index)
        {
            if (!_lookupsCreated)
            {
                return LatLon.Empty;
            }

            _llLookup.TryGetValue(index, out var result);
            return result;
        }

        public HashSet<LatLon> GetTropicsLatitudes()
        {
            return _tropicsLatitudes;
        }
        public (LatLon above, LatLon below) GetEquatorLatitudes()
        {
            return (_equatorLatitudes[0], _equatorLatitudes[1]);
        }
        public HashSet<LatLon> GetMeridianPoints()
        {
            return _meridians;
        }

        public LatLon GetForModIndex(int index)
        {
            if (!_lookupsCreated)
            {
                return LatLon.Empty;
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

            _initUpdateCounter++;
            if (_initUpdateCounter > 2_000)
                return;
            var endLat = Math.Min(_latLookupWorkItemIndex + LatitudesPerPass, 90);
            var planetRawData = platformSystem.planet.data;
            var start = DateTime.Now;
            var maxRuntimeMS = GetMaxRuntimeMS();
            var latLonPrecision = planetData.realRadius < 250f ? 10 : 10;
            var latitudeCount = platformSystem.latitudeCount;
            var latDegIncrement = (90 * 2.0f) / latitudeCount;

            var prevStart = -5;

            for (; _latLookupWorkItemIndex <= endLat; _latLookupWorkItemIndex += latDegIncrement)
            {
                var (startNdx, endNdxExclusive) = GetReformIndexesForLatitude(_latLookupWorkItemIndex);
                if (startNdx == prevStart)
                    continue;
                prevStart = startNdx;
                var longitudeCounts = endNdxExclusive - startNdx;
                if (longitudeCounts != prevLength)
                {
                    // got ourselves a new tropic here
                    _tropicsLatitudes.Add(LatLon.FromCoords(_latLookupWorkItemIndex, 0, latLonPrecision));
                }

                var latCoord = LatLon.FromCoords(_latLookupWorkItemIndex, 0, latLonPrecision);
                if (_latLookupWorkItemIndex < 0)
                {
                    if (_equatorLatitudes[1].IsEmpty())
                        _equatorLatitudes[1] = latCoord;
                    else if (_equatorLatitudes[1].Lat < latCoord.Lat)
                        _equatorLatitudes[1] = latCoord;
                }
                if (_latLookupWorkItemIndex >= 0)
                {
                    if (_equatorLatitudes[0].IsEmpty())
                        _equatorLatitudes[0] = latCoord;
                    else if (_equatorLatitudes[0].Lat > latCoord.Lat)
                        _equatorLatitudes[0] = latCoord;
                }

                prevLength = longitudeCounts;

                if (startNdx < endNdxExclusive)
                {
                    var numLongitudes = (endNdxExclusive - startNdx);
                    // longitudes above this index are in going west from 0
                    var maxPositiveLongitudeIndex = (endNdxExclusive + startNdx) / 2;
                    var negCounts = numLongitudes / 2;
                    var degPerIndex = 180f / negCounts;
                    var longMod = numLongitudes / 4;
                    for (var desired = startNdx; desired < endNdxExclusive; desired++)
                    {
                        if (desired > maxPositiveLongitudeIndex)
                        {
                            var longDegrees = degPerIndex * (desired - maxPositiveLongitudeIndex);
                            _llLookup[desired] = LatLon.FromCoords(_latLookupWorkItemIndex, -longDegrees, latLonPrecision);

                            var pos = GeoUtil.LatLonToPosition(_latLookupWorkItemIndex, -longDegrees, platformSystem.planet.realRadius);
                            var currentDataIndex = planetRawData.QueryIndex(pos);
                            _llModLookup[currentDataIndex] = _llLookup[desired];
                        }
                        else
                        {
                            var longDegrees = degPerIndex * (desired - startNdx);
                            _llLookup[desired] = LatLon.FromCoords(_latLookupWorkItemIndex, longDegrees, latLonPrecision);
                            var pos = GeoUtil.LatLonToPosition(_latLookupWorkItemIndex, longDegrees, platformSystem.planet.realRadius);
                            var currentDataIndex = planetRawData.QueryIndex(pos);
                            _llModLookup[currentDataIndex] = _llLookup[desired];
                        }

                        if ((desired - startNdx) % longMod == 0 || (desired + 1 == endNdxExclusive))
                        {
                            // meridian since it's a multiple of 4 (or last point in run)
                            _meridians.Add(_llLookup[desired]);
                        }
                        else if (((desired - startNdx) + 1) % longMod == 0)
                        {
                            // not actual meridian, but next to it so it makes our line look better
                            _meridians.Add(_llLookup[desired]);
                        }
                    }
                }

                if ((DateTime.Now - start).TotalMilliseconds > maxRuntimeMS)
                {
                    // Debug($"bailing at {_latLookupWorkItemIndex}, {latDegIncrement} {maxRuntimeMS} {incr}");
                    break;
                }
            }

            if (_latLookupWorkItemIndex > 89)
            {
                _lookupsCreated = true;
            }

            if (_lookupsCreated)
            {
                var maxRef = platformSystem.maxReformCount;
                var maxRaw = planetRawData.dataLength;
                Debug(
                    $"Completed position computations for {_llLookup.Count}/{maxRef}, {_llModLookup.Count}/{maxRaw} indices. Updates required: {_initUpdateCounter}. Found {_tropicsLatitudes.Count} tropics. {_meridians.Count} meridian points");
            }
        }

        private static int GetMaxRuntimeMS()
        {
            if (!PluginConfig.NeedReformIndexProvider())
            {
                return Math.Min(10, PluginConfig.maxInitMsPerFrame.Value);
            }

            return PluginConfig.maxInitMsPerFrame.Value;
        }

        public int InitPercentComplete()
        {
            var zeroBasedIndex = _latLookupWorkItemIndex + 90;
            var percent = zeroBasedIndex / 180f;
            return (int)(percent * 100);
        }

        public (int offsetStartIndex, int offsetEndIndex) GetReformIndexesForLatitude(float latDegrees)
        {
            double latInRads = Mathf.Deg2Rad * latDegrees;
            var latIndex = (float)(latInRads / (Mathf.PI * 2)) * platformSystem.segment;
            var scaledLat = Mathf.Round(latIndex * 10f);
            var absScaledLat = Mathf.Abs(scaledLat);
            var scaledLatFloat = scaledLat >= 0.0 ? absScaledLat : -absScaledLat;
            var latitudeSeg = scaledLatFloat / 10f;

            var scaledLatSeg = latitudeSeg > 0.0 ? Mathf.CeilToInt(latitudeSeg * 5f) : Mathf.FloorToInt(latitudeSeg * 5f);
            var latCountsHalf = platformSystem.latitudeCount / 2;
            var y = scaledLatSeg > 0 ? scaledLatSeg - 1 : latCountsHalf - scaledLatSeg - 1;
            var startIndex = platformSystem.reformOffsets[y];
            var unscaledCount = PlatformSystem.DetermineLongitudeSegmentCount(Mathf.FloorToInt(Mathf.Abs(latitudeSeg)), platformSystem.segment);

            var endIndex = startIndex + unscaledCount * 5;
            return (startIndex, endIndex);
        }
    }
}