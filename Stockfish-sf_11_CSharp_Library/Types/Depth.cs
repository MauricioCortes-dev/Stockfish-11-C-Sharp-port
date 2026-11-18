using System;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct Depth : IEquatable<Depth>
    {
        private readonly int value;
        private Depth(int value) { this.value = value; }
        public static readonly Depth DEPTH_QS_CHECKS = 0, DEPTH_QS_NO_CHECKS = -1, DEPTH_QS_RECAPTURES = -5, DEPTH_NONE = -6, DEPTH_OFFSET = -6;
        public static implicit operator int(Depth value) => value.value;
        public static implicit operator Depth(int value) => new Depth(value);
        public bool Equals(Depth other) => value == other.value;
        public override bool Equals(object? other) => other is Depth x && Equals(x);
        public override int GetHashCode() => value;
        public override string ToString() => value.ToString();
    }
}
