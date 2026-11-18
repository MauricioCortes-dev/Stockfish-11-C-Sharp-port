using System;
using System.Runtime.InteropServices;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Stockfish 11's compact ten-byte transposition-table entry.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 2, Size = 10)]
    public struct TTEntry
    {
        internal ushort key16;
        private ushort move16;
        private short value16, eval16;
        internal byte genBound8, depth8;

        public Move Move => move16;
        public Value Value => value16;
        public Value Eval => eval16;
        public Depth Depth => depth8 + (int)Types.Depth.DEPTH_OFFSET;
        public bool IsPv => (genBound8 & 4) != 0;
        public Bound Bound => genBound8 & 3;

        /// <summary>The table supplies its generation; mutations through this reference update the original slot.</summary>
        public void Save(TranspositionTable table, Key key, Value value, bool pv, Bound bound, Depth depth, Move move, Value evaluation)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            ulong k = key;
            ushort keyPart = (ushort)(k >> 48);
            if ((int)move != 0 || keyPart != key16) move16 = unchecked((ushort)(int)move);
            if (keyPart != key16 || (int)depth - (int)Types.Depth.DEPTH_OFFSET > depth8 - 4 || (int)bound == (int)Types.Bound.BOUND_EXACT)
            {
                if ((int)depth < (int)Types.Depth.DEPTH_OFFSET) throw new ArgumentOutOfRangeException(nameof(depth));
                key16 = keyPart;
                value16 = unchecked((short)(int)value);
                eval16 = unchecked((short)(int)evaluation);
                genBound8 = unchecked((byte)(table.Generation | (pv ? 4 : 0) | (int)bound));
                depth8 = unchecked((byte)((int)depth - (int)Types.Depth.DEPTH_OFFSET));
            }
        }
    }
}
