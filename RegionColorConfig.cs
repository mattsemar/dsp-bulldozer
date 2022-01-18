using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Bulldozer
{
    public class RegionColorConfig
    {
        public int minLatitude;
        public int maxLatitude;
        public int minLongitude;
        public int maxLongitude;
        public bool mirror;
        public int colorIndex;

        public bool ContainsPosition(int lat, int lng)
        {
            bool allLatitudes = minLatitude == maxLatitude;
            bool allLongs = minLongitude == maxLongitude;
            if (allLatitudes && allLongs)
            {
                return true;
            }

            if (!allLatitudes && allLongs)
            {
                
                bool defaultLatsMatch = (minLatitude <= lat && maxLatitude >= lat);
                if (defaultLatsMatch)
                    return true;
                if (mirror)
                    return (-minLatitude >= lat && -maxLatitude <= lat);
                return false;
            }
            if (allLatitudes && !allLongs)
            {
                
                bool defaultLonsMatch = (minLongitude <= lng && maxLongitude >= lng);
                if (defaultLonsMatch)
                    return true;
                return false;
            }
            
            if ((minLatitude <= lat && maxLatitude >= lat) && (minLongitude <= lng && maxLongitude >= lng))
                return true;
            if (!mirror)
                return false;
            return (-minLatitude >= lat && -maxLatitude <= lat)
                &&  minLongitude <= lng && maxLongitude >= lng;
        }
    }

    public class RegionalColors
    {
        private static RegionalColors _instance;
        private List<RegionColorConfig> _regionColorConfigs;

        public RegionalColors()
        {
            _regionColorConfigs = new List<RegionColorConfig>();
        }


        public static RegionalColors Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = DeserializeFromConfigProperty();
                }

                return _instance;
            }
            private set => _instance = value;
        }

        public int Count
        {
            get => _regionColorConfigs.Count;
        }

        public void Save()
        {
            if (_regionColorConfigs.Count == 0)
            {
                PluginConfig.regionColors.Value = "";
                return;
            }

            var newResult = new StringBuilder();
            for (int i = 0; i < _regionColorConfigs.Count; i++)
            {
                newResult.Append(JsonUtility.ToJson(_regionColorConfigs[i]));
                if (i + 1 < _regionColorConfigs.Count)
                    newResult.Append("$");
            }

            Log.Debug($"Added {_regionColorConfigs.Count} values to plugin config");
            PluginConfig.regionColors.Value = newResult.ToString();
        }

        private static RegionalColors DeserializeFromConfigProperty()
        {
            var strVal = PluginConfig.regionColors.Value;
            if (strVal.Trim().Length == 0)
            {
                var regionColorConfig = new RegionColorConfig();
                regionColorConfig.colorIndex = 2;
                regionColorConfig.minLatitude = -89;
                regionColorConfig.maxLatitude = -70;
                regionColorConfig.minLongitude = 0;
                regionColorConfig.maxLongitude = 0;
                var tmpResult = new RegionalColors();
                tmpResult._regionColorConfigs.Add(regionColorConfig);
                return tmpResult;
            }

            var result = new RegionalColors();

            // format is "JSONREP$JSONREP"
            var parts = strVal.Split('$');
            Log.Debug($"Loading region color config from json {parts.Length} {strVal}");
            foreach (var savedValue in parts)
            {
                try
                {
                    var regionColorConfig = JsonUtility.FromJson<RegionColorConfig>(savedValue.Trim());
                    result._regionColorConfigs.Add(regionColorConfig);
                }
                catch (Exception e)
                {
                    Log.Warn($"Failed to parse {savedValue} into color config {e.Message}");
                }
            }

            return result;
        }

        public RegionColorConfig GetForPosition(int lat, int lng)
        {
            foreach (var regionColorConfig in _regionColorConfigs)
            {
                if (regionColorConfig.ContainsPosition(lat, lng))
                {
                    return regionColorConfig;
                }
            }

            return null;
        }

        public List<RegionColorConfig> GetRegions()
        {
            return _regionColorConfigs;
        }

        public void Remove(RegionColorConfig regionalColor)
        {
            _regionColorConfigs.Remove(regionalColor);
        }

        public void Create()
        {
            var regionColorConfig = new RegionColorConfig();
            _regionColorConfigs.Add(regionColorConfig);
        }
    }
}