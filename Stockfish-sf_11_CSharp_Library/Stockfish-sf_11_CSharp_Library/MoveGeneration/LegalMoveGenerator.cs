using System.Collections.Generic;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.MoveGeneration
{
    /// <summary>Convenience entry point for Stockfish's ordered legal moves.</summary>
    public static class LegalMoveGenerator
    {
        public static List<Move> Generate(Position position) => MoveGenerator.Generate(position, GenType.LEGAL);
    }
}
