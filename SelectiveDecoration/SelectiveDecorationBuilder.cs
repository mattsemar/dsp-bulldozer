namespace Bulldozer.SelectiveDecoration
{
    public static class SelectiveDecorationBuilder
    {
        public static readonly float POLE_LATITUDE_START = 84.6f;
        public static SelectivePlanetPainter Build(ReformIndexInfoProvider reformIndexInfoProvider)
        {
            var result = new SelectivePlanetPainter(reformIndexInfoProvider);

            if (PluginConfig.addGuideLinesPoles.Value)
            {
                result.Register(new PolePainter());
            }

            if (PluginConfig.addGuideLinesEquator.Value)
            {
                result.Register(new EquatorPainter(reformIndexInfoProvider));
            }

            if (PluginConfig.addGuideLinesMeridian.Value)
            {
                result.Register(new MajorMeridianPainter(reformIndexInfoProvider));
            }

            if (PluginConfig.minorMeridianInterval.Value > 0)
            {
                result.Register(new MinorMeridianPainter(reformIndexInfoProvider));
            }

            if (PluginConfig.addGuideLinesTropic.Value)
            {
                result.Register(new TropicsPainter(reformIndexInfoProvider));
            }

            if (PluginConfig.enableRegionColor.Value)
            {
                result.Register(new SelectiveRegionalPlanetDecorator());
            }

// #if DEBUG
            // result.Register(new DebugBackgroundPainter());
// #endif

            return result;
        }
    }
}