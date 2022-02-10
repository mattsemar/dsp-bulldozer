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