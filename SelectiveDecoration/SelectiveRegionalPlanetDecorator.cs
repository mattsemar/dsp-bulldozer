namespace Bulldozer.SelectiveDecoration
{
    public class SelectiveRegionalPlanetDecorator : ISelectivePlanetDecorator
    {
        public DecorationConfig GetDecorationForLocation(LatLon location)
        {
            var regionColorConfig = RegionalColors.Instance.GetForPosition(location.Lat, location.Long);
            return regionColorConfig == null ? DecorationConfig.None : new DecorationConfig(1, regionColorConfig.colorIndex);
        }

        public string ActionSummary() => $"Regions ({RegionalColors.Instance.Count})";
    }
}