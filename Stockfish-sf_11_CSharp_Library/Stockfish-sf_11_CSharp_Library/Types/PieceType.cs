using System;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct PieceType : IEquatable<PieceType>
    {
        private readonly int value;
        private PieceType(int value) { this.value = value; }
        public static readonly PieceType NO_PIECE_TYPE = 0, ALL_PIECES = 0, PAWN = 1, KNIGHT = 2, BISHOP = 3, ROOK = 4, QUEEN = 5, KING = 6, PIECE_TYPE_NB = 8;
        public static implicit operator int(PieceType x) => x.value;
        public static implicit operator PieceType(int x) => new PieceType(x);
        public static PieceType operator ++(PieceType x) => checked(x.value + 1);
        public static PieceType operator --(PieceType x) => checked(x.value - 1);
        public bool Equals(PieceType other) => value == other.value;
        public override bool Equals(object? other) => other is PieceType x && Equals(x);
        public override int GetHashCode() => value;
    }
}
