using System.ComponentModel;
using BepInEx.Configuration;

namespace Bulldozer
{
    public enum BuryVeinMode
    {
        [Description("Use the same mode as the game's foundation tool")]
        Tool,

        [Description("Always bury veins, ignore tool setting")]
        Bury,

        [Description("Explicitly (try to) raise veins, ignore tool setting. Note that repave must be enabled and that some veins will be missed when raising buried ones")]
        Raise
    }

    public enum FoundationDecorationMode
    {
        [Description("Use the same mode as the game's tool")]
        Tool,

        [Description("Add painted foundation (brushType: 1)")]
        Paint,

        [Description("Add decorated, but not painted foundation (brushType: 2)")]
        Decorate,

        [Description("Reset height but add foundation with no decoration or paint (brushType: 7)")]
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


        public static void InitConfig(ConfigFile configFile)
        {
            alterVeinState = configFile.Bind("UIOnly", "AlterVeinState", false,
                "Don't edit, use UI checkbox. By default, veins will not be lowered or raised. Enabling takes much longer");
            destroyFactoryAssemblers = configFile.Bind("UIOnly", "DestroyFactoryAssemblers", false,
                "Don't edit, use UI checkbox. Destroy all factory machines (labs, assemblers, etc). It can be very slow so if you get bored waiting and want to delete stuff yourself, make sure to stop the bulldoze process. ");
            addGuideLines = configFile.Bind("UIOnly", "AddGuideLines", true,
                "Don't edit, this property backs the checkbox in the UI. If enabled painted lines at certain points on planet will be added");

            workItemsPerFrame = configFile.Bind("Performance", "WorkItemsPerFrame", 1,
                "Number of actions attempted per Frame. Default value is 1 (minimum since 0 would not do anything other than queue up work). " +
                "Larger values might make the job complete more quickly, but will also slow your system down noticeably");

            soilPileConsumption = configFile.Bind("Cheatiness", "SoilPileConsumption", OperationMode.FullCheat,
                "Controls whether bulldozing consumes and or requires available soil pile");
            foundationConsumption = configFile.Bind("Cheatiness", "FoundationConsumption", OperationMode.FullCheat,
                "Controls whether bulldozing consumes and or requires available foundation pile");
            disableTechRequirement = configFile.Bind("Cheatiness", "DisableTechRequirement", false,
                "Controls whether you actually have to meet tech requirement");

            buryVeinMode = configFile.Bind("Veins", "BuryVeinMode", BuryVeinMode.Tool, "No impact if AlterVeinState is false");

            addGuideLinesEquator = configFile.Bind("Decoration", "AddGuideLinesEquator", true,
                "Allows disabling of the equator guideline individually. No effect if AddGuideLines is disabled");
            addGuideLinesMeridian = configFile.Bind("Decoration", "AddGuideLinesMeridian", true,
                "Allows disabling of the meridian guidelines individually. No effect if AddGuideLines is disabled");
            addGuideLinesTropic = configFile.Bind("Decoration", "AddGuideLinesTropic", false,
                "Allows disabling of the tropic guidelines individually. No effect if AddGuideLines is disabled. Currently bugged with larger radius planets");
            foundationDecorationMode = configFile.Bind("Decoration", "FoundationDecorationMode", FoundationDecorationMode.Tool);

            deleteFactoryTrash = configFile.Bind("Destruction", "DeleteFactoryTrash", false,
                "Erase all items littered while destroying factory items");
            flattenWithFactoryTearDown = configFile.Bind("Destruction", "FlattenWithFactoryTearDown", false,
                "Use this to enable adding foundation when destroying existing factory");
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
    }
}