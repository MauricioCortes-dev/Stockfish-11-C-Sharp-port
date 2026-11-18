using System;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;
using ChessFile=Stockfish_sf_11_CSharp_Library.Types.File;

namespace Stockfish_sf_11_CSharp_Library.Evaluation
{
    /// <summary>Attack maps and piece scoring for one unchanged position during a classic evaluation.</summary>
    public sealed partial class EvaluationAttacks
    {
        private Position position=null!;
        private PawnEntry pawns;
        private readonly Bitboard[,] attackedBy=new Bitboard[2,8];
        private readonly Bitboard[] attackedByTwo=new Bitboard[2], mobilityArea=new Bitboard[2], kingRing=new Bitboard[2];
        private readonly Score[] mobility=new Score[2];
        private readonly int[] kingAttackersCount=new int[2],kingAttackersWeight=new int[2],kingAttacksCount=new int[2];
        private static readonly int[] AttackWeights={0,0,81,52,44,10,0,0};
        private static readonly int[][] MobilityMg={new[]{-62,-53,-12,-4,3,13,22,28,33},new[]{-48,-20,16,26,38,51,55,63,63,68,81,81,91,98},new[]{-58,-27,-15,-10,-5,-2,9,16,30,29,32,38,46,48,58},new[]{-39,-21,3,3,14,22,28,41,43,48,56,60,60,66,67,70,71,73,79,88,88,99,102,102,106,109,113,116}};
        private static readonly int[][] MobilityEg={new[]{-81,-56,-30,-14,8,15,23,27,33},new[]{-59,-23,-3,13,24,42,54,57,65,73,78,86,88,97},new[]{-76,-18,28,55,69,82,112,118,132,142,155,165,166,169,171},new[]{-36,-15,8,18,34,54,61,73,79,92,94,104,113,120,123,126,133,136,140,143,148,166,170,175,184,191,206,212}};
        private static readonly Score Outpost=Score.MakeScore(30,21),ReachableOutpost=Score.MakeScore(32,10),MinorBehindPawn=Score.MakeScore(18,3),KingProtector=Score.MakeScore(7,8),BishopPawns=Score.MakeScore(3,7),LongDiagonalBishop=Score.MakeScore(45,0),CorneredBishop=Score.MakeScore(50,50),RookOnQueenFile=Score.MakeScore(7,6),TrappedRook=Score.MakeScore(52,10),WeakQueen=Score.MakeScore(49,15);
        private static readonly Score[] RookOnFile={Score.MakeScore(21,4),Score.MakeScore(47,25)};
        private const ulong CenterFiles=0x3c3c3c3c3c3c3c3cUL, Center=0x0000001818000000UL;
        public EvaluationAttacks(Position position,PawnEntry pawnEntry)
        { this.position=position??throw new ArgumentNullException(nameof(position));pawns=pawnEntry; }
        internal EvaluationAttacks() { }
        internal void Reset(Position position,PawnEntry pawnEntry)
        {
            this.position=position??throw new ArgumentNullException(nameof(position));pawns=pawnEntry;
            // Match a fresh evaluation, including maps not populated until Pieces.
            // Initialize overwrites the remaining maps/counters for both colors.
            Array.Clear(attackedBy,0,attackedBy.Length);
            mobility[0]=mobility[1]=Score.SCORE_ZERO;
        }
        internal void ClearPosition() { position=null!;pawns=default; }
        public Bitboard AttackedBy(Color color,PieceType type)=>attackedBy[color,type];
        public Bitboard AttackedByTwo(Color color)=>attackedByTwo[color];
        public Bitboard MobilityArea(Color color)=>mobilityArea[color];
        public Bitboard KingRing(Color color)=>kingRing[color];
        public Score Mobility(Color color)=>mobility[color];
        public int KingAttackersCount(Color color)=>kingAttackersCount[color];
        public int KingAttackersWeight(Color color)=>kingAttackersWeight[color];
        public int KingAttacksCount(Color color)=>kingAttacksCount[color];
        public void Initialize(Color us)
        {
            Color them=~us;Direction down=-Direction.PawnPush(us);Square king=position.KingSquare(us);
            Bitboard low=us==Color.WHITE?0x0000000000ffff00UL:0x00ffff0000000000UL;
            Bitboard doublePawn=Bitboard.PawnDoubleAttacks(us,position.Pieces(us,PieceType.PAWN));
            Bitboard blocked=position.Pieces(us,PieceType.PAWN)&(Bitboard.Shift(down,position.Pieces())|low);
            mobilityArea[us]=~(blocked|position.Pieces(us,PieceType.KING,PieceType.QUEEN)|position.BlockersForKing(us)|pawns.PawnAttacks(them));
            attackedBy[us,PieceType.KING]=position.AttacksFrom(PieceType.KING,king);
            attackedBy[us,PieceType.PAWN]=pawns.PawnAttacks(us);
            attackedBy[us,PieceType.ALL_PIECES]=attackedBy[us,PieceType.KING]|attackedBy[us,PieceType.PAWN];
            attackedByTwo[us]=doublePawn|(attackedBy[us,PieceType.KING]&attackedBy[us,PieceType.PAWN]);
            Square center=Square.MakeSquare(Math.Max(1,Math.Min(6,(int)ChessFile.FileOf(king))),Math.Max(1,Math.Min(6,(int)Rank.RankOf(king))));
            kingRing[us]=Bitboards.PseudoAttacks(PieceType.KING,center)|center;
            kingAttackersCount[them]=Bitboard.PopCount(kingRing[us]&pawns.PawnAttacks(them));
            kingAttacksCount[them]=kingAttackersWeight[them]=0;
            kingRing[us]&=~doublePawn;
        }
        /// <remarks>Initialize both colors first, then evaluate knight, bishop, rook and queen for each color in the original order.</remarks>
        public Score Pieces(Color us,PieceType pt)
        {
            if(pt<PieceType.KNIGHT || pt>PieceType.QUEEN)throw new ArgumentOutOfRangeException(nameof(pt));
            Color them=~us;Direction down=-Direction.PawnPush(us);Square king=position.KingSquare(us);
            Bitboard outpostRanks=us==Color.WHITE?0x0000ffffff000000UL:0x000000ffffff0000UL;
            Score score=Score.SCORE_ZERO;attackedBy[us,pt]=0UL;Piece piece=Piece.MakePiece(us,pt);
            for(int i=0;i<position.Count(piece);++i)
            {
                Square square=position.PieceListAt(piece,i);Bitboard attacks;
                if(pt==PieceType.BISHOP)attacks=Bitboards.Attacks(pt,square,position.Pieces()^position.Pieces(PieceType.QUEEN));
                else if(pt==PieceType.ROOK)attacks=Bitboards.Attacks(pt,square,position.Pieces()^position.Pieces(PieceType.QUEEN)^position.Pieces(us,PieceType.ROOK));
                else attacks=position.AttacksFrom(pt,square);
                if(position.BlockersForKing(us)&square)attacks&=Bitboards.Line(king,square);
                attackedByTwo[us]|=attackedBy[us,PieceType.ALL_PIECES]&attacks;
                attackedBy[us,pt]|=attacks;attackedBy[us,PieceType.ALL_PIECES]|=attacks;
                if(attacks&kingRing[them]){++kingAttackersCount[us];kingAttackersWeight[us]+=AttackWeights[pt];kingAttacksCount[us]+=Bitboard.PopCount(attacks&attackedBy[them,PieceType.KING]);}
                int mob=Bitboard.PopCount(attacks&mobilityArea[us]);int table=(int)pt-2;
                mobility[us]+=Score.MakeScore(MobilityMg[table][mob],MobilityEg[table][mob]);
                if(pt==PieceType.KNIGHT || pt==PieceType.BISHOP)
                {
                    Bitboard outposts=outpostRanks&attackedBy[us,PieceType.PAWN]&~pawns.PawnAttacksSpan(them);
                    if(outposts&square)score+=Outpost*(pt==PieceType.KNIGHT?2:1);
                    else if(pt==PieceType.KNIGHT && (bool)(outposts&attacks&~position.Pieces(us)))score+=ReachableOutpost;
                    if(Bitboard.Shift(down,position.Pieces(PieceType.PAWN))&square)score+=MinorBehindPawn;
                    score-=KingProtector*Bitboards.Distance(square,king);
                    if(pt==PieceType.BISHOP)
                    {
                        Bitboard blocked=position.Pieces(us,PieceType.PAWN)&Bitboard.Shift(down,position.Pieces());
                        score-=BishopPawns*position.PawnsOnSameColorSquares(us,square)*(1+Bitboard.PopCount(blocked&(Bitboard)CenterFiles));
                        if(Bitboard.MoreThanOne(Bitboards.Attacks(PieceType.BISHOP,square,position.Pieces(PieceType.PAWN))&(Bitboard)Center))score+=LongDiagonalBishop;
                        if(position.IsChess960 && (square==Square.RelativeSquare(us,Square.SQ_A1) || square==Square.RelativeSquare(us,Square.SQ_H1)))
                        {
                            Direction d=Direction.PawnPush(us)+(ChessFile.FileOf(square)==ChessFile.FILE_A?Direction.EAST:Direction.WEST);
                            if(position.PieceOn(square+d)==Piece.MakePiece(us,PieceType.PAWN))
                                score-=CorneredBishop*(!position.Empty(square+d+Direction.PawnPush(us))?4:position.PieceOn(square+d+d)==Piece.MakePiece(us,PieceType.PAWN)?2:1);
                        }
                    }
                }
                if(pt==PieceType.ROOK)
                {
                    if(Bitboard.FileBB(square)&position.Pieces(PieceType.QUEEN))score+=RookOnQueenFile;
                    if(position.IsOnSemiopenFile(us,square))score+=RookOnFile[position.IsOnSemiopenFile(them,square)?1:0];
                    else if(mob<=3){ChessFile kf=ChessFile.FileOf(king);if((kf<ChessFile.FILE_E)==(ChessFile.FileOf(square)<kf))score-=TrappedRook*(1+(position.CastlingRightsFor(us)==0?1:0));}
                }
                if(pt==PieceType.QUEEN && (bool)position.SliderBlockers(position.Pieces(them,PieceType.ROOK,PieceType.BISHOP),square,out _))score-=WeakQueen;
            }
            return score;
        }
    }
}
