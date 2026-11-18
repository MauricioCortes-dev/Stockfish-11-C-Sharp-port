using System;
namespace Stockfish_sf_11_CSharp_Library.MoveGeneration
{
    public readonly struct GenType : IEquatable<GenType>
    {
        private readonly int value;
        private GenType(int value) { this.value=value; }
        public static readonly GenType CAPTURES=0, QUIETS=1, QUIET_CHECKS=2, EVASIONS=3, NON_EVASIONS=4, LEGAL=5;
        public static implicit operator int(GenType type) => type.value;
        public static implicit operator GenType(int value) => new GenType(value);
        public bool Equals(GenType other) => value==other.value;
        public override bool Equals(object? other) => other is GenType type && Equals(type);
        public override int GetHashCode() => value;
    }
}
