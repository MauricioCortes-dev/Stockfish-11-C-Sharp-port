using System;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct Bound : IEquatable<Bound>
    {
        private readonly int value;
        private Bound(int value) { this.value = value; }
        public static readonly Bound BOUND_NONE = 0, BOUND_UPPER = 1, BOUND_LOWER = 2, BOUND_EXACT = 3;
        public static implicit operator int(Bound bound) => bound.value;
        public static implicit operator Bound(int value) => new Bound(value);
        public bool Equals(Bound other) => value == other.value;
        public override bool Equals(object? other) => other is Bound bound && Equals(bound);
        public override int GetHashCode() => value;
        public override string ToString() => value.ToString();
    }
}
