using System;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Search constants and history updates from Stockfish 11.</summary>
    public sealed class SearchHeuristics
    {
        private static readonly int[] ContinuationOffsets={1,2,4,6};
        private readonly int[] reductions = new int[ChessConstants.MAX_MOVES];
        public SearchHeuristics(int threadCount)
        {
            if(threadCount<1)throw new ArgumentOutOfRangeException(nameof(threadCount));
            for(int i=1;i<reductions.Length;++i)reductions[i]=(int)((24.8+Math.Log(threadCount)/2)*Math.Log(i));
        }
        public int Reduction(bool improving,int depth,int moveNumber)
        {
            int r=reductions[depth]*reductions[moveNumber];
            return (r+511)/1024+(!improving && r>1007?1:0);
        }
        public static int StatBonus(int depth) => depth>15?-8:19*depth*depth+155*depth-132;
        public static int FutilityMargin(int depth,bool improving)=>217*(depth-(improving?1:0));
        public static int FutilityMoveCount(bool improving,int depth)=>(5+depth*depth)*(1+(improving?1:0))/2-1;

        public static void UpdateContinuationHistories(SearchWorkerState worker,int ply,Piece piece,Square to,int bonus)
        {
            if(worker==null)throw new ArgumentNullException(nameof(worker));
            foreach(int offset in ContinuationOffsets)
            {
                Stack previous=worker.Frame(ply-offset);
                if(Move.IsOk(previous.CurrentMove)) previous.ContinuationHistory![piece,to] <<= bonus;
            }
        }
        public static void UpdateQuietStats(Position position,SearchWorkerState worker,int ply,Move move,int bonus)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            if(worker==null)throw new ArgumentNullException(nameof(worker));
            Stack frame=worker.Frame(ply);
            if((int)frame.Killers[0]!=(int)move){frame.Killers[1]=frame.Killers[0];frame.Killers[0]=move;}
            Color us=position.SideToMove;
            worker.MainHistory[us,Move.FromTo(move)] <<= bonus;
            UpdateContinuationHistories(worker,ply,position.MovedPiece(move),Move.ToSquare(move),bonus);
            if(Piece.TypeOf(position.MovedPiece(move))!=PieceType.PAWN)
                worker.MainHistory[us,Move.FromTo(Move.ReverseMove(move))] <<= -bonus;
            Move previous=worker.Frame(ply-1).CurrentMove;
            if(Move.IsOk(previous)){Square square=Move.ToSquare(previous);worker.CounterMoves[position.PieceOn(square),square]=move;}
        }
        public static void UpdateAllStats(Position position,SearchWorkerState worker,int ply,Move bestMove,Value bestValue,Value beta,Square previousSquare,Move[] quiets,Move[] captures,Depth depth)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            if(worker==null)throw new ArgumentNullException(nameof(worker));
            if(quiets==null)throw new ArgumentNullException(nameof(quiets));
            if(captures==null)throw new ArgumentNullException(nameof(captures));
            UpdateAllStats(position,worker,ply,bestMove,bestValue,beta,previousSquare,quiets.AsSpan(),captures.AsSpan(),depth);
        }
        internal static void UpdateAllStats(Position position,SearchWorkerState worker,int ply,Move bestMove,Value bestValue,Value beta,Square previousSquare,ReadOnlySpan<Move> quiets,ReadOnlySpan<Move> captures,Depth depth)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            if(worker==null)throw new ArgumentNullException(nameof(worker));
            Color us=position.SideToMove;Piece piece=position.MovedPiece(bestMove);PieceType captured=Piece.TypeOf(position.PieceOn(Move.ToSquare(bestMove)));
            int bonus1=StatBonus((int)depth+1),bonus2=bestValue>beta+Value.PawnValueMg?bonus1:StatBonus(depth);
            if(!position.CaptureOrPromotion(bestMove))
            {
                UpdateQuietStats(position,worker,ply,bestMove,bonus2);
                foreach(Move move in quiets){worker.MainHistory[us,Move.FromTo(move)] <<= -bonus2;UpdateContinuationHistories(worker,ply,position.MovedPiece(move),Move.ToSquare(move),-bonus2);}
            }
            else worker.CaptureHistory[piece,Move.ToSquare(bestMove),captured] <<= bonus1;
            Stack previous=worker.Frame(ply-1);
            if((previous.MoveCount==1 || previous.CurrentMove==previous.Killers[0]) && position.CapturedPiece==Piece.NO_PIECE)
                UpdateContinuationHistories(worker,ply-1,position.PieceOn(previousSquare),previousSquare,-bonus1);
            foreach(Move move in captures){piece=position.MovedPiece(move);captured=Piece.TypeOf(position.PieceOn(Move.ToSquare(move)));worker.CaptureHistory[piece,Move.ToSquare(move),captured] <<= -bonus1;}
        }
    }
}
