using System;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct Value : IEquatable<Value>
    {
        private readonly int value;
        private Value(int value) { this.value = value; }
        public static readonly Value VALUE_ZERO=0, VALUE_DRAW=0, VALUE_KNOWN_WIN=10000, VALUE_MATE=32000, VALUE_INFINITE=32001, VALUE_NONE=32002,
            VALUE_MATE_IN_MAX_PLY=32000-2*ChessConstants.MAX_PLY, VALUE_MATED_IN_MAX_PLY=-32000+2*ChessConstants.MAX_PLY,
            PawnValueMg=128, PawnValueEg=213, KnightValueMg=781, KnightValueEg=854, BishopValueMg=825, BishopValueEg=915,
            RookValueMg=1276, RookValueEg=1380, QueenValueMg=2538, QueenValueEg=2682, MidgameLimit=15258, EndgameLimit=3915;
        public static implicit operator int(Value x) => x.value;
        public static implicit operator Value(int x) => new Value(x);
        public static Value operator +(Value x,Value y) => checked(x.value+y.value);
        public static Value operator -(Value x,Value y) => checked(x.value-y.value);
        public static Value operator +(Value x,int y) => checked(x.value+y);
        public static Value operator -(Value x,int y) => checked(x.value-y);
        public static Value operator -(Value x) => checked(-x.value);
        public static Value operator *(Value x,int n) => checked(x.value*n);
        public static Value operator *(int n,Value x) => checked(n*x.value);
        public static Value operator /(Value x,int n) => x.value/n;
        public static int operator /(Value x,Value y) => x.value/y.value;
        public static Value MateIn(int ply) => checked((int)VALUE_MATE-ply);
        public static Value MatedIn(int ply) => checked(-(int)VALUE_MATE+ply);
        public bool Equals(Value other) => value==other.value;
        public override bool Equals(object? other) => other is Value x && Equals(x);
        public override int GetHashCode() => value;
    }
}
