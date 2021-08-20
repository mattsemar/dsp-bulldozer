namespace Bulldozer
{
    public enum ItemDestructionPhase
    {
        Inserters = 0,
        Belts = 1,
        Assemblers = 2,
        Stations = 3,
        Other = 4,
        Done = 5
    }

    public class ClearFactoryWorkItem
    {
        public int ItemId;
        public ItemDestructionPhase Phase;
        public PlanetFactory PlanetFactory;
        public Player Player;
    }
}