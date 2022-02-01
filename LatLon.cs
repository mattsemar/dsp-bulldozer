using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bulldozer
{
    public class LatLon : IEquatable<LatLon>
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