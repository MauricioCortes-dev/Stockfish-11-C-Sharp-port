using System;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Mate-score conversions between root-relative search and TT storage.</summary>
    public static class SearchValue
    {
        public static Value ToTt(Value value, int ply)
        {
            if (value == Value.VALUE_NONE) throw new ArgumentException("VALUE_NONE cannot be saved as a search score.", nameof(value));
            return value >= Value.VALUE_MATE_IN_MAX_PLY ? value + ply : value <= Value.VALUE_MATED_IN_MAX_PLY ? value - ply : value;
        }
        public static Value FromTt(Value value, int ply, int rule50)
        {
            if (value == Value.VALUE_NONE) return Value.VALUE_NONE;
            if (value >= Value.VALUE_MATE_IN_MAX_PLY)
                return Value.VALUE_MATE - value > 99 - rule50 ? Value.VALUE_MATE_IN_MAX_PLY : value - ply;
            if (value <= Value.VALUE_MATED_IN_MAX_PLY)
                return Value.VALUE_MATE + value > 99 - rule50 ? Value.VALUE_MATED_IN_MAX_PLY : value + ply;
            return value;
        }
    }
}
