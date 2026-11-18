using System;
using System.Threading;
using System.Collections.Generic;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Evaluation;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Single-worker quiescence search with caller-owned caches and transposition table.</summary>
    public sealed class QuiescenceSearch
    {
        private readonly SearchWorkerState state;
        public Action<string>? NodeTrace { get; set; }
        public long Nodes => state.Nodes;
        public IReadOnlyList<Move> PrincipalVariation => pv;
        private readonly List<Move> pv = new List<Move>();
        private readonly List<Move> pendingPv = new List<Move>();
        private CancellationToken cancellation;
        private Action? nodeVisited;

        public QuiescenceSearch(EvaluationContext evaluation, TranspositionTable table, ButterflyHistory main, CapturePieceToHistory capture)
            : this(new SearchWorkerState(evaluation,table,main,capture)) { }
        public QuiescenceSearch(SearchWorkerState state)
        { this.state=state ?? throw new ArgumentNullException(nameof(state)); }

        public Value Search(Position position, Value alpha, Value beta, Depth depth, bool pvNode, CancellationToken cancellation = default, Action? nodeVisited = null)
        {
            state.ResetFrames();
            this.cancellation=cancellation; this.nodeVisited=nodeVisited;
            try { return SearchFromFrame(position,0,alpha,beta,depth,pvNode); }
            finally { this.cancellation=default; this.nodeVisited=null; }
        }
        /// <summary>Search from an existing worker frame without resetting its previous plies or histories.</summary>
        public Value SearchFromFrame(Position position, int ply, Value alpha, Value beta, Depth depth, bool pvNode)
        {
            Validate(position,ply,alpha,beta,depth,pvNode);
            pv.Clear();
            pendingPv.Clear();
            Value result=Run(position,ply,alpha,beta,depth,pvNode,pendingPv);
            pv.AddRange(pendingPv);
            return result;
        }
        internal Value SearchInto(Position position,int ply,Value alpha,Value beta,Depth depth,bool pvNode,List<Move>? line)
        {
            Validate(position,ply,alpha,beta,depth,pvNode);
            line?.Clear();
            return Run(position,ply,alpha,beta,depth,pvNode,line);
        }
        private static void Validate(Position position,int ply,Value alpha,Value beta,Depth depth,bool pvNode)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            if(ply<0 || ply>ChessConstants.MAX_PLY)throw new ArgumentOutOfRangeException(nameof(ply));
            if(depth>Depth.DEPTH_QS_CHECKS || alpha>=beta || alpha < -Value.VALUE_INFINITE || beta > Value.VALUE_INFINITE || (!pvNode && alpha != beta-1)) throw new ArgumentOutOfRangeException(nameof(depth), "Invalid quiescence window or depth.");
        }
        private void TraceNode(Position position,int index,int depth,Value alphaValue,Value betaValue,bool pvNode)
            => NodeTrace?.Invoke($"trace|q|{(ulong)position.Key}|{index}|{depth}|{(int)alphaValue}|{(int)betaValue}|{state.Nodes}|{(pvNode?1:0)}");
        private Value Run(Position position,int index,Value alphaValue,Value betaValue,int depth,bool pvNode,List<Move>? line)
        {
            cancellation.ThrowIfCancellationRequested();
            if(NodeTrace!=null)TraceNode(position,index,depth,alphaValue,betaValue,pvNode);
            int alpha=alphaValue,beta=betaValue,oldAlpha=alpha,ply=index;
            bool inCheck=position.Checkers;
            if(position.IsDraw(ply) || ply>=ChessConstants.MAX_PLY)
                return ply>=ChessConstants.MAX_PLY && !inCheck ? ClassicEvaluator.Evaluate(position,state.Evaluation,state.Contempt) : Value.VALUE_DRAW;
            Stack frame=state.Frame(index);
            int ttDepth=inCheck || depth>=0 ? 0 : -1;
            Key key=position.Key;
            ref TTEntry entry=ref state.Table.Probe(key,out bool hit);
            Value ttValue=hit ? SearchValue.FromTt(entry.Value,ply,position.Rule50Count) : Value.VALUE_NONE;
            Move ttMove=hit ? entry.Move : Move.MOVE_NONE;
            bool pvHit=hit && entry.IsPv;
            if(!pvNode && hit && entry.Depth>=ttDepth && ttValue!=Value.VALUE_NONE && ((int)entry.Bound & (ttValue>=beta?2:1))!=0)return ttValue;
            int best,futilityBase;
            if(inCheck){frame.StaticEval=Value.VALUE_NONE;best=futilityBase=-(int)Value.VALUE_INFINITE;}
            else
            {
                if(hit)
                {
                    frame.StaticEval=entry.Eval;
                    if(frame.StaticEval==Value.VALUE_NONE)frame.StaticEval=ClassicEvaluator.Evaluate(position,state.Evaluation,state.Contempt);
                    best=frame.StaticEval;
                    if(ttValue!=Value.VALUE_NONE && ((int)entry.Bound & (ttValue>best?2:1))!=0)best=ttValue;
                }
                else
                {
                    frame.StaticEval=state.Frame(index-1).CurrentMove!=Move.MOVE_NULL ? ClassicEvaluator.Evaluate(position,state.Evaluation,state.Contempt) : -state.Frame(index-1).StaticEval+2*ClassicEvaluationTerms.Tempo;
                    best=frame.StaticEval;
                }
                if(best>=beta)
                {
                    if(!hit)entry.Save(state.Table,key,SearchValue.ToTt(best,ply),pvHit,Bound.BOUND_LOWER,Depth.DEPTH_NONE,Move.MOVE_NONE,frame.StaticEval);
                    return best;
                }
                if(pvNode && best>alpha)alpha=best;
                futilityBase=best+154;
            }
            using var activation=state.Buffers.Rent(index);
            SearchBuffers buffers=activation.Buffers;
            var history=buffers.Histories;
            history[0]=state.Frame(index-1).ContinuationHistory!;history[1]=state.Frame(index-2).ContinuationHistory!;
            history[3]=state.Frame(index-4).ContinuationHistory!;history[5]=state.Frame(index-6).ContinuationHistory!;
            Square recapture=Move.ToSquare(state.Frame(index-1).CurrentMove);
            var picker=buffers.Picker;picker.ResetQuiescence(position,ttMove,(Depth)depth,state.MainHistory,state.CaptureHistory,history,recapture);
            int moveCount=0;Move bestMove=Move.MOVE_NONE;
            while(true)
            {
                Move move=picker.NextMove();if((int)move==0)break;
                bool givesCheck=position.GivesCheck(move),captureOrPromotion=position.CaptureOrPromotion(move);
                ++moveCount;
                if(!inCheck && !givesCheck && futilityBase > -(int)Value.VALUE_KNOWN_WIN && !position.AdvancedPawnPush(move))
                {
                    int futility=futilityBase+(int)PSQT.PieceValue[Phase.EG,position.PieceOn(Move.ToSquare(move))];
                    if(futility<=alpha){best=Math.Max(best,futility);continue;}
                    if(futilityBase<=alpha && !position.SeeGe(move,1)){best=Math.Max(best,futilityBase);continue;}
                }
                bool evasionPrunable=inCheck && (depth!=0 || moveCount>2) && best>(int)Value.VALUE_MATED_IN_MAX_PLY && !position.Capture(move);
                if((!inCheck || evasionPrunable) && !position.SeeGe(move,Value.VALUE_ZERO))continue;
                if(!position.Legal(move)){--moveCount;continue;}
                frame.CurrentMove=move;
                frame.ContinuationHistory=state.Continuations(inCheck,captureOrPromotion)[position.MovedPiece(move),Move.ToSquare(move)];
                var child=buffers.ChildPv;child.Clear();Value value;
                position.DoMove(move,buffers.NextState(),givesCheck);++state.Nodes;
                try { nodeVisited?.Invoke(); value=-Run(position,index+1,-beta,-alpha,depth-1,pvNode,child); }
                finally { position.UndoMove(move); }
                if(value>best)
                {
                    best=value;
                    if(value>alpha)
                    {
                        bestMove=move;
                        if(pvNode && line!=null){line.Clear();line.Add(move);line.AddRange(child);}
                        if(pvNode && value<beta)alpha=value;else break;
                    }
                }
            }
            if(inCheck && best==-(int)Value.VALUE_INFINITE)return Value.MatedIn(ply);
            entry.Save(state.Table,key,SearchValue.ToTt(best,ply),pvHit,best>=beta?Bound.BOUND_LOWER:pvNode && best>oldAlpha?Bound.BOUND_EXACT:Bound.BOUND_UPPER,ttDepth,bestMove,frame.StaticEval);
            return best;
        }
    }
}
