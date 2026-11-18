using System;
using System.Runtime.CompilerServices;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    /// <summary>Stockfish 11 Move is an int enum encoding moves in its lower 16 bits.</summary>
    public readonly struct Move : IEquatable<Move>
    {
        private readonly int value;
        private Move(int value) { this.value=value; }
        public static readonly Move MOVE_NONE=0, MOVE_NULL=65;
        public static implicit operator int(Move move) => move.value;
        public static implicit operator Move(int value) => new Move(value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square FromSquare(Move move) => (move.value>>6)&63;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square ToSquare(Move move) => move.value&63;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FromTo(Move move) => move.value&4095;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MoveType TypeOf(Move move) => move.value&(3<<14);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PieceType PromotionType(Move move) => ((move.value>>12)&3)+(int)PieceType.KNIGHT;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Move MakeMove(Square from,Square to) => ((int)from<<6)+(int)to;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Move ReverseMove(Move move) => MakeMove(ToSquare(move),FromSquare(move));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Move Make(MoveType type,Square from,Square to,PieceType promotion) => (int)type+(((int)promotion-(int)PieceType.KNIGHT)<<12)+((int)from<<6)+(int)to;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Move Make(MoveType type,Square from,Square to) => Make(type,from,to,PieceType.KNIGHT);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOk(Move move) => FromSquare(move)!=ToSquare(move);
        public bool Equals(Move other) => value==other.value;
        public override bool Equals(object? other) => other is Move move && Equals(move);
        public override int GetHashCode() => value;
    }
}
