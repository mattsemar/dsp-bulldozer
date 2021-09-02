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
        public static ConfigEntry<OperationMode> soilPileConsumption;
        public static ConfigEntry<OperationMode> foundationConsumption;

        // used to make UI checkbox values persistent
        public static ConfigEntry<bool> addGuideLines;
        public static ConfigEntry<bool> alterVeinState;
        public static ConfigEntry<bool> destroyFactoryAssemblers;

        public static ConfigEntry<bool> addGuideLinesEquator;
        public static ConfigEntry<bool> addGuideLinesMeridian;
        public static ConfigEntry<bool> addGuideLinesTropic;

        public static ConfigEntry<BuryVeinMode> buryVeinMode;
        public static ConfigEntry<FoundationDecorationMode> foundationDecorationMode;

        public static ConfigEntry<bool> deleteFactoryTrash;
        public static ConfigEntry<bool> disableTechRequirement;

        // enable normal action of plugin when destroyAssemblers is enabled
        public static ConfigEntry<bool> flattenWithFactoryTearDown;

        public static ConfigFile PluginConfigFile;


        public static void InitConfig(ConfigFile configFile)
        {
            PluginConfigFile = configFile;


            workItemsPerFrame = configFile.Bind("Performance", "WorkItemsPerFrame", 1,
                new ConfigDescription("Number of actions attempted per Frame. Default value is 1 (minimum since 0 would not do anything other than queue up work). " +
                                      "Larger values might make the job complete more quickly, but will also slow your system down noticeably",
                    new AcceptableValueRange<int>(1, 5), "configEditOnly"));

            soilPileConsumption = configFile.Bind("Cheatiness", "SoilPileConsumption", OperationMode.FullCheat,
                "Controls whether bulldozing consumes and or requires available soil pile");
            foundationConsumption = configFile.Bind("Cheatiness", "FoundationConsumption", OperationMode.FullCheat,
                "Controls whether bulldozing consumes and or requires available foundation pile");
            disableTechRequirement = configFile.Bind("Cheatiness", "DisableTechRequirement", false,
                "Enable/disable tech requirement for using mod");

            buryVeinMode = configFile.Bind("Veins", "BuryVeinMode", BuryVeinMode.Tool, "No impact if raise/lower checkbox is not set");

            addGuideLinesEquator = configFile.Bind("Decoration", "AddGuideLinesEquator", true,
                "Enable/disable of the equator guideline individually. No effect if AddGuideLines is disabled");
            addGuideLinesMeridian = configFile.Bind("Decoration", "AddGuideLinesMeridian", true,
                "Enable/disable of the meridian guidelines individually. No effect if AddGuideLines is disabled");
            addGuideLinesTropic = configFile.Bind("Decoration", "AddGuideLinesTropic", true,
                "Enable/disable of the tropic guidelines individually. No effect if AddGuideLines is disabled. Currently bugged with larger radius planets");
            foundationDecorationMode = configFile.Bind("Decoration", "FoundationDecorationMode", FoundationDecorationMode.Tool,
                "Change to have a permanent setting instead of tracking the game's current config");

            deleteFactoryTrash = configFile.Bind("Destruction", "DeleteFactoryTrash", false,
                "Erase all items littered while destroying factory items");
            flattenWithFactoryTearDown = configFile.Bind("Destruction", "FlattenWithFactoryTearDown", false,
                "Use this to enable adding foundation while destroying existing factory");

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