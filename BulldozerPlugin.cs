using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        public const string PluginVersion = "1.0.10";

        public static ManualLogSource logger;

        private static readonly List<ClearFactoryWorkItem> BulldozerWork = new List<ClearFactoryWorkItem>();
        private static readonly List<PaveWorkItem> FlattenWorkList = new List<PaveWorkItem>();

        private static ItemDestructionPhase previousPhase = ItemDestructionPhase.Done;

        private static Stopwatch clearStopWatch;

        public static BulldozerPlugin Instance;
        private bool _flattenRequested;
        private Harmony harmony;

        private UIElements ui;


        // Awake is called once when both the game and the plugin are loaded
        private void Awake()
        {
            logger = Logger;
            GuideMarker.logger = logger;
            Instance = this;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(BulldozerPlugin));
            PluginConfig.InitConfig(Config);
            Debug.Log("Bulldozer Plugin Loaded");
        }

        private void Update()
        {
            DoBulldozeUpdate();
            DoPaveUpdate();
            if (ui != null && ui.countText != null)
                ui.countText.text = $"{FlattenWorkList.Count + BulldozerWork.Count}";
        }


        private void OnDestroy()
        {
            // For ScriptEngine hot-reloading
            BulldozerWork?.Clear();
            FlattenWorkList?.Clear();
            if (ui != null)
            {
                ui.Unload();
            }

            harmony.UnpatchSelf();
        }


        private void ClearFactory()
        {
            clearStopWatch = new Stopwatch();
            clearStopWatch.Start();
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
                            BulldozerWork.Add(
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

            Logger.LogDebug($"added {BulldozerWork.Count} items to delete {countsByPhase}");
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
            if (FlattenWorkList.Count > 0)
            {
                var countDown = Math.Min(Math.Min(FlattenWorkList.Count, PluginConfig.workItemsPerFrame.Value), 10);
                while (countDown-- > 0)
                {
                    var flattenTask = FlattenWorkList[0];
                    FlattenWorkList.RemoveAt(0);

                    var point = flattenTask.Position;
                    var planetFactory = flattenTask.Factory;
                    if (GameMain.mainPlayer?.planetId != planetFactory.planetId)
                    {
                        logger.LogDebug($"player not on planet for work task");
                        continue;
                    }

                    var reformTool = flattenTask.Player.controller.actionBuild.reformTool;
                    bool bury = PluginConfig.buryVeinMode.Value == BuryVeinMode.Tool ? reformTool.buryVeins : PluginConfig.buryVeinMode.Value == BuryVeinMode.Bury;

                    try
                    {
                        planetFactory.FlattenTerrain(point, Quaternion.identity,
                            new Bounds(Vector3.zero, new Vector3(100f, 100f, 100f)), removeVein: bury,
                            lift: true);
                        planetFactory.FlattenTerrainReform(point, 0.991f * 10f, 10, bury);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning($"exception while paving {e.Message} {e.StackTrace}");
                    }

                    if (FlattenWorkList.Count == 0) planetFactory.planet.landPercentDirty = true;
                }

                if (FlattenWorkList.Count % 100 == 0)
                {
                    logger.LogDebug($"flattened point, {FlattenWorkList.Count} remain");
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
                // reform brush type of 7 is foundation with no decoration
                // brush type 2 is decorated, but not painted
                // 1 seems to be paint mode
                int brushType = 1;
                switch (PluginConfig.foundationDecorationMode.Value)
                {
                    case FoundationDecorationMode.Tool:
                        brushType = actionBuild.reformTool.brushType;
                        break;
                    case FoundationDecorationMode.Paint:
                        brushType = 1;
                        break;
                    case FoundationDecorationMode.Decorate:
                        brushType = 2;
                        break;
                    case FoundationDecorationMode.Clear:
                        brushType = 7;
                        break;
                    default:
                        logger.LogWarning($"unexpected brush type requested {PluginConfig.foundationDecorationMode.Value}");
                        break;
                }

                for (var index = 0; index < maxReformCount; ++index)
                    try
                    {
                        var reformToolBrushColor = actionBuild.reformTool.brushColor;
                        platformSystem.SetReformType(index, brushType);
                        platformSystem.SetReformColor(index, reformToolBrushColor);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning($"exception while applying coat of paint {e.Message}");
                        logger.LogWarning(e.StackTrace);
                    }

                if (PluginConfig.addGuideLines.Value)
                {
                    PaintGuideMarkings(platformSystem);
                }

                UIRealtimeTip.Popup("Bulldozer done");
            }
        }

        private void PaintGuideMarkings(PlatformSystem platformSystem)
        {
            GuideMarkTypes guideMarkTypes = GuideMarkTypes.None;

            if (PluginConfig.addGuideLinesEquator.Value)
            {
                guideMarkTypes |= GuideMarkTypes.Equator;
            }

            if (PluginConfig.addGuideLinesMeridian.Value)
            {
                guideMarkTypes |= GuideMarkTypes.Meridian;
            }

            GuideMarker.AddGuideMarks(platformSystem, guideMarkTypes);
        }

        private void DoBulldozeUpdate()
        {
            if (BulldozerWork.Count > 0)
            {
                var countDown = PluginConfig.workItemsPerFrame.Value;
                while (countDown-- > 0)
                    if (BulldozerWork.Count > 0)
                    {
                        var bulldozeTask = BulldozerWork[0];
                        BulldozerWork.RemoveAt(0);
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

        private void InvokePavePlanet()
        {
            var mainPlayer = GameMain.mainPlayer;
            var mainPlayerFactory = mainPlayer.factory;
            if (mainPlayerFactory == null) return;

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
            FlattenWorkList.Clear();
            var tmpFlattenWorkList = new List<PaveWorkItem>();
            platformSystem.EnsureReformData();
            var adjustedOffset = (int)GuideMarker.GetCoordLineOffset(planet, 9);
            if (adjustedOffset != 9)
            {
                logger.LogDebug($"using coord offset of {adjustedOffset} due to planet size == {planet.radius}");
            }

            for (var lat = -89; lat < 90; lat += adjustedOffset)
            {
                for (var lon = -179; lon < 180; lon += adjustedOffset)
                {
                    var position = GuideMarker.LatLonToPosition(lat, lon, planet.radius);

                    var reformIndexForPosition = platformSystem.GetReformIndexForPosition(position);
                    if (reformIndexForPosition >= platformSystem.reformData.Length)
                    {
                        logger.LogWarning($"reformIndex = {reformIndexForPosition} is out of bounds, apparently");
                        continue;
                    }

                    if (!platformSystem.IsTerrainReformed(platformSystem.GetReformType(reformIndexForPosition)) || PluginConfig.repaveAll.Value)
                    {
                        tmpFlattenWorkList.Add(
                            new PaveWorkItem
                            {
                                Position = position,
                                Player = mainPlayer,
                                Factory = mainPlayerFactory
                            });
                    }
                }
            }

            if (PluginConfig.repaveAll.Value)
            {
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
                    }
                }
            }

            tmpFlattenWorkList.Sort((item1, item2) =>
            {
                var distance1 = Vector3.Distance(mainPlayer.position, item1.Position);
                var distance2 = Vector3.Distance(mainPlayer.position, item2.Position);
                return distance1.CompareTo(distance2);
            });

            FlattenWorkList.AddRange(tmpFlattenWorkList);
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
                if (FlattenWorkList.Count > 0 || BulldozerWork.Count > 0)
                {
                    FlattenWorkList.Clear();
                    BulldozerWork.Clear();
                    UIRealtimeTip.Popup("Stopping...");
                    ui.countText.text = $"{FlattenWorkList.Count}";
                }
                else
                {
                    if (PluginConfig.destroyFactoryAssemblers.Value)
                    {
                        UIRealtimeTip.Popup("Bulldozing factory belts, inserters, assemblers, labs, stations, you name it");
                        ClearFactory();
                    }
                    else
                    {
                        InvokePavePlanet();
                        UIRealtimeTip.Popup("Adding foundation");
                    }
                }
            });
        }
    }
}