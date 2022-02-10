using System;

namespace Bulldozer.SelectiveDecoration
{
    public class PolePainter : ISelectivePlanetDecorator
    {
        public DecorationConfig GetDecorationForLocation(LatLon location)
        {

            if (Math.Abs(location.Lat) < SelectiveDecorationBuilder.POLE_LATITUDE_START)
            {
                return DecorationConfig.None;
            }
            return new DecorationConfig(1, PluginConfig.guideLinesPoleColor.Value);
        }

        public string ActionSummary() => "Poles";
    }
}