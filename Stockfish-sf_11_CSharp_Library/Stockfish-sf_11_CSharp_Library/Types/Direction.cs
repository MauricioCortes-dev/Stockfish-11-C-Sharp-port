using System;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct Direction : IEquatable<Direction>
    {
        private readonly int value;
        private Direction(int value) { this.value = value; }
        public static readonly Direction NORTH = 8, EAST = 1, SOUTH = -8, WEST = -1, NORTH_EAST = 9, SOUTH_EAST = -7, SOUTH_WEST = -9, NORTH_WEST = 7;
        public static implicit operator int(Direction value) => value.value;
        public static implicit operator Direction(int value) => new Direction(value);
        public static Direction operator +(Direction a, Direction b) => checked(a.value + b.value);
        public static Direction operator -(Direction a, Direction b) => checked(a.value - b.value);
        public static Direction operator -(Direction x) => checked(-x.value);
        public static Direction operator *(Direction x, int n) => checked(x.value * n);
        public static Direction operator *(int n, Direction x) => checked(n * x.value);
        public static Direction operator /(Direction x, int n) => x.value / n;
        public static int operator /(Direction x, Direction y) => x.value / y.value;
        public static Direction PawnPush(Color color) => color == Color.WHITE ? NORTH : SOUTH;
        public bool Equals(Direction other) => value == other.value;
        public override bool Equals(object? other) => other is Direction x && Equals(x);
        public override int GetHashCode() => value;
        public override string ToString() => value.ToString();
    }
}
