using System;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct ScaleFactor : IEquatable<ScaleFactor>
    {
        private readonly int value;
        private ScaleFactor(int value) { this.value = value; }
        public static readonly ScaleFactor SCALE_FACTOR_DRAW = 0, SCALE_FACTOR_NORMAL = 64, SCALE_FACTOR_MAX = 128, SCALE_FACTOR_NONE = 255;
        public static implicit operator int(ScaleFactor value) => value.value;
        public static implicit operator ScaleFactor(int value) => new ScaleFactor(value);
        public bool Equals(ScaleFactor other) => value == other.value;
        public override bool Equals(object? other) => other is ScaleFactor x && Equals(x);
        public override int GetHashCode() => value;
        public override string ToString() => value.ToString();
    }
}
