using System;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct CastlingRights : IEquatable<CastlingRights>
    {
        private readonly int value;
        private CastlingRights(int value) { this.value = value; }
        public static readonly CastlingRights NO_CASTLING = 0, WHITE_OO = 1, WHITE_OOO = 2, BLACK_OO = 4, BLACK_OOO = 8,
            KING_SIDE = 5, QUEEN_SIDE = 10, WHITE_CASTLING = 3, BLACK_CASTLING = 12, ANY_CASTLING = 15, CASTLING_RIGHT_NB = 16;
        public static implicit operator int(CastlingRights rights) => rights.value;
        public static implicit operator CastlingRights(int value) => new CastlingRights(value);
        public bool Equals(CastlingRights other) => value == other.value;
        public override bool Equals(object? other) => other is CastlingRights rights && Equals(rights);
        public override int GetHashCode() => value;
        public override string ToString() => value.ToString();
    }
}
