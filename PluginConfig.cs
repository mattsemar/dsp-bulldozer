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

    public class PluginConfig
    {
        public static ConfigEntry<int> workItemsPerFrame;

        // make the checkbox values persistent
        public static ConfigEntry<bool> addGuideLines;
        public static ConfigEntry<bool> repaveAll;

        public static ConfigEntry<bool> addGuideLinesEquator;
        public static ConfigEntry<bool> addGuideLinesMeridian;
        public static ConfigEntry<bool> addGuideLinesTropic;

        public static ConfigEntry<BuryVeinMode> buryVeinMode;
        public static ConfigEntry<FoundationDecorationMode> foundationDecorationMode;

        // dangerous, don't enable unless you are certain
        public static ConfigEntry<bool> destroyFactoryAssemblers;
        
        // enable normal action of plugin when destroyAssemblers is enabled
        public static ConfigEntry<bool> flattenWithFactoryTearDown;


        public static void InitConfig(ConfigFile configFile)
        {
            workItemsPerFrame = configFile.Bind("Performance", "WorkItemsPerFrame", 1,
                "Number of actions attempted per Frame. Default value is 1 (minimum since 0 would not do anything other than queue up work). " +
                "Larger values might make the job complete more quickly, but will also slow your system down noticeably");

            addGuideLines = configFile.Bind("Paint", "AddGuideLines", true,
                "If enabled painted lines at certain points on planet will be added");
            repaveAll = configFile.Bind("Paint", "RepaveAll", true,
                "If disabled only unpaved sections will be repaved. If disabled veins won't be explicitly impacted");
            buryVeinMode = configFile.Bind("Paint", "BuryVeinMode", BuryVeinMode.Tool);
            foundationDecorationMode = configFile.Bind("Paint", "FoundationDecorationMode", FoundationDecorationMode.Tool);

            addGuideLinesEquator = configFile.Bind("Paint", "AddGuideLinesEquator", true,
                "Allows disabling of the equator guideline individually. No effect if AddGuideLines is disabled");
            addGuideLinesMeridian = configFile.Bind("Paint", "AddGuideLinesMeridian", true,
                "Allows disabling of the meridian guidelines individually. No effect if AddGuideLines is disabled");
            addGuideLinesTropic = configFile.Bind("Paint", "AddGuideLinesTropic", false,
                "Allows disabling of the tropic guidelines individually. No effect if AddGuideLines is disabled. Currently bugged with larger radius planets");

            destroyFactoryAssemblers = configFile.Bind("Destruction", "DestroyFactoryAssemblers", false,
                "DANGEROUS - Destroy all factory machines (labs, assemblers, etc). It can be very slow so if you get bored waiting and want to delete stuff yourself, make sure to stop the bulldoze process. ");
            flattenWithFactoryTearDown = configFile.Bind("Destruction", "FlattenWithFactoryTearDown", false,
                "Use this only to enable adding foundation when destroying existing factory");
        }
    }
}