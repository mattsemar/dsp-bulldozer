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
            var planetRawData = GameMain.localPlanet.data;

            var actionBuild = GameMain.mainPlayer?.controller.actionBuild;
            if (actionBuild == null)
            {
                return;
            }

            platformSystem.EnsureReformData();
            Log.Debug($"starting selective mode with {_decorators.Count} decorators registered");
            var reformCount = platformSystem.maxReformCount;
            var setModIndexes = new HashSet<int>();
            var consumedFoundation = 0;
            var foundationUsedUp = false;
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
                
                if (PluginConfig.guideLinesOnly.Value)
                {
                    // probably need some flattening action
                    var pointsAround = GetPointsAround(latLon);
                    foreach (var point in pointsAround)
                    {
                        var pos = GeoUtil.LatLonToPosition(point.Lat, point.Long, platformSystem.planet.realRadius);
                        var currentDataIndex = planetRawData.QueryIndex(pos);
                        if (setModIndexes.Contains(currentDataIndex))
                        {
                            continue;
                        }
                        GameMain.localPlanet.AddHeightMapModLevel(currentDataIndex, 3);
                        planetRawData.modData[currentDataIndex / 2] = 51;
                        setModIndexes.Add(currentDataIndex);
                    }
                }
                var foundationNeeded = platformSystem.IsTerrainReformed(platformSystem.GetReformType(index)) ? 0 : 1;
                if (foundationNeeded > 0 && PluginConfig.foundationConsumption.Value != OperationMode.FullCheat)
                {
                    consumedFoundation += foundationNeeded;
                    var (_, successful) = StorageSystemManager.RemoveItems(PlatformSystem.REFORM_ID, foundationNeeded);

                    if (!successful)
                    {
                        if (PluginConfig.foundationConsumption.Value == OperationMode.Honest)
                        {
                            Log.LogAndPopupMessage("Out of foundation, halting.");
                            break;
                        }
                    }
                }
                platformSystem.SetReformType(index, decoration.ReformType);
                platformSystem.SetReformColor(index, decoration.ColorIndex);
            }
            for (int index = 0; index < platformSystem.planet.dirtyFlags.Length; ++index)
                platformSystem.planet.dirtyFlags[index] = true;
            platformSystem.planet.landPercentDirty =true;
            if (platformSystem.planet.UpdateDirtyMeshes())
                platformSystem.planet.factory.RenderLocalPlanetHeightmap();
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

        public DecorationConfig DecoratorForLocation(LatLon location)
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

        public SelectivePlanetPainter Flatten()
        {
            if (!PluginConfig.guideLinesOnly.Value)
                return this;
            // var knownValues = LatLon.GetKnownValues();
            // foreach (var latLon in knownValues)
            // {
            //     if (ShouldPave(latLon))
            //     {
            //         var position = GeoUtil.LatLonToPosition(latLon.Lat, latLon.Long, GameMain.localPlanet.realRadius);
            //
            //         GameMain.localPlanet.factory.FlattenTerrain(position, Quaternion.identity, new Bounds(position, Vector3.one * 100));
            //     }
            // }
            for (var index = 0; index < GameMain.localPlanet.modData.Length << 1; ++index)
            {
                var latLonForModIndex = _reformIndexInfoProvider.GetForModIndex(index);
                if (latLonForModIndex.Equals(LatLon.Empty))
                {
                    Log.LogNTimes("No coord mapped to data index for {0}", 200, index);
                    GameMain.localPlanet.AddHeightMapModLevel(index, 3);
                    continue;
                }
            
                if (PluginConfig.LatitudeOutOfBounds(latLonForModIndex.Lat))
                {
                    continue;
                }
            
                if (!ShouldPave(latLonForModIndex))
                {
                    continue;
                }
            
                GameMain.localPlanet.AddHeightMapModLevel(index, 3);
            }
            bool[] dirtyFlags = GameMain.localPlanet.dirtyFlags;
            int length2 = dirtyFlags.Length;
            for (int index = 0; index < length2; ++index)
                dirtyFlags[index] = true;
            GameMain.localPlanet.landPercentDirty = true;
            if (GameMain.localPlanet.UpdateDirtyMeshes())
                GameMain.localPlanet.factory.RenderLocalPlanetHeightmap();

            return this;
        }


        private bool ShouldPave(LatLon latLon)
        {
            var decoration = DecoratorForLocation(latLon);
            if (!decoration.IsNone())
            {
                return true;
            }
            
            if (!DecoratorForLocation(latLon.Offset(-1, -1)).IsNone())
            {
                return true;
            }
            
            if (!DecoratorForLocation(latLon.Offset(-1, 0)).IsNone())
            {
                return true;
            }
            
            if (!DecoratorForLocation(latLon.Offset(-1, 1)).IsNone())
            {
                return true;
            }
            
            if (!DecoratorForLocation(latLon.Offset(0, -1)).IsNone())
            {
                return true;
            }
            
            if (!DecoratorForLocation(latLon.Offset(0, 1)).IsNone())
            {
                return true;
            }
            
            if (!DecoratorForLocation(latLon.Offset(1, -1)).IsNone())
            {
                return true;
            }
            
            if (!DecoratorForLocation(latLon.Offset(1, 0)).IsNone())
            {
                return true;
            }
            
            if (!DecoratorForLocation(latLon.Offset(1, 1)).IsNone())
            {
                return true;
            }

            return false;
        }

        private LatLon[] GetPointsAround(LatLon latLon)
        {
            return new LatLon[]
            {
                latLon.Offset(-1, -1),
                latLon.Offset(-1, 0),
                latLon.Offset(-1, 1),
                latLon.Offset(0, -1),
                latLon,
                latLon.Offset(0, 1),
                latLon.Offset(1, -1),
                latLon.Offset(1, 0),
                latLon.Offset(1, 1),
            };
        }
    }
}