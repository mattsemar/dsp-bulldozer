using System.Collections;
using System.ComponentModel;
using BepInEx.Configuration;

namespace Bulldozer
{
    public enum BuryVeinMode
    {
        [Description("Use game setting for raise/lower. Note that the raise/lower checkbox must be enabled for this to have an impact")]
        Tool,

        [Description("Always bury veins, ignoring game setting. Note that the raise/lower checkbox must be enabled for this to have an impact")]
        Bury,

        [Description("Always raise veins, ignoring game setting. Note that the raise/lower checkbox must be enabled for this to have an impact")]
        Raise
    }

    public enum FoundationDecorationMode
    {
        [Description("Use game setting for brushType (whatever selected in game is what will be used)")]
        Tool,

        [Description("Ignore game setting, always use painted foundation (brushType: 1)")]
        Paint,

        [Description("Ignore game setting, always use painted foundation (brushType: 2)")]
        Decorate,

        [Description("Ignore game setting, always use 'no decoration' mode (brushType: 7)")]
        Clear,
    }

    public enum OperationMode
    {
        [Description("Full cheat mode, resource not consumed")]
        FullCheat,

        [Description("Consume available resource and continue")]
        HalfCheat,

        [Description("Consume available resources, halt when empty")]
        Honest
    }

    public class PluginConfig
    {
        public static ConfigEntry<int> workItemsPerFrame;
        public static ConfigEntry<bool> useActionBuildTearDown;
        public static ConfigEntry<int> factoryTeardownRunTimePerFrame;
        public static ConfigEntry<OperationMode> soilPileConsumption;
        public static ConfigEntry<OperationMode> foundationConsumption;

        // used to make UI checkbox values persistent
        public static ConfigEntry<bool> addGuideLines;
        public static ConfigEntry<bool> alterVeinState;
        public static ConfigEntry<bool> destroyFactoryAssemblers;

        public static ConfigEntry<bool> addGuideLinesEquator;
        public static ConfigEntry<bool> addGuideLinesMeridian;
        public static ConfigEntry<bool> addGuideLinesTropic;
        public static ConfigEntry<bool> addGuideLinesPoles;
        public static ConfigEntry<int> minorMeridianInterval;

        public static ConfigEntry<int> guideLinesEquatorColor;
        public static ConfigEntry<int> guideLinesTropicColor;
        public static ConfigEntry<int> guideLinesMeridianColor;
        public static ConfigEntry<int> guideLinesMinorMeridianColor;
        public static ConfigEntry<int> guideLinesPoleColor;
        
        public static ConfigEntry<bool> enableRegionColor;
        public static ConfigEntry<string> regionColors;

        public static ConfigEntry<BuryVeinMode> buryVeinMode;
        public static ConfigEntry<FoundationDecorationMode> foundationDecorationMode;

        public static ConfigEntry<bool> removeVegetation;
        public static ConfigEntry<bool> deleteFactoryTrash;
        public static ConfigEntry<bool> disableTechRequirement;

        // enable normal action of plugin when destroyAssemblers is enabled
        public static ConfigEntry<bool> flattenWithFactoryTearDown;
        public static ConfigEntry<bool> skipDestroyingStations;
        public static ConfigEntry<bool> featureFastDelete;

        public static ConfigFile PluginConfigFile;

