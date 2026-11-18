using System;
using System.Collections.Generic;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.MoveGeneration;
using Stockfish_sf_11_CSharp_Library.Types;
namespace Stockfish_sf_11_CSharp_Library.Evaluation
{
    public enum EndgameCode { EVALUATION_FUNCTIONS, KNNK, KNNKP, KXK, KBNK, KPK, KRKP, KRKB, KRKN, KQKP, KQKR, SCALING_FUNCTIONS, KBPsK, KQKRPs, KRPKR, KRPKB, KRPPKRP, KPsK, KBPKB, KBPPKB, KBPKN, KNPK, KNPKB, KPKP }
    public abstract class EndgameBase<T>
    {
        protected EndgameBase(Color strongSide)
        {
            if (strongSide != Color.WHITE && strongSide != Color.BLACK) throw new ArgumentOutOfRangeException(nameof(strongSide));
            StrongSide = strongSide;
        }
        public Color StrongSide { get; }
        public Color WeakSide => ~StrongSide;
        public abstract T Evaluate(Position position);
    }
    /// <summary>A specialized endgame evaluation bound to the stronger side.</summary>
    public sealed class EndgameValue : EndgameBase<Value>
    {
        public EndgameCode Code { get; }
        public EndgameValue(EndgameCode code, Color strongSide) : base(strongSide)
        {
            if (code <= EndgameCode.EVALUATION_FUNCTIONS || code >= EndgameCode.SCALING_FUNCTIONS) throw new ArgumentOutOfRangeException(nameof(code));
            Code=code;
        }
        private static readonly int[] Edges={100,90,80,70,70,80,90,100,90,70,60,50,50,60,70,90,80,60,40,30,30,40,60,80,70,50,30,20,20,30,50,70,70,50,30,20,20,30,50,70,80,60,40,30,30,40,60,80,90,70,60,50,50,60,70,90,100,90,80,70,70,80,90,100};
        private static readonly int[] Corners={6400,6080,5760,5440,5120,4800,4480,4160,6080,5760,5440,5120,4800,4480,4160,4480,5760,5440,4960,4480,4480,4000,4480,4800,5440,5120,4480,3840,3520,4480,4800,5120,5120,4800,4480,3520,3840,4480,5120,5440,4800,4480,4000,4480,4480,4960,5440,5760,4480,4160,4480,4800,5120,5440,5760,6080,4160,4480,4800,5120,5440,5760,6080,6400};
        private static readonly int[] Close={0,0,100,80,60,40,20,10}, Away={0,5,20,40,60,80,90,100};
        private Square Normalize(Position p, Square sq) { if((int)Types.File.FileOf(p.SquareOf(StrongSide,PieceType.PAWN))>=4)sq=(int)sq^7; return StrongSide==Color.WHITE?sq:~sq; }
        public override Value Evaluate(Position p)
        {
            if(p==null)throw new ArgumentNullException(nameof(p));
            Color strong=StrongSide,weak=WeakSide; Square wk=p.KingSquare(strong),bk=p.KingSquare(weak); int result;
            switch(Code)
            {
                case EndgameCode.KNNK:return Value.VALUE_DRAW;
                case EndgameCode.KXK:
                    if(p.SideToMove==weak && !p.HasLegalMove())return Value.VALUE_DRAW;
                    result=(int)p.NonPawnMaterial(strong)+p.Count(strong,PieceType.PAWN)*(int)Value.PawnValueEg+Edges[bk]+Close[Bitboards.Distance(wk,bk)];
                    const ulong dark=0xAA55AA55AA55AA55UL;ulong bishops=p.Pieces(strong,PieceType.BISHOP);
                    if(p.Count(strong,PieceType.QUEEN)!=0 || p.Count(strong,PieceType.ROOK)!=0 || (p.Count(strong,PieceType.BISHOP)!=0 && p.Count(strong,PieceType.KNIGHT)!=0) || ((bishops&dark)!=0 && (bishops&~dark)!=0)) result=Math.Min(result+(int)Value.VALUE_KNOWN_WIN,(int)Value.VALUE_MATE_IN_MAX_PLY-1);
                    break;
                case EndgameCode.KBNK:
                    Square bishop=p.SquareOf(strong,PieceType.BISHOP);result=(int)Value.VALUE_KNOWN_WIN+Close[Bitboards.Distance(wk,bk)]+Corners[Bitboard.OppositeColors(bishop,Square.SQ_A1)?~bk:bk];break;
                case EndgameCode.KPK:
                    Square pawn=Normalize(p,p.SquareOf(strong,PieceType.PAWN));
                    if(!Bitbases.Probe(Normalize(p,wk),pawn,Normalize(p,bk),strong==p.SideToMove?Color.WHITE:Color.BLACK))return Value.VALUE_DRAW;
                    result=(int)Value.VALUE_KNOWN_WIN+(int)Value.PawnValueEg+(int)Rank.RankOf(pawn);break;
                case EndgameCode.KRKP:
                    wk=Square.RelativeSquare(strong,wk);bk=Square.RelativeSquare(strong,bk);Square rook=Square.RelativeSquare(strong,p.SquareOf(strong,PieceType.ROOK));Square ps=Square.RelativeSquare(strong,p.SquareOf(weak,PieceType.PAWN));Square queen=Square.MakeSquare(Types.File.FileOf(ps),Rank.RANK_1);
                    if((bool)(Bitboard.ForwardFile(Color.WHITE,wk)&ps) || (Bitboards.Distance(bk,ps)>=3+(p.SideToMove==weak?1:0) && Bitboards.Distance(bk,rook)>=3))result=(int)Value.RookValueEg-Bitboards.Distance(wk,ps);
                    else if(Rank.RankOf(bk)<=Rank.RANK_3 && Bitboards.Distance(bk,ps)==1 && Rank.RankOf(wk)>=Rank.RANK_4 && Bitboards.Distance(wk,ps)>2+(p.SideToMove==strong?1:0))result=80-8*Bitboards.Distance(wk,ps);
                    else result=200-8*(Bitboards.Distance(wk,ps+Direction.SOUTH)-Bitboards.Distance(bk,ps+Direction.SOUTH)-Bitboards.Distance(ps,queen));break;
                case EndgameCode.KRKB:result=Edges[bk];break;
                case EndgameCode.KRKN:result=Edges[bk]+Away[Bitboards.Distance(bk,p.SquareOf(weak,PieceType.KNIGHT))];break;
                case EndgameCode.KQKP:
                    Square wp=p.SquareOf(weak,PieceType.PAWN);int file=Types.File.FileOf(wp);result=Close[Bitboards.Distance(wk,bk)];
                    if(Rank.RelativeRank(weak,wp)!=Rank.RANK_7 || Bitboards.Distance(bk,wp)!=1 || (file!=0 && file!=2 && file!=5 && file!=7)) result+=(int)Value.QueenValueEg-(int)Value.PawnValueEg;break;
                case EndgameCode.KQKR:result=(int)Value.QueenValueEg-(int)Value.RookValueEg+Edges[bk]+Close[Bitboards.Distance(wk,bk)];break;
                case EndgameCode.KNNKP:result=2*(int)Value.KnightValueEg-(int)Value.PawnValueEg+Edges[bk];break;
                default:throw new InvalidOperationException();
            }
            return strong==p.SideToMove?result:-result;
        }
    }
    public static class Endgames
    {
        private static readonly Lazy<Dictionary<ulong,EndgameValue>> Values=new Lazy<Dictionary<ulong,EndgameValue>>(Build,true);
        private static readonly Lazy<Dictionary<ulong,EndgameScale>> Scales=new Lazy<Dictionary<ulong,EndgameScale>>(BuildScales,true);
        private static Dictionary<ulong,EndgameValue> Build()
        {
            var result=new Dictionary<ulong,EndgameValue>();
            foreach(var entry in new[]{(EndgameCode.KPK,"KPK"),(EndgameCode.KNNK,"KNNK"),(EndgameCode.KBNK,"KBNK"),(EndgameCode.KRKP,"KRKP"),(EndgameCode.KRKB,"KRKB"),(EndgameCode.KRKN,"KRKN"),(EndgameCode.KQKP,"KQKP"),(EndgameCode.KQKR,"KQKR"),(EndgameCode.KNNKP,"KNNKP")})
                for(int c=0;c<2;++c){var pos=new Position();pos.SetEndgame(entry.Item2,c,new StateInfo());result[(ulong)pos.MaterialKey]=new EndgameValue(entry.Item1,c);}
            return result;
        }
        private static Dictionary<ulong,EndgameScale> BuildScales()
        {
            var result=new Dictionary<ulong,EndgameScale>();
            foreach(var entry in new[]{(EndgameCode.KNPK,"KNPK"),(EndgameCode.KNPKB,"KNPKB"),(EndgameCode.KRPKR,"KRPKR"),(EndgameCode.KRPKB,"KRPKB"),(EndgameCode.KBPKB,"KBPKB"),(EndgameCode.KBPKN,"KBPKN"),(EndgameCode.KBPPKB,"KBPPKB"),(EndgameCode.KRPPKRP,"KRPPKRP")})
                for(int c=0;c<2;++c){var pos=new Position();pos.SetEndgame(entry.Item2,c,new StateInfo());result[(ulong)pos.MaterialKey]=new EndgameScale(entry.Item1,c);}
            return result;
        }
        public static void Initialize(){_=Values.Value;_=Scales.Value;}
        public static EndgameValue? Probe(Key key) => Values.Value.TryGetValue(key,out EndgameValue value)?value:null;
        public static EndgameScale? ProbeScale(Key key) => Scales.Value.TryGetValue(key,out EndgameScale value)?value:null;
    }
}
