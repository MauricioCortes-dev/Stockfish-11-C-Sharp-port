using System;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;
using ChessFile = Stockfish_sf_11_CSharp_Library.Types.File;
namespace Stockfish_sf_11_CSharp_Library.Evaluation
{
    /// <summary>Specialized scale factors for classic endgames.</summary>
    public sealed class EndgameScale : EndgameBase<ScaleFactor>
    {
        private static readonly int[] KRPPKRPScaleFactors={0,9,10,14,21,44,0,0};
        public EndgameCode Code { get; }
        public EndgameScale(EndgameCode code, Color strongSide) : base(strongSide)
        {
            if (code <= EndgameCode.SCALING_FUNCTIONS || code > EndgameCode.KPKP) throw new ArgumentOutOfRangeException(nameof(code));
            Code=code;
        }
        private Square Normalize(Position p, Square square)
        {
            if (ChessFile.FileOf(p.SquareOf(StrongSide,PieceType.PAWN)) >= ChessFile.FILE_E) square=(int)square^7;
            return StrongSide==Color.WHITE?square:~square;
        }
        public override ScaleFactor Evaluate(Position p)
        {
            if(p==null)throw new ArgumentNullException(nameof(p));
            Color strong=StrongSide,weak=WeakSide;Square wk=p.KingSquare(strong),bk=p.KingSquare(weak);
            switch(Code)
            {
                case EndgameCode.KRPKR:
                    Square wking=Normalize(p,wk),bking=Normalize(p,bk),wrook=Normalize(p,p.SquareOf(strong,PieceType.ROOK)),wpawn=Normalize(p,p.SquareOf(strong,PieceType.PAWN)),brook=Normalize(p,p.SquareOf(weak,PieceType.ROOK));
                    ChessFile f=ChessFile.FileOf(wpawn);Rank r=Rank.RankOf(wpawn);Square promote=Square.MakeSquare(f,Rank.RANK_8);int tempo=p.SideToMove==strong?1:0;
                    if(r<=Rank.RANK_5 && Bitboards.Distance(bking,promote)<=1 && wking<=Square.SQ_H5 && (Rank.RankOf(brook)==Rank.RANK_6 || (r<=Rank.RANK_3 && Rank.RankOf(wrook)!=Rank.RANK_6)))return ScaleFactor.SCALE_FACTOR_DRAW;
                    if(r==Rank.RANK_6 && Bitboards.Distance(bking,promote)<=1 && (int)Rank.RankOf(wking)+tempo<=Rank.RANK_6 && (Rank.RankOf(brook)==Rank.RANK_1 || (tempo==0 && Bitboards.FileDistance(brook,wpawn)>=3)))return ScaleFactor.SCALE_FACTOR_DRAW;
                    if(r>=Rank.RANK_6 && bking==promote && Rank.RankOf(brook)==Rank.RANK_1 && (tempo==0 || Bitboards.Distance(wking,wpawn)>=2))return ScaleFactor.SCALE_FACTOR_DRAW;
                    if(wpawn==Square.SQ_A7 && wrook==Square.SQ_A8 && (bking==Square.SQ_H7 || bking==Square.SQ_G7) && ChessFile.FileOf(brook)==ChessFile.FILE_A && (Rank.RankOf(brook)<=Rank.RANK_3 || ChessFile.FileOf(wking)>=ChessFile.FILE_D || Rank.RankOf(wking)<=Rank.RANK_5))return ScaleFactor.SCALE_FACTOR_DRAW;
                    if(r<=Rank.RANK_5 && bking==wpawn+Direction.NORTH && Bitboards.Distance(wking,wpawn)-tempo>=2 && Bitboards.Distance(wking,brook)-tempo>=2)return ScaleFactor.SCALE_FACTOR_DRAW;
                    if(r==Rank.RANK_7 && f!=ChessFile.FILE_A && ChessFile.FileOf(wrook)==f && wrook!=promote && Bitboards.Distance(wking,promote)<Bitboards.Distance(bking,promote)-2+tempo && Bitboards.Distance(wking,promote)<Bitboards.Distance(bking,wrook)+tempo)return (int)ScaleFactor.SCALE_FACTOR_MAX-2*Bitboards.Distance(wking,promote);
                    if(f!=ChessFile.FILE_A && ChessFile.FileOf(wrook)==f && wrook<wpawn && Bitboards.Distance(wking,promote)<Bitboards.Distance(bking,promote)-2+tempo && Bitboards.Distance(wking,wpawn+Direction.NORTH)<Bitboards.Distance(bking,wpawn+Direction.NORTH)-2+tempo && (Bitboards.Distance(bking,wrook)+tempo>=3 || (Bitboards.Distance(wking,promote)<Bitboards.Distance(bking,wrook)+tempo && Bitboards.Distance(wking,wpawn+Direction.NORTH)<Bitboards.Distance(bking,wrook)+tempo)))return (int)ScaleFactor.SCALE_FACTOR_MAX-8*Bitboards.Distance(wpawn,promote)-2*Bitboards.Distance(wking,promote);
                    if(r<=Rank.RANK_4 && bking>wpawn)
                    {
                        if(ChessFile.FileOf(bking)==ChessFile.FileOf(wpawn))return 10;
                        if(Bitboards.FileDistance(bking,wpawn)==1 && Bitboards.Distance(wking,bking)>2)return 24-2*Bitboards.Distance(wking,bking);
                    }
                    return ScaleFactor.SCALE_FACTOR_NONE;
                case EndgameCode.KBPsK:
                    Bitboard own=p.Pieces(strong,PieceType.PAWN);ChessFile ownFile=ChessFile.FileOf(Bitboard.Lsb(own));Square ownBishop=p.SquareOf(strong,PieceType.BISHOP);
                    if((ownFile==ChessFile.FILE_A || ownFile==ChessFile.FILE_H) && !(own&~Bitboard.FileBB(ownFile)))
                    {
                        Square promotion=Square.RelativeSquare(strong,Square.MakeSquare(ownFile,Rank.RANK_8));
                        if(Bitboard.OppositeColors(promotion,ownBishop) && Bitboards.Distance(promotion,bk)<=1)return ScaleFactor.SCALE_FACTOR_DRAW;
                    }
                    if((ownFile==ChessFile.FILE_B || ownFile==ChessFile.FILE_G) && !(p.Pieces(PieceType.PAWN)&~Bitboard.FileBB(ownFile)) && p.NonPawnMaterial(weak)==Value.VALUE_ZERO && p.Count(weak,PieceType.PAWN)>=1)
                    {
                        Square weakPawn=Bitboard.FrontmostSquare(strong,p.Pieces(weak,PieceType.PAWN));
                        if(Rank.RelativeRank(strong,weakPawn)==Rank.RANK_7 && (p.Pieces(strong,PieceType.PAWN)&(weakPawn+Direction.PawnPush(weak))) && (Bitboard.OppositeColors(ownBishop,weakPawn)||p.Count(strong,PieceType.PAWN)==1))
                        {
                            int sd=Bitboards.Distance(weakPawn,wk),wd=Bitboards.Distance(weakPawn,bk);
                            if(Rank.RelativeRank(strong,bk)>=Rank.RANK_7 && wd<=2 && wd<=sd)return ScaleFactor.SCALE_FACTOR_DRAW;
                        }
                    }
                    return ScaleFactor.SCALE_FACTOR_NONE;
                case EndgameCode.KRPKB:
                    ulong rookFiles=(ulong)Bitboard.FileBB(ChessFile.FILE_A)|(ulong)Bitboard.FileBB(ChessFile.FILE_H);
                    if(((ulong)p.Pieces(PieceType.PAWN)&rookFiles)!=0)
                    {
                        Square bs=p.SquareOf(weak,PieceType.BISHOP),rp=p.SquareOf(strong,PieceType.PAWN);Rank rr=Rank.RelativeRank(strong,rp);Direction push=Direction.PawnPush(strong);
                        if(rr==Rank.RANK_5 && !Bitboard.OppositeColors(bs,rp))
                        {
                            int distance=Bitboards.Distance(rp+3*push,bk);
                            return distance<=2 && !(distance==0 && bk==wk+2*push)?(ScaleFactor)24:(ScaleFactor)48;
                        }
                        if(rr==Rank.RANK_6 && Bitboards.Distance(rp+2*push,bk)<=1 && (Bitboards.PseudoAttacks(PieceType.BISHOP,bs)&(rp+push)) && Bitboards.FileDistance(bs,rp)>=2)return 8;
                    }
                    return ScaleFactor.SCALE_FACTOR_NONE;
                case EndgameCode.KBPPKB:
                    Square wb=p.SquareOf(strong,PieceType.BISHOP),bb=p.SquareOf(weak,PieceType.BISHOP);
                    if(!Bitboard.OppositeColors(wb,bb))return ScaleFactor.SCALE_FACTOR_NONE;
                    Piece pawnPiece=Piece.MakePiece(strong,PieceType.PAWN);Square a=p.PieceListAt(pawnPiece,0),b=p.PieceListAt(pawnPiece,1),block1,block2;
                    if(Rank.RelativeRank(strong,a)>Rank.RelativeRank(strong,b)){block1=a+Direction.PawnPush(strong);block2=Square.MakeSquare(ChessFile.FileOf(b),Rank.RankOf(a));}
                    else {block1=b+Direction.PawnPush(strong);block2=Square.MakeSquare(ChessFile.FileOf(a),Rank.RankOf(b));}
                    int df=Bitboards.FileDistance(a,b);
                    if(df==0 && ChessFile.FileOf(bk)==ChessFile.FileOf(block1) && Rank.RelativeRank(strong,bk)>=Rank.RelativeRank(strong,block1) && Bitboard.OppositeColors(bk,wb))return ScaleFactor.SCALE_FACTOR_DRAW;
                    if(df==1 && Bitboard.OppositeColors(bk,wb))
                    {
                        if(bk==block1 && (bb==block2 || (bool)(p.AttacksFrom(PieceType.BISHOP,block2)&p.Pieces(weak,PieceType.BISHOP)) || Bitboards.RankDistance(a,b)>=2))return ScaleFactor.SCALE_FACTOR_DRAW;
                        if(bk==block2 && (bb==block1 || (bool)(p.AttacksFrom(PieceType.BISHOP,block1)&p.Pieces(weak,PieceType.BISHOP))))return ScaleFactor.SCALE_FACTOR_DRAW;
                    }
                    return ScaleFactor.SCALE_FACTOR_NONE;
                case EndgameCode.KNPK:
                    Square np=Normalize(p,p.SquareOf(strong,PieceType.PAWN));
                    return np==Square.SQ_A7 && Bitboards.Distance(Square.SQ_A8,Normalize(p,bk))<=1 ? ScaleFactor.SCALE_FACTOR_DRAW : ScaleFactor.SCALE_FACTOR_NONE;
                case EndgameCode.KNPKB:
                    Square npb=p.SquareOf(strong,PieceType.PAWN), bishop=p.SquareOf(weak,PieceType.BISHOP);
                    return (bool)(Bitboard.ForwardFile(strong,npb)&p.AttacksFrom(PieceType.BISHOP,bishop)) ? (ScaleFactor)Bitboards.Distance(bk,npb) : ScaleFactor.SCALE_FACTOR_NONE;
                case EndgameCode.KPKP:
                    Square pawn=Normalize(p,p.SquareOf(strong,PieceType.PAWN));
                    if(Rank.RankOf(pawn)>=Rank.RANK_5 && ChessFile.FileOf(pawn)!=ChessFile.FILE_A)return ScaleFactor.SCALE_FACTOR_NONE;
                    return Bitbases.Probe(Normalize(p,wk),pawn,Normalize(p,bk),strong==p.SideToMove?Color.WHITE:Color.BLACK) ? ScaleFactor.SCALE_FACTOR_NONE : ScaleFactor.SCALE_FACTOR_DRAW;
                case EndgameCode.KBPKB:
                case EndgameCode.KBPKN:
                    Square bp=p.SquareOf(strong,PieceType.PAWN),sb=p.SquareOf(strong,PieceType.BISHOP);
                    if(ChessFile.FileOf(bk)==ChessFile.FileOf(bp) && Rank.RelativeRank(strong,bp)<Rank.RelativeRank(strong,bk) && (Bitboard.OppositeColors(bk,sb) || Rank.RelativeRank(strong,bk)<=Rank.RANK_6))return ScaleFactor.SCALE_FACTOR_DRAW;
                    if(Code==EndgameCode.KBPKB && Bitboard.OppositeColors(sb,p.SquareOf(weak,PieceType.BISHOP)))return ScaleFactor.SCALE_FACTOR_DRAW;
                    return ScaleFactor.SCALE_FACTOR_NONE;
                case EndgameCode.KPsK:
                    Bitboard pawns=p.Pieces(strong,PieceType.PAWN);ulong maskA=(ulong)Bitboard.FileBB(ChessFile.FILE_A),maskH=(ulong)Bitboard.FileBB(ChessFile.FILE_H);
                    if(!(pawns&~Bitboard.ForwardRanks(weak,bk)) && ((((ulong)pawns&~maskA)==0)||(((ulong)pawns&~maskH)==0)) && Bitboards.FileDistance(bk,Bitboard.Lsb(pawns))<=1)return ScaleFactor.SCALE_FACTOR_DRAW;
                    return ScaleFactor.SCALE_FACTOR_NONE;
                case EndgameCode.KQKRPs:
                    Square rook=p.SquareOf(weak,PieceType.ROOK);
                    if(Rank.RelativeRank(weak,bk)<=Rank.RANK_2 && Rank.RelativeRank(weak,wk)>=Rank.RANK_4 && Rank.RelativeRank(weak,rook)==Rank.RANK_3 && (p.Pieces(weak,PieceType.PAWN)&p.AttacksFrom(PieceType.KING,bk)&Bitboards.PawnAttacks(strong,rook)))return ScaleFactor.SCALE_FACTOR_DRAW;
                    return ScaleFactor.SCALE_FACTOR_NONE;
                case EndgameCode.KRPPKRP:
                    Piece sp=Piece.MakePiece(strong,PieceType.PAWN);Square p1=p.PieceListAt(sp,0),p2=p.PieceListAt(sp,1);
                    if(p.PawnPassed(strong,p1)||p.PawnPassed(strong,p2))return ScaleFactor.SCALE_FACTOR_NONE;
                    int rank=Math.Max((int)Rank.RelativeRank(strong,p1),(int)Rank.RelativeRank(strong,p2));
                    if(Bitboards.FileDistance(bk,p1)<=1 && Bitboards.FileDistance(bk,p2)<=1 && Rank.RelativeRank(strong,bk)>rank)return KRPPKRPScaleFactors[rank];
                    return ScaleFactor.SCALE_FACTOR_NONE;
                default:throw new NotSupportedException("This scale function has not yet been ported.");
            }
        }
    }
}
