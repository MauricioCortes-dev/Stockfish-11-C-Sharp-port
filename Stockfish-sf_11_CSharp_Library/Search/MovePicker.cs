using System;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.MoveGeneration;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Stockfish 11 staged pseudo-legal move selection for one unchanged position.</summary>
    public sealed class MovePicker
    {
        private enum Stage { MainTT, CaptureInit, GoodCapture, Refutation, QuietInit, Quiet, BadCapture, EvasionTT, EvasionInit, Evasion, ProbcutTT, ProbcutInit, Probcut, QsearchTT, QcaptureInit, Qcapture, QcheckInit, Qcheck, Done }
        private Position position=null!;
        private ButterflyHistory main=null!;
        private CapturePieceToHistory capture=null!;
        private PieceToHistory[] continuation=null!;
        private readonly Move[] refutations = new Move[3];
        private readonly ExtMove[] moves = new ExtMove[ChessConstants.MAX_MOVES];
        private Move ttMove;
        private int depth, threshold, recapture;
        private Stage stage;
        private int current, end, badEnd, refEnd;

        internal MovePicker() { }
        public MovePicker(Position position, Move ttMove, Depth depth, ButterflyHistory main, CapturePieceToHistory capture, PieceToHistory[] continuation, Move counterMove, Move[] killers)
            => ResetMain(position,ttMove,depth,main,capture,continuation,counterMove,killers);
        internal void ResetMain(Position position, Move ttMove, Depth depth, ButterflyHistory main, CapturePieceToHistory capture, PieceToHistory[] continuation, Move counterMove, Move[] killers)
        {
            ClearReferences();
            if ((int)depth <= 0) throw new ArgumentOutOfRangeException(nameof(depth));
            this.position=position ?? throw new ArgumentNullException(nameof(position)); this.main=main ?? throw new ArgumentNullException(nameof(main)); this.capture=capture ?? throw new ArgumentNullException(nameof(capture));
            this.continuation=CheckContinuation(continuation); if(killers==null || killers.Length<2) throw new ArgumentException("Two killers are required.",nameof(killers));
            this.depth=depth; refutations[0]=killers[0]; refutations[1]=killers[1]; refutations[2]=counterMove;
            this.ttMove=(int)ttMove!=0 && position.PseudoLegal(ttMove) ? ttMove : Move.MOVE_NONE;
            stage=position.Checkers?Stage.EvasionTT:Stage.MainTT; if((int)this.ttMove==0) ++stage;
        }
        public MovePicker(Position position, Move ttMove, Depth depth, ButterflyHistory main, CapturePieceToHistory capture, PieceToHistory[] continuation, Square recaptureSquare)
            => ResetQuiescence(position,ttMove,depth,main,capture,continuation,recaptureSquare);
        internal void ResetQuiescence(Position position, Move ttMove, Depth depth, ButterflyHistory main, CapturePieceToHistory capture, PieceToHistory[] continuation, Square recaptureSquare)
        {
            ClearReferences();
            if ((int)depth > 0) throw new ArgumentOutOfRangeException(nameof(depth));
            this.position=position ?? throw new ArgumentNullException(nameof(position)); this.main=main ?? throw new ArgumentNullException(nameof(main)); this.capture=capture ?? throw new ArgumentNullException(nameof(capture));
            this.continuation=CheckContinuation(continuation); this.depth=depth; recapture=recaptureSquare;
            this.ttMove=(int)ttMove!=0 && (depth>Depth.DEPTH_QS_RECAPTURES || Move.ToSquare(ttMove)==recaptureSquare) && position.PseudoLegal(ttMove) ? ttMove : Move.MOVE_NONE;
            stage=position.Checkers?Stage.EvasionTT:Stage.QsearchTT; if((int)this.ttMove==0) ++stage;
        }
        public MovePicker(Position position, Move ttMove, Value threshold, CapturePieceToHistory capture)
            => ResetProbcut(position,ttMove,threshold,capture);
        internal void ResetProbcut(Position position, Move ttMove, Value threshold, CapturePieceToHistory capture)
        {
            ClearReferences();
            this.position=position ?? throw new ArgumentNullException(nameof(position)); this.capture=capture ?? throw new ArgumentNullException(nameof(capture));
            if(position.Checkers) throw new ArgumentException("ProbCut requires a position not in check.",nameof(position));
            this.threshold=threshold; main=null!; continuation=null!;
            this.ttMove=(int)ttMove!=0 && position.Capture(ttMove) && position.PseudoLegal(ttMove) && position.SeeGe(ttMove,threshold) ? ttMove : Move.MOVE_NONE;
            stage=Stage.ProbcutTT; if((int)this.ttMove==0) ++stage;
        }
        private static PieceToHistory[] CheckContinuation(PieceToHistory[] tables)
        { if(tables==null || tables.Length<6 || tables[0]==null || tables[1]==null || tables[3]==null || tables[5]==null) throw new ArgumentException("Continuation tables 0, 1, 3 and 5 are required.",nameof(tables)); return tables; }
        private void Generate(GenType type, int start)
        {
            current=start; end=start+MoveGenerator.GenerateInto(position,type,moves.AsSpan(start));
            if(type==GenType.CAPTURES || type==GenType.QUIETS || type==GenType.EVASIONS)
                for(int i=start;i<end;++i) { ExtMove item=moves[i]; Move m=item.move; int piece=position.MovedPiece(m),to=Move.ToSquare(m),victim=position.PieceOn(to);
                    if(type==GenType.CAPTURES) item.value=(int)PSQT.PieceValue[0,victim]*6+capture[piece,to,Piece.TypeOf(victim)];
                    else if(type==GenType.QUIETS) item.value=main[position.SideToMove,Move.FromTo(m)]+2*continuation[0][piece,to]+2*continuation[1][piece,to]+2*continuation[3][piece,to]+continuation[5][piece,to];
                    else item.value=position.Capture(m) ? (int)PSQT.PieceValue[0,victim]-(int)Piece.TypeOf(piece) : main[position.SideToMove,Move.FromTo(m)]+continuation[0][piece,to]-(1<<28);
                    moves[i]=item;
                }
        }
        internal void ClearReferences()
        {
            position=null!;main=null!;capture=null!;continuation=null!;
            ttMove=Move.MOVE_NONE;depth=threshold=recapture=0;
            current=end=badEnd=refEnd=0;stage=Stage.Done;
        }
        private void Best()
        { int best=current; for(int i=current+1;i<end;++i) if(moves[best]<moves[i]) best=i; ExtMove tmp=moves[current];moves[current]=moves[best];moves[best]=tmp; }
        private void PartialSort(int begin,int limit)
        { int sortedEnd=begin;for(int p=begin+1;p<end;++p) if(moves[p].value>=limit){ExtMove tmp=moves[p];moves[p]=moves[++sortedEnd];int q=sortedEnd;while(q!=begin && moves[q-1]<tmp){moves[q]=moves[q-1];--q;}moves[q]=tmp;} }
        public Move NextMove(bool skipQuiets=false)
        {
            while(true) switch(stage)
            {
                case Stage.MainTT: case Stage.EvasionTT: case Stage.ProbcutTT: case Stage.QsearchTT: ++stage;return ttMove;
                case Stage.CaptureInit: case Stage.ProbcutInit: case Stage.QcaptureInit: badEnd=0;Generate(GenType.CAPTURES,0);++stage;break;
                case Stage.GoodCapture:
                    while(current<end){Best();ExtMove m=moves[current++];if((int)m.move==(int)ttMove)continue;if(position.SeeGe(m.move,-55*m.value/1024))return m.move;moves[badEnd++]=m;}
                    current=0;refEnd=(int)refutations[0]==(int)refutations[2] || (int)refutations[1]==(int)refutations[2]?2:3;stage=Stage.Refutation;break;
                case Stage.Refutation:
                    while(current<refEnd){Move m=refutations[current++];if((int)m!=(int)ttMove && (int)m!=0 && !position.Capture(m) && position.PseudoLegal(m))return m;}
                    stage=Stage.QuietInit;break;
                case Stage.QuietInit: if(!skipQuiets){Generate(GenType.QUIETS,badEnd);PartialSort(badEnd,-3000*depth);}stage=Stage.Quiet;break;
                case Stage.Quiet:
                    while(!skipQuiets && current<end){Move m=moves[current++].move;if((int)m!=(int)ttMove && (int)m!=(int)refutations[0] && (int)m!=(int)refutations[1] && (int)m!=(int)refutations[2])return m;}
                    current=0;end=badEnd;stage=Stage.BadCapture;break;
                case Stage.BadCapture: while(current<end){Move m=moves[current++].move;if((int)m!=(int)ttMove)return m;}return Move.MOVE_NONE;
                case Stage.EvasionInit: Generate(GenType.EVASIONS,0);stage=Stage.Evasion;break;
                case Stage.Evasion: case Stage.Probcut: case Stage.Qcapture:
                    while(current<end){Best();Move m=moves[current++].move;if((int)m==(int)ttMove)continue;if(stage==Stage.Probcut && !position.SeeGe(m,threshold))continue;if(stage==Stage.Qcapture && depth<=Depth.DEPTH_QS_RECAPTURES && Move.ToSquare(m)!=recapture)continue;return m;}
                    if(stage==Stage.Qcapture && depth==Depth.DEPTH_QS_CHECKS){stage=Stage.QcheckInit;break;} stage=Stage.Done;return Move.MOVE_NONE;
                case Stage.QcheckInit: Generate(GenType.QUIET_CHECKS,0);stage=Stage.Qcheck;break;
                case Stage.Qcheck: while(current<end){Move m=moves[current++].move;if((int)m!=(int)ttMove)return m;}stage=Stage.Done;return Move.MOVE_NONE;
                default:return Move.MOVE_NONE;
            }
        }
    }
}
