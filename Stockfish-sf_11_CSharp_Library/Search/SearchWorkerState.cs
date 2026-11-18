using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Stockfish_sf_11_CSharp_Library.Evaluation;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Mutable search state owned by one worker and shared by search and quiescence.</summary>
    public sealed class SearchWorkerState
    {
        public EvaluationContext Evaluation { get; }
        public TranspositionTable Table { get; }
        public ButterflyHistory MainHistory { get; }
        public CapturePieceToHistory CaptureHistory { get; }
        public CounterMoveHistory CounterMoves { get; } = new CounterMoveHistory();
        // Both coordinates come from booleans; no invalid column can alias a row.
        private readonly ContinuationHistory[] continuation = new ContinuationHistory[2*2];
        private readonly Stack[] frames = new Stack[ChessConstants.MAX_PLY+10];
        internal readonly SearchBufferBank Buffers=new SearchBufferBank();
        /// <summary>Evaluation bias belongs to the worker and is shared by all its search plies.</summary>
        public Score Contempt { get; set; } = Score.SCORE_ZERO;
        /// <summary>Running transposition-hit metric, initialized as in the original root loop.</summary>
        public long TtHitAverage { get; set; } = 4096L * 1024 / 2;
        private long nodes, tbHits;
        public long Nodes { get => Interlocked.Read(ref nodes); internal set => Interlocked.Exchange(ref nodes,value); }
        public long TbHits { get => Interlocked.Read(ref tbHits); internal set => Interlocked.Exchange(ref tbHits,value); }
        public SearchWorkerState(EvaluationContext evaluation, TranspositionTable table, ButterflyHistory main, CapturePieceToHistory capture)
        {
            Evaluation=evaluation ?? throw new ArgumentNullException(nameof(evaluation));
            Table=table ?? throw new ArgumentNullException(nameof(table));
            MainHistory=main ?? throw new ArgumentNullException(nameof(main));
            CaptureHistory=capture ?? throw new ArgumentNullException(nameof(capture));
            for(int i=0;i<2;++i)for(int j=0;j<2;++j){continuation[i*2+j]=new ContinuationHistory();continuation[i*2+j][0,0].Fill(-1);}
            for(int i=0;i<frames.Length;++i)frames[i]=new Stack();
            ResetFrames();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ContinuationHistory Continuations(bool inCheck,bool capture) => continuation[(inCheck?2:0)+(capture?1:0)];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stack Frame(int ply)
        {
            int index=ply+7;if((uint)index>=(uint)frames.Length)throw new ArgumentOutOfRangeException(nameof(ply));
            return frames[index];
        }
        public void ResetFrames()
        {
            if(Buffers.ActiveCount!=0)throw new InvalidOperationException("Cannot reset an active search worker.");
            PieceToHistory sentinel=continuation[0][0,0];
            for(int i=0;i<frames.Length;++i)
            {
                Stack frame=frames[i];frame.ContinuationHistory=sentinel;frame.CurrentMove=Move.MOVE_NONE;
                frame.ExcludedMove=Move.MOVE_NONE;frame.StaticEval=Value.VALUE_ZERO;frame.Ply=i-7;
                frame.Killers[0]=frame.Killers[1]=Move.MOVE_NONE;frame.StatScore=frame.MoveCount=0;frame.Pv=null;
            }
            Nodes=0; TbHits=0;
            TtHitAverage=4096L*1024/2;
        }
    }
}
