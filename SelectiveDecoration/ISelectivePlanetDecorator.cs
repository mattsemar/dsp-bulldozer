namespace Bulldozer.SelectiveDecoration
{
    public interface ISelectivePlanetDecorator
    {
        public DecorationConfig GetDecorationForLocation(LatLon location);
        public string ActionSummary();
    }
}