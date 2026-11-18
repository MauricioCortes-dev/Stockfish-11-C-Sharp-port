using System;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct Rank : IEquatable<Rank>
    {
        private readonly int value;
        private Rank(int value) { this.value = value; }
        public static readonly Rank RANK_1 = 0, RANK_2 = 1, RANK_3 = 2, RANK_4 = 3, RANK_5 = 4, RANK_6 = 5, RANK_7 = 6, RANK_8 = 7, RANK_NB = 8;
        public static implicit operator int(Rank value) => value.value;
        public static implicit operator Rank(int value) => new Rank(value);
        public static Rank operator ++(Rank x) => new Rank(checked(x.value + 1));
        public static Rank operator --(Rank x) => new Rank(checked(x.value - 1));
        public static Rank RankOf(Square square) => (int)square >> 3;
        public static Rank RelativeRank(Color color, Square square) => RelativeRank(color, RankOf(square));
        public static Rank RelativeRank(Color color, Rank rank) => (int)rank ^ ((int)color * 7);
        public bool Equals(Rank other) => value == other.value;
        public override bool Equals(object? other) => other is Rank x && Equals(x);
        public override int GetHashCode() => value;
        public override string ToString() => value.ToString();
    }
}
