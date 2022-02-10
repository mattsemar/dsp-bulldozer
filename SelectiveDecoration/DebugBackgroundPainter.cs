namespace Bulldozer.SelectiveDecoration
{
    public class DebugBackgroundPainter : ISelectivePlanetDecorator
    {
        public DecorationConfig GetDecorationForLocation(LatLon location)
        {
            return DecorationConfig.None;
        }

        public string ActionSummary() => "DEBUG BACKGROUND";
    }
}