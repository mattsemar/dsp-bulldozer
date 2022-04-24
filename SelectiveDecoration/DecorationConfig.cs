using System;

namespace Bulldozer.SelectiveDecoration
{
    public readonly struct DecorationConfig : IEquatable<DecorationConfig>
    {
        // 1, 2 or 7 only, probably
        public readonly int ReformType;
        public readonly int ColorIndex;
        public static DecorationConfig None = new(-1, -1); 

        public DecorationConfig(int reformType, int colorIndex)
        {
            ReformType = reformType;
            ColorIndex = colorIndex;
        }

        public DecorationConfig(int colorIndex)
        {
            ColorIndex = colorIndex;

            var actionBuild = GameMain.mainPlayer?.controller.actionBuild;
            if (actionBuild == null)
            {
                ReformType = 1;
                return;
            }           
            
            // either use 1 or 2 depending on what the setting has 
            var brushType = 1;
            switch (PluginConfig.foundationDecorationMode.Value)
            {
                case FoundationDecorationMode.Tool:
                    brushType = actionBuild.reformTool.brushType;
                    if (brushType > 2)
                    {
                        // don't want to use Clear here, just use default
                        brushType = 1;
                    }
                    break;
                case FoundationDecorationMode.Paint:
                    brushType = 1;
                    break;
                case FoundationDecorationMode.Decorate:
                    brushType = 2;
                    break;
                case FoundationDecorationMode.Clear:
                    // don't allow clear, has to either be 1 or 2
                    brushType = 1;
                    break;
                default:
                    Log.Warn($"unexpected brush type requested {PluginConfig.foundationDecorationMode.Value}");
                    break;
            }

            ReformType = brushType;
        }

        public bool Equals(DecorationConfig other) => ReformType == other.ReformType && ColorIndex == other.ColorIndex;

        public override bool Equals(object obj) => obj is DecorationConfig other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (ReformType * 397) ^ ColorIndex;
            }
        }

        public bool IsNone()
        {
            return Equals(None);
        }
    }
}