using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bulldozer
{
    public readonly struct LatLon : IEquatable<LatLon>
    {
        private readonly int _lat;
        private readonly int _lng;

        public int Lat => _lat;

        public int Long => _lng;
        public static LatLon Empty => new (-1000, -1000);

        private static readonly Dictionary<int, Dictionary<int, LatLon>> PoolLatToLonToInstance = new();

        private LatLon(int lat, int lng)
        {
            _lat = lat;
            _lng = lng;
        }

        public static LatLon FromCoords(double lat, double lon)
        {
            int newLat = lat < 0 ? Mathf.CeilToInt((float)lat) : Mathf.FloorToInt((float)lat);
            int newLon = lon < 0 ? Mathf.CeilToInt((float)lon) : Mathf.FloorToInt((float)lon);
            if (!PoolLatToLonToInstance.TryGetValue(newLat, out var lonLookup))
            {
                lonLookup = new Dictionary<int, LatLon>();
                PoolLatToLonToInstance[newLat] = lonLookup;
            }

            if (!lonLookup.TryGetValue(newLon, out var inst))
            {
                lonLookup[newLon] = inst = new LatLon(lat: newLat, lng: newLon);
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
            return _lat == Empty._lat && _lng == Empty._lng;
        }
    }
}