namespace Bulldozer.SelectiveDecoration
{
    public class EquatorPainter : ISelectivePlanetDecorator
    {
        private static DecorationConfig _decorationConfig;
        private readonly ReformIndexInfoProvider _infoProvider;

        public EquatorPainter(ReformIndexInfoProvider infoProvider)
        {
            _infoProvider = infoProvider;
            _decorationConfig = new DecorationConfig(1, PluginConfig.guideLinesEquatorColor.Value);
        }

        public DecorationConfig GetDecorationForLocation(LatLon location)
        {
            var (above, below) = _infoProvider.GetEquatorLatitudes();
            if (above.RawLat() == location.RawLat() || below.RawLat() == location.RawLat())
            {
                return _decorationConfig;
            }

            return DecorationConfig.None;
        }

        public string ActionSummary() => "Equator";
    }
}