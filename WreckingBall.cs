using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using static Bulldozer.Log;

namespace Bulldozer
{
    public class WreckingBallWorkItem
    {
        public int ItemId;
        public ItemDestructionPhase Phase;
    }

    public enum ItemDestructionPhase
    {
        Inserters = 0,
        Belts = 1,
        Assemblers = 2,
        Stations = 3,
        Other = 4,
        Done = 5
    }

    /// <summary>Does planet-wide factory destruction</summary>
    public class WreckingBall
    {
        private static WreckingBall _instance;
        private readonly Stopwatch _clearStopWatch = new();
        private readonly PlanetFactory _factory;
        private readonly Player _player;
        private readonly Queue<WreckingBallWorkItem> _wreckingBallWork = new();

        private ItemDestructionPhase _previousPhase = ItemDestructionPhase.Done;

        private int _itemsDestroyed;
        private long _msTakenTotal;
        private int _updatesRun;
        private bool _running;

        private WreckingBall(PlanetFactory factory, Player player)
        {
            _factory = factory;
            _player = player;
        }

        public static void Init(PlanetFactory factory, Player player)
        {
            Stop();
            _instance = new WreckingBall(factory, player);
            _instance.Start();
        }

        public static int RemainingTaskCount() => _instance == null ? 0 : _instance._wreckingBallWork.Count;

        public static void DoWorkItems(PlanetFactory factory)
        {
            if (_instance != null)
            {
                if (_instance._factory != factory)
                {
                    logger.LogWarning($"Factory has changed since task start. Halting");
                    Stop();
                    return;
                }

                _instance.Update();
            }
        }

        public static bool IsRunning() => _instance != null && _instance._running;

        public static void Stop()
        {
            _instance?.LogRateMessage();
            _instance?._wreckingBallWork.Clear();
            _instance = null;
        }

        private void Update()
        {
            if (_wreckingBallWork.Count > 0)
            {
                WreckingBallWorkItem task = null;
                try
                {
                    var startTime = DateTime.Now;
                    int destructionCounter = 0;
                    while (_wreckingBallWork.Count > 0)
                    {
                        task = _wreckingBallWork.Dequeue();
                        if (task.Phase != _previousPhase)
                        {
                            LogAndPopupMessage($"Starting phase {task.Phase} {task.ItemId}");
                            logger.LogDebug(
                                $"next phase started {Enum.GetName(typeof(ItemDestructionPhase), task.Phase)}");
                            _previousPhase = task.Phase;
                        }

                        RemoveBuild(task.ItemId);
                        destructionCounter++;
                        if ((DateTime.Now - startTime).TotalMilliseconds > PluginConfig.factoryTeardownRunTimePerFrame.Value)
                            break;
                    }

                    _itemsDestroyed += destructionCounter;
                    _msTakenTotal += (long)(DateTime.Now - startTime).TotalMilliseconds;
                    _updatesRun++;
                }

                catch (Exception e)
                {
                    Warn($"got exception while removing component. Re-adding task to front of queue {e.Message}");
                    lock (_wreckingBallWork)
                    {
                        _wreckingBallWork.Enqueue(task);
                    }
                }

                if (Time.frameCount % 60 * 10 * 10 == 0)
                {
                    LogRateMessage();
                }
            }

            if (_running && _wreckingBallWork.Count < 1)
            {
                _clearStopWatch.Stop();
                var elapsedMs = _clearStopWatch.ElapsedMilliseconds;
                var tearDownMode = PluginConfig.useActionBuildTearDown.Value ? "actionBuild.DoDismantleObject mode" : "RemoveEntityWithComponents mode";
                logger.LogInfo($"wreckingBall {elapsedMs} ms to complete in {tearDownMode}, {_updatesRun} frames");
                LogAndPopupMessage("Done destroying factory");
                LogRateMessage();
                _running = false;
            }
        }

        private void LogRateMessage()
        {
            if (_updatesRun == 0 || _msTakenTotal == 0 || _clearStopWatch.ElapsedMilliseconds < 1)
                return;
            var avgDestroyedPerUpdate = _itemsDestroyed / _updatesRun;
            var avgMSPerUpdate = _msTakenTotal / _updatesRun;
            var rate = 1000 * _itemsDestroyed / (double)_msTakenTotal;
            var absoluteRate = 1000 * _itemsDestroyed / (_clearStopWatch.ElapsedMilliseconds);
            logger.LogInfo(
                $"(perFrameValue={PluginConfig.factoryTeardownRunTimePerFrame.Value}) Destroyed: {_itemsDestroyed} in {_clearStopWatch.ElapsedMilliseconds / 1000} seconds over {_updatesRun} updates. " +
                $"Average destroyed per update {avgDestroyedPerUpdate}. Average time per update (aka lag added) {avgMSPerUpdate}. \r\n" +
                $"Local rate of destruction: {rate} entities / second\r\n" +
                $"Absolute rate of destruction: {absoluteRate} entities / second");
        }

