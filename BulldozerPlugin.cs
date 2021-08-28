using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Resources = Bulldozer.Properties.Resources;

namespace Bulldozer
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("DSPGAME.exe")]
    public class BulldozerPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "semarware.dysonsphereprogram.bulldozer";
        public const string PluginName = "Bulldozer";
        public const string PluginVersion = "1.0.13";

        public static ManualLogSource logger;

        private static readonly List<ClearFactoryWorkItem> BulldozerWork = new List<ClearFactoryWorkItem>();
        private static readonly List<PaveWorkItem> FlattenWorkList = new List<PaveWorkItem>();

        private static ItemDestructionPhase previousPhase = ItemDestructionPhase.Done;

        private static Stopwatch clearStopWatch;

        public static BulldozerPlugin instance;
        private bool _flattenRequested;
        private Harmony _harmony;

        private UIElements _ui;

        // Awake is called once when both the game and the plugin are loaded
        private void Awake()
        {
            logger = Logger;
            GuideMarker.logger = logger;
            instance = this;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(BulldozerPlugin));
            PluginConfig.InitConfig(Config);
            Debug.Log("Bulldozer Plugin Loaded");
        }

        private void Update()
        {
            DoBulldozeUpdate();
            DoPaveUpdate();
            if (_ui != null && _ui.countText != null)
                _ui.countText.text = $"{FlattenWorkList.Count + BulldozerWork.Count}";
        }


        private void OnDestroy()
        {
            // For ScriptEngine hot-reloading
            BulldozerWork?.Clear();
            FlattenWorkList?.Clear();
            if (_ui != null)
            {
                _ui.Unload();
            }

            _harmony.UnpatchSelf();
        }


        private void ClearFactory()
        {
            UIRealtimeTip.Popup("Bulldozing factory belts, inserters, assemblers, labs, stations, you name it");

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

            AddTasksForBluePrintGhosts(currentPlanetFactory);

            Logger.LogDebug($"added {BulldozerWork.Count} items to delete {countsByPhase}");
        }

        private void AddTasksForBluePrintGhosts(PlanetFactory currentPlanetFactory)
        {
            foreach (var prebuildData in currentPlanetFactory.prebuildPool)
            {
                if (prebuildData.id < 1)
                    continue;
                BulldozerWork.Add(new ClearFactoryWorkItem
                {
                    Phase = ItemDestructionPhase.Other,
                    Player = GameMain.mainPlayer,
                    ItemId = -prebuildData.id,
                    PlanetFactory = currentPlanetFactory
                });
            }
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
                // ReSharper disable once InconsistentNaming
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

                UIRealtimeTip.Popup("Bulldozer done adding foundation");
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

            if (PluginConfig.addGuideLinesTropic.Value)
            {
                guideMarkTypes |= GuideMarkTypes.Tropic;
            }

            GuideMarker.AddGuideMarks(platformSystem, guideMarkTypes);
        }

        private void DoBulldozeUpdate()
        {
            if (BulldozerWork.Count > 0)
            {
                var countDown = PluginConfig.workItemsPerFrame.Value * 5; // takes less time so we can do more per tick
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
                UIRealtimeTip.Popup("Bulldozer done destroying factory");
            }
        }

        private void InvokePavePlanet()
        {
            UIRealtimeTip.Popup("Adding foundation");
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

        [HarmonyPostfix, HarmonyPatch(typeof(GameScenarioLogic),  "NotifyOnUnlockTech")]
        public static void GameScenarioLogic_NotifyOnUnlockTech_Postfix(int techId)
        {
            if (instance._ui == null || instance._ui.TechUnlockedState)
            {
                return;
            }
            TechProto techProto = LDB.techs.Select(techId);
            
            if (techProto.Level == 3 && techProto.Name.Contains("宇宙探索"))
            {
                instance._ui.TechUnlockedState = true;
            }
            Console.WriteLine($"tech proto not matched {JsonUtility.ToJson(techProto)}");
        }
        
        [HarmonyPostfix, HarmonyPatch(typeof(UIBuildMenu), "OnCategoryButtonClick")]
        public static void UIBuildMenu_OnCategoryButtonClick_Postfix(UIBuildMenu __instance)
        {
            var uiBuildMenu = __instance;
            if (logger == null || instance == null)
            {
                Console.WriteLine(Resources.BulldozerPlugin_Not_Initialized, logger, instance);
                return;
            }

            if (uiBuildMenu.currentCategory != 9)
            {
                if (instance._ui != null)
                {
                    instance._ui.Hide();
                }

                return;
            }

            if (instance._ui == null)
            {
                instance.InitUi(uiBuildMenu);
            }
            else
            {
                instance._ui.TechUnlockedState = instance.CheckResearchedTech();
                instance._ui.Show();
            }
        }

        private void InitUi(UIBuildMenu uiBuildMenu)
        {
            GameObject environmentModificationContainer = GameObject.Find("UI Root/Overlay Canvas/In Game/Function Panel/Build Menu/child-group");
            var containerRect = environmentModificationContainer.GetComponent<RectTransform>();
            var button1 = GameObject.Find("UI Root/Overlay Canvas/In Game/Function Panel/Build Menu/child-group/button-1");
            _ui = containerRect.gameObject.AddComponent<UIElements>();
            UIElements.logger = logger;
            if (containerRect == null || button1 == null)
            {
                return;
            }

            _ui.AddBulldozeComponents(containerRect, uiBuildMenu, button1, bt =>
            {
                StartCoroutine(InvokeAction(-1, () =>
                {
                    GameMain.mainPlayer.SetHandItems(0, 0);
                    GameMain.mainPlayer.controller.actionBuild.reformTool._Close();
                }));

                if (FlattenWorkList.Count > 0 || BulldozerWork.Count > 0)
                {
                    FlattenWorkList.Clear();
                    BulldozerWork.Clear();
                    UIRealtimeTip.Popup("Stopping...");
                    _ui.countText.text = $"{FlattenWorkList.Count}";
                }
                else
                {
                    var popupMessage = $"This action can take a bit to complete. Please confirm that you would like to do the following: ";
                    if (PluginConfig.destroyFactoryAssemblers.Value)
                    {
                        popupMessage += $"\nDestroy all factory machines (assemblers, belts, stations, etc)";
                        if (PluginConfig.flattenWithFactoryTearDown.Value)
                        {
                            popupMessage += $"\nAdd foundation to all locations on planet";
                        }
                    }
                    else
                    {
                        popupMessage += $"\nAdd foundation to all locations on planet";
                        if (PluginConfig.repaveAll.Value)
                        {
                            popupMessage += "\nRepave already paved areas";
                        }

                        if (PluginConfig.addGuideLines.Value)
                        {
                            var markingTypes = PluginConfig.addGuideLinesEquator.Value ? " equator " : "";
                            if (PluginConfig.addGuideLinesMeridian.Value)
                            {
                                markingTypes += "meridians";
                            }

                            popupMessage += $"\nAdd guide markings to certain points on planet ({markingTypes})";
                        }
                    }


                    UIMessageBox.Show("Bulldoze planet", popupMessage.Translate(),
                        "Ok", "Cancel", 0, InvokePluginCommands, () => { UIRealtimeTip.Popup($"Canceled"); });
                }
            });

            _ui.TechUnlockedState = CheckResearchedTech();
        }

        private bool CheckResearchedTech()
        {
            TechProto requiredTech = null;
            foreach (TechProto techProto in new List<TechProto>(LDB._techs.dataArray))
            {
                if (techProto.Name.Contains("宇宙探索") && techProto.Level == 3)
                {
                    requiredTech = techProto;
                }
            }

            if (requiredTech == null)
            {
                logger.LogWarning($"did not find universe exploration tech item, assuming unlocked");
                return true;
            }

            return GameMain.history.techStates[requiredTech.ID].unlocked;
        }

        private IEnumerator InvokeAction(int delay, Action action)
        {
            logger.LogDebug("pre yield");
            if (delay > 0)
                yield return new WaitForSeconds(2);
            else
                yield return new WaitForEndOfFrame();
            logger.LogDebug("Performing action");
            action();
        }

        private void InvokePluginCommands()
        {
            if (PluginConfig.destroyFactoryAssemblers.Value)
            {
                ClearFactory();
                if (PluginConfig.flattenWithFactoryTearDown.Value)
                {
                    InvokePavePlanet();
                }
            }
            else
            {
                InvokePavePlanet();
            }
        }
    }
}






