using static Bulldozer.Log;

namespace Bulldozer
{
    public class RegionPainter
    {
        private readonly PlatformSystem platformSystem;
        private readonly ReformIndexInfoProvider _reformIndexInfoProvider;

        public RegionPainter(PlatformSystem platformSystem, ReformIndexInfoProvider reformIndexInfoProvider)
        {
            this.platformSystem = platformSystem;
            _reformIndexInfoProvider = reformIndexInfoProvider;
        }

        public void PaintRegions()
        {
            if (!PluginConfig.enableRegionColor.Value)
                return;
            if (RegionalColors.Instance.Count == 0)
                return;

            var actionBuild = GameMain.mainPlayer?.controller.actionBuild;
            if (actionBuild == null)
            {
                return;
            }

            RegionalColors.Instance.Save();

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

            var reformCount = platformSystem.maxReformCount;
            for (var index = 0; index < reformCount; ++index)
            {
                var latLon = _reformIndexInfoProvider.GetForIndex(index);
                var regionColorConfig = RegionalColors.Instance.GetForPosition(latLon.Lat, latLon.Long);
                if (regionColorConfig == null)
                {
                    continue;
                }

                if (!platformSystem.IsTerrainReformed(platformSystem.GetReformType(index)))
                    continue;

                platformSystem.SetReformType(index, brushType);
                platformSystem.SetReformColor(index, regionColorConfig.colorIndex);
            }
        }
    }
}