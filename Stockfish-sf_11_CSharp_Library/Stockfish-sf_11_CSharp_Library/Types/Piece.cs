using System;
using System.Runtime.CompilerServices;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct Piece : IEquatable<Piece>
    {
        private readonly int value;
        private Piece(int value) { this.value = value; }
        public static readonly Piece NO_PIECE = 0, W_PAWN = 1, W_KNIGHT = 2, W_BISHOP = 3, W_ROOK = 4, W_QUEEN = 5, W_KING = 6,
            B_PAWN = 9, B_KNIGHT = 10, B_BISHOP = 11, B_ROOK = 12, B_QUEEN = 13, B_KING = 14, PIECE_NB = 16;
        public static implicit operator int(Piece x) => x.value;
        public static implicit operator Piece(int x) => new Piece(x);
        public static Piece operator ~(Piece x) => x.value ^ 8;
        public static Piece operator ++(Piece x) => checked(x.value + 1);
        public static Piece operator --(Piece x) => checked(x.value - 1);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Piece MakePiece(Color color, PieceType type) => ((int)color << 3) + (int)type;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PieceType TypeOf(Piece piece) => piece.value & 7;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color ColorOf(Piece piece) {
            if (piece.value == NO_PIECE) throw new ArgumentException("A piece is required.", nameof(piece));
            return piece.value >> 3;
        }
        public bool Equals(Piece other) => value == other.value;
        public override bool Equals(object? other) => other is Piece x && Equals(x);
        public override int GetHashCode() => value;
    }
}
