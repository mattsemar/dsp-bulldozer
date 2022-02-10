namespace Bulldozer.SelectiveDecoration
{
    public class MajorMeridianPainter : ISelectivePlanetDecorator
    {
        private static DecorationConfig _majorMeridianConfig;
        private readonly ReformIndexInfoProvider infoProvider;

        public MajorMeridianPainter(ReformIndexInfoProvider reformIndexInfoProvider)
        {
            infoProvider = reformIndexInfoProvider;
            _majorMeridianConfig = new DecorationConfig(1, PluginConfig.guideLinesMeridianColor.Value);
        }

        public DecorationConfig GetDecorationForLocation(LatLon location)
        {
            if (PluginConfig.addGuideLinesMeridian.Value)
            {
                if (infoProvider.GetMeridianPoints().Contains(location))
                    return _majorMeridianConfig;
            }
            
            return DecorationConfig.None;
        }

        public string ActionSummary()
        {
            return "Major Meridians";
        }
    }
}