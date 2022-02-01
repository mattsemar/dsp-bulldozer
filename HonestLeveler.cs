using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using static Bulldozer.Log;

namespace Bulldozer
{
    public class LevelerWorkItem
    {
        public Vector3 center;
        public int reformIndex;
    }

    public enum HonestLevelerEndState
    {
        COMPLETE,
        ENDED_EARLY,
        IN_PROGRESS,
        ENDED_ERROR
    }

    /// <summary>Does planet-wide leveling, honoring player soil pile</summary>
    public class HonestLeveler
    {
        private static HonestLeveler _instance;
        private readonly Stopwatch _clearStopWatch = new Stopwatch();
        private readonly PlanetFactory _factory;
        private readonly List<LevelerWorkItem> _levelerWork = new List<LevelerWorkItem>();
        private readonly Player _player;
        private ItemDestructionPhase _previousPhase = ItemDestructionPhase.Done;
        private bool _running;

        private HonestLeveler(PlanetFactory factory, Player player)
        {
            _factory = factory;
            _player = player;
        }

        public static void Init(PlanetFactory factory, Player player)
        {
            Stop();
            _instance = new HonestLeveler(factory, player);
            _instance.Start();
        }

        public static int RemainingTaskCount() => _instance == null ? 0 : _instance._levelerWork.Count;

        public static HonestLevelerEndState DoWorkItems(PlanetFactory factory)
        {
            if (_instance != null && _instance._levelerWork.Count > 0)
            {
                if (_instance._factory != factory)
                {
                    logger.LogWarning("Factory has changed since task start. Halting");
                    Stop();
                    return HonestLevelerEndState.ENDED_ERROR;
                }

                return _instance.Update();
            }

            return HonestLevelerEndState.IN_PROGRESS;
        }

        public static bool IsRunning() => _instance != null && _instance._running && _instance._levelerWork.Count > 0;

        public static void Stop()
        {
            if (_instance != null)
            {
                _instance._running = false;
            }

            _instance?._levelerWork.Clear();
            _instance = null;
        }

        private HonestLevelerEndState Update()
        {
            if (_levelerWork.Count > 0)
            {
                var countDown = PluginConfig.workItemsPerFrame.Value;
                while (countDown-- > 0)
                {
                    if (_levelerWork.Count > 0)
                    {
                        var task = _levelerWork[0];
                        _levelerWork.RemoveAt(0);
                        var result = ProcessWorkItem(task);
                        if (result == HonestLevelerEndState.ENDED_EARLY)
                        {
                            _running = false;
                            _levelerWork.Clear();
                            return result;
                        }
                    }
                }

                return HonestLevelerEndState.IN_PROGRESS;
            }

            if (_running)
            {
                _clearStopWatch.Stop();
                var elapsedMs = _clearStopWatch.ElapsedMilliseconds;
                logger.LogInfo($"leveler {elapsedMs} ms to complete");
                LogAndPopupMessage("Done leveling");
                _running = false;
                return HonestLevelerEndState.COMPLETE;
            }

            return HonestLevelerEndState.COMPLETE;
        }

        private HonestLevelerEndState ProcessWorkItem(LevelerWorkItem item)
        {
            var radius = 0.990946f * 10;
            try
            {
                //
                var snapArgs = new SnapArgs();
                Vector3 outpos;
                var cursorPointCount = _factory.planet.aux.ReformSnap(item.center, 10, 1, 1, snapArgs.points, snapArgs.indices, _factory.platformSystem,
                    out var center);


                var flattenTerrainReform = _factory.ComputeFlattenTerrainReform(snapArgs.points, center, radius, cursorPointCount);
                if (flattenTerrainReform > _player.sandCount)
                {
                    LogAndPopupMessage("Ending task, not enough soil pile");
                    return HonestLevelerEndState.ENDED_EARLY;
                }

                _factory.FlattenTerrainReform(center, radius, 10, false);
                _player.SetSandCount(_player.sandCount - flattenTerrainReform);
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                logger.LogError(e.StackTrace);
                return HonestLevelerEndState.ENDED_ERROR;
            }

            return HonestLevelerEndState.IN_PROGRESS;
        }

        private void Start()
        {
            LogAndPopupMessage("Leveling planet");
            _clearStopWatch.Start();
            _running = true;
            if (_factory == null)
            {
                logger.LogDebug("no current factory found");
                return;
            }

            AddWorkItems();
        }

        private void AddWorkItems()
        {
            // var reformTool  = _f
            var platformSystem = _factory.platformSystem;
            if (platformSystem.IsAllReformed())
            {
                return;
            }

            int cursorPointCount;
            var radius = 0.990946f * 10;
            var neededSoilPile = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (var lat = -89.9f; lat < 90; lat += 0.25f)
            {
                for (var lon = -180f; lon < 180; lon += 0.25f)
                {
                    var position = GeoUtil.LatLonToPosition(lat, 0, _factory.planet.radius);
                    position.Normalize();
                    var reformIndexForPosition = platformSystem.GetReformIndexForPosition(position);

                    _levelerWork.Add(new LevelerWorkItem
                    {
                        center = position,
                        reformIndex = reformIndexForPosition
                    });
                }
            }

            var mainPlayerPosition = GameMain.mainPlayer.position;

            _levelerWork.Sort((item1, item2) => Vector3.Distance(mainPlayerPosition, item1.center).CompareTo(Vector3.Distance(mainPlayerPosition, item2.center)));
        }
    }
}