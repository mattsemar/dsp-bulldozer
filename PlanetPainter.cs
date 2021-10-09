using UnityEngine;
using static Bulldozer.Log;
using static PlatformSystem;

namespace Bulldozer
{
    public static class PlanetPainter
    {

        public static bool PaintPlanet(PlatformSystem platformSystem)
        {
            var maxReformCount = platformSystem.maxReformCount;
            var actionBuild = GameMain.mainPlayer?.controller.actionBuild;
            if (actionBuild == null)
            {
                return true;
            }

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

            var consumedFoundation = 0;
            var foundationUsedUp = false;
            var outOfFoundationMessageShown = false;
            for (var index = 0; index < maxReformCount; ++index)
            {
                var foundationNeeded = platformSystem.IsTerrainReformed(platformSystem.GetReformType(index)) ? 0 : 1;
                if (foundationNeeded > 0 && PluginConfig.foundationConsumption.Value != OperationMode.FullCheat)
                {
                    consumedFoundation += foundationNeeded;
                    var reformId = REFORM_ID;
                    var (itemsRemoved, successful) = StorageSystemManager.RemoveItems(REFORM_ID, foundationNeeded);

                    if (!successful)
                    {
                        if (PluginConfig.foundationConsumption.Value == OperationMode.Honest)
                        {
                            LogAndPopupMessage($"Out of foundation, halting.");
                            foundationUsedUp = true;
                            break;
                        }

                        if (!outOfFoundationMessageShown)
                        {
                            outOfFoundationMessageShown = true;
                            LogAndPopupMessage($"All foundation used, continuing");
                        }
                    }
                }

                platformSystem.SetReformType(index, brushType);
                platformSystem.SetReformColor(index, actionBuild.reformTool.brushColor);
            }

            GameMain.mainPlayer.mecha.AddConsumptionStat(REFORM_ID, consumedFoundation, GameMain.mainPlayer.nearestFactory);
            return foundationUsedUp;
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
    }
}