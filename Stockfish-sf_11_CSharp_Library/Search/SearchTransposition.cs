using System;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Lookup and early TT cutoff in Stockfish 11's main search.</summary>
    public static class SearchTransposition
    {
        /// <summary>Probes the position key, refreshes its entry and applies the non-PV cutoff and history updates.</summary>
        public static bool TryCutoff(Position position, SearchWorkerState worker, int ply, Depth depth, Value beta, bool pvNode,
            out Move ttMove, out Value ttValue, out bool ttHit, out bool ttPv)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            if(worker==null)throw new ArgumentNullException(nameof(worker));
            if(ply<1 || ply>=ChessConstants.MAX_PLY)throw new ArgumentOutOfRangeException(nameof(ply));
            Stack frame=worker.Frame(ply);
            Key key=(ulong)position.Key ^ unchecked((ulong)(long)((int)frame.ExcludedMove << 16));
            ref TTEntry entry=ref worker.Table.Probe(key,out ttHit);
            return TryCutoff(position,worker,ply,depth,beta,pvNode,ref entry,ttHit,out ttMove,out ttValue,out ttPv);
        }

        // The search already owns this live slot reference and its hit result.
        // Do not probe again: another worker may change the cluster in between.
        internal static bool TryCutoff(Position position,SearchWorkerState worker,int ply,Depth depth,Value beta,bool pvNode,
            ref TTEntry entry,bool ttHit,out Move ttMove,out Value ttValue,out bool ttPv)
        {
            Stack previous=worker.Frame(ply-1);
            ttValue=ttHit?SearchValue.FromTt(entry.Value,ply,position.Rule50Count):Value.VALUE_NONE;
            ttMove=ttHit?entry.Move:Move.MOVE_NONE;
            ttPv=pvNode || (ttHit && entry.IsPv);
            worker.TtHitAverage=4095*worker.TtHitAverage/4096+(ttHit?1024:0);
            if(!pvNode && ttHit && entry.Depth>=depth && ttValue!=Value.VALUE_NONE
                && ((int)entry.Bound & (ttValue>=beta?2:1))!=0)
            {
                if((int)ttMove!=0)
                {
                    Square previousSquare=Move.ToSquare(previous.CurrentMove);
                    if(ttValue>=beta)
                    {
                        if(!position.CaptureOrPromotion(ttMove))
                            SearchHeuristics.UpdateQuietStats(position,worker,ply,ttMove,SearchHeuristics.StatBonus(depth));
                        if(previous.MoveCount<=2 && position.CapturedPiece==Piece.NO_PIECE)
                            SearchHeuristics.UpdateContinuationHistories(worker,ply-1,position.PieceOn(previousSquare),previousSquare,-SearchHeuristics.StatBonus((int)depth+1));
                    }
                    else if(!position.CaptureOrPromotion(ttMove))
                    {
                        int penalty=-SearchHeuristics.StatBonus(depth);
                        worker.MainHistory[position.SideToMove,Move.FromTo(ttMove)] <<= penalty;
                        SearchHeuristics.UpdateContinuationHistories(worker,ply,position.MovedPiece(ttMove),Move.ToSquare(ttMove),penalty);
                    }
                }
                if(position.Rule50Count<90)return true;
            }
            return false;
        }
    }
}
