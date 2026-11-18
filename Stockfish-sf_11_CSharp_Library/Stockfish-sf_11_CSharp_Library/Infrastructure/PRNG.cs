using System;
using Stockfish_sf_11_CSharp_Library.Types;
namespace Stockfish_sf_11_CSharp_Library.Infrastructure
{
    /// <summary>Stockfish 11 xorshift64*; mutable state belongs to the caller.</summary>
    public struct PRNG
    {
        private ulong state;
        public PRNG(ulong seed) { if(seed==0) throw new ArgumentOutOfRangeException(nameof(seed)); state=seed; }
        private ulong Next64() { if(state==0) throw new InvalidOperationException("PRNG requires a nonzero seed."); unchecked { state^=state>>12; state^=state<<25; state^=state>>27; return state*2685821657736338717UL; } }
        public ulong Rand64() => Next64();
        public ulong SparseRand64() { ulong a=Next64(), b=Next64(), c=Next64(); return a&b&c; }
        public T Rand<T>() where T:struct => ConvertResult<T>(Next64());
        public T SparseRand<T>() where T:struct => ConvertResult<T>(SparseRand64());
        private static T ConvertResult<T>(ulong x) where T:struct {
            object result;
            unchecked {
                if(typeof(T)==typeof(ulong)) result=x;
                else if(typeof(T)==typeof(uint)) result=(uint)x;
                else if(typeof(T)==typeof(ushort)) result=(ushort)x;
                else if(typeof(T)==typeof(byte)) result=(byte)x;
                else if(typeof(T)==typeof(long)) result=(long)x;
                else if(typeof(T)==typeof(int)) result=(int)x;
                else if(typeof(T)==typeof(short)) result=(short)x;
                else if(typeof(T)==typeof(sbyte)) result=(sbyte)x;
                else if(typeof(T)==typeof(Key)) result=(Key)x;
                else if(typeof(T)==typeof(Bitboard)) result=(Bitboard)x;
                else throw new NotSupportedException("Unsupported PRNG result type.");
            }
            return (T)result;
        }
    }
}
