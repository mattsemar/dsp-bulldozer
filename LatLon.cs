using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Bulldozer
{
    public readonly struct LatLon : IEquatable<LatLon>
    {
        private readonly int _lat;
        private readonly int _lng;

        public float Lat => Precision == 1 ? _lat : _lat / (float) Precision;

        public float Long => Precision == 1 ? _lng : _lng / (float) Precision;
        public static LatLon Empty => new (-1000, -1000, 1);

        private static readonly Dictionary<int, Dictionary<int, LatLon>> PoolLatToLonToInstance = new();
        public readonly int Precision;

        private LatLon(int lat, int lng, int precision)
        {
            _lat = lat;
            _lng = lng;
            Precision = precision;
        }

        public int RawLat()
        {
            return _lat;
        }
        public int RawLon()
        {
            return _lng;
        }

        public static LatLon FromCoords(double lat, double lon, int precisionMultiple = 1)
        {
            if (precisionMultiple != 1)
            {
                if (precisionMultiple < 1)
                    throw new InvalidDataException("Invalid precision multiple " + precisionMultiple);
                if (precisionMultiple % 10 != 0)
                    throw new InvalidDataException("Invalid precision multiple " + precisionMultiple);
            }

            int newLat = lat < 0 ? Mathf.CeilToInt((float)lat * precisionMultiple) : Mathf.FloorToInt((float)lat * precisionMultiple);
            int newLon = lon < 0 ? Mathf.CeilToInt((float)lon * precisionMultiple) : Mathf.FloorToInt((float)lon * precisionMultiple);
            if (!PoolLatToLonToInstance.TryGetValue(newLat, out var lonLookup))
            {
                lonLookup = new Dictionary<int, LatLon>();
                PoolLatToLonToInstance[newLat] = lonLookup;
            }

            if (!lonLookup.TryGetValue(newLon, out var inst))
            {
                lonLookup[newLon] = inst = new LatLon(lat: newLat, lng: newLon, precisionMultiple);
            }

            return inst;
        }

        public bool Equals(LatLon other)
        {
            return _lat == other._lat && _lng == other._lng;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
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
                return (_lat * 397) ^ _lng;
            }
        }

        public bool IsEmpty()
        {
            return Precision == 0 || _lat == Empty._lat && _lng == Empty._lng;
        }

        public override string ToString()
        {
            return $"{Lat},{Long}";
        }

        public LatLon Offset(float latOffset, float lonOffset)
        {
            return FromCoords(Lat + latOffset, Long + lonOffset, Precision);
        }

        public static HashSet<LatLon> GetKnownValues()
        {
            var result = new HashSet<LatLon>();
            foreach (var lonLookup in PoolLatToLonToInstance.Values)
            {
                foreach (var value in lonLookup.Values)
                {
                    result.Add(value);
                }
            }

            return result;
        }
    }
}