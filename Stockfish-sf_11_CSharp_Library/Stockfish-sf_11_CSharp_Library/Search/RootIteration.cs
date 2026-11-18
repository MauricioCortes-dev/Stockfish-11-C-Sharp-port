using System;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Arguments for a root-node search invoked by iterative deepening.</summary>
    public readonly struct RootSearchRequest
    {
        public readonly int Depth, PvIndex, PvLast;
        public readonly Value Alpha, Beta;
        public RootSearchRequest(int depth,int pvIndex,int pvLast,Value alpha,Value beta)
        { Depth=depth;PvIndex=pvIndex;PvLast=pvLast;Alpha=alpha;Beta=beta; }
    }

    /// <summary>
    /// Depth-limited root iteration. The caller supplies the actual root-node search;
    /// this class alone does not implement search or time management.
    /// </summary>
    public sealed class RootIteration
    {
        public int RootDepth { get; private set; }
        public int CompletedDepth { get; private set; }
        public int PvIndex { get; private set; }
        public int PvLast { get; private set; }
        public Value BestValue { get; private set; } = -Value.VALUE_INFINITE;
        public bool StopOnPonderHit { get; private set; }
        /// <summary>True when the requested mate limit ended this search.</summary>
        public bool MateLimitReached { get; private set; }
        public void Run(RootMoves roots, SearchWorkerState state, Color side, int maxDepth, int multiPv, int contempt,
            Func<RootSearchRequest,Value> searchRoot, Func<bool> stopped, int mateLimit = 0, Func<int,RootMoves,Value,bool>? iterationFinished = null, Func<bool>? increaseDepth = null, Action? aspirationFailLow = null, Action<int,RootMoves,int,Value,Value>? iterationInterrupted = null,
            Func<long>? elapsed = null, Action<int,RootMoves,int,Value,Value>? pvUpdated = null)
        {
            if(roots==null)throw new ArgumentNullException(nameof(roots));
            if(state==null)throw new ArgumentNullException(nameof(state));
            if(searchRoot==null)throw new ArgumentNullException(nameof(searchRoot));
            if(stopped==null)throw new ArgumentNullException(nameof(stopped));
            if(maxDepth<1 || maxDepth>=ChessConstants.MAX_PLY)throw new ArgumentOutOfRangeException(nameof(maxDepth));
            if(multiPv<1)throw new ArgumentOutOfRangeException(nameof(multiPv));
            if(mateLimit<0)throw new ArgumentOutOfRangeException(nameof(mateLimit));
            MateLimitReached=false;
            if(roots.Count==0)return;
            state.ResetFrames();RootDepth=CompletedDepth=0;StopOnPonderHit=false;BestValue=-Value.VALUE_INFINITE;
            Value alpha=-Value.VALUE_INFINITE,beta=Value.VALUE_INFINITE,delta=-Value.VALUE_INFINITE;
            Score baseContempt=Score.MakeScore(contempt,contempt/2);state.Contempt=side==Color.WHITE?baseContempt:-baseContempt;
            int count=Math.Min(multiPv,roots.Count), searchAgainCounter=0;
            while(++RootDepth<ChessConstants.MAX_PLY && RootDepth<=maxDepth && !stopped())
            {
                if(increaseDepth != null && !increaseDepth())++searchAgainCounter;
                foreach(RootMove root in roots)root.PreviousScore=root.Score;
                int pvFirst=0;PvLast=0;
                for(PvIndex=0;PvIndex<count && !stopped();++PvIndex)
                {
                    if(PvIndex==PvLast)
                    {
                        pvFirst=PvLast;
                        for(++PvLast;PvLast<roots.Count && roots[PvLast].TbRank==roots[pvFirst].TbRank;++PvLast) { }
                    }
                    var window=new AspirationWindow(RootDepth,roots[PvIndex].PreviousScore,alpha,beta,delta,contempt,side);
                    if(RootDepth>=4)state.Contempt=window.Contempt;
                    while(true)
                    {
                        int adjusted=window.AdjustedDepth(RootDepth,searchAgainCounter);
                        BestValue=searchRoot(new RootSearchRequest(adjusted,PvIndex,PvLast,window.Alpha,window.Beta));
                        roots.StableSort(PvIndex,PvLast-PvIndex);
                        if(stopped())break;
                        if(count==1 && (BestValue<=window.Alpha || BestValue>=window.Beta) && elapsed!=null && elapsed()>3000)
                            pvUpdated?.Invoke(RootDepth,roots,PvIndex,window.Alpha,window.Beta);
                        if(BestValue<=window.Alpha) { StopOnPonderHit=false; aspirationFailLow?.Invoke(); }
                        if(window.Accept(BestValue)){++roots[PvIndex].BestMoveCount;break;}
                    }
                    alpha=window.Alpha;beta=window.Beta;delta=window.Delta;
                    roots.StableSort(pvFirst,PvIndex+1-pvFirst);
                    if(stopped() || PvIndex+1==count || (elapsed!=null && elapsed()>3000))
                        pvUpdated?.Invoke(RootDepth,roots,PvIndex,alpha,beta);
                    if (stopped()) iterationInterrupted?.Invoke(RootDepth,roots,PvIndex,alpha,beta);
                }
                if(!stopped())CompletedDepth=RootDepth;
                MateLimitReached=mateLimit!=0 && BestValue>=Value.VALUE_MATE_IN_MAX_PLY && Value.VALUE_MATE-BestValue<=2*mateLimit;
                // UCI receives the completed PV before a mate limit ends the search.
                if(!stopped() && iterationFinished != null && iterationFinished(CompletedDepth,roots,BestValue))break;
                if(MateLimitReached)break;
            }
        }
    }
}
