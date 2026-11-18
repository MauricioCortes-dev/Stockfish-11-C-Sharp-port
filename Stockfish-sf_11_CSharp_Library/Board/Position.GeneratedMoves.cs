using System;
using Stockfish_sf_11_CSharp_Library.MoveGeneration;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Board
{
    public sealed partial class Position
    {
        // Keep the stack buffer in the infrequent query, not in IsDraw's hot path.
        internal bool HasLegalMove()
        {
            Span<Move> moves=stackalloc Move[ChessConstants.MAX_MOVES];
            return MoveGenerator.GenerateInto(this,GenType.LEGAL,moves)!=0;
        }
        internal bool ContainsLegalMove(Move move)
        {
            Span<Move> moves=stackalloc Move[ChessConstants.MAX_MOVES];
            int count=MoveGenerator.GenerateInto(this,GenType.LEGAL,moves);
            for(int i=0;i<count;++i)if(moves[i]==move)return true;
            return false;
        }
    }
}
