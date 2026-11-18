using System;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct MoveType : IEquatable<MoveType>
    {
        private readonly int value;
        private MoveType(int value) { this.value=value; }
        public static readonly MoveType NORMAL=0, PROMOTION=1<<14, ENPASSANT=2<<14, CASTLING=3<<14;
        public static implicit operator int(MoveType type) => type.value;
        public static implicit operator MoveType(int value) => new MoveType(value);
        public bool Equals(MoveType other) => value==other.value;
        public override bool Equals(object? other) => other is MoveType type && Equals(type);
        public override int GetHashCode() => value;
    }
}
