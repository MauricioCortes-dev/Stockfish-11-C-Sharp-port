using System;
using Stockfish_sf_11_CSharp_Library.Types;
using Stockfish_sf_11_CSharp_Library.Board;
using ChessFile = Stockfish_sf_11_CSharp_Library.Types.File;

namespace Stockfish_sf_11_CSharp_Library.Evaluation
{
    public sealed partial class EvaluationAttacks
    {
        private static readonly Score[] PassedRank = {
            Score.MakeScore(0,0), Score.MakeScore(10,28), Score.MakeScore(17,33),
            Score.MakeScore(15,41), Score.MakeScore(62,72), Score.MakeScore(168,177),
            Score.MakeScore(276,260)
        };
        private static readonly Score PassedFile = Score.MakeScore(11,8);

        /// <summary>Scores passed and candidate passed pawns after both attack maps are complete.</summary>
        public Score Passed(Color us)
        {
            Color them = ~us;
            Direction up = Direction.PawnPush(us);
            Score score = Score.SCORE_ZERO;
            Bitboard remaining = pawns.PassedPawns(us);
            while (remaining)
            {
                Square square = Bitboard.PopLsb(ref remaining);
                int rank = Rank.RelativeRank(us, square);
                Score bonus = PassedRank[rank];
                if (rank > Rank.RANK_3)
                {
                    int weight = 5 * rank - 13;
                    Square block = square + up;
                    int enemyDistance = Math.Min(Bitboards.Distance(position.KingSquare(them), block), 5);
                    int ownDistance = Math.Min(Bitboards.Distance(position.KingSquare(us), block), 5);
                    bonus += Score.MakeScore(0, (enemyDistance * 19 / 4 - ownDistance * 2) * weight);
                    if (rank != Rank.RANK_7)
                        bonus -= Score.MakeScore(0, Math.Min(Bitboards.Distance(position.KingSquare(us), block + up), 5) * weight);
                    if (position.Empty(block))
                    {
                        Bitboard path = Bitboard.ForwardFile(us, square);
                        Bitboard unsafeSquares = Bitboard.PassedPawnSpan(us, square);
                        Bitboard behind = Bitboard.ForwardFile(them, square) & position.Pieces(PieceType.ROOK, PieceType.QUEEN);
                        if (!(position.Pieces(them) & behind))
                            unsafeSquares &= attackedBy[them, PieceType.ALL_PIECES];
                        int k = !unsafeSquares ? 35 : !(unsafeSquares & path) ? 20 : !(unsafeSquares & block) ? 9 : 0;
                        if ((bool)(position.Pieces(us) & behind) || (bool)(attackedBy[us, PieceType.ALL_PIECES] & block)) k += 5;
                        bonus += Score.MakeScore(k * weight, k * weight);
                    }
                }
                if (!position.PawnPassed(us, square + up) || (position.Pieces(PieceType.PAWN) & (square + up)))
                    bonus /= 2;
                score += bonus - PassedFile * (int)ChessFile.MapToQueenside(ChessFile.FileOf(square));
            }
            return score;
        }
    }
}
