using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using static Bulldozer.Log;

namespace Bulldozer
{
    public class SnapArgs
    {
        public int[] indices = new int[100];
        public Vector3[] points = new Vector3[100];
    }

    public class GridExplorer
    {
        public delegate void PostComputeReformAction(SnapArgs snapArgs, Vector3 center, float radius, int reformSize, int neededSoilPile, bool timeExpired = false,
            float lastLat = 0, float lastLon = 0);


        public static void IterateReform(BuildTool_Reform reformTool, PostComputeReformAction postComputeFn, int maxExecutionMs, float startLat = -89.9f, 
            float startLon = -179.9f)
        {
            var checkedReformIndices = new HashSet<int>();
            var checkedDataPos = new HashSet<int>();
            var platformSystem = reformTool.planet.factory.platformSystem;
            if (platformSystem.IsAllReformed())
            {
                return;
            }

            int cursorPointCount;
            var radius = 0.990946f * 10;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (var lat = startLat; lat < 90; lat += 0.25f)
            {
                if (PluginConfig.LatitudeOutOfBounds(lat))
                    continue;
                for (var lon = -179.9f; lon < 180; lon += 0.25f)
                {
                    var position = GeoUtil.LatLonToPosition(lat, 0, reformTool.planet.radius);
                    position.Normalize();
                    var reformIndexForPosition = platformSystem.GetReformIndexForPosition(position);
                    if (reformIndexForPosition < 0)
                        continue;
                    if (checkedReformIndices.Contains(reformIndexForPosition))
                    {
                        continue;
                    }

                    checkedReformIndices.Add(reformIndexForPosition);
                    if (platformSystem.IsTerrainReformed(platformSystem.GetReformType(reformIndexForPosition)))
                    {
                        continue;
                    }

                    var snapArgs = new SnapArgs();
                    cursorPointCount = reformTool.planet.aux.ReformSnap(position, 10, 1, 1, snapArgs.points, snapArgs.indices, platformSystem,
                        out var center);
                    var queryIndex = reformTool.planet.data.QueryIndex(center);
                    if (checkedDataPos.Contains(queryIndex))
                    {
                        continue;
                    }

                    checkedDataPos.Add(queryIndex);
                    foreach (var cursorPoint in snapArgs.points)
                    {
                        checkedDataPos.Add(reformTool.planet.data.QueryIndex(cursorPoint));
                    }

                    var flattenTerrainReform = reformTool.factory.ComputeFlattenTerrainReform(snapArgs.points, center, radius, cursorPointCount);
                    postComputeFn?.Invoke(snapArgs, center, radius, 10, flattenTerrainReform);

                    if (stopwatch.ElapsedMilliseconds > maxExecutionMs)
                    {
                        LogAndPopupMessage($"cancel after running ${stopwatch.ElapsedMilliseconds} lat={lat} / lon={lon}");
                        stopwatch.Stop();
                        // signal that we did not finish this task
                        if (postComputeFn != null)
                            postComputeFn(snapArgs, center, radius, 10, 0, true, lat, lon);
                        break;
                    }
                }
            }
        }

        public static int GetNeededFoundation(PlatformSystem platformSystem, ReformIndexInfoProvider reformIndexInfoProvider)
        {
            var foundationNeeded = 0;
            for (var index = 0; index < platformSystem.maxReformCount; ++index)
            {
                if (PluginConfig.IsLatConstrained())
                {
                    var latLon = reformIndexInfoProvider.GetForIndex(index);
                    if (!latLon.IsEmpty()  && PluginConfig.LatitudeOutOfBounds(latLon.Lat))
                    {
                        continue;
                    }
                } 
                foundationNeeded += platformSystem.IsTerrainReformed(platformSystem.GetReformType(index)) ? 0 : 1;
            }

            return foundationNeeded;
        }

        public static (int foundation, int soilPile) CountNeededResources(PlatformSystem platformSystem, ReformIndexInfoProvider indexInfoProvider)
        {
            logger.LogInfo($"player current soil pile {GameMain.mainPlayer.sandCount}");
            var platformSystemPlanet = platformSystem?.planet;
            if (platformSystemPlanet == null)
            {
                logger.LogWarning("null platform system passed in");
                return (int.MaxValue, int.MaxValue);
            }

            platformSystem.EnsureReformData();
            var neededFoundation = 0;
            var neededSoil = 0;
            if (PluginConfig.foundationConsumption.Value != OperationMode.FullCheat)
            {
                neededFoundation = GetNeededFoundation(platformSystem, indexInfoProvider);
            }

            if (PluginConfig.soilPileConsumption.Value != OperationMode.FullCheat)
            {
                _soilNeeded = 0;
                IterateReform(GameMain.mainPlayer.controller.actionBuild.reformTool, SumReform, 1000 * 5);
                neededSoil = _soilNeeded;
            }

            return (neededFoundation, neededSoil);
        }

        private static int _soilNeeded;

        public static void SumReform(SnapArgs snapArgs, Vector3 center, float radius, int reformSize, int neededSoilPile, bool timeExpired = false,
            float lastLat = 0, float lastLon = 0)
        {
            _soilNeeded += neededSoilPile;
        }
    }
}