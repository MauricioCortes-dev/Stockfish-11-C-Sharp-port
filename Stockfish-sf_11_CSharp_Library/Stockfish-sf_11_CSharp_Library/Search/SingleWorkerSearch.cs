using System;
using System.Collections.Generic;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Evaluation;
using Stockfish_sf_11_CSharp_Library.MoveGeneration;
using Stockfish_sf_11_CSharp_Library.Types;
using Stockfish_sf_11_CSharp_Library.Tablebases.Syzygy;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>One worker’s recursive search; the coordinator supplies shared limits and tablebases.</summary>
    public sealed class SingleWorkerSearch
    {
        private readonly SearchWorkerState worker;
        private SearchHeuristics heuristics = new SearchHeuristics(1);
        private readonly QuiescenceSearch quiescence;
        private readonly Action tablebaseNodeVisited;
        private readonly RootIteration iteration = new RootIteration();
        private RootMoves roots = new RootMoves();
        private Func<bool> stopped = () => false;
        private int pvIndex, pvLast, selectedDepth, nmpMinPly;
        private bool terminal;
        private Value terminalValue;
        private Color nmpColor;
        public RootMoves RootMoves => roots;
        public int CompletedDepth => terminal?0:iteration.CompletedDepth;
        internal bool MateLimitReached => !terminal && iteration.MateLimitReached;
        public long Nodes => worker.Nodes;
        public Value BestValue => terminal?terminalValue:roots.Count!=0?roots[0].Score:iteration.BestValue;
        /// <summary>Optional observer invoked after each root-node search and before root sorting.</summary>
        public Action<int,RootSearchRequest,Value,RootMoves>? RootTrace { get; set; }
        public Action<string>? NodeTrace { get; set; }
        /// <summary>Optional observer for selected pruning and reduction branches.</summary>
        public Action<string>? BranchTrace { get; set; }
        public Func<int,RootMoves,Value,bool>? IterationFinished { get; set; }
        internal Action<int,RootMoves,int,Value,Value>? IterationInterrupted { get; set; }
        internal Func<long>? Elapsed { get; set; }
        internal Action<int,RootMoves,int,Value,Value>? PvUpdated { get; set; }
        internal Action<int,Move,int>? RootMoveStarted { get; set; }
        private int bestMoveChanges;
        public int BestMoveChanges { get => System.Threading.Volatile.Read(ref bestMoveChanges); internal set => System.Threading.Interlocked.Exchange(ref bestMoveChanges,value); }
        internal int TakeBestMoveChanges() => System.Threading.Interlocked.Exchange(ref bestMoveChanges,0);
        internal void SetThreadCount(int count) => heuristics = new SearchHeuristics(count);
        internal Action? CheckTime { get; set; }
        internal Func<bool>? IncreaseDepth { get; set; }
        internal Action? AspirationFailLow { get; set; }
        internal TbProbe? Tablebases { get; set; }
        internal TablebaseConfig TablebaseConfig { get; set; }
        internal Action? TablebaseProbed { get; set; }
        internal SearchBreadcrumbs? Breadcrumbs { get; set; }
        public SingleWorkerSearch(SearchWorkerState worker)
        { this.worker=worker ?? throw new ArgumentNullException(nameof(worker));quiescence=new QuiescenceSearch(worker);tablebaseNodeVisited=()=>++worker.Nodes; }
        public void Search(Position position,int depth,int multiPv=1,int contempt=0,Func<bool>? stop=null,int mateLimit=0,IReadOnlyCollection<Move>? searchMoves=null,bool startNewSearch=true,RootMoves? preparedRoots=null)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            if(worker.Table.ClusterCount==0)throw new InvalidOperationException("Allocate the transposition table before searching.");
            roots=preparedRoots == null ? new RootMoves() : new RootMoves(preparedRoots);
            if(preparedRoots == null)
            {
                Span<Move> candidates=stackalloc Move[ChessConstants.MAX_MOVES];
                int candidateCount=MoveGenerator.GenerateInto(position,GenType.LEGAL,candidates);
                for(int candidateIndex=0;candidateIndex<candidateCount;++candidateIndex)
                {
                    Move move=candidates[candidateIndex];
                    bool selected=searchMoves==null || searchMoves.Count==0;
                    if(!selected)foreach(Move candidate in searchMoves!)if(candidate==move){selected=true;break;}
                    if(selected)roots.Add(new RootMove(move));
                }
            }
            if(startNewSearch)worker.Table.NewSearch();terminal=roots.Count==0;
            if(terminal){terminalValue=position.Checkers?-Value.VALUE_MATE:Value.VALUE_DRAW;worker.ResetFrames();return;}
            stopped=stop ?? (()=>false);quiescence.NodeTrace=NodeTrace;selectedDepth=0;nmpMinPly=0;BestMoveChanges=0;
            int lastRootDepth=-1,lastPvIndex=-1;
            iteration.Run(roots,worker,position.SideToMove,depth,multiPv,contempt,request=>{
                pvIndex=request.PvIndex;pvLast=request.PvLast;
                if(lastRootDepth!=iteration.RootDepth || lastPvIndex!=pvIndex){selectedDepth=0;lastRootDepth=iteration.RootDepth;lastPvIndex=pvIndex;}
                Value value=SearchNode(position,0,request.Depth,request.Alpha,request.Beta,true,false,null);
                RootTrace?.Invoke(iteration.RootDepth,request,value,roots);
                return value;
            },stopped,mateLimit,IterationFinished,IncreaseDepth,AspirationFailLow,IterationInterrupted,Elapsed,PvUpdated);
        }
        private void TraceNode(Position pos,int ply,int depth,int alpha,int beta,bool pvNode)
            => NodeTrace?.Invoke($"trace|s|{(ulong)pos.Key}|{ply}|{depth}|{alpha}|{beta}|{worker.Nodes}|{(pvNode?1:0)}");
        internal int DrawValue()=>2*(int)(worker.Nodes&1)-1;
        private int SearchNode(Position pos,int ply,int depth,int alpha,int beta,bool pvNode,bool cutNode,List<Move>? line)
        {
            if(NodeTrace!=null)TraceNode(pos,ply,depth,alpha,beta,pvNode);
            bool rootNode=pvNode && ply==0;
            if(!rootNode && pos.Rule50Count>=3 && alpha<0 && pos.HasGameCycle(ply))
            { alpha=DrawValue();if(alpha>=beta)return alpha; }
            if(depth<=0)
            {
                return quiescence.SearchInto(pos,ply,alpha,beta,0,pvNode,line);
            }
            CheckTime?.Invoke();
            bool inCheck=pos.Checkers,priorCapture=pos.CapturedPiece!=Piece.NO_PIECE;
            Color us=pos.SideToMove;Stack frame=worker.Frame(ply),previous=worker.Frame(ply-1);
            int best=-(int)Value.VALUE_INFINITE,maxValue=Value.VALUE_INFINITE,moveCount=0;
            frame.MoveCount=0;
            if(pvNode && selectedDepth<ply+1)selectedDepth=ply+1;
            if(!rootNode)
            {
                if(stopped() || pos.IsDraw(ply) || ply>=ChessConstants.MAX_PLY)
                    return ply>=ChessConstants.MAX_PLY && !inCheck?(int)ClassicEvaluator.Evaluate(pos,worker.Evaluation,worker.Contempt):DrawValue();
                alpha=Math.Max((int)Value.MatedIn(ply),alpha);beta=Math.Min((int)Value.MateIn(ply+1),beta);
                if(alpha>=beta)return alpha;
            }
            worker.Frame(ply+1).Ply=ply+1;worker.Frame(ply+1).ExcludedMove=Move.MOVE_NONE;
            worker.Frame(ply+2).Killers[0]=worker.Frame(ply+2).Killers[1]=Move.MOVE_NONE;
            Square prevSquare=Move.ToSquare(previous.CurrentMove);
            worker.Frame(ply+(rootNode?4:2)).StatScore=0;
            Move excluded=frame.ExcludedMove;
            Key key=(ulong)pos.Key ^ unchecked((ulong)(long)((int)excluded<<16));
            ref TTEntry entry=ref worker.Table.Probe(key,out bool hit);
            Value ttValue;Move ttMove;bool ttPv;
            if(rootNode)
            {
                ttValue=hit?SearchValue.FromTt(entry.Value,ply,pos.Rule50Count):Value.VALUE_NONE;
                ttMove=roots[pvIndex].Pv[0];ttPv=true;
                worker.TtHitAverage=4095*worker.TtHitAverage/4096+(hit?1024:0);
            }
            else
            {
                bool cutoff=SearchTransposition.TryCutoff(pos,worker,ply,depth,beta,pvNode,ref entry,hit,out ttMove,out ttValue,out ttPv);
                if(cutoff)return ttValue;
            }
            // Step 5: probe WDL only where the original permits a position without castling or 50-move history.
            if (!rootNode && Tablebases != null && TablebaseConfig.Cardinality != 0)
            {
                int pieces=Bitboard.PopCount(pos.Pieces());
                if (pieces<=TablebaseConfig.Cardinality && (pieces<TablebaseConfig.Cardinality || depth>=TablebaseConfig.ProbeDepth)
                    && pos.Rule50Count==0 && !pos.CanCastle(CastlingRights.ANY_CASTLING))
                {
                    WDLScore wdl=Tablebases.ProbeWdl(pos,out ProbeState result,tablebaseNodeVisited); TablebaseProbed?.Invoke();
                    if (result!=ProbeState.FAIL)
                    {
                        ++worker.TbHits; int drawScore=TablebaseConfig.UseRule50?1:0;
                        int value=wdl < -drawScore ? -(int)Value.VALUE_MATE+ChessConstants.MAX_PLY+ply+1
                            : wdl > drawScore ? (int)Value.VALUE_MATE-ChessConstants.MAX_PLY-ply-1 : 2*(int)wdl*drawScore;
                        Bound bound=wdl < -drawScore ? Bound.BOUND_UPPER : wdl > drawScore ? Bound.BOUND_LOWER : Bound.BOUND_EXACT;
                        if(bound==Bound.BOUND_EXACT || (bound==Bound.BOUND_LOWER ? value>=beta : value<=alpha))
                        {
                            entry.Save(worker.Table,key,SearchValue.ToTt(value,ply),ttPv,bound,Math.Min(ChessConstants.MAX_PLY-1,depth+6),Move.MOVE_NONE,Value.VALUE_NONE);
                            return value;
                        }
                        if(pvNode) { if(bound==Bound.BOUND_LOWER) { best=value;alpha=Math.Max(alpha,best); } else maxValue=value; }
                    }
                }
            }
            using var activation=worker.Buffers.Rent(ply);
            SearchBuffers buffers=activation.Buffers;
            int eval=Value.VALUE_NONE;bool improving=false;
            if(inCheck)frame.StaticEval=Value.VALUE_NONE;
            else
            {
                if(hit)
                {
                    frame.StaticEval=entry.Eval;if(frame.StaticEval==Value.VALUE_NONE)frame.StaticEval=ClassicEvaluator.Evaluate(pos,worker.Evaluation,worker.Contempt);
                    eval=frame.StaticEval;if(eval==0)eval=DrawValue();
                    if(ttValue!=Value.VALUE_NONE && ((int)entry.Bound & (ttValue>eval?2:1))!=0)eval=ttValue;
                }
                else
                {
                    frame.StaticEval=previous.CurrentMove!=Move.MOVE_NULL?ClassicEvaluator.Evaluate(pos,worker.Evaluation,worker.Contempt)-(previous.StatScore/512):-previous.StaticEval+2*ClassicEvaluationTerms.Tempo;
                    eval=frame.StaticEval;entry.Save(worker.Table,key,Value.VALUE_NONE,ttPv,Bound.BOUND_NONE,Depth.DEPTH_NONE,Move.MOVE_NONE,eval);
                }
                if(!rootNode && depth<2 && eval<=alpha-531)
                {return quiescence.SearchInto(pos,ply,alpha,beta,0,pvNode,line);}
                Stack two=worker.Frame(ply-2),four=worker.Frame(ply-4);
                improving=two.StaticEval==Value.VALUE_NONE?(frame.StaticEval>=four.StaticEval || four.StaticEval==Value.VALUE_NONE):frame.StaticEval>=two.StaticEval;
                if(!pvNode && depth<6 && eval-SearchHeuristics.FutilityMargin(depth,improving)>=beta && eval<Value.VALUE_KNOWN_WIN)return eval;
                if(!pvNode && previous.CurrentMove!=Move.MOVE_NULL && previous.StatScore<23397 && eval>=beta && eval>=frame.StaticEval && frame.StaticEval>=beta-32*depth+292-(improving?30:0) && (int)excluded==0 && pos.NonPawnMaterial(us)!=Value.VALUE_ZERO && (ply>=nmpMinPly || us!=nmpColor))
                {
                    BranchTrace?.Invoke("null");
                    int reduction=(854+68*depth)/258+Math.Min((eval-beta)/192,3);
                    frame.CurrentMove=Move.MOVE_NULL;frame.ContinuationHistory=worker.Continuations(false,false)[0,0];
                    pos.DoNullMove(buffers.NextState());int nullValue;
                    try{nullValue=-SearchNode(pos,ply+1,depth-reduction,-beta,-beta+1,false,!cutNode,null);}finally{pos.UndoNullMove();}
                    if(nullValue>=beta)
                    {
                        if(nullValue>=Value.VALUE_MATE_IN_MAX_PLY)nullValue=beta;
                        if(nmpMinPly!=0 || (Math.Abs(beta)<Value.VALUE_KNOWN_WIN && depth<13))return nullValue;
                        nmpMinPly=ply+3*(depth-reduction)/4;nmpColor=us;
                        int verify;try{verify=SearchNode(pos,ply,depth-reduction,beta-1,beta,false,false,null);}finally{nmpMinPly=0;}
                        if(verify>=beta)return nullValue;
                    }
                }
                if(!pvNode && depth>=5 && Math.Abs(beta)<Value.VALUE_MATE_IN_MAX_PLY)
                {
                    int raisedBeta=Math.Min(beta+189-(improving?45:0),(int)Value.VALUE_INFINITE);
                    BranchTrace?.Invoke("probcut");
                    var probcut=buffers.Picker;probcut.ResetProbcut(pos,ttMove,(Value)(raisedBeta-frame.StaticEval),worker.CaptureHistory);int count=0;
                    while(true)
                    {
                        Move move=probcut.NextMove();if((int)move==0 || count>=2+(cutNode?2:0))break;
                        if(move==excluded || !pos.Legal(move))continue;
                        ++count;frame.CurrentMove=move;frame.ContinuationHistory=worker.Continuations(inCheck,true)[pos.MovedPiece(move),Move.ToSquare(move)];
                        pos.DoMove(move,buffers.NextState());++worker.Nodes;int value;
                        try{value=-(int)quiescence.SearchInto(pos,ply+1,-raisedBeta,-raisedBeta+1,0,false,null);if(value>=raisedBeta)value=-SearchNode(pos,ply+1,depth-4,-raisedBeta,-raisedBeta+1,false,!cutNode,null);}finally{pos.UndoMove(move);}
                        if(value>=raisedBeta)return value;
                    }
                }
                if(depth>=7 && (int)ttMove==0)
                {
                    BranchTrace?.Invoke("iid");
                    SearchNode(pos,ply,depth-7,alpha,beta,pvNode,cutNode,null);
                    entry=ref worker.Table.Probe(key,out hit);ttValue=hit?SearchValue.FromTt(entry.Value,ply,pos.Rule50Count):Value.VALUE_NONE;ttMove=hit?entry.Move:Move.MOVE_NONE;
                }
            }
            var histories=buffers.Histories;histories[0]=worker.Frame(ply-1).ContinuationHistory!;histories[1]=worker.Frame(ply-2).ContinuationHistory!;histories[3]=worker.Frame(ply-4).ContinuationHistory!;histories[5]=worker.Frame(ply-6).ContinuationHistory!;
            Move counter=worker.CounterMoves[pos.PieceOn(prevSquare),prevSquare];
            var picker=buffers.Picker;picker.ResetMain(pos,ttMove,(Depth)depth,worker.MainHistory,worker.CaptureHistory,histories,counter,frame.Killers);
            int currentValue=best;bool singularLmr=false,moveCountPruning=false,ttCapture=(int)ttMove!=0 && pos.CaptureOrPromotion(ttMove);
            Move[] quiets=buffers.Quiets,captures=buffers.Captures;int quietCount=0,captureCount=0;Move bestMove=Move.MOVE_NONE;
            using var holding = Breadcrumbs?.Enter(worker, key, ply) ?? default;
            while(true)
            {
                Move move=picker.NextMove(moveCountPruning);if((int)move==0)break;if(move==excluded)continue;
                if(rootNode){int index=roots.Find(move);if(index<pvIndex || index>=pvLast)continue;}
                frame.MoveCount=++moveCount;
                if(rootNode && Elapsed!=null && Elapsed()>3000)
                    RootMoveStarted?.Invoke(depth,move,moveCount+pvIndex);
                int extension=0;bool capture=pos.CaptureOrPromotion(move),givesCheck=pos.GivesCheck(move);Piece piece=pos.MovedPiece(move);int newDepth=depth-1;
                if(!rootNode && pos.NonPawnMaterial(us)!=Value.VALUE_ZERO && best>Value.VALUE_MATED_IN_MAX_PLY)
                {
                    moveCountPruning=moveCount>=SearchHeuristics.FutilityMoveCount(improving,depth);
                    if(!capture && !givesCheck)
                    {
                        int lmrDepth=Math.Max(newDepth-heuristics.Reduction(improving,depth,moveCount),0);
                        if(lmrDepth<4+(previous.StatScore>0 || previous.MoveCount==1?1:0) && histories[0][piece,Move.ToSquare(move)]<0 && histories[1][piece,Move.ToSquare(move)]<0)continue;
                        if(lmrDepth<6 && !inCheck && frame.StaticEval+235+172*lmrDepth<=alpha && worker.MainHistory[us,Move.FromTo(move)]+histories[0][piece,Move.ToSquare(move)]+histories[1][piece,Move.ToSquare(move)]+histories[3][piece,Move.ToSquare(move)]<25000)continue;
                        if(!pos.SeeGe(move,-(32-Math.Min(lmrDepth,18))*lmrDepth*lmrDepth))continue;
                    }
                    else if(!pos.SeeGe(move,-194*depth))continue;
                }
                if(depth>=6 && move==ttMove && !rootNode && (int)excluded==0 && Math.Abs((int)ttValue)<Value.VALUE_KNOWN_WIN && ((int)entry.Bound&2)!=0 && entry.Depth>=depth-3 && pos.Legal(move))
                {
                    BranchTrace?.Invoke("singular");
                    int singularBeta=ttValue-2*depth;frame.ExcludedMove=move;int value;
                    try{value=SearchNode(pos,ply,depth/2,singularBeta-1,singularBeta,false,cutNode,null);}finally{frame.ExcludedMove=Move.MOVE_NONE;}
                    if(value<singularBeta){extension=1;singularLmr=true;}else if(singularBeta>=beta)return singularBeta;
                }
                else if(givesCheck && (pos.IsDiscoveryCheckOnKing(~us,move) || pos.SeeGe(move,0)))extension=1;
                else if(move==frame.Killers[0] && pos.AdvancedPawnPush(move) && pos.PawnPassed(us,Move.ToSquare(move)))extension=1;
                else if(PSQT.PieceValue[Phase.EG,pos.CapturedPiece]>Value.PawnValueEg && pos.TotalNonPawnMaterial<=2*Value.RookValueMg)extension=1;
                if(Move.TypeOf(move)==MoveType.CASTLING)extension=1;
                newDepth+=extension;
                if(!rootNode && !pos.Legal(move)){frame.MoveCount=--moveCount;continue;}
                frame.CurrentMove=move;frame.ContinuationHistory=worker.Continuations(inCheck,capture)[piece,Move.ToSquare(move)];
                var child=buffers.ChildPv;child.Clear();
                pos.DoMove(move,buffers.NextState(),givesCheck);++worker.Nodes;
                try
                {
                    bool didLmr=false,full;
                    if(depth>=3 && moveCount>1+(rootNode?1:0)+(rootNode && best<alpha?1:0) && (!rootNode || roots[roots.Find(move)].BestMoveCount==0) && (!capture || moveCountPruning || frame.StaticEval+PSQT.PieceValue[Phase.EG,pos.CapturedPiece]<=alpha || cutNode || worker.TtHitAverage<375L*1024*4096/1024))
                    {
                        BranchTrace?.Invoke("lmr");
                        int r=heuristics.Reduction(improving,depth,moveCount);
                        if(worker.TtHitAverage>500L*1024*4096/1024)--r;if(holding.Marked)++r;if(ttPv)r-=2;if(previous.MoveCount>14)--r;if(singularLmr)r-=2;
                        if(!capture)
                        {
                            if(ttCapture)++r;if(cutNode)r+=2;else if(Move.TypeOf(move)==MoveType.NORMAL && !pos.SeeGe(Move.ReverseMove(move),0))r-=2;
                            frame.StatScore=worker.MainHistory[us,Move.FromTo(move)]+histories[0][piece,Move.ToSquare(move)]+histories[1][piece,Move.ToSquare(move)]+histories[3][piece,Move.ToSquare(move)]-4926;
                            if(frame.StatScore<0 && histories[0][piece,Move.ToSquare(move)]>=0 && histories[1][piece,Move.ToSquare(move)]>=0 && worker.MainHistory[us,Move.FromTo(move)]>=0)frame.StatScore=0;
                            if(frame.StatScore>=-102 && previous.StatScore< -114)--r;else if(previous.StatScore>=-116 && frame.StatScore< -154)++r;r-=frame.StatScore/16384;
                        }
                        else if(depth<8 && moveCount>2)++r;
                        int d=Math.Max(1,Math.Min(newDepth,newDepth-r));currentValue=-SearchNode(pos,ply+1,d,-alpha-1,-alpha,false,true,child);full=currentValue>alpha && d!=newDepth;didLmr=true;
                    }
                    else full=!pvNode || moveCount>1;
                    if(full)
                    {
                        currentValue=-SearchNode(pos,ply+1,newDepth,-alpha-1,-alpha,false,!cutNode,child);
                        if(didLmr && !capture){int bonus=currentValue>alpha?SearchHeuristics.StatBonus(newDepth):-SearchHeuristics.StatBonus(newDepth);if(move==frame.Killers[0])bonus+=bonus/4;SearchHeuristics.UpdateContinuationHistories(worker,ply,piece,Move.ToSquare(move),bonus);}
                    }
                    if(pvNode && (moveCount==1 || (currentValue>alpha && (rootNode || currentValue<beta))))
                    {child.Clear();currentValue=-SearchNode(pos,ply+1,newDepth,-beta,-alpha,true,false,child);}
                }
                finally{pos.UndoMove(move);}
                if(stopped())return 0;
                if(rootNode)
                {
                    RootMove rm=roots[roots.Find(move)];
                    if(moveCount==1 || currentValue>alpha){if(moveCount>1)System.Threading.Interlocked.Increment(ref bestMoveChanges);rm.Score=currentValue;rm.SelDepth=selectedDepth;rm.Pv.Clear();rm.Pv.Add(move);rm.Pv.AddRange(child);}
                    else rm.Score=-Value.VALUE_INFINITE;
                }
                if(currentValue>best)
                {
                    best=currentValue;
                    if(currentValue>alpha)
                    {
                        bestMove=move;if(pvNode && !rootNode && line!=null){line.Clear();line.Add(move);line.AddRange(child);}
                        if(pvNode && currentValue<beta)alpha=currentValue;else{frame.StatScore=0;break;}
                    }
                }
                if(move!=bestMove){if(capture && captureCount<captures.Length)captures[captureCount++]=move;else if(!capture && quietCount<quiets.Length)quiets[quietCount++]=move;}
            }
            if(moveCount==0)best=(int)excluded!=0?alpha:inCheck?(int)Value.MatedIn(ply):0;
            else if((int)bestMove!=0)SearchHeuristics.UpdateAllStats(pos,worker,ply,bestMove,best,beta,prevSquare,quiets.AsSpan(0,quietCount),captures.AsSpan(0,captureCount),depth);
            else if((depth>=3 || pvNode) && !priorCapture)SearchHeuristics.UpdateContinuationHistories(worker,ply-1,pos.PieceOn(prevSquare),prevSquare,SearchHeuristics.StatBonus(depth));
            if(pvNode)best=Math.Min(best,maxValue);
            if((int)excluded==0)entry.Save(worker.Table,key,SearchValue.ToTt(best,ply),ttPv,best>=beta?Bound.BOUND_LOWER:pvNode && (int)bestMove!=0?Bound.BOUND_EXACT:Bound.BOUND_UPPER,depth,bestMove,frame.StaticEval);
            return best;
        }
    }
}
