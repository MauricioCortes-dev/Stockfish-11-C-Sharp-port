using System;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;
using ChessFile=Stockfish_sf_11_CSharp_Library.Types.File;

namespace Stockfish_sf_11_CSharp_Library.Evaluation
{
    public sealed partial class EvaluationAttacks
    {
        private static readonly Score PawnlessFlank=Score.MakeScore(17,95),FlankAttacks=Score.MakeScore(8,0);
        private static readonly Score ThreatByKing=Score.MakeScore(24,89),Hanging=Score.MakeScore(69,36),RestrictedPiece=Score.MakeScore(7,7),ThreatBySafePawn=Score.MakeScore(173,94),ThreatByPawnPush=Score.MakeScore(48,39),KnightOnQueen=Score.MakeScore(16,12),SliderOnQueen=Score.MakeScore(59,18);
        private static readonly Score[] ThreatByMinor={Score.MakeScore(0,0),Score.MakeScore(6,32),Score.MakeScore(59,41),Score.MakeScore(79,56),Score.MakeScore(90,119),Score.MakeScore(79,161),Score.SCORE_ZERO,Score.SCORE_ZERO};
        private static readonly Score[] ThreatByRook={Score.MakeScore(0,0),Score.MakeScore(3,44),Score.MakeScore(38,71),Score.MakeScore(38,61),Score.MakeScore(0,38),Score.MakeScore(51,38),Score.SCORE_ZERO,Score.SCORE_ZERO};
        private static readonly ulong[] KingFlank={0x0707070707070707UL,0x0f0f0f0f0f0f0f0fUL,0x0f0f0f0f0f0f0f0fUL,0x3c3c3c3c3c3c3c3cUL,0x3c3c3c3c3c3c3c3cUL,0xf0f0f0f0f0f0f0f0UL,0xf0f0f0f0f0f0f0f0UL,0xe0e0e0e0e0e0e0e0UL};

        /// <summary>King score after all piece maps have been populated. Pass the live pawn-cache slot so updates persist.</summary>
        public Score King(Color us, ref PawnEntry pawnEntry)
        {
            Color them=~us;Square king=position.KingSquare(us);
            Bitboard camp=us==Color.WHITE?0x000000ffffffffffUL:0xffffffffff000000UL;
            Score score=pawnEntry.KingSafety(position,us);
            Bitboard weak=attackedBy[them,PieceType.ALL_PIECES]&~attackedByTwo[us]&(~attackedBy[us,PieceType.ALL_PIECES]|attackedBy[us,PieceType.KING]|attackedBy[us,PieceType.QUEEN]);
            Bitboard safe=~position.Pieces(them)&(~attackedBy[us,PieceType.ALL_PIECES]|(weak&attackedByTwo[them]));
            Bitboard rookRays=Bitboards.Attacks(PieceType.ROOK,king,position.Pieces()^position.Pieces(us,PieceType.QUEEN));
            Bitboard bishopRays=Bitboards.Attacks(PieceType.BISHOP,king,position.Pieces()^position.Pieces(us,PieceType.QUEEN));
            Bitboard unsafeChecks=0UL;
            Bitboard rookChecks=rookRays&safe&attackedBy[them,PieceType.ROOK];
            int danger=0;
            if(rookChecks)danger+=1080;else unsafeChecks|=rookRays&attackedBy[them,PieceType.ROOK];
            Bitboard queenChecks=(rookRays|bishopRays)&attackedBy[them,PieceType.QUEEN]&safe&~attackedBy[us,PieceType.QUEEN]&~rookChecks;
            if(queenChecks)danger+=780;
            Bitboard bishopChecks=bishopRays&attackedBy[them,PieceType.BISHOP]&safe&~queenChecks;
            if(bishopChecks)danger+=635;else unsafeChecks|=bishopRays&attackedBy[them,PieceType.BISHOP];
            Bitboard knightChecks=position.AttacksFrom(PieceType.KNIGHT,king)&attackedBy[them,PieceType.KNIGHT];
            if(knightChecks&safe)danger+=790;else unsafeChecks|=knightChecks;
            Bitboard flank=KingFlank[ChessFile.FileOf(king)];
            Bitboard b1=attackedBy[them,PieceType.ALL_PIECES]&flank&camp,b2=b1&attackedByTwo[them],b3=attackedBy[us,PieceType.ALL_PIECES]&flank&camp;
            int flankAttack=Bitboard.PopCount(b1)+Bitboard.PopCount(b2),flankDefense=Bitboard.PopCount(b3);
            danger += kingAttackersCount[them] * kingAttackersWeight[them]
                    + 185 * Bitboard.PopCount(kingRing[us] & weak)
                    + 148 * Bitboard.PopCount(unsafeChecks)
                    + 98 * Bitboard.PopCount(position.BlockersForKing(us))
                    + 69 * kingAttacksCount[them]
                    + 3 * flankAttack * flankAttack / 8
                    + (int)Score.MgValue(mobility[them] - mobility[us])
                    - 873 * (position.Count(them, PieceType.QUEEN) == 0 ? 1 : 0)
                    - 100 * ((bool)(attackedBy[us, PieceType.KNIGHT] & attackedBy[us, PieceType.KING]) ? 1 : 0)
                    - 6 * (int)Score.MgValue(score) / 8
                    - 4 * flankDefense + 37;
            if(danger>100)score-=Score.MakeScore(danger*danger/4096,danger/16);
            if(!(position.Pieces(PieceType.PAWN)&flank))score-=PawnlessFlank;
            return score-FlankAttacks*flankAttack;
        }

