using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
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
        public const string PluginVersion = "1.0.2";

        public static ManualLogSource logger;
        private static Thread bulldozerActionThread;

        private static List<ClearFactoryWorkItem> bulldozerWork = new List<ClearFactoryWorkItem>();
        private static readonly List<PaveWorkItem> flattenWorkList = new List<PaveWorkItem>();

        private static ItemDestructionPhase previousPhase = ItemDestructionPhase.Done;

        public static Image ClearResourceImage;
        public static Image PaveResourceImage;


        private static object destroyFactorMutexLock = new object();
        private static Stopwatch clearStopWatch;


        public static BulldozerPlugin Instance;
        private static bool _genRequestFlag;
        private bool _flattenRequested;
        private int _lastOffset = 0;
        private ActionButton clearButton;
        private Harmony harmony;
        private ActionButton paveButton;
        private UIElements ui;

        // Awake is called once when both the game and the plugin are loaded
        private void Awake()
        {
            logger = Logger;
            Instance = this;
            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(BulldozerPlugin));
            Debug.Log("Bulldozer Plugin Loaded");
            if (GameMain.instance != null && GameObject.Find("Game Menu/button-1-bg"))
            {
                if (ClearResourceImage != null)
                {
                    ClearResourceImage.fillAmount = 0;
                }
                else
                {
                    logger.LogInfo("Loading bulldoze buttons");
                    Instance.AddClearButton();
                    logger.LogInfo("Bulldoze button load complete");
                }

                if (PaveResourceImage != null)
                {
                    PaveResourceImage.fillAmount = 0;
                }
                else
                {
                    logger.LogInfo("Loading pave button");
                    Instance.AddPaveButton();
                    logger.LogInfo("Pave button load complete");
                }
            }
        }

        private void Update()
        {
            DoBulldozeUpdate();
            DoPaveUpdate();
        }


        private void OnDestroy()
        {
            // For ScriptEngine hot-reloading
            if (clearButton != null && clearButton.TriggerButton != null) Destroy(clearButton.TriggerButton);

            if (clearButton != null && clearButton.uiButton != null) Destroy(clearButton.uiButton);

            if (paveButton != null && paveButton.TriggerButton != null) Destroy(paveButton.TriggerButton);

            if (paveButton != null && paveButton.uiButton != null) Destroy(paveButton.uiButton);

            if (ClearResourceImage != null) Destroy(ClearResourceImage);

            if (PaveResourceImage != null) Destroy(PaveResourceImage);


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
                    try
                    {
                        var reformIndexForPosition = planetFactory.platformSystem.GetReformIndexForPosition(point);
                        planetFactory.platformSystem.SetReformType(reformIndexForPosition,
                            flattenTask.Player.controller.actionBuild.reformTool.brushType);
                        planetFactory.platformSystem.SetReformColor(reformIndexForPosition,
                            flattenTask.Player.controller.actionBuild.reformTool.brushColor);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning($"exception while applying coat of paint {e.Message}");
                        logger.LogWarning(e.StackTrace);
                    }

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
                var midPoint = maxReformCount / 2;
                var minEquator = midPoint - 10;
                var maxEquator = midPoint + 500 * 20;
                for (var index = 0; index < maxReformCount; ++index)
                    try
                    {
                        var reformToolBrushColor = actionBuild.reformTool.brushColor;
                        var reformToolBrushType = actionBuild.reformTool.brushType;

                        if (minEquator < index && maxEquator > index)
                        {
                            reformToolBrushColor = 7; // Green
                            reformToolBrushType = 1; // paint mode, despite what the foundation tool has currently set
                        }

                        platformSystem.SetReformType(index, reformToolBrushType);
                        platformSystem.SetReformColor(index, reformToolBrushColor);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning($"exception while applying coat of paint {e.Message}");
                        logger.LogWarning(e.StackTrace);
                    }
                // }
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

        private static void InvokeClearPlanet()
        {
            Monitor.Enter(destroyFactorMutexLock);
            var localGenRequestFlag = _genRequestFlag;
            Monitor.Exit(destroyFactorMutexLock);

            if (localGenRequestFlag)
            {
                logger.LogInfo("Bulldozer already in progress.");
                return;
            }

            Monitor.Enter(destroyFactorMutexLock);
            _genRequestFlag = true;
            Monitor.Exit(destroyFactorMutexLock);
            bulldozerActionThread = new Thread(ClearPlanetThread);
            bulldozerActionThread.Start();
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


        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameMain), "Begin")]
        public static void GameMain_Begin_Prefix()
        {
        }


        private void AddClearButton()
        {
            // Instance._lastOffset += 2; // skip one
            // clearButton = AddButton("clear-resource", "Clear resource", "Click to bulldoze factory",
            //     Instance._lastOffset * 15,
            //     bt => { InvokeClearPlanet(); }, ref ClearResourceImage);
        }

        private void AddPaveButton()
        {
            Instance._lastOffset++;
            paveButton = AddButton("pave-planet", "Pave resource", "Click to pave planet",
                Instance._lastOffset * 15,
                bt => { InvokePavePlanet(); }, ref PaveResourceImage);
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
                var points = new[] { mainPlayer.position };
                mainPlayerFactory.ComputeFlattenTerrainReform(points, mainPlayer.position, 1100, 1);
                mainPlayerFactory.FlattenTerrainReform(mainPlayer.position, 90, 10,
                    reformTool.reformTool.buryVeins, 10f);
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

                    if (!platformSystem.IsTerrainReformed(platformSystem.GetReformType(reformIndexForPosition)))
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

            tmpFlattenWorkList.Sort((item1, item2) =>
            {
                var distance1 = Vector3.Distance(mainPlayer.position, item1.Position);
                var distance2 = Vector3.Distance(mainPlayer.position, item2.Position);
                return distance1.CompareTo(distance2);
            });

            logger.LogInfo(
                $"Created {tmpFlattenWorkList.Count} points to bulldoze over. player at {mainPlayer.position} ");
            flattenWorkList.AddRange(tmpFlattenWorkList);
            SetFlattenRequestedFlag(true);
        }

        private void SetFlattenRequestedFlag(bool value)
        {
            logger.LogDebug($"setting flatten requested to {value}");
            _flattenRequested = value;
        }

        private static ActionButton AddButton(string name, string tipTitle, string tipText, int offset,
            Action<UIButton> action,
            ref Image progressImage)
        {
            return new ActionButton(name, tipTitle, tipText, offset, action, ref progressImage);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIBuildMenu), "OnCategoryButtonClick")]
        public static void UIBuildMenu_OnCategoryButtonClick_Postfix(UIBuildMenu __instance)
            //   [HarmonyPostfix, HarmonyPatch(typeof(UIStatisticsWindow), "_OnOpen")]
            // public static void UIStatisticsWindow__OnOpen_Postfix(UIStatisticsWindow __instance)
        {
            var uiBuildMenu = __instance;
            if (logger == null || Instance == null)
            {
                Console.WriteLine($"Not initialized, either logger ({logger}) or Instance ({Instance}) is null");
                return;
            }

            if (uiBuildMenu.currentCategory != 9)
            {
                return;
            }

            if (Instance.ui == null)
            {
                Instance.InitUi(uiBuildMenu);
            }

            // UI Root/Overlay Canvas/In Game/Function Panel/Build Menu/child-group
        }

        private void InitUi(UIBuildMenu uiBuildMenu)
        {
            GameObject environmentModificationContainer = GameObject.Find("UI Root/Overlay Canvas/In Game/Function Panel/Build Menu/child-group");
            var containerRect = environmentModificationContainer.GetComponent<RectTransform>();
            var button1 = GameObject.Find("UI Root/Overlay Canvas/In Game/Function Panel/Build Menu/child-group/button-1");
            logger.LogDebug($"container: {containerRect} {button1}");
            ui = containerRect.gameObject.AddComponent<UIElements>();
            if (containerRect == null || button1 == null)
            {
                return;
            }

            ui.AddDrawEquatorCheckbox(containerRect, button1);
        }

        private static Vector3 LatLonToPosition(float lat, float lon, float earthRadius)
        {
            var latRad = Math.PI / 180 * lat;
            var lonRad = Math.PI / 180 * lon;
            var x = (float)(Math.Cos(latRad) * Math.Cos(lonRad));
            var z = (float)(Math.Cos(latRad) * Math.Sin(lonRad));
            var y = (float)Math.Sin(lat);
            return new Vector3(x, y, z).normalized * earthRadius;
        }
    }
}