namespace Bulldozer.SelectiveDecoration
{
    public class TropicsPainter : ISelectivePlanetDecorator
    {
        private readonly ReformIndexInfoProvider _infoProvider;
        private readonly DecorationConfig _tropicsDecorationConfig;

        public TropicsPainter(ReformIndexInfoProvider provider)
        {
            _infoProvider = provider;
            _tropicsDecorationConfig = new DecorationConfig(PluginConfig.guideLinesTropicColor.Value);
        }

        public DecorationConfig GetDecorationForLocation(LatLon location)
        {
            if (_infoProvider.GetTropicsLatitudes().Contains(LatLon.FromCoords(location.Lat, 0, location.Precision)))
                return _tropicsDecorationConfig;

            return DecorationConfig.None;
        }

        public string ActionSummary() => $"Tropics (detected {_infoProvider.GetTropicsLatitudes().Count})";
    }
}