using System.Collections.Generic;
using static Bulldozer.Log;

namespace Bulldozer
{
    /// <summary>
    /// This code was adapted from code contributed by Velociraptor115 on Github / Raptor#4825 on Discord 
    /// </summary>
    public static class RaptorFastDelete
    {
        public static void Execute()
        {
            if (GameMain.mainPlayer == null)
                return;

            FastDeleteEntities();
        }

        private static void FastDeleteEntities()
        {
            var player = GameMain.mainPlayer;

            if (player.factory == null)
                return;

            var factory = player.factory;
            var factorySystem = factory.factorySystem;
            var cargoTraffic = factory.cargoTraffic;
            var powerSystem = factory.powerSystem;

            // Close all the build tools, so we don't have to worry about BuildTool.buildPreview
            foreach (var buildTool in player.controller.actionBuild.tools)
                buildTool._Close();

            // Close inspect
            player.controller.actionInspect.InspectNothing();

            //const int maxItemId = 12000;
            //var takeBackCount = new int[maxItemId];
            //var takeBackInc = new int[maxItemId];
            var takeBackCount = new Dictionary<int, int>();
            var takeBackInc = new Dictionary<int, int>();

            var powerConRemoval = new Dictionary<int, HashSet<int>>();

            for (int i = 0; i < powerSystem.netCursor; i++)
            {
                powerConRemoval[i] = new HashSet<int>();
            }

            foreach (var item in LDB.items.dataArray)
            {
                takeBackCount[item.ID] = 0;
                takeBackInc[item.ID] = 0;
            }

            void DeleteInserters()
            {
                var inserterPool = factorySystem.inserterPool;
                var entityPool = factory.entityPool;
                var consumerPool = powerSystem.consumerPool;

                void TakeBackItemsOptimized(ref InserterComponent inserter)
                {
                    if (inserter.itemId > 0 && inserter.stackCount > 0)
                    {
                        takeBackCount[inserter.itemId] += inserter.itemCount;
                        takeBackInc[inserter.itemId] += inserter.itemInc;
                    }
                }

                void RemoveConsumerComponent(int id)
                {
                    ref var powerCon = ref consumerPool[id];
                    if (powerCon.id != 0)
                    {
                        powerConRemoval[powerCon.networkId].Add(id);
                        powerCon.SetEmpty();
                        powerSystem.consumerRecycle[powerSystem.consumerRecycleCursor] = id;
                        powerSystem.consumerRecycleCursor++;
                    }
                }

                for (int i = 1; i < factorySystem.inserterCursor; i++)
                {
                    ref var inserter = ref inserterPool[i];
                    var entityId = inserter.entityId;
                    if (entityId == 0)
                        continue;

                    if (inserter.id == i)
                    {
                        // record the inserter in the takeback data 
                        var entityData = factory.entityPool[entityId];
                        takeBackCount[entityData.protoId]++;
                        TakeBackItemsOptimized(ref inserter);
                    }

                    RemoveConsumerComponent(entityPool[entityId].powerConId);
                    // Help remove the power consumers before removing the entity
                    factory.RemoveEntityWithComponents(entityId);
                }

                for (int i = 0; i < powerSystem.netCursor; i++)
                {
                    var network = powerSystem.netPool[i];
                    if (network != null && network.id == i)
                    {
                        var consumersToRemove = powerConRemoval[network.id];
                        foreach (var node in network.nodes)
                            node.consumers.RemoveAll(x => consumersToRemove.Contains(x));
                        network.consumers.RemoveAll(x => consumersToRemove.Contains(x));
                    }
                }

                if (factory.planet.factoryModel != null)
                    factory.planet.factoryModel.RefreshPowerConsumers();
            }

            void DeleteBelts()
            {
                var beltPool = cargoTraffic.beltPool;

                void TakeBackItemsOptimized()
                {
                    var cargoContainer = cargoTraffic.container;
                    var cargoPool = cargoContainer.cargoPool;
                    var cursor = cargoContainer.cursor;
                    for (int i = 0; i < cursor; i++)
                    {
                        ref var cargo = ref cargoPool[i];
                        if (cargo.item == 0)
                            continue;
                        takeBackCount[cargo.item] += cargo.stack;
                        takeBackInc[cargo.item] += cargo.inc;

                        cargo.stack = 0;
                        cargo.inc = 0;
                        cargo.item = 0;
                        cargoContainer.recycleIds[cargoContainer.recycleEnd & (cargoContainer.poolCapacity - 1)] = i;
                        cargoContainer.recycleEnd++;
                    }
                }

                TakeBackItemsOptimized();
                for (int i = 1; i < cargoTraffic.beltCursor; i++)
                {
                    ref var belt = ref beltPool[i];
                    var entityId = belt.entityId;

                    if (entityId == 0)
                        continue;
                    var entityData = factory.entityPool[belt.entityId];
                    // record the belt in the take back data 
                    takeBackCount[entityData.protoId]++;

                    // factory.RemoveEntityWithComponents(entityId);
                    // The above call is potentially too expensive,
                    // so we try to remove the component before letting it run
                    if (belt.id != 0)
                    {
                        cargoTraffic.RemoveBeltRenderer(i);
                        cargoTraffic.RemoveCargoPath(belt.segPathId);
                        belt.SetEmpty();
                        cargoTraffic.beltRecycle[cargoTraffic.beltRecycleCursor] = i;
                        cargoTraffic.beltRecycleCursor++;
                    }

                    factory.RemoveEntityWithComponents(entityId);
                }
            }

            var stopwatch = new HighStopwatch();
            stopwatch.Begin();
            DeleteBelts();
            DeleteInserters();
            foreach (var kvp in takeBackCount)
                player.TryAddItemToPackage(kvp.Key, kvp.Value, takeBackInc[kvp.Key], true);
            Debug($"Took {stopwatch.duration} to fast delete belts");
        }
    }
}