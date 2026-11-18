using System;
using Stockfish_sf_11_CSharp_Library.Types;
using Stockfish_sf_11_CSharp_Library.MoveGeneration;

namespace Stockfish_sf_11_CSharp_Library.Board
{
    public sealed partial class Position
    {
        /// <summary>The fifty-move rule and repetition, excluding stalemate as in the original.</summary>
        public bool IsDraw(int ply)
        {
            if (State.rule50 > 99 && (!State.checkersBB || HasLegalMove())) return true;
            return State.repetition != 0 && State.repetition < ply;
        }

        public bool HasRepeated()
        {
            StateInfo current = State;
            int end = Math.Min(current.rule50, current.pliesFromNull);
            while (end-- >= 4)
            {
                if (current.repetition != 0) return true;
                current = current.previous!;
            }
            return false;
        }

        public bool HasGameCycle(int ply)
        {
            int end = Math.Min(State.rule50, State.pliesFromNull);
            if (end < 3) return false;
            Key originalKey = State.key;
            StateInfo previous = State.previous!;
            for (int distance = 3; distance <= end; distance += 2)
            {
                previous = previous.previous!.previous!;
                Key moveKey = (ulong)originalKey ^ (ulong)previous.key;
                int index = Zobrist.H1(moveKey);
                if ((ulong)Zobrist.CuckooKey(index) != (ulong)moveKey)
                {
                    index = Zobrist.H2(moveKey);
                    if ((ulong)Zobrist.CuckooKey(index) != (ulong)moveKey) continue;
                }
                Move move = Zobrist.CuckooMove(index);
                Square from = Move.FromSquare(move), to = Move.ToSquare(move);
                if ((bool)(Bitboards.Between(from, to) & Pieces())) continue;
                if (ply > distance) return true;
                if (Piece.ColorOf(PieceOn(Empty(from) ? to : from)) != sideToMove) continue;
                if (previous.repetition != 0) return true;
            }
            return false;
        }
    }
}
