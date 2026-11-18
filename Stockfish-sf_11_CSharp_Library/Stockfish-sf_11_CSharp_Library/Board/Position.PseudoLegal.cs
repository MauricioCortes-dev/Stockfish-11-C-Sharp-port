using Stockfish_sf_11_CSharp_Library.Types;
using Stockfish_sf_11_CSharp_Library.MoveGeneration;

namespace Stockfish_sf_11_CSharp_Library.Board
{
    public sealed partial class Position
    {
        /// <summary>Validate a move obtained from a potentially corrupted transposition table.</summary>
        public bool PseudoLegal(Move move)
        {
            Color us = sideToMove;
            Square from = Move.FromSquare(move), to = Move.ToSquare(move);
            Piece pc = PieceOn(from);
            MoveType kind = Move.TypeOf(move);
            if (kind != MoveType.NORMAL) return ContainsLegalMove(move);
            if (kind == MoveType.NORMAL && (((int)move & (3 << 12)) != 0)) return false;
            if (pc == Piece.NO_PIECE || Piece.ColorOf(pc) != us) return false;
            PieceType pt = Piece.TypeOf(pc);
            if (Pieces(us) & to) return false;
            if (pt == PieceType.PAWN)
            {
                Rank rank = Rank.RankOf(to);
                if (rank == Rank.RANK_1 || rank == Rank.RANK_8) return false;
                Direction up = Direction.PawnPush(us);
                if (!(Bitboards.PawnAttacks(us, from) & Pieces(~us) & to)
                    && !(from + up == to && Empty(to))
                    && !(from + up + up == to && Rank.RelativeRank(us, Rank.RankOf(from)) == Rank.RANK_2 && Empty(to) && Empty(to-up))) return false;
            }
            else if (!(Bitboards.Attacks(pt, from, Pieces()) & to)) return false;
            return EvasionAllowed(pt, from, to);
        }

        private bool EvasionAllowed(PieceType type, Square from, Square to)
        {
            if (!State.checkersBB) return true;
            if (type == PieceType.KING) return !(AttackersTo(to, Pieces() ^ from) & Pieces(~sideToMove));
            if (Bitboard.MoreThanOne(State.checkersBB)) return false;
            return (Bitboards.Between(Bitboard.Lsb(State.checkersBB), KingSquare(sideToMove)) | State.checkersBB) & to;
        }
    }
}