        /// <summary>Threats from a color after all piece attacks have been populated.</summary>
        public Score Threats(Color us)
        {
            Color them=~us;Direction up=Direction.PawnPush(us);
            Bitboard rank3=Bitboard.RankBB(us==Color.WHITE?Rank.RANK_3:Rank.RANK_6);
            Bitboard nonPawnEnemies=position.Pieces(them)&~position.Pieces(PieceType.PAWN);
            Bitboard protectedSquares=attackedBy[them,PieceType.PAWN]|(attackedByTwo[them]&~attackedByTwo[us]);
            Bitboard defended=nonPawnEnemies&protectedSquares;
            Bitboard weak=position.Pieces(them)&~protectedSquares&attackedBy[us,PieceType.ALL_PIECES];
            Score score=Score.SCORE_ZERO;
            if(defended|weak)
            {
                Bitboard b=(defended|weak)&(attackedBy[us,PieceType.KNIGHT]|attackedBy[us,PieceType.BISHOP]);
                while(b)score+=ThreatByMinor[Piece.TypeOf(position.PieceOn(Bitboard.PopLsb(ref b)))];
                b=weak&attackedBy[us,PieceType.ROOK];
                while(b)score+=ThreatByRook[Piece.TypeOf(position.PieceOn(Bitboard.PopLsb(ref b)))];
                if(weak&attackedBy[us,PieceType.KING])score+=ThreatByKing;
                b=~attackedBy[them,PieceType.ALL_PIECES]|(nonPawnEnemies&attackedByTwo[us]);
                score+=Hanging*Bitboard.PopCount(weak&b);
            }
            Bitboard restricted=attackedBy[them,PieceType.ALL_PIECES]&~protectedSquares&attackedBy[us,PieceType.ALL_PIECES];
            score+=RestrictedPiece*Bitboard.PopCount(restricted);
            Bitboard safe=~attackedBy[them,PieceType.ALL_PIECES]|attackedBy[us,PieceType.ALL_PIECES];
            Bitboard pawnAttacks=Bitboard.PawnAttacks(us,position.Pieces(us,PieceType.PAWN)&safe)&nonPawnEnemies;
            score+=ThreatBySafePawn*Bitboard.PopCount(pawnAttacks);
            Bitboard pushes=Bitboard.Shift(up,position.Pieces(us,PieceType.PAWN))&~position.Pieces();
            pushes|=Bitboard.Shift(up,pushes&rank3)&~position.Pieces();
            pushes&=~attackedBy[them,PieceType.PAWN]&safe;
            score+=ThreatByPawnPush*Bitboard.PopCount(Bitboard.PawnAttacks(us,pushes)&nonPawnEnemies);
            if(position.Count(them,PieceType.QUEEN)==1)
            {
                Square queen=position.SquareOf(them,PieceType.QUEEN);
                safe=mobilityArea[us]&~protectedSquares;
                Bitboard knight=attackedBy[us,PieceType.KNIGHT]&position.AttacksFrom(PieceType.KNIGHT,queen);
                score+=KnightOnQueen*Bitboard.PopCount(knight&safe);
                Bitboard sliders=(attackedBy[us,PieceType.BISHOP]&position.AttacksFrom(PieceType.BISHOP,queen))|(attackedBy[us,PieceType.ROOK]&position.AttacksFrom(PieceType.ROOK,queen));
                score+=SliderOnQueen*Bitboard.PopCount(sliders&safe&attackedByTwo[us]);
            }
            return score;
        }
    }
}
