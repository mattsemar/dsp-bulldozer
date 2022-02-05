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
        public const string PluginVersion = "1.1.3";

        private static int _soilToDeduct;

        private static Stopwatch clearStopWatch;

        public static BulldozerPlugin instance;
        private RegionPainter _regionPainter;
        private ReformIndexInfoProvider _reformIndexInfoProvider;
        private PlanetData _regionPainterPlanet;
        private bool _flattenRequested;
        private Harmony _harmony;
        private DebugFactoryData _debugFactoryData;

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
            if (!GameMain.isRunning 
                || DSPGame.IsMenuDemo 
                || GameMain.localPlanet == null 
                || GameMain.localPlanet.factory == null 
                || GameMain.localPlanet.factory.platformSystem == null)
            {
                return;
            }

            var platformSystem = GameMain.localPlanet.factory.platformSystem;
            if (_reformIndexInfoProvider == null)
            {
                _reformIndexInfoProvider = new ReformIndexInfoProvider(platformSystem);
            }

            if (_regionPainter == null)
            {
                _regionPainter = new RegionPainter(platformSystem, _reformIndexInfoProvider);
            }

            _reformIndexInfoProvider.DoInitWork(GameMain.localPlanet);

            if (_ui != null && _ui.IsShowing())
            {
                if (PluginConfig.enableRegionColor.Value || PluginConfig.IsLatConstrained())
                {
                    _ui.ReadyForAction = _reformIndexInfoProvider is { Initted: true };
                }
                else
                {
                    _ui.ReadyForAction = true;
                }
            }

            WreckingBall.DoWorkItems(GameMain.mainPlayer?.factory);
            var result = HonestLeveler.DoWorkItems(GameMain.mainPlayer?.factory);
            if (HonestLevelerEndState.ENDED_EARLY == result)
            {
                logger.LogInfo("ran out of soil pile");
            }

            DoPaveUpdate();
            if (_ui != null && _ui.countText != null)
                _ui.countText.text = $"{WreckingBall.RemainingTaskCount() + HonestLeveler.RemainingTaskCount()}";
        }


        private void OnDestroy()
        {
            // For ScriptEngine hot-reloading
            WreckingBall.Stop();
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
            if (_flattenRequested)
            {
                Logger.LogDebug("repaint requested");
                SetFlattenRequestedFlag(false);
                try
                {
                    if (GameMain.mainPlayer.planetData.UpdateDirtyMeshes())
                        GameMain.mainPlayer.factory.RenderLocalPlanetHeightmap();
                    Decorate();
                    LogAndPopupMessage("Bulldozer done adding foundation");
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"exception painting {e}");
                    LogAndPopupMessage("Failure while painting. Check logs");
                }
            }
        }

        private void Decorate()
        {
            var platformSystem = GameMain.mainPlayer?.factory?.platformSystem;
            var actionBuild = GameMain.mainPlayer?.controller.actionBuild;
            if (platformSystem == null || actionBuild == null)
            {
                return;
            }

            var foundationUsedUp = PlanetPainter.PaintPlanet(platformSystem, _reformIndexInfoProvider);
            if (PluginConfig.enableRegionColor.Value && !foundationUsedUp)
            {
                _regionPainter.PaintRegions();
            }

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
            if (PluginConfig.alterVeinState.Value)
            {
                InvokePaveWithVeinAlteration();
            }
            else
            {
                InvokePavePlanetNoBury();
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
                LogAndPopupMessage("Bulldozer doesn't work on gas giants");
                return;
            }

            if (PluginConfig.removeVegetation.Value)
            {
                for (var id = 0; id < factory.vegePool.Length; ++id)
                {
                    if (factory.vegePool[id].protoId == 9999)
                    {
                        continue;
                    }

                    if (PluginConfig.IsLatConstrained())
                    {
                        var lat = GeoUtil.GetLatitudeDegForPosition(factory.vegePool[id].pos);
                        if (PluginConfig.LatitudeOutOfBounds(lat))
                            continue;
                    }

                    factory.RemoveVegeWithComponents(id);
                }
            }
            else
            {
                PlanetAlterer.UpdateVegeHeight(factory);
            }

            GameMain.gpuiManager.SyncAllGPUBuffer();

            int levelCount = 0;
            int firstLatLeveled = -1;
            int firstLongLeveled = -1;
            int firstIndex = -1;
            for (var index = 0; index < GameMain.localPlanet.modData.Length << 1; ++index)
            {
                if (PluginConfig.IsLatConstrained())
                {
                    var latLonForModIndex = _reformIndexInfoProvider.GetForModIndex(index);
                    if (latLonForModIndex.Equals(LatLon.Empty))
                    {
                        LogNTimes("No coord mapped to data index for {0}", 15, index);
                        continue;
                    }

                    if (PluginConfig.LatitudeOutOfBounds(latLonForModIndex.Lat))
                    {
                        continue;
                    }

                    if (firstLatLeveled < 0)
                    {
                        firstLatLeveled = latLonForModIndex.Lat;
                        firstLongLeveled = latLonForModIndex.Long;
                        firstIndex = index;
                    }
                }

                levelCount++;
                GameMain.localPlanet.AddHeightMapModLevel(index, 3);
            }

            Debug(
                $"leveled {levelCount} points for {PluginConfig.minLatitude.Value} {PluginConfig.maxLatitude.Value}. First ({firstLatLeveled}, {firstLongLeveled}) ndx: {firstIndex}");

            var outOfSoilPile = false;
            if (_soilToDeduct != 0 && PluginConfig.soilPileConsumption.Value != OperationMode.FullCheat)
            {
                // currently we don't have an easy way to see how much soil pile would've been deducted
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
                Decorate();
            }
            else
            {
                LogAndPopupMessage("not adding foundation failed to level everything");
            }

            LogAndPopupMessage("Bulldozer done adding foundation");
        }

        private void InvokePaveWithVeinAlteration()
        {
            LogAndPopupMessage("Altering veins");
            PlanetAlterer.RaiseLowerVeins();
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
                if (instance._reformIndexInfoProvider != null && instance._ui != null)
                    instance._ui.ReadyForAction = instance._reformIndexInfoProvider.Initted || !PluginConfig.IsLatConstrained();
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

                if (WreckingBall.IsRunning() || HonestLeveler.IsRunning())
                {
                    HonestLeveler.Stop();
                    WreckingBall.Stop();
                    LogAndPopupMessage("Stopping...");
                    _ui.countText.text = "0";
                }
                else
                {
                    var popupMessage = ConstructPopupMessage(GameMain.localPlanet);
                    var boxTitle = PluginConfig.IsLatConstrained() ? "Bulldoze selected latitudes" : "Bulldoze planet";
                    UIMessageBox.Show(boxTitle, popupMessage.Translate(),
                        "Ok", "Cancel", 0, InvokePluginCommands, () => { LogAndPopupMessage("Canceled"); });
                }
            });

            _ui.TechUnlockedState = CheckResearchedTech() || PluginConfig.disableTechRequirement.Value;
            if (PluginConfig.enableRegionColor.Value || PluginConfig.IsLatConstrained())
            {
                _ui.ReadyForAction = _reformIndexInfoProvider is { Initted: true };
            }
            else
            {
                _ui.ReadyForAction = true;
            }
        }

        private string ConstructPopupMessage(PlanetData localPlanet)
        {
            if (localPlanet == null)
            {
                return "No local planet to bulldoze found.";
            }

            var popupMessage = "Please confirm the following actions: ";
            if (PluginConfig.IsLatConstrained())
            {
                popupMessage += $"\r\n\t[For Selected Latitude Range {PluginConfig.GetLatRangeString()}]";
            }

            if (PluginConfig.destroyFactoryAssemblers.Value)
            {
                var machinesMsg = PluginConfig.skipDestroyingStations.Value ? "(assemblers, belts, but not stations)" : "(assemblers, belts, stations, etc)";
                if (PluginConfig.IsLatConstrained())
                {
                    popupMessage += $"\nDestroy factory machines {machinesMsg}";
                    popupMessage += "\nNote: this can be much slower than tearing down the entire factory";
                }
                else
                {
                    popupMessage += $"\nDestroy all factory machines {machinesMsg}";
                }

                var countBuildGhosts = WreckingBall.CountBuildGhosts(GameMain.mainPlayer.factory);
                if (countBuildGhosts > 0)
                {
                    popupMessage += $". Including {countBuildGhosts} not yet built machines";
                }

                if (PluginConfig.deleteFactoryTrash.Value)
                {
                    popupMessage += "\nDelete all littered factory items (existing litter should not be affected)";
                }

                if (!PluginConfig.deleteFactoryTrash.Value && Utils.IsOtherAssemblyLoaded("PersonalLogistics"))
                {
                    popupMessage += "\nNote: Personal Logistics is also installed. Be aware that by default\r\n" +
                                    "          it will try and send littered items to your logistics stations.\r\n";
                }
            }
            else if (PluginConfig.alterVeinState.Value)
            {
                if (PluginConfig.IsLatConstrained())
                {
                    popupMessage += $"\nAttempt to {PluginConfig.GetCurrentVeinsRaiseState()} veins in Selected Latitudes";
                }
                else
                {
                    popupMessage += $"\nAttempt to {PluginConfig.GetCurrentVeinsRaiseState()} all veins on planet.";
                }
            }
            else
            {
                if (PluginConfig.IsLatConstrained())
                {
                    popupMessage += "\nAdd foundation to locations in Selected Latitudes";
                }
                else
                {
                    popupMessage += "\nAdd foundation to all locations on planet";
                }

                if (PluginConfig.removeVegetation.Value)
                {
                    if (PluginConfig.IsLatConstrained())
                        popupMessage += "\r\nRemove all plants trees and rocks in the Selected Latitudes";
                    else
                        popupMessage += "\r\nRemove all plants trees and rocks";
                }
                else
                {
                    popupMessage += "\r\nSkip removing plants trees and rocks".Translate();
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
                    if (PluginConfig.IsLatConstrained())
                    {
                        popupMessage += "\n\tNote that guide marks outside of Selected Latitudes will not be applied.";
                    }
                }

                if (PluginConfig.enableRegionColor.Value)
                {
                    var regionCount = RegionalColors.RegionCountDefined();
                    popupMessage += $"\nPaint custom colors for {regionCount} regions";
                }
                else if (RegionalColors.RegionCountDefined() > 0)
                {
                    popupMessage += $"\nSkip painting {RegionalColors.RegionCountDefined()} regions (config option 'Enable Region Color' not set).";
                }

                if (PluginConfig.soilPileConsumption.Value != OperationMode.FullCheat || PluginConfig.foundationConsumption.Value != OperationMode.FullCheat)
                {
                    var (foundationNeeded, soilPile) = GridExplorer.CountNeededResources(GameMain.localPlanet.factory.platformSystem, _reformIndexInfoProvider);
                    if (PluginConfig.soilPileConsumption.Value != OperationMode.FullCheat && soilPile != 0)
                    {
                        var verb = soilPile < 0 ? "Gain" : "Consume";
                        popupMessage += $"\n{verb} {Math.Abs(soilPile)} soil pile. (Current amount:  {GameMain.mainPlayer.sandCount})";
                        if (soilPile > GameMain.mainPlayer.sandCount)
                        {
                            if (PluginConfig.soilPileConsumption.Value == OperationMode.Honest)
                            {
                                popupMessage +=
                                    ". Be aware that this process will halt after your soil pile is consumed.";
                            }
                            else
                            {
                                popupMessage += ". All of your soil pile will be consumed but the process will continue.";
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
            foreach (TechProto techProto in new List<TechProto>(LDB.techs.dataArray))
            {
                if (techProto.Name.Contains("宇宙探索") && techProto.Level == 3)
                {
                    requiredTech = techProto;
                }
            }

            if (requiredTech == null)
            {
                logger.LogWarning("did not find universe exploration tech item, assuming unlocked");
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