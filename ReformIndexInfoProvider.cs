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
        private readonly LatLon[] _llModLookup = new LatLon[GameMain.localPlanet.data.modData.Length * 2];
        private readonly HashSet<LatLon> _tropicsLatitudes = new();
        private readonly LatLon[] _equatorLatitudes = { LatLon.Empty, LatLon.Empty };
        private readonly HashSet<LatLon> _meridians = new();
        public PlatformSystem platformSystem;
        private bool _lookupsCreated;
        private float _latLookupWorkItemIndex;
        private int _initUpdateCounter;
        private int _planetId;
        private int prevLength = -1;

        public ReformIndexInfoProvider(PlatformSystem platformSystem)
        {
            this.platformSystem = platformSystem;
            _planetId = platformSystem.planet.id;
            _latLookupWorkItemIndex = -90f + 90f / platformSystem?.latitudeCount ?? 500f;
        }

        public int PlanetId => _planetId;
        public bool Initted => _lookupsCreated;

        private void SetInitValues(PlatformSystem newPlatformSystem, int planetId)
        {
            _llLookup.Clear();
            Array.Clear(_llModLookup, 0, _llLookup.Count);
            _tropicsLatitudes.Clear();
            prevLength = -1;
            _equatorLatitudes[0] = LatLon.Empty;
            _equatorLatitudes[1] = LatLon.Empty;
            _meridians.Clear();
            _lookupsCreated = false;
            _planetId = planetId;
            platformSystem = newPlatformSystem;
            _latLookupWorkItemIndex = -90f + 90f / platformSystem?.latitudeCount ?? 500f;
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

            if (_llModLookup[index].IsEmpty())
                return LatLon.Empty;
            return _llModLookup[index];
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
            var latLonPrecision = 1000;
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
                if (longitudeCounts != prevLength && prevLength >= 0)
                {
                    // got ourselves a new tropic here
                    // mark the north side of the tropic in the southern hemisphere and the south side in the northern hemisphere
                    // it might be better to switch these or add a setting to choose
                    if (_latLookupWorkItemIndex < 0)
                        _tropicsLatitudes.Add(LatLon.FromCoords(_latLookupWorkItemIndex, 0, latLonPrecision));
                    else
                        _tropicsLatitudes.Add(LatLon.FromCoords(_latLookupWorkItemIndex - latDegIncrement, 0, latLonPrecision));
                }

                var latCoord = LatLon.FromCoords(_latLookupWorkItemIndex, 0, latLonPrecision);
                if (_latLookupWorkItemIndex <= 0)
                {
                    if (_equatorLatitudes[1].IsEmpty())
                    {
                        _equatorLatitudes[1] = latCoord;
                    }
                    else if (_equatorLatitudes[1].Lat < latCoord.Lat)
                    {
                        _equatorLatitudes[1] = latCoord;
                    }
                }
                if (_latLookupWorkItemIndex >= 0)
                {
                    if (_equatorLatitudes[0].IsEmpty())
                    {
                        _equatorLatitudes[0] = latCoord;
                    }
                    else if (_equatorLatitudes[0].Lat > latCoord.Lat)
                    {
                        _equatorLatitudes[0] = latCoord;
                    }
                }

                prevLength = longitudeCounts;

                if (startNdx < endNdxExclusive)
                {
                    var numLongitudes = (endNdxExclusive - startNdx);
                    // longitudes above this index are in going west from 0
                    var maxPositiveLongitudeIndex = (endNdxExclusive + startNdx) / 2 - 1;
                    var negCounts = numLongitudes / 2;
                    var degPerIndex = 180f / negCounts;
                    var longMod = numLongitudes / 4;
                    for (var desired = startNdx; desired < endNdxExclusive; desired++)
                    {
                        if (desired > maxPositiveLongitudeIndex)
                        {
                            var longDegrees = degPerIndex * (desired - maxPositiveLongitudeIndex - 0.5f);
                            _llLookup[desired] = LatLon.FromCoords(_latLookupWorkItemIndex, -longDegrees, latLonPrecision);

                            var pos = GeoUtil.LatLonToPosition(_latLookupWorkItemIndex, -longDegrees, platformSystem.planet.realRadius);
                            var currentDataIndex = planetRawData.QueryIndex(pos);
                            _llModLookup[currentDataIndex] = _llLookup[desired];
                        }
                        else
                        {
                            var longDegrees = degPerIndex * (desired - startNdx + 0.5f);
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
                    break;
                }
            }

            if (_latLookupWorkItemIndex >= 90)
            {
                _lookupsCreated = true;
            }

            if (_lookupsCreated)
            {
                var maxRef = platformSystem.maxReformCount;
                var maxRaw = planetRawData.dataLength;
                Debug(
                    $"Completed position computations for {_llLookup.Count}/{maxRef}, {_llModLookup.Length}/{maxRaw} indices. Updates required: {_initUpdateCounter}. Found {_tropicsLatitudes.Count} tropics. {_meridians.Count} meridian points");
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
            var latCountsHalf = platformSystem.latitudeCount / 2;
            var scaledLat = Mathf.FloorToInt(Mathf.Abs(latDegrees) * platformSystem.latitudeCount / 180f);
            var latIndex = latDegrees >= 0 ? scaledLat : scaledLat + latCountsHalf;
            var startIndex = platformSystem.reformOffsets[latIndex];
            var unscaledCount = PlatformSystem.DetermineLongitudeSegmentCount(scaledLat / 5, platformSystem.segment);

            var endIndex = startIndex + unscaledCount * 5;
            return (startIndex, endIndex);
        }
    }
}