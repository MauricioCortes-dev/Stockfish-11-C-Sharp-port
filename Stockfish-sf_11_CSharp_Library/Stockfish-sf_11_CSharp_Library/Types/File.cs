using System;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct File : IEquatable<File>
    {
        private readonly int value;
        private File(int value) { this.value = value; }
        public static readonly File FILE_A = 0, FILE_B = 1, FILE_C = 2, FILE_D = 3, FILE_E = 4, FILE_F = 5, FILE_G = 6, FILE_H = 7, FILE_NB = 8;
        public static implicit operator int(File value) => value.value;
        public static implicit operator File(int value) => new File(value);
        public static File operator ++(File x) => new File(checked(x.value + 1));
        public static File operator --(File x) => new File(checked(x.value - 1));
        public static File FileOf(Square square) => (int)square & 7;
        public static File MapToQueenside(File file) => Math.Min((int)file, (int)FILE_H - file);
        public bool Equals(File other) => value == other.value;
        public override bool Equals(object? other) => other is File x && Equals(x);
        public override int GetHashCode() => value;
        public override string ToString() => value.ToString();
    }
}
