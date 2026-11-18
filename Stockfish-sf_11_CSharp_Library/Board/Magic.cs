using System;
using Stockfish_sf_11_CSharp_Library.Types;
namespace Stockfish_sf_11_CSharp_Library.Board
{
    /// <summary>Stockfish 11 magic index and its per-square attack table.</summary>
    public readonly struct Magic
    {
        public Bitboard Mask { get; }
        public Bitboard Multiplier { get; }
        public int Shift { get; }
        private readonly Bitboard[] attacks;
        private readonly bool is64Bit;
        internal Magic(Bitboard mask,Bitboard multiplier,int shift,Bitboard[] attacks,bool is64Bit)
        { Mask=mask; Multiplier=multiplier; Shift=shift; this.attacks=attacks; this.is64Bit=is64Bit; }
        public uint Index(Bitboard occupied) => ComputeIndex(occupied,Mask,Multiplier,Shift,is64Bit);
        public static uint ComputeIndex(Bitboard occupied,Bitboard mask,Bitboard multiplier,int shift,bool is64Bit)
        {
            unchecked {
                ulong o=occupied,m=mask,k=multiplier;
                if(is64Bit) return (uint)(((o&m)*k)>>shift);
                uint lo=(uint)o&(uint)m, hi=(uint)(o>>32)&(uint)(m>>32);
                return (lo*(uint)k ^ hi*(uint)(k>>32))>>shift;
            }
        }
        public Bitboard Attack(Bitboard occupied) {
            if(attacks==null) throw new InvalidOperationException("Magic is not initialized.");
            return attacks[Index(occupied)];
        }
    }
}
