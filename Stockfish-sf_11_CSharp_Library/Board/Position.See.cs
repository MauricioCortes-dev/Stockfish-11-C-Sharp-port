using System;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Board
{
    public sealed partial class Position
    {
        /// <summary>Static exchange evaluation against a threshold, as in Position::see_ge.</summary>
        public bool SeeGe(Move move, Value threshold)
        {
            if (!Move.IsOk(move)) throw new ArgumentException("Invalid move.", nameof(move));
            if (Move.TypeOf(move) != MoveType.NORMAL) return (int)threshold <= 0;
            Square from = Move.FromSquare(move), to = Move.ToSquare(move);
            int swap = (int)PSQT.PieceValue[0, PieceOn(to)] - (int)threshold;
            if (swap < 0) return false;
            swap = (int)PSQT.PieceValue[0, PieceOn(from)] - swap;
            if (swap <= 0) return true;

            Bitboard occupied = Pieces() ^ from ^ to;
            Color side = Piece.ColorOf(PieceOn(from));
            Bitboard attackers = AttackersTo(to, occupied);
            int result = 1;
            while (true)
            {
                side = ~side;
                attackers &= occupied;
                Bitboard sideAttackers = attackers & Pieces(side);
                if (!sideAttackers) break;
                if (State.pinners[~side] & occupied)
                    sideAttackers &= ~State.blockersForKing[side];
                if (!sideAttackers) break;
                result ^= 1;

                PieceType type;
                Bitboard candidates;
                if ((bool)(candidates = sideAttackers & Pieces(PieceType.PAWN))) type = PieceType.PAWN;
                else if ((bool)(candidates = sideAttackers & Pieces(PieceType.KNIGHT))) type = PieceType.KNIGHT;
                else if ((bool)(candidates = sideAttackers & Pieces(PieceType.BISHOP))) type = PieceType.BISHOP;
                else if ((bool)(candidates = sideAttackers & Pieces(PieceType.ROOK))) type = PieceType.ROOK;
                else if ((bool)(candidates = sideAttackers & Pieces(PieceType.QUEEN))) type = PieceType.QUEEN;
                else return (bool)(attackers & ~Pieces(side)) ? (result ^ 1) != 0 : result != 0;

                swap = (int)PSQT.PieceValue[0, (int)type] - swap;
                if (swap < result) break;
                occupied ^= Bitboard.Lsb(candidates);
                if (type == PieceType.PAWN || type == PieceType.BISHOP || type == PieceType.QUEEN)
                    attackers |= Bitboards.Attacks(PieceType.BISHOP, to, occupied) & Pieces(PieceType.BISHOP, PieceType.QUEEN);
                if (type == PieceType.ROOK || type == PieceType.QUEEN)
                    attackers |= Bitboards.Attacks(PieceType.ROOK, to, occupied) & Pieces(PieceType.ROOK, PieceType.QUEEN);
            }
            return result != 0;
        }
    }
}
