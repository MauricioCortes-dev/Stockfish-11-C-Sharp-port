using System;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct Square : IEquatable<Square>
    {
        private readonly int value;
        private Square(int value) { this.value = value; }
        public static readonly Square SQ_A1 = 0, SQ_B1 = 1, SQ_C1 = 2, SQ_D1 = 3, SQ_E1 = 4, SQ_F1 = 5, SQ_G1 = 6, SQ_H1 = 7, SQ_A2 = 8, SQ_B2 = 9, SQ_C2 = 10, SQ_D2 = 11, SQ_E2 = 12, SQ_F2 = 13, SQ_G2 = 14, SQ_H2 = 15, SQ_A3 = 16, SQ_B3 = 17, SQ_C3 = 18, SQ_D3 = 19, SQ_E3 = 20, SQ_F3 = 21, SQ_G3 = 22, SQ_H3 = 23, SQ_A4 = 24, SQ_B4 = 25, SQ_C4 = 26, SQ_D4 = 27, SQ_E4 = 28, SQ_F4 = 29, SQ_G4 = 30, SQ_H4 = 31, SQ_A5 = 32, SQ_B5 = 33, SQ_C5 = 34, SQ_D5 = 35, SQ_E5 = 36, SQ_F5 = 37, SQ_G5 = 38, SQ_H5 = 39, SQ_A6 = 40, SQ_B6 = 41, SQ_C6 = 42, SQ_D6 = 43, SQ_E6 = 44, SQ_F6 = 45, SQ_G6 = 46, SQ_H6 = 47, SQ_A7 = 48, SQ_B7 = 49, SQ_C7 = 50, SQ_D7 = 51, SQ_E7 = 52, SQ_F7 = 53, SQ_G7 = 54, SQ_H7 = 55, SQ_A8 = 56, SQ_B8 = 57, SQ_C8 = 58, SQ_D8 = 59, SQ_E8 = 60, SQ_F8 = 61, SQ_G8 = 62, SQ_H8 = 63, SQ_NONE = 64, SQUARE_NB = 64;
        public static implicit operator int(Square x) => x.value;
        public static implicit operator Square(int x) => new Square(x);
        public static Bitboard operator |(Square a, Square b) => (ulong)Bitboard.SquareBB(a) | (ulong)Bitboard.SquareBB(b);
        public static Square operator ~(Square x) => x.value ^ (int)SQ_A8;
        public static Square operator +(Square x, Direction d) => checked(x.value + (int)d);
        public static Square operator -(Square x, Direction d) => checked(x.value - (int)d);
        public static Square operator ++(Square x) => checked(x.value + 1);
        public static Square operator --(Square x) => checked(x.value - 1);
        public static Square MakeSquare(File file, Rank rank) => ((int)rank << 3) + (int)file;
        public static bool IsOk(Square square) => square.value >= SQ_A1 && square.value <= SQ_H8;
        public static Square RelativeSquare(Color color, Square square) => square.value ^ ((int)color * 56);
        public bool Equals(Square other) => value == other.value;
        public override bool Equals(object? other) => other is Square x && Equals(x);
        public override int GetHashCode() => value;
        public override string ToString() => value.ToString();
    }
}
