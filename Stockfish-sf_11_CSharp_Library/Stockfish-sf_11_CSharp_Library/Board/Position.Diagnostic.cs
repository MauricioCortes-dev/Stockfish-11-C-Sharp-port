using System.Text;
using Stockfish_sf_11_CSharp_Library.Types;
using Stockfish_sf_11_CSharp_Library.Tablebases.Syzygy;
using System.Globalization;

namespace Stockfish_sf_11_CSharp_Library.Board
{
    public sealed partial class Position
    {
        public override string ToString() => ToString(null);

        /// <summary>Board diagnostic and optional real Syzygy probes on a private position, as in operator&lt;&lt;.</summary>
        public string ToString(TbProbe? tablebases)
        {
            const string border = " +---+---+---+---+---+---+---+---+\n";
            var text = new StringBuilder("\n");
            text.Append(border);
            for (int rank = 7; rank >= 0; --rank)
            {
                for (int file = 0; file < 8; ++file)
                    text.Append(" | ").Append(PieceToChar[(int)PieceOn(rank * 8 + file)]);
                text.Append(" |\n").Append(border);
            }
            text.Append("\nFen: ").Append(Fen()).Append("\nKey: ").Append(((ulong)Key).ToString("X16"));
            text.Append("\nCheckers: ");
            Bitboard checkers = Checkers;
            while (checkers)
            {
                Square square = Bitboard.PopLsb(ref checkers);
                text.Append((char)('a' + ((int)square & 7))).Append((char)('1' + ((int)square >> 3))).Append(' ');
            }
            if(tablebases!=null && tablebases.MaxCardinality>=Bitboard.PopCount(Pieces()) && !CanCastle(CastlingRights.ANY_CASTLING))
            {
                var copy=new Position();copy.Set(Fen(),IsChess960,new StateInfo());
                WDLScore wdl=tablebases.ProbeWdl(copy,out ProbeState wdlState);
                int dtz=tablebases.ProbeDtz(copy,out ProbeState dtzState);
                text.Append("\nTablebases WDL: ").Append(wdl.ToString().PadLeft(4)).Append(" (").Append(wdlState.ToString()).Append(')')
                    .Append("\nTablebases DTZ: ").Append(dtz.ToString(CultureInfo.InvariantCulture).PadLeft(4)).Append(" (").Append(dtzState.ToString()).Append(')');
            }
            return text.ToString();
        }
    }
}
