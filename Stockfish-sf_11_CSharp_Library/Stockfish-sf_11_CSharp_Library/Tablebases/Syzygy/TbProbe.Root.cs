using System;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.MoveGeneration;
using Stockfish_sf_11_CSharp_Library.Search;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Tablebases.Syzygy
{
    /// <summary>Tablebase settings fixed for the duration of one search.</summary>
    public readonly struct TablebaseConfig
    {
        public readonly int Cardinality, ProbeDepth;
        public readonly bool UseRule50, RootInTb;
        public TablebaseConfig(int cardinality, int probeDepth, bool useRule50, bool rootInTb)
        { Cardinality=cardinality; ProbeDepth=probeDepth; UseRule50=useRule50; RootInTb=rootInTb; }
    }
    public sealed partial class TbProbe
    {
        private static readonly int[] RootWdlRanks={-1000,-899,0,899,1000};
        private static readonly int[] RootWdlValues={-(int)Value.VALUE_MATE+ChessConstants.MAX_PLY+1,-2,0,2,(int)Value.VALUE_MATE-ChessConstants.MAX_PLY-1};
        public TablebaseConfig RankRootMoves(Position pos, RootMoves roots, int limit, int depth, bool rule50)
        {
            if (pos == null) throw new ArgumentNullException(nameof(pos));
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            if (limit < 0 || depth < 0) throw new ArgumentOutOfRangeException();
            lifetime.EnterReadLock();
            try
            {
                RequireAlive();
                int cardinality = limit, probeDepth = depth;
                if (cardinality > tables.MaxCardinality) { cardinality = tables.MaxCardinality; probeDepth = 0; }
                bool rootInTb = false, dtzAvailable = true;
                if (Enabled && roots.Count != 0 && cardinality >= Bitboard.PopCount(pos.Pieces()) && !pos.CanCastle(CastlingRights.ANY_CASTLING))
                {
                    rootInTb = RootProbeCore(pos, roots, rule50);
                    if (!rootInTb) { dtzAvailable = false; rootInTb = RootProbeWdlCore(pos, roots, rule50); }
                }
                if (rootInTb)
                {
                    roots.SortByTablebaseRank();
                    if (dtzAvailable || roots[0].TbScore <= Value.VALUE_DRAW) cardinality = 0;
                }
                else foreach (RootMove root in roots) root.TbRank = 0;
                return new TablebaseConfig(cardinality, probeDepth, rule50, rootInTb);
            }
            finally { lifetime.ExitReadLock(); }
        }
        public bool RootProbe(Position pos, RootMoves roots, bool rule50, bool wdlOnly = false)
        {
            if (pos == null || roots == null) throw new ArgumentNullException();
            lifetime.EnterReadLock();
            try { RequireAlive(); return Enabled && (wdlOnly ? RootProbeWdlCore(pos, roots, rule50) : RootProbeCore(pos, roots, rule50)); }
            finally { lifetime.ExitReadLock(); }
        }
        private bool RootProbeCore(Position pos, RootMoves roots, bool rule50)
        {
            var bank=queryScratch.Value!;
            var scratch=bank.Acquire();
            try
            {
                int cnt50 = pos.Rule50Count, bound = rule50 ? 900 : 1;
                bool repeated = pos.HasRepeated();
                foreach (RootMove root in roots)
                {
                    StateInfo state=scratch.State;state.checkSquares[0]=state.checkSquares[7]=0UL; ProbeState result = ProbeState.OK; int dtz;
                    pos.DoMove(root.Pv[0], state);
                    try
                    {
                        if (pos.Rule50Count == 0) dtz = (-Search(pos, ref result, false)).DtzBeforeZeroing();
                        else { dtz = -ProbeDtzCore(pos, ref result); dtz = dtz > 0 ? dtz+1 : dtz < 0 ? dtz-1 : dtz; }
                        if (pos.Checkers && dtz == 2 && !pos.HasLegalMove()) dtz = 1;
                    }
                    finally { pos.UndoMove(root.Pv[0]);state.previous=null; }
                    if (result == ProbeState.FAIL) return false;
                    int rank = dtz > 0 ? (dtz + cnt50 <= 99 && !repeated ? 1000 : 1000 - (dtz + cnt50))
                        : dtz < 0 ? (-dtz * 2 + cnt50 < 100 ? -1000 : -1000 + (-dtz + cnt50)) : 0;
                    root.TbRank = rank;
                    root.TbScore = rank >= bound ? (int)Value.VALUE_MATE - ChessConstants.MAX_PLY - 1
                        : rank > 0 ? Math.Max(3, rank-800) * (int)Value.PawnValueEg / 200
                        : rank == 0 ? 0 : rank > -bound ? Math.Min(-3,rank+800) * (int)Value.PawnValueEg / 200
                        : -(int)Value.VALUE_MATE + ChessConstants.MAX_PLY + 1;
                }
                return true;
            }
            finally { bank.Release(); }
        }
        private bool RootProbeWdlCore(Position pos, RootMoves roots, bool rule50)
        {
            var bank=queryScratch.Value!;
            var scratch=bank.Acquire();
            try
            {
                int[] ranks=RootWdlRanks,values=RootWdlValues;
                foreach (RootMove root in roots)
                {
                    StateInfo state=scratch.State;state.checkSquares[0]=state.checkSquares[7]=0UL; ProbeState result = ProbeState.OK; WDLScore wdl;
                    pos.DoMove(root.Pv[0], state);
                    try { wdl = -Search(pos, ref result, false); }
                    finally { pos.UndoMove(root.Pv[0]);state.previous=null; }
                    if (result == ProbeState.FAIL) return false;
                    root.TbRank = ranks[wdl+2];
                    if (!rule50) wdl = wdl > WDLScore.WDLDraw ? WDLScore.WDLWin : wdl < WDLScore.WDLDraw ? WDLScore.WDLLoss : WDLScore.WDLDraw;
                    root.TbScore = values[wdl+2];
                }
                return true;
            }
            finally { bank.Release(); }
        }
    }
}
