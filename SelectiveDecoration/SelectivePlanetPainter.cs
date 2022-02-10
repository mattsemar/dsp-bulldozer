using System.Collections.Generic;
using System.Text;

namespace Bulldozer.SelectiveDecoration
{
    public class SelectivePlanetPainter
    {
        private readonly ReformIndexInfoProvider _reformIndexInfoProvider;
        private readonly List<ISelectivePlanetDecorator> _decorators = new();

        public SelectivePlanetPainter(ReformIndexInfoProvider reformIndexInfoProvider)
        {
            _reformIndexInfoProvider = reformIndexInfoProvider;
        }

        public void Decorate()
        {
            if (!_reformIndexInfoProvider.Initted)
            {
                Log.Warn("Can't paint regions, index provider is not initted");
                return;
            }

            var platformSystem = _reformIndexInfoProvider.platformSystem;

            var actionBuild = GameMain.mainPlayer?.controller.actionBuild;
            if (actionBuild == null)
            {
                return;
            }

            platformSystem.EnsureReformData();
            Log.Debug($"starting selective mode with {_decorators.Count} decorators registered");
            var reformCount = platformSystem.maxReformCount;
            for (var index = 0; index < reformCount; ++index)
            {
                var latLon = _reformIndexInfoProvider.GetForIndex(index);
                if (latLon.IsEmpty())
                {
                    Log.Warn($"latLon for index: {index} was really empty");
                }

                if (PluginConfig.LatitudeOutOfBounds(latLon.Lat))
                {
                    continue;
                }

                var decoration = DecoratorForLocation(latLon);
                if (decoration.IsNone())
                    continue;

                platformSystem.SetReformType(index, decoration.ReformType);
                platformSystem.SetReformColor(index, decoration.ColorIndex);
            }
        }

        public void Register(ISelectivePlanetDecorator decorator)
        {
            if (!_decorators.Contains(decorator))
                _decorators.Add(decorator);
        }

        public void UnregisterAll()
        {
            _decorators.Clear();
        }

        private DecorationConfig DecoratorForLocation(LatLon location)
        {
            foreach (var decorator in _decorators)
            {
                var decor = decorator.GetDecorationForLocation(location);
                if (!decor.IsNone())
                {
                    return decor;
                }
            }

            return DecorationConfig.None;
        }

        public string BuildActionSummary()
        {
            var sb = new StringBuilder($"Add the following guide markings: \n");
            foreach (var planetDecorator in _decorators)
            {
                var actionSum = planetDecorator.ActionSummary();
                if (!string.IsNullOrWhiteSpace(actionSum))
                {
                    sb.Append($"\t{actionSum}\n");
                }
            }

            return sb.ToString();
        }
    }
}