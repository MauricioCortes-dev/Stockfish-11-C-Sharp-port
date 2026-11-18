using System;
using System.Runtime.CompilerServices;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    /// <summary>Stockfish 11 packed middle-game and end-game score.</summary>
    public readonly struct Score : IEquatable<Score>
    {
        private readonly int value;
        private Score(int value) { this.value=value; }
        public static readonly Score SCORE_ZERO=0;
        public static implicit operator int(Score x) => x.value;
        public static implicit operator Score(int x) => new Score(x);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Score MakeScore(int mg,int eg) => unchecked((int)((uint)eg<<16)+mg);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value MgValue(Score x) => unchecked((short)(ushort)x.value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value EgValue(Score x) => unchecked((short)(ushort)((uint)(x.value+0x8000)>>16));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Score operator +(Score x,Score y) => checked(x.value+y.value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Score operator -(Score x,Score y) => checked(x.value-y.value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Score operator -(Score x) => checked(-x.value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Score operator /(Score x,int n) => MakeScore((int)MgValue(x)/n,(int)EgValue(x)/n);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Score operator *(Score x,int n) => checked(x.value*n);
        [Obsolete("Stockfish forbids multiplying two scores; explicitly convert if intended.", true)]
        public static Score operator *(Score x,Score y) => throw new NotSupportedException();
        public static Score operator *(Score x,bool b) => b ? x : SCORE_ZERO;
        public bool Equals(Score other) => value==other.value;
        public override bool Equals(object? other) => other is Score x && Equals(x);
        public override int GetHashCode() => value;
    }
}
