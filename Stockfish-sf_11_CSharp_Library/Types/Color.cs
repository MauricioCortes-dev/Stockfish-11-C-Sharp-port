using System;

namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct Color : IEquatable<Color>
    {
        private readonly int value;
        private Color(int value) { this.value = value; }
        public static readonly Color WHITE = new Color(0), BLACK = new Color(1), COLOR_NB = new Color(2);
        public static implicit operator int(Color color) => color.value;
        public static implicit operator Color(int value) => new Color(value);
        public static Color operator ~(Color color) => new Color(color.value ^ BLACK.value);
        public static CastlingRights operator &(Color color, CastlingRights rights) =>
            (color.value == WHITE.value ? CastlingRights.WHITE_CASTLING : CastlingRights.BLACK_CASTLING) & (int)rights;
        public bool Equals(Color other) => value == other.value;
        public override bool Equals(object? other) => other is Color color && Equals(color);
        public override int GetHashCode() => value;
        public override string ToString() => value.ToString();
    }
}