        private void RemoveBuild(int objId)
        {
            if (PluginConfig.useActionBuildTearDown.Value)
            {
                _player.controller.actionBuild.DoDismantleObject(objId);
                return;
            }

            if (objId > 0)
            {
                _player.factory.RemoveEntityWithComponents(objId);
            }
            else if (objId < 0)
            {
                _player.factory.RemovePrebuildWithComponents(-objId);
            }
        }

        private void Start()
        {
            LogAndPopupMessage("Bulldozing factory belts, inserters, assemblers, labs, stations, you name it");
            _clearStopWatch.Start();
            _msTakenTotal = 0;
            _updatesRun = 0;
            _itemsDestroyed = 0;
            if (PluginConfig.featureFastDelete.Value)
                RaptorFastDelete.Execute();
            var phase = ItemDestructionPhase.Inserters;
            if (_factory == null)
            {
                logger.LogDebug($"no current factory found");
                return;
            }

            var countsByPhase = new Dictionary<ItemDestructionPhase, int>();

            var scheduledItemIds = new HashSet<int>();
            var itemIdsToProcess = new List<int>();
            for (var i = 1; i < _factory.entityCursor; i++)
            {
                if (_factory.entityPool[i].protoId > 0)
                {
                    itemIdsToProcess.Add(i);
                }
            }

            while (phase < ItemDestructionPhase.Done)
            {
                foreach (var itemId in itemIdsToProcess)
                {
                    if (_factory.entityPool[itemId].protoId > 0)
                    {
                        if (scheduledItemIds.Contains(itemId))
                        {
                            continue;
                        }

                        var itemMatchesPhase = false;
                        switch (phase)
                        {
                            case ItemDestructionPhase.Inserters:
                                itemMatchesPhase = _factory.entityPool[itemId].inserterId > 0;
                                break;
                            case ItemDestructionPhase.Assemblers:
                                itemMatchesPhase = _factory.entityPool[itemId].assemblerId > 0;
                                break;
                            case ItemDestructionPhase.Belts:
                                itemMatchesPhase = _factory.entityPool[itemId].beltId > 0;
                                break;
                            case ItemDestructionPhase.Stations:
                                itemMatchesPhase = _factory.entityPool[itemId].stationId > 0;
                                break;
                            case ItemDestructionPhase.Other:
                                itemMatchesPhase = true;
                                break;
                        }

                        bool skipItem = PluginConfig.skipDestroyingStations.Value && _factory.entityPool[itemId].stationId > 0;
                        if (itemMatchesPhase && !skipItem)
                        {
                            if (!countsByPhase.ContainsKey(phase))
                            {
                                countsByPhase[phase] = 0;
                            }

                            countsByPhase[phase]++;

                            scheduledItemIds.Add(itemId);
                            _wreckingBallWork.Enqueue(
                                new WreckingBallWorkItem
                                {
                                    Phase = phase,
                                    ItemId = itemId
                                });
                        }
                    }
                }

                phase++;
            }

            AddTasksForBluePrintGhosts(_factory);
            _running = true;

            logger.LogDebug($"added {_wreckingBallWork.Count} items to delete {countsByPhase}");
        }

        public static int CountBuildGhosts(PlanetFactory currentPlanetFactory)
        {
            return currentPlanetFactory.prebuildPool.Count(prebuildData => prebuildData.id >= 1);
        }

        private void AddTasksForBluePrintGhosts(PlanetFactory currentPlanetFactory)
        {
            var ctr = 0;
            foreach (var prebuildData in currentPlanetFactory.prebuildPool)
            {
                if (prebuildData.id < 1)
                {
                    continue;
                }

                _wreckingBallWork.Enqueue(new WreckingBallWorkItem
                {
                    Phase = ItemDestructionPhase.Other,
                    ItemId = -prebuildData.id
                });
                ctr++;
            }

            if (ctr > 0)
            {
                LogAndPopupMessage($"Found {ctr} build ghosts");
            }
            else
            {
                logger.LogDebug($"no ghosts found");
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "ThrowTrash")]
        public static bool Player_ThrowTrash_Prefix()
        {
            if (_instance == null || !_instance._running)
            {
                return true;
            }

            if (PluginConfig.deleteFactoryTrash.Value)
            {
                return false;
            }

            return true;
        }

        // patch out that stupid "knock-0" sound that is played when trash is thrown
        [HarmonyPrefix]
        [HarmonyPatch(typeof(VFAudio), nameof(VFAudio.Create), new[]
        {
            typeof(string),
            typeof(Transform),
            typeof(Vector3),
            typeof(bool),
            typeof(int),
            typeof(int),
            typeof(long)
        })]
        public static bool VFAudio_Create_Prefix(string _name)
        {
            if (_instance is not { _running: true })
            {
                return true;
            }

            if (!string.Equals(_name, "knock-0"))
            {
                return true;
            }

            return false;
        }
    }
}