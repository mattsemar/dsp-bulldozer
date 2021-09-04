using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using static Bulldozer.Log;
using static PlatformSystem;
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
        public const string PluginVersion = "1.0.19";

        private static readonly List<PaveWorkItem> RaiseVeinsWorkList = new List<PaveWorkItem>();
        private static int _soilToDeduct = 0;

        private static Stopwatch clearStopWatch;

        public static BulldozerPlugin instance;
        private bool _flattenRequested;
        private Harmony _harmony;

        private UIElements _ui;

        // Awake is called once when both the game and the plugin are loaded
        private void Awake()
        {
            logger = Logger;
            GuideMarker.logger = Logger;
            instance = this;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(BulldozerPlugin));
            _harmony.PatchAll(typeof(WreckingBall));
            _harmony.PatchAll(typeof(PluginConfigWindow));
            PluginConfig.InitConfig(Config);
            Debug.Log("Bulldozer Plugin Loaded");
        }

        private void Update()
        {
            WreckingBall.DoWorkItems(GameMain.mainPlayer?.factory);
            var result = HonestLeveler.DoWorkItems(GameMain.mainPlayer?.factory);
            if (HonestLevelerEndState.ENDED_EARLY == result)
            {
                logger.LogInfo($"ran out of soil pile");
            }

            DoPaveUpdate();
            if (_ui != null && _ui.countText != null)
                _ui.countText.text = $"{RaiseVeinsWorkList.Count + WreckingBall.RemainingTaskCount() + HonestLeveler.RemainingTaskCount()}";
        }


        private void OnDestroy()
        {
            // For ScriptEngine hot-reloading
            WreckingBall.Stop();
            RaiseVeinsWorkList?.Clear();
            if (_ui != null)
            {
                _ui.Unload();
            }

            PluginConfigWindow.NeedReinit = true;

            _harmony.UnpatchSelf();
        }

        public void OnGUI()
        {
            if (PluginConfigWindow.visible)
            {
                PluginConfigWindow.OnGUI();
            }
        }

        private void DoPaveUpdate()
        {
            if (RaiseVeinsWorkList.Count > 0)
            {
                var countDown = Math.Min(Math.Min(RaiseVeinsWorkList.Count, PluginConfig.workItemsPerFrame.Value), 10);
                while (countDown-- > 0)
                {
                    var flattenTask = RaiseVeinsWorkList[0];
                    RaiseVeinsWorkList.RemoveAt(0);

                    var point = flattenTask.Position;
                    var planetFactory = flattenTask.Factory;
                    if (GameMain.mainPlayer?.planetId != planetFactory.planetId)
                    {
                        Logger.LogDebug($"player not on planet for work task");
                        continue;
                    }

                    var reformTool = flattenTask.Player.controller.actionBuild.reformTool;
                    bool bury = PluginConfig.buryVeinMode.Value == BuryVeinMode.Tool ? reformTool.buryVeins : PluginConfig.buryVeinMode.Value == BuryVeinMode.Bury;

                    try
                    {
                        planetFactory.FlattenTerrainReform(point, 0.991f * 10f, 10, bury);
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning($"exception while paving {e.Message} {e.StackTrace}");
                    }

                    if (RaiseVeinsWorkList.Count == 0)
                    {
                        planetFactory.planet.landPercentDirty = true;
                    }
                }

                if (RaiseVeinsWorkList.Count % 100 == 0)
                {
                    Logger.LogDebug($"flattened point, {RaiseVeinsWorkList.Count} remain");
                }
            }
            else if (_flattenRequested)
            {
                Logger.LogDebug($"repaint requested");
                SetFlattenRequestedFlag(false);
                try
                {
                    PaintPlanet();
                    LogAndPopupMessage("Bulldozer done adding foundation");
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"exception painting {e}");
                    LogAndPopupMessage($"Failure while painting. Check logs");
                }
            }
        }

        private void PaintPlanet()
        {
            var maxReformCount = GameMain.mainPlayer?.factory?.platformSystem.maxReformCount;
            var platformSystem = GameMain.mainPlayer?.factory?.platformSystem;
            var actionBuild = GameMain.mainPlayer?.controller.actionBuild;
            if (maxReformCount == null || platformSystem == null || actionBuild == null)
            {
                return;
            }

            // reform brush type of 7 is foundation with no decoration
            // brush type 2 is decorated, but not painted
            // 1 seems to be paint mode
            var brushType = 1;
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
                    Logger.LogWarning($"unexpected brush type requested {PluginConfig.foundationDecorationMode.Value}");
                    break;
            }

            var consumedFoundation = 0;
            var foundationUsedUp = false;
            var outOfFoundationMessageShown = false;
            for (var index = 0; index < maxReformCount; ++index)
            {
                var foundationNeeded = platformSystem.IsTerrainReformed(platformSystem.GetReformType(index)) ? 0 : 1;
                if (foundationNeeded > 0 && PluginConfig.foundationConsumption.Value != OperationMode.FullCheat)
                {
                    consumedFoundation += foundationNeeded;
                    var reformId = REFORM_ID;
                    var (itemsRemoved, successful) = StorageSystemManager.RemoveItems(REFORM_ID, foundationNeeded);

                    if (!successful)
                    {
                        if (PluginConfig.foundationConsumption.Value == OperationMode.Honest)
                        {
                            LogAndPopupMessage($"Out of foundation, halting.");
                            foundationUsedUp = true;
                            break;
                        }

                        if (!outOfFoundationMessageShown)
                        {
                            outOfFoundationMessageShown = true;
                            LogAndPopupMessage($"All foundation used, continuing");
                        }
                    }
                }

                platformSystem.SetReformType(index, brushType);
                platformSystem.SetReformColor(index, actionBuild.reformTool.brushColor);
            }

            GameMain.mainPlayer.mecha.AddConsumptionStat(REFORM_ID, consumedFoundation, GameMain.mainPlayer.nearestFactory);

            if (PluginConfig.addGuideLines.Value && !foundationUsedUp)
            {
                PaintGuideMarkings(platformSystem);
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
            if (PluginConfig.addGuideLinesPoles.Value)
            {
                guideMarkTypes |= GuideMarkTypes.Pole;
            }

            if (PluginConfig.minorMeridianInterval.Value > 0)
            {
                guideMarkTypes |= GuideMarkTypes.MinorMeridian;
            }

            GuideMarker.AddGuideMarks(platformSystem, guideMarkTypes);
        }


        private void InvokePavePlanet()
        {
            // if (_soilToDeduct > GameMain.mainPlayer.sandCount || PluginConfig.foundationConsumption.Value == OperationMode.Honest)
            // {
            //     // have to use the slower method of iterating each area and using ComputeT
            //     HonestLeveler.Init(GameMain.mainPlayer.factory, GameMain.mainPlayer);
            // }
            // else
            {
                InvokePavePlanetNoBury();
            }

            if (PluginConfig.alterVeinState.Value)
            {
                InvokePaveWithVeinAlteration();
            }
        }

        private void InvokePavePlanetNoBury()
        {
            var actionBuild = GameMain.mainPlayer?.controller.actionBuild;
            var platformSystem = GameMain.mainPlayer?.factory?.platformSystem;
            var factory = GameMain.localPlanet.factory;
            if (actionBuild == null || platformSystem == null || factory == null)
            {
                LogAndPopupMessage("invalid state");
                return;
            }

            if (GameMain.localPlanet == null || GameMain.localPlanet.type == EPlanetType.Gas)
            {
                LogAndPopupMessage($"Bulldozer doesn't work on gas giants");
                return;
            }

            for (var id = 0; id < factory.vegePool.Length; ++id)
            {
                factory.RemoveVegeWithComponents(id);
            }

            GameMain.gpuiManager.SyncAllGPUBuffer();

            for (var index = 0; index < GameMain.localPlanet.modData.Length << 1; ++index)
            {
                GameMain.localPlanet.AddHeightMapModLevel(index, 3);
            }

            var outOfSoilPile = false;
            if (_soilToDeduct != 0 && PluginConfig.soilPileConsumption.Value != OperationMode.FullCheat)
            {
                // currently we don't have an easy way to see how much soil pile that would've been deducted
                outOfSoilPile = GameMain.mainPlayer.sandCount - _soilToDeduct <= 0;
                GameMain.mainPlayer.SetSandCount(Math.Max(GameMain.mainPlayer.sandCount - _soilToDeduct, 0));
                _soilToDeduct = 0;
            }

            if (GameMain.localPlanet.UpdateDirtyMeshes())
            {
                GameMain.localPlanet.factory.RenderLocalPlanetHeightmap();
            }

            factory.planet.landPercentDirty = true;

            if (!outOfSoilPile || PluginConfig.soilPileConsumption.Value != OperationMode.Honest)
            {
                LogAndPopupMessage("Adding foundation");

                platformSystem.EnsureReformData();
                PaintPlanet();
                if (PluginConfig.addGuideLines.Value)
                {
                    PaintGuideMarkings(platformSystem);
                }
            }
            else
            {
                LogAndPopupMessage($"not adding foundation failed to level everything");
            }

            LogAndPopupMessage("Bulldozer done adding foundation");
        }

        // This version is much slower but will pretty reliable raise or lower all veins
        private void InvokePaveWithVeinAlteration()
        {
            LogAndPopupMessage("Altering veins");
            var mainPlayer = GameMain.mainPlayer;
            var mainPlayerFactory = mainPlayer.factory;
            if (mainPlayerFactory == null) return;

            var platformSystem = mainPlayerFactory.platformSystem;
            if (platformSystem == null) return;

            platformSystem.EnsureReformData();
            if (platformSystem.reformData == null)
            {
                Logger.LogWarning($"no reform data skipping pave");
                return;
            }

            var planet = mainPlayerFactory.planet;
            RaiseVeinsWorkList.Clear();
            var tmpVeinsAlterWorkList = new List<PaveWorkItem>();

            foreach (var vein in planet.factory.veinPool)
            {
                var positions = BuildPositionsToRaiseVeins(vein.pos);
                foreach (var position in positions)
                {
                    tmpVeinsAlterWorkList.Add(
                        new PaveWorkItem
                        {
                            Position = position,
                            Player = mainPlayer,
                            Factory = mainPlayerFactory
                        });
                }
            }


            tmpVeinsAlterWorkList.Sort((item1, item2) =>
            {
                var distance1 = Vector3.Distance(mainPlayer.position, item1.Position);
                var distance2 = Vector3.Distance(mainPlayer.position, item2.Position);
                return distance1.CompareTo(distance2);
            });

            RaiseVeinsWorkList.AddRange(tmpVeinsAlterWorkList);
            SetFlattenRequestedFlag(true);
        }

        private Vector3[] BuildPositionsToRaiseVeins(Vector3 centerPosition)
        {
            var quaternion = Maths.SphericalRotation(centerPosition, 22.5f);
            var radius = 0.991f * 10f;
            return new[]
            {
                centerPosition,
                centerPosition + quaternion * (new Vector3(1f, 0.0f, 1f) * radius),
                centerPosition + quaternion * (new Vector3(-1f, 0.0f, -1f) * radius),
                centerPosition + quaternion * (new Vector3(1f, 0.0f, -1f) * radius),
                centerPosition + quaternion * (new Vector3(-1f, 0.0f, 1f) * radius)
            };
        }

        private void SetFlattenRequestedFlag(bool value)
        {
            Logger.LogDebug($"setting flatten requested to {value}");
            _flattenRequested = value;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GameScenarioLogic), "NotifyOnUnlockTech")]
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

            logger.LogDebug($"tech proto not matched {JsonUtility.ToJson(techProto)}");
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
                instance._ui.TechUnlockedState = instance.CheckResearchedTech() || PluginConfig.disableTechRequirement.Value;
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
                StartCoroutine(InvokeAction(1, () =>
                {
                    GameMain.mainPlayer.SetHandItems(0, 0);
                    GameMain.mainPlayer.controller.actionBuild.reformTool._Close();
                }));

                if (RaiseVeinsWorkList.Count > 0 || WreckingBall.IsRunning() || HonestLeveler.IsRunning())
                {
                    RaiseVeinsWorkList.Clear();
                    HonestLeveler.Stop();
                    WreckingBall.Stop();
                    LogAndPopupMessage("Stopping...");
                    _ui.countText.text = "0";
                }
                else
                {
                    var popupMessage = ConstructPopupMessage(GameMain.localPlanet);
                    UIMessageBox.Show("Bulldoze planet", popupMessage.Translate(),
                        "Ok", "Cancel", 0, InvokePluginCommands, () => { LogAndPopupMessage($"Canceled"); });
                }
            });

            _ui.TechUnlockedState = CheckResearchedTech() || PluginConfig.disableTechRequirement.Value;
        }

        private string ConstructPopupMessage(PlanetData localPlanet)
        {
            if (localPlanet == null)
            {
                return "No local planet to bulldoze found.";
            }

            var popupMessage = $"Please confirm that you would like to do the following: ";
            if (PluginConfig.destroyFactoryAssemblers.Value)
            {
                popupMessage += $"\nDestroy all factory machines (assemblers, belts, stations, etc)";
                var countBuildGhosts = WreckingBall.CountBuildGhosts(GameMain.mainPlayer.factory);
                if (countBuildGhosts > 0)
                {
                    popupMessage += $". Including {countBuildGhosts} not yet built machines";
                }

                if (PluginConfig.deleteFactoryTrash.Value)
                {
                    popupMessage += $"\nDelete all littered factory items (existing litter should not be affected)";
                }


                if (PluginConfig.flattenWithFactoryTearDown.Value)
                {
                    popupMessage += $"\nAdd foundation to all locations on planet";
                }
            }
            else
            {
                popupMessage += $"\nAdd foundation to all locations on planet";
                if (PluginConfig.alterVeinState.Value)
                {
                    popupMessage += $"\nAttempt to {PluginConfig.GetCurrentVeinsRaiseState()} all veins (slow)".Translate();
                    popupMessage += "\nThis action can take a bit to complete (uncheck raise/lower veins option to finish instantly).".Translate();
                }

                if (PluginConfig.addGuideLines.Value)
                {
                    var markingTypes = PluginConfig.addGuideLinesEquator.Value ? "equator" : "";
                    if (PluginConfig.addGuideLinesMeridian.Value)
                    {
                        if (PluginConfig.minorMeridianInterval.Value > 0)
                            markingTypes += " (minor and major)";
                        markingTypes += " meridians";
                    }

                    if (PluginConfig.addGuideLinesTropic.Value)
                    {
                        markingTypes += " tropics";
                    }

                    popupMessage += $"\nAdd guide markings to certain points on planet ({markingTypes})";
                }

                if (PluginConfig.soilPileConsumption.Value != OperationMode.FullCheat || PluginConfig.foundationConsumption.Value != OperationMode.FullCheat)
                {
                    var (foundationNeeded, soilPile) = GridExplorer.CountNeededResources(GameMain.localPlanet.factory.platformSystem);
                    if (PluginConfig.soilPileConsumption.Value != OperationMode.FullCheat && soilPile != 0)
                    {
                        var verb = soilPile < 0 ? "Gain" : "Consume";
                        popupMessage += $"\n{verb} {Math.Abs(soilPile)} soil pile. (Current amount:  {GameMain.mainPlayer.sandCount})";
                        if (soilPile > GameMain.mainPlayer.sandCount)
                        {
                            if (PluginConfig.soilPileConsumption.Value == OperationMode.Honest)
                            {
                                popupMessage +=
                                    $". Be aware that this process will halt after your soil pile is consumed.";
                            }
                            else
                            {
                                popupMessage += $". All of your soil pile will be consumed but the process will continue.";
                            }
                        }

                        _soilToDeduct = soilPile;
                    }

                    if (PluginConfig.foundationConsumption.Value != OperationMode.FullCheat)
                    {
                        popupMessage += $"\nConsume {foundationNeeded} foundation\n";
                        var (message, allRemoved, remainingToRemove) = StorageSystemManager.BuildRemovalMessage(REFORM_ID, foundationNeeded);
                        popupMessage += message;
                        if (!allRemoved)
                        {
                            if (OperationMode.HalfCheat == PluginConfig.foundationConsumption.Value)
                                popupMessage += $"\nProcess will continue after all available foundation ({foundationNeeded - remainingToRemove}) is used up.";
                            else
                                popupMessage += $"\nProcess will halt after all available foundation ({foundationNeeded - remainingToRemove}) is used up.";
                        }
                    }
                }
            }

            return popupMessage;
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
            {
                yield return new WaitForSeconds(delay);
            }
            else if (delay == -2)
            {
                yield return new WaitForFixedUpdate();
            }
            else
            {
                yield return new WaitForEndOfFrame();
            }

            logger.LogDebug("Performing action");
            action();
        }

        private void InvokePluginCommands()
        {
            try
            {
                if (PluginConfig.destroyFactoryAssemblers.Value)
                {
                    WreckingBall.Init(GameMain.mainPlayer.factory, GameMain.mainPlayer);
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
            catch (Exception e)
            {
                logger.LogWarning($"InvokePlugin failed {e}");
            }
        }
    }
}