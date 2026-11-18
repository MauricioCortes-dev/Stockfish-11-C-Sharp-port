using System.Collections.Generic;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;
using File = Stockfish_sf_11_CSharp_Library.Types.File;

namespace Stockfish_sf_11_CSharp_Library.Tablebases.Syzygy
{
    /// <summary>Canonical Syzygy geometry and combinatorial indices from Stockfish 11.</summary>
    internal sealed class SyzygyIndex
    {
        internal readonly int[] MapPawns = new int[64], MapB1H1H7 = new int[64], MapA1D1D4 = new int[64];
        internal readonly int[,] MapKK = new int[10,64], Binomial = new int[6,64], LeadPawnIdx = new int[6,64], LeadPawnsSize = new int[6,4];
        internal static int OffA1H8(Square square) => Rank.RankOf(square) - File.FileOf(square);
        internal bool PawnsComp(Square first, Square second) => MapPawns[first] < MapPawns[second];
        internal SyzygyIndex()
        {
            int code = 0;
            for (Square square = Square.SQ_A1; square <= Square.SQ_H8; ++square)
                if (OffA1H8(square) < 0) MapB1H1H7[square] = code++;
            var diagonal = new List<Square>(); code = 0;
            for (Square square = Square.SQ_A1; square <= Square.SQ_D4; ++square)
                if (OffA1H8(square) < 0 && File.FileOf(square) <= File.FILE_D) MapA1D1D4[square] = code++;
                else if (OffA1H8(square) == 0 && File.FileOf(square) <= File.FILE_D) diagonal.Add(square);
            foreach (Square square in diagonal) MapA1D1D4[square] = code++;
            var both = new List<(int,Square)>(); code = 0;
            for (int index = 0; index < 10; ++index)
                for (Square first = Square.SQ_A1; first <= Square.SQ_D4; ++first)
                    if (MapA1D1D4[first] == index && (index != 0 || first == Square.SQ_B1))
                        for (Square second = Square.SQ_A1; second <= Square.SQ_H8; ++second)
                            if ((Bitboards.PseudoAttacks(PieceType.KING,first) | first) & second) continue;
                            else if (OffA1H8(first) == 0 && OffA1H8(second) > 0) continue;
                            else if (OffA1H8(first) == 0 && OffA1H8(second) == 0) both.Add((index,second));
                            else MapKK[index,second] = code++;
            foreach (var pair in both) MapKK[pair.Item1,pair.Item2] = code++;
            Binomial[0,0] = 1;
            for (int n = 1; n < 64; ++n)
                for (int k = 0; k < 6 && k <= n; ++k)
                    Binomial[k,n] = (k > 0 ? Binomial[k-1,n-1] : 0) + (k < n ? Binomial[k,n-1] : 0);
            int available = 47;
            for (int count = 1; count <= 5; ++count)
                for (File file = File.FILE_A; file <= File.FILE_D; ++file)
                {
                    int index = 0;
                    for (Rank rank = Rank.RANK_2; rank <= Rank.RANK_7; ++rank)
                    {
                        Square square = Square.MakeSquare(file,rank);
                        if (count == 1) { MapPawns[square] = available--; MapPawns[(int)square ^ 7] = available--; }
                        LeadPawnIdx[count,square] = index; index += Binomial[count-1,MapPawns[square]];
                    }
                    LeadPawnsSize[count,file] = index;
                }
        }
    }
}
