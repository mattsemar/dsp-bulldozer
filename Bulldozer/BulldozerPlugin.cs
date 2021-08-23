using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Bulldozer
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("DSPGAME.exe")]
    [SuppressMessage("ReSharper", "LocalizableElement")]
    public class BulldozerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "semarware.dysonsphereprogram.bulldozer";
        public const string PluginName = "Bulldozer";
        public const string PluginVersion = "1.0.4";

        public static ManualLogSource logger;
        private static Thread bulldozerActionThread;

        private static readonly List<ClearFactoryWorkItem> bulldozerWork = new List<ClearFactoryWorkItem>();
        private static readonly List<PaveWorkItem> flattenWorkList = new List<PaveWorkItem>();

        private static ItemDestructionPhase previousPhase = ItemDestructionPhase.Done;

        private static object destroyFactorMutexLock = new object();
        private static Stopwatch clearStopWatch;


        public static BulldozerPlugin Instance;
        private static bool _genRequestFlag;
        private bool _flattenRequested;
        private Harmony harmony;

        private UIElements ui;

        // Awake is called once when both the game and the plugin are loaded
        private void Awake()
        {
            logger = Logger;
            Instance = this;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(BulldozerPlugin));
            Debug.Log("Bulldozer Plugin Loaded");
        }

        private void Update()
        {
            DoBulldozeUpdate();
            DoPaveUpdate();
        }


        private void OnDestroy()
        {
            // For ScriptEngine hot-reloading
            bulldozerWork?.Clear();
            flattenWorkList?.Clear();
            if (ui != null)
            {
                ui.Unload();
                Destroy(ui);
            }

            harmony.UnpatchSelf();
            bulldozerActionThread = null;
        }


        private void ClearFactory()
        {
            var phase = ItemDestructionPhase.Inserters;
            var currentPlanetFactory = GameMain.mainPlayer.planetData?.factory;
            if (currentPlanetFactory == null)
            {
                logger.LogDebug($"no current factory found");
                return;
            }

            var countsByPhase = new Dictionary<ItemDestructionPhase, int>();

            var scheduledItemIds = new HashSet<int>();
            var itemIdsToProcess = new List<int>();
            for (var i = 1; i < currentPlanetFactory.entityCursor; i++)
                if (currentPlanetFactory.entityPool[i].protoId > 0)
                    itemIdsToProcess.Add(i);

            var mainPlayerPosition = GameMain.mainPlayer.position;
            itemIdsToProcess.Sort((item1, item2) =>
            {
                var pos1 = currentPlanetFactory.entityPool[item1].pos;
                var pos2 = currentPlanetFactory.entityPool[item2].pos;
                return Vector3.Distance(mainPlayerPosition, pos1).CompareTo(Vector3.Distance(mainPlayerPosition, pos2));
            });

            while (phase < ItemDestructionPhase.Done)
            {
                foreach (var itemId in itemIdsToProcess)
                    if (currentPlanetFactory.entityPool[itemId].protoId > 0)
                    {
                        if (scheduledItemIds.Contains(itemId)) continue;

                        var itemMatchesPhase = false;
                        switch (phase)
                        {
                            case ItemDestructionPhase.Inserters:
                                itemMatchesPhase = currentPlanetFactory.entityPool[itemId].inserterId > 0;
                                break;
                            case ItemDestructionPhase.Assemblers:
                                itemMatchesPhase = currentPlanetFactory.entityPool[itemId].assemblerId > 0;
                                break;
                            case ItemDestructionPhase.Belts:
                                itemMatchesPhase = currentPlanetFactory.entityPool[itemId].beltId > 0;
                                break;
                            case ItemDestructionPhase.Stations:
                                itemMatchesPhase = currentPlanetFactory.entityPool[itemId].stationId > 0;
                                break;
                            case ItemDestructionPhase.Other:
                                itemMatchesPhase = true;
                                break;
                        }

                        if (itemMatchesPhase)
                        {
                            if (!countsByPhase.ContainsKey(phase)) countsByPhase[phase] = 0;

                            countsByPhase[phase]++;
                            scheduledItemIds.Add(itemId);
                            bulldozerWork.Add(
                                new ClearFactoryWorkItem
                                {
                                    Phase = phase,
                                    Player = GameMain.mainPlayer,
                                    ItemId = itemId,
                                    PlanetFactory = currentPlanetFactory
                                });
                        }
                    }

                phase++;
            }

            Logger.LogDebug($"added {bulldozerWork.Count} items to delete {countsByPhase}");
        }

        public static bool RemoveBuild(Player player, PlanetFactory factory, int objId)
        {
            try
            {
                var num = -objId;

                ItemProto itemProto = null;
                if (objId > 0) itemProto = LDB.items.Select(factory.entityPool[objId].protoId);

                if (num > 0) itemProto = LDB.items.Select(factory.prebuildPool[num].protoId);

                var itemId = itemProto == null ? 0 : itemProto.ID;
                if (itemId > 0) factory.DismantleFinally(player, objId, ref itemId);

                return true;
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                logger.LogError(e.StackTrace);
                return false;
            }
        }

        private void DoPaveUpdate()
        {
            if (flattenWorkList.Count > 0)
            {
                var countDown = Math.Min(flattenWorkList.Count, 4);
                while (countDown-- > 0)
                {
                    var flattenTask = flattenWorkList[0];
                    flattenWorkList.RemoveAt(0);
                    ui.countText.text = $"{flattenWorkList.Count}";

                    var point = flattenTask.Position;
                    var planetFactory = flattenTask.Factory;
                    if (GameMain.mainPlayer?.planetId != planetFactory.planetId)
                    {
                        logger.LogDebug($"player not on planet for work task");
                        continue;
                    }

                    var reformTool = flattenTask.Player.controller.actionBuild.reformTool;

                    planetFactory.FlattenTerrain(point, Quaternion.identity,
                        new Bounds(Vector3.zero, new Vector3(100f, 100f, 100f)), removeVein: reformTool.buryVeins,
                        lift: true);
                    planetFactory.FlattenTerrainReform(point, 0.991f * 10f, 10, reformTool.buryVeins);
                    if (flattenWorkList.Count == 0) planetFactory.planet.landPercentDirty = true;
                }

                if (flattenWorkList.Count % 100 == 0)
                {
                    logger.LogDebug($"flattened point, {flattenWorkList.Count} remain");
                }
            }
            else if (_flattenRequested)
            {
                logger.LogDebug($"repaint requested");
                SetFlattenRequestedFlag(false);
                var maxReformCount = GameMain.mainPlayer?.factory?.platformSystem.maxReformCount;
                var platformSystem = GameMain.mainPlayer?.factory?.platformSystem;
                var actionBuild = GameMain.mainPlayer?.controller.actionBuild;
                if (maxReformCount == null || platformSystem == null || actionBuild == null) return;
                for (var index = 0; index < maxReformCount; ++index)
                    try
                    {
                        var reformToolBrushColor = actionBuild.reformTool.brushColor;
                        var reformToolBrushType = actionBuild.reformTool.brushType;
                        platformSystem.SetReformType(index, reformToolBrushType);
                        platformSystem.SetReformColor(index, reformToolBrushColor);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning($"exception while applying coat of paint {e.Message}");
                        logger.LogWarning(e.StackTrace);
                    }

                if (ui.DrawEquatorField)
                {
                    PaintGuideMarkings(platformSystem, actionBuild);
                }
            }
        }

        private void PaintGuideMarkings(PlatformSystem platformSystem, PlayerAction_Build playerActionBuild)
        {
            var planetRadius = platformSystem.planet.radius;
            // meridians
            for (var lat = -89.9f; lat < 90; lat += 0.25f)
            {
                for (var lonOffset = -1; lonOffset < 1; lonOffset++)
                {
                    for (var merdidianIndex = 0; merdidianIndex < 4; merdidianIndex++)
                    {
                        var lon = 0.25f * lonOffset + merdidianIndex * 90f;
                        var position = LatLonToPosition(lat, lon, planetRadius);

                        var reformIndexForPosition = platformSystem.GetReformIndexForPosition(position);
                        if (reformIndexForPosition >= platformSystem.reformData.Length)
                        {
                            logger.LogWarning($"reformIndex = {reformIndexForPosition} is out of bounds, apparently");
                            continue;
                        }

                        try
                        {
                            platformSystem.SetReformType(reformIndexForPosition, 1);
                            platformSystem.SetReformColor(reformIndexForPosition, 12);
                        }
                        catch (Exception e)
                        {
                            logger.LogWarning($"exception while setting reform at index {reformIndexForPosition} max={platformSystem.reformData.Length} {e.Message}");
                        }
                    }
                }
            }

            // equator stripe
            for (var lon = -179.9f; lon < 180; lon += 0.25f)
            {
                for (var latOffset = -1; latOffset < 1; latOffset++)
                {
                    var position = LatLonToPosition(0f + latOffset * 0.25f, lon, planetRadius);

                    var reformIndexForPosition = platformSystem.GetReformIndexForPosition(position);
                    if (reformIndexForPosition >= platformSystem.reformData.Length)
                    {
                        logger.LogWarning($"reformIndex = {reformIndexForPosition} is out of bounds, apparently");
                        continue;
                    }

                    try
                    {
                        platformSystem.SetReformType(reformIndexForPosition, 1);
                        platformSystem.SetReformColor(reformIndexForPosition, 7);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning($"exception while setting reform at index {reformIndexForPosition} max={platformSystem.reformData.Length} {e.Message}");
                    }
                }
            }
        }

        private void DoBulldozeUpdate()
        {
            if (bulldozerWork.Count > 0)
            {
                var countDown = 5;
                while (countDown-- > 0)
                    if (bulldozerWork.Count > 0)
                    {
                        var bulldozeTask = bulldozerWork[0];
                        bulldozerWork.RemoveAt(0);
                        if (bulldozeTask.Phase != previousPhase)
                        {
                            UIRealtimeTip.Popup($"Starting phase {bulldozeTask.Phase} {bulldozeTask.ItemId}");
                            logger.LogDebug(
                                $"next phase started {Enum.GetName(typeof(ItemDestructionPhase), bulldozeTask.Phase)}");
                            previousPhase = bulldozeTask.Phase;
                        }

                        RemoveBuild(bulldozeTask.Player, bulldozeTask.PlanetFactory, bulldozeTask.ItemId);
                    }
            }
            else if (clearStopWatch != null && clearStopWatch.IsRunning)
            {
                clearStopWatch.Stop();
                var elapsedMs = clearStopWatch.ElapsedMilliseconds;
                logger.LogInfo($"bulldozer {elapsedMs} ms to complete");
            }
        }

        private static void ClearPlanetThread()
        {
            clearStopWatch = Stopwatch.StartNew();
            logger.LogInfo("Bulldozer thread started.");

            Instance.ClearFactory();
            Monitor.Enter(destroyFactorMutexLock);
            _genRequestFlag = false;
            Monitor.Exit(destroyFactorMutexLock);
        }
        
        private void InvokePavePlanet()
        {
            var mainPlayer = GameMain.mainPlayer;
            var mainPlayerFactory = mainPlayer.factory;
            if (mainPlayerFactory == null) return;

            var reformTool = mainPlayer.controller.actionBuild;
            var platformSystem = mainPlayerFactory.platformSystem;
            if (platformSystem == null) return;

            if (platformSystem.reformData == null)
            {
                platformSystem.EnsureReformData();
            }

            if (platformSystem.reformData == null)
            {
                logger.LogWarning($"no reform data skipping pave");
                return;
            }

            var planet = mainPlayerFactory.planet;
            flattenWorkList.Clear();
            var tmpFlattenWorkList = new List<PaveWorkItem>();
            platformSystem.EnsureReformData();
            for (var lat = -89; lat < 90; lat += 9)
            {
                for (var lon = -179; lon < 180; lon += 9)
                {
                    var position = LatLonToPosition(lat, lon, planet.radius);

                    var reformIndexForPosition = platformSystem.GetReformIndexForPosition(position);
                    if (reformIndexForPosition >= platformSystem.reformData.Length)
                    {
                        logger.LogWarning($"reformIndex = {reformIndexForPosition} is out of bounds, apparently");
                        continue;
                    }

                    // if (!platformSystem.IsTerrainReformed(platformSystem.GetReformType(reformIndexForPosition)))
                    // {
                        tmpFlattenWorkList.Add(
                            new PaveWorkItem
                            {
                                Position = position,
                                Player = mainPlayer,
                                Factory = mainPlayerFactory
                            });
                    // }
                }
            }

            foreach (var vein in planet.factory.veinPool)
            {
                if (vein.id > 0)
                {
                    tmpFlattenWorkList.Add(
                        new PaveWorkItem
                        {
                            Position = vein.pos,
                            Player = mainPlayer,
                            Factory = mainPlayerFactory
                        });
                    // logger.LogDebug($"{PositionToLatLonString(vein.pos)}");
                }
            }

            tmpFlattenWorkList.Sort((item1, item2) =>
            {
                var distance1 = Vector3.Distance(mainPlayer.position, item1.Position);
                var distance2 = Vector3.Distance(mainPlayer.position, item2.Position);
                return distance1.CompareTo(distance2);
            });

            flattenWorkList.AddRange(tmpFlattenWorkList);
            SetFlattenRequestedFlag(true);
        }

        private void SetFlattenRequestedFlag(bool value)
        {
            logger.LogDebug($"setting flatten requested to {value}");
            _flattenRequested = value;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIBuildMenu), "OnCategoryButtonClick")]
        public static void UIBuildMenu_OnCategoryButtonClick_Postfix(UIBuildMenu __instance)
        {
            var uiBuildMenu = __instance;
            if (logger == null || Instance == null)
            {
                Console.WriteLine($"Not initialized, either logger ({logger}) or Instance ({Instance}) is null");
                return;
            }

            if (uiBuildMenu.currentCategory != 9)
            {
                if (Instance.ui != null)
                {
                    Instance.ui.Hide();
                }

                return;
            }

            if (Instance.ui == null)
            {
                Instance.InitUi(uiBuildMenu);
            }
            else
            {
                Instance.ui.Show();
            }
        }

        private void InitUi(UIBuildMenu uiBuildMenu)
        {
            GameObject environmentModificationContainer = GameObject.Find("UI Root/Overlay Canvas/In Game/Function Panel/Build Menu/child-group");
            var containerRect = environmentModificationContainer.GetComponent<RectTransform>();
            var button1 = GameObject.Find("UI Root/Overlay Canvas/In Game/Function Panel/Build Menu/child-group/button-1");
            ui = containerRect.gameObject.AddComponent<UIElements>();
            UIElements.logger = logger;
            if (containerRect == null || button1 == null)
            {
                return;
            }

            ui.AddBulldozeComponents(containerRect, uiBuildMenu, button1, bt =>
            {
                InvokePavePlanet();
                UIRealtimeTip.Popup("Paving");
            });
        }

        private static Vector3 LatLonToPosition(float lat, float lon, float earthRadius)
        {
            var latRad = Math.PI / 180 * lat;
            var lonRad = Math.PI / 180 * lon;
            var x = (float)(Math.Cos(latRad) * Math.Cos(lonRad));
            var z = (float)(Math.Cos(latRad) * Math.Sin(lonRad));
            var y = (float)Math.Sin(latRad);
            return new Vector3(x, y, z).normalized * earthRadius;
        }
    }
}