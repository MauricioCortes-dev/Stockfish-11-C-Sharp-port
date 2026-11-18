using System;
using System.Runtime.CompilerServices;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct Bitboard : IEquatable<Bitboard>
    {
        private readonly ulong value;
        // A power of two multiplied by this sequence selects a unique six-bit
        // index. Both scans reuse this immutable 64-byte lookup table.
        private const ulong DeBruijn = 0x03f79d71b4cb0a89UL;
        private static readonly byte[] SingleBitIndices =
        {
            0,1,48,2,57,49,28,3,61,58,50,42,38,29,17,4,
            62,55,59,36,53,51,43,22,45,39,33,30,24,18,12,5,
            63,47,56,27,60,41,37,16,54,35,52,21,44,32,23,11,
            46,26,40,15,34,20,31,10,25,14,19,9,13,8,7,6
        };
        private Bitboard(ulong value) { this.value=value; }
        public static implicit operator ulong(Bitboard board) => board.value;
        public static implicit operator Bitboard(ulong value) => new Bitboard(value);
        public static implicit operator bool(Bitboard board) => board.value != 0;
        public static Bitboard operator &(Bitboard a,Bitboard b) => a.value & b.value;
        public static Bitboard operator |(Bitboard a,Bitboard b) => a.value | b.value;
        public static Bitboard operator ^(Bitboard a,Bitboard b) => a.value ^ b.value;
        public static Bitboard operator ~(Bitboard b) => ~b.value;
        public static Bitboard operator <<(Bitboard b,int n) => b.value << n;
        public static Bitboard operator >>(Bitboard b,int n) => b.value >> n;
        public static Bitboard operator +(Bitboard a,Bitboard b) => unchecked(a.value+b.value);
        public static Bitboard operator -(Bitboard a,Bitboard b) => unchecked(a.value-b.value);
        public static Bitboard operator *(Bitboard a,Bitboard b) => unchecked(a.value*b.value);
        public static Bitboard operator &(Bitboard b,Square s) => b.value & (ulong)SquareBB(s);
        public static Bitboard operator |(Bitboard b,Square s) => b.value | (ulong)SquareBB(s);
        public static Bitboard operator ^(Bitboard b,Square s) => b.value ^ (ulong)SquareBB(s);
        public static Bitboard operator &(Square s,Bitboard b) => b & s;
        public static Bitboard operator |(Square s,Bitboard b) => b | s;
        public static Bitboard operator ^(Square s,Bitboard b) => b ^ s;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard SquareBB(Square s) { if(!Square.IsOk(s)) throw new ArgumentOutOfRangeException(nameof(s)); return 1UL << (int)s; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool MoreThanOne(Bitboard board) => (board.value & unchecked(board.value-1)) != 0;
        /// <summary>Counts bits in a fixed number of scalar operations (SWAR).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(Bitboard board)
        {
            unchecked
            {
                ulong x=board.value;
                x-=(x>>1)&0x5555555555555555UL;
                x=(x&0x3333333333333333UL)+((x>>2)&0x3333333333333333UL);
                x=(x+(x>>4))&0x0f0f0f0f0f0f0f0fUL;
                return (int)((x*0x0101010101010101UL)>>56);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Square SingleBitIndex(ulong bit) => SingleBitIndices[unchecked(bit*DeBruijn)>>58];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square Lsb(Bitboard board)
        {
            ulong x=board.value;
            if(x==0) throw new ArgumentException("Bitboard must be nonzero.",nameof(board));
            return SingleBitIndex(x&unchecked(0UL-x));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square Msb(Bitboard board)
        {
            ulong x=board.value;
            if(x==0) throw new ArgumentException("Bitboard must be nonzero.",nameof(board));
            // Fill below the highest bit, then isolate that bit. This avoids
            // data-dependent scan loops and also works on 32-bit runtimes.
            x|=x>>1; x|=x>>2; x|=x>>4;
            x|=x>>8; x|=x>>16; x|=x>>32;
            return SingleBitIndex(x-(x>>1));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square PopLsb(ref Bitboard board) {Square s=Lsb(board);board=board.value & unchecked(board.value-1);return s;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Square FrontmostSquare(Color color,Bitboard board) => color == Color.WHITE ? Msb(board) : Lsb(board);
        public static Bitboard RankBB(Rank rank) => 0xffUL << (8*(int)rank);
        public static Bitboard FileBB(File file) => 0x0101010101010101UL << (int)file;
        public static Bitboard RankBB(Square square) => RankBB(Rank.RankOf(square));
        public static Bitboard FileBB(Square square) => FileBB(File.FileOf(square));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard Shift(Direction direction,Bitboard board) {
            ulong b=board.value, a=0x0101010101010101UL, h=0x8080808080808080UL;
            switch((int)direction) {case 8:return b<<8;case -8:return b>>8;case 16:return b<<16;case -16:return b>>16;
                case 1:return (b&~h)<<1;case -1:return (b&~a)>>1;case 9:return (b&~h)<<9;case 7:return (b&~a)<<7;
                case -7:return (b&~h)>>7;case -9:return (b&~a)>>9;default:return 0UL;}
        }
        public static bool OppositeColors(Square a,Square b) {
            const ulong dark=0xAA55AA55AA55AA55UL;
            return ((dark & (ulong)SquareBB(a)) != 0) != ((dark & (ulong)SquareBB(b)) != 0);
        }
        public static Bitboard AdjacentFiles(Square square) => Shift(Direction.EAST,FileBB(square)) | Shift(Direction.WEST,FileBB(square));
        public static Bitboard ForwardRanks(Color color,Square square) => color==Color.WHITE
            ? ~0xffUL << (8*(int)Rank.RankOf(square)) : ~0xff00000000000000UL >> (8*(7-(int)Rank.RankOf(square)));
        public static Bitboard ForwardFile(Color color,Square square) => ForwardRanks(color,square) & FileBB(square);
        public static Bitboard PawnAttackSpan(Color color,Square square) => ForwardRanks(color,square) & AdjacentFiles(square);
        public static Bitboard PassedPawnSpan(Color color,Square square) => ForwardRanks(color,square) & (AdjacentFiles(square) | FileBB(square));
        public static Bitboard PawnAttacks(Color color,Bitboard board) => color==Color.WHITE
            ? (ulong)Shift(Direction.NORTH_WEST,board) | (ulong)Shift(Direction.NORTH_EAST,board)
            : (ulong)Shift(Direction.SOUTH_WEST,board) | (ulong)Shift(Direction.SOUTH_EAST,board);
        public static Bitboard PawnDoubleAttacks(Color color,Bitboard board) => color==Color.WHITE
            ? (ulong)Shift(Direction.NORTH_WEST,board) & (ulong)Shift(Direction.NORTH_EAST,board)
            : (ulong)Shift(Direction.SOUTH_WEST,board) & (ulong)Shift(Direction.SOUTH_EAST,board);
        public bool Equals(Bitboard other) => value==other.value;
        public override bool Equals(object? other) => other is Bitboard board && Equals(board);
        public override int GetHashCode() => value.GetHashCode();
        public override string ToString() => value.ToString();
    }
}
