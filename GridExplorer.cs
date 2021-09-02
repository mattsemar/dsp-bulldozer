using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using static Bulldozer.Log;

namespace Bulldozer
{
    internal class SnapArgs
    {
        public int[] indices = new int[100];
        public Vector3[] points = new Vector3[100];
    }

    public class GridExplorer
    {
        public static int GetNeededSoilPile(BuildTool_Reform reformTool)
        {
            var checkedReformIndices = new HashSet<int>();
            var checkedDataPos = new HashSet<int>();
            var platformSystem = reformTool.planet.factory.platformSystem;
            if (platformSystem.IsAllReformed())
            {
                return 0;
            }

            int cursorPointCount;
            var radius = 0.990946f * 10;
            var neededSoilPile = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (var lat = -89.9f; lat < 90; lat += 0.25f)
            {
                for (var lon = -179.9f; lon < 180; lon += 0.25f)
                {
                    var position = GuideMarker.LatLonToPosition(lat, 0, reformTool.planet.radius);
                    position.Normalize();
                    var reformIndexForPosition = platformSystem.GetReformIndexForPosition(position);
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
                    Vector3 outpos;
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
                    neededSoilPile += flattenTerrainReform;
                    if (stopwatch.ElapsedMilliseconds > 1000 * 5)
                    {
                        LogAndPopupMessage($"cancel after running ${stopwatch.ElapsedMilliseconds} lat={lat} / lon={lon}");
                        stopwatch.Stop();
                        break;
                    }
                }
            }

            return neededSoilPile;
        }

        public static int GetNeededFoundation(PlatformSystem platformSystem)
        {
            var foundationNeeded = 0;
            for (var index = 0; index < platformSystem.maxReformCount; ++index)
            {
                foundationNeeded += platformSystem.IsTerrainReformed(platformSystem.GetReformType(index)) ? 0 : 1;
            }

            return foundationNeeded;
        }

        public static (int foundation, int soilPile) CountNeededResources(PlatformSystem platformSystem)
        {
            var platformSystemPlanet = platformSystem?.planet;
            if (platformSystemPlanet == null)
            {
                logger.LogWarning($"null platform system passed in");
                return (int.MaxValue, int.MaxValue);
            }

            platformSystem.EnsureReformData();
            var neededFoundation = 0;
            var neededSoil = 0;
            if (PluginConfig.foundationConsumption.Value != OperationMode.FullCheat)
            {
                neededFoundation = GetNeededFoundation(platformSystem);
            }

            if (PluginConfig.soilPileConsumption.Value != OperationMode.FullCheat)
            {
                neededSoil = GetNeededSoilPile(GameMain.mainPlayer.controller.actionBuild.reformTool);
            }

            return (neededFoundation, neededSoil);
        }
    }
}