        public static void InitConfig(ConfigFile configFile)
        {
            PluginConfigFile = configFile;


            workItemsPerFrame = configFile.Bind("Performance", "WorkItemsPerFrame", 1,
                new ConfigDescription("Number of actions attempted per Frame. Default value is 1 (minimum since 0 would not do anything other than queue up work). " +
                                      "Larger values might make the job complete more quickly, but will also slow your system down noticeably",
                    new AcceptableValueRange<int>(1, 25), "configEditOnly"));

            useActionBuildTearDown = configFile.Bind("Performance", "UseActionBuildTearDown", true,
                new ConfigDescription("Method to use for teardown. Disabled causes exceptions sometimes but might run a little faster",
                                      null, "configEditOnly"));
            
            factoryTeardownRunTimePerFrame = configFile.Bind("Performance", "Teardown MS Per Frame", 500,
                new ConfigDescription("How long in ms to let the teardown task run per update. Note that 1000 ms means your game will be running at 1 UPS, but at 1 UPS the UI should still let you halt the task (click button again)\r\n" +
                                      "Larger values might make the job complete more quickly, but will also slow your system down noticeably",
                    new AcceptableValueRange<int>(20, 3000)));

            soilPileConsumption = configFile.Bind("Cheatiness", "SoilPileConsumption", OperationMode.FullCheat,
                "Controls whether bulldozing consumes and or requires available soil pile");
            foundationConsumption = configFile.Bind("Cheatiness", "FoundationConsumption", OperationMode.Honest,
                "Controls whether bulldozing consumes and or requires available foundation pile");
            disableTechRequirement = configFile.Bind("Cheatiness", "DisableTechRequirement", false,
                "Enable/disable tech requirement for using mod");

            buryVeinMode = configFile.Bind("Veins", "BuryVeinMode", BuryVeinMode.Tool, "No impact if raise/lower checkbox is not set");

            addGuideLinesEquator = configFile.Bind("Decoration", "AddGuideLinesEquator", true,
                "Enable/disable of the equator guideline individually. No effect if AddGuideLines is disabled");
            addGuideLinesMeridian = configFile.Bind("Decoration", "AddGuideLinesMeridian", true,
                "Enable/disable of the meridian guidelines individually. No effect if AddGuideLines is disabled");
            addGuideLinesTropic = configFile.Bind("Decoration", "AddGuideLinesTropic", true,
                "Enable/disable of the tropic guidelines individually. No effect if AddGuideLines is disabled");
            addGuideLinesPoles = configFile.Bind("Decoration", "AddGuideLinesPoles", false,
                "Enable/disable painting polar areas. No effect if AddGuideLines is disabled. Poles are considered first 2 tropics");
            minorMeridianInterval = configFile.Bind("Decoration", "MinorMeridianInterval", 0,
                new ConfigDescription(
                    "Paint meridians starting at 0 and incrementing by this value. E.g., a value of 10 would add a meridian line every 10 degrees 18, 36 total. 0 to disable",
                    new AcceptableValueRange<int>(0, 89), "meridians"));
            
            guideLinesEquatorColor = configFile.Bind("CustomColors", "Equator Color", 7,
                new ConfigDescription("Index of color in palette to paint equator. Default is green", new AcceptableValueRange<int>(0, 31), "color"));
            guideLinesMeridianColor = configFile.Bind("CustomColors", "Meridian Color", 12,
                new ConfigDescription("Index of color in palette to paint major meridian lines", new AcceptableValueRange<int>(0, 31), "color"));
            guideLinesMinorMeridianColor = configFile.Bind("CustomColors", "Minor Meridian Color", 3,
                new ConfigDescription("Index of color in palette to paint minor meridian lines", new AcceptableValueRange<int>(0, 31), "color"));

            guideLinesTropicColor = configFile.Bind("CustomColors", "Tropic Color", 2,
                new ConfigDescription("Index of color in palette to paint tropic lines", new AcceptableValueRange<int>(0, 31), "color"));
            guideLinesPoleColor = configFile.Bind("CustomColors", "Pole Color", 1,
                new ConfigDescription("Index of color in palette to paint poles", new AcceptableValueRange<int>(0, 31), "color"));

            enableRegionColor = configFile.Bind("CustomColors", "Enable Region Color", false,
                "Enable painting colors based on coordinates");
            regionColors = configFile.Bind("UIOnly", "Region Colors JSON", "", "Not for editing, use UI to define values");
          
            foundationDecorationMode = configFile.Bind("Decoration", "FoundationDecorationMode", FoundationDecorationMode.Tool,
                "Change to have a permanent setting instead of tracking the game's current config");

            deleteFactoryTrash = configFile.Bind("Destruction", "DeleteFactoryTrash", false,
                "Erase all items littered while destroying factory items");
            removeVegetation = configFile.Bind("Destruction", "RemoveVegetation", true,
                "Erase vegetation");
            flattenWithFactoryTearDown = configFile.Bind("Destruction", "FlattenWithFactoryTearDown", false,
                "Use this to enable adding foundation while destroying existing factory");
            skipDestroyingStations = configFile.Bind("Destruction", "SkipDestroyingStations", false,
                "Enable/disable teardown of logistics stations");

            // Config edit only, but just in case it needs to be turned off at some point in the future
            featureFastDelete = configFile.Bind("Destruction", "Enable Fast Delete", true,
                new ConfigDescription("Enable Raptor's fast delete for factory teardown", null, "configEditOnly"));

            alterVeinState = configFile.Bind("UIOnly", "AlterVeinState", false,
                new ConfigDescription("Don't edit, use UI checkbox. By default, veins will not be lowered or raised. Enabling takes much longer",
                    null, "uiEditOnly"));

            destroyFactoryAssemblers = configFile.Bind("UIOnly", "DestroyFactoryAssemblers", false,
                $"Don't edit, use UI checkbox. Destroy all factory machines (labs, assemblers, etc). It can be very slow so if you get bored waiting and want to delete stuff yourself, make sure to stop the bulldoze process.");
            addGuideLines = configFile.Bind("UIOnly", "AddGuideLines", true,
                "Don't edit, this property backs the checkbox in the UI. If enabled painted lines at certain points on planet will be added");
        }

        public static string GetCurrentVeinsRaiseState()
        {
            var reformTool = GameMain.mainPlayer?.controller.actionBuild.reformTool;
            if (reformTool == null)
            {
                return "UNKNOWN";
            }

            var bury = buryVeinMode.Value == BuryVeinMode.Tool ? reformTool.buryVeins : buryVeinMode.Value == BuryVeinMode.Bury;
            return bury ? "bury" : "restore";
        }

        public static void ResetConfigWindowOptionsToDefault()
        {
            foreach (var configDefinition in PluginConfigFile.Keys)
            {
                // edit config to tweak performance config settings 
                if (configDefinition.Section == "UIOnly")
                {
                    continue;
                }

                var configEntry = PluginConfigFile[configDefinition];
                if (((IList)configEntry.Description.Tags).Contains("configEditOnly"))
                {
                    continue;
                }

                configEntry.BoxedValue = configEntry.DefaultValue;
            }
        }
    }
}