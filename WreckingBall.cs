using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
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
        private readonly Stopwatch _clearStopWatch = new Stopwatch();
        private readonly PlanetFactory _factory;
        private readonly Player _player;
        private readonly List<WreckingBallWorkItem> _wreckingBallWork = new List<WreckingBallWorkItem>();
        private ItemDestructionPhase _previousPhase = ItemDestructionPhase.Done;
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
            _instance?._wreckingBallWork.Clear();
            _instance = null;
        }

        private void Update()
        {
            if (_wreckingBallWork.Count > 0)
            {
                var countDown = PluginConfig.workItemsPerFrame.Value * 10; // takes less time so we can do more per tick
                while (countDown-- > 0)
                {
                    if (_wreckingBallWork.Count > 0)
                    {
                        var task = _wreckingBallWork[0];
                        _wreckingBallWork.RemoveAt(0);
                        if (task.Phase != _previousPhase)
                        {
                            LogAndPopupMessage($"Starting phase {task.Phase} {task.ItemId}");
                            logger.LogDebug(
                                $"next phase started {Enum.GetName(typeof(ItemDestructionPhase), task.Phase)}");
                            _previousPhase = task.Phase;
                        }

                        RemoveBuild(task.ItemId);
                    }
                }
            }
            if (_running && _wreckingBallWork.Count < 1)
            {
                _clearStopWatch.Stop();
                var elapsedMs = _clearStopWatch.ElapsedMilliseconds;
                logger.LogInfo($"wreckingBall {elapsedMs} ms to complete");
                LogAndPopupMessage("Done destroying factory");
                _running = false;
            }
        }

        private void RemoveBuild(int objId)
        {
            try
            {
                _player.controller.actionBuild.DoDismantleObject(objId);
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                logger.LogError(e.StackTrace);
            }
        }

        private void Start()
        {
            LogAndPopupMessage("Bulldozing factory belts, inserters, assemblers, labs, stations, you name it");
            _clearStopWatch.Start();
            _running = true;
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
                            _wreckingBallWork.Add(
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

                _wreckingBallWork.Add(new WreckingBallWorkItem
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
    }
}