using Stockfish_sf_11_CSharp_Library.Types;
namespace Stockfish_sf_11_CSharp_Library.Board
{
    /// <summary>Stockfish 11 material values and piece-square tables.</summary>
    public static class PSQT
    {
        // Canonical middle-game/end-game parameters from psqt.cpp.
        private static readonly int[] Bonus = { -175, -96, -92, -65, -74, -49, -73, -21, -77, -67, -41, -54, -27, -18, -15, 8, -61, -40, -17, -27, 6, -8, 12, 29, -35, -35, 8, -2, 40, 13, 49, 28, -34, -45, 13, -16, 44, 9, 51, 39, -9, -51, 22, -44, 58, -16, 53, 17, -67, -69, -27, -50, 4, -51, 37, 12, -201, -100, -83, -88, -56, -56, -26, -17, -53, -57, -5, -30, -8, -37, -23, -12, -15, -37, 8, -13, 19, -17, 4, 1, -7, -16, 21, -1, -5, -2, 17, 10, -5, -20, 11, -6, 25, 0, 39, 17, -12, -17, 29, -1, 22, -14, 31, 15, -16, -30, 6, 6, 1, 4, 11, 6, -17, -31, -14, -20, 5, -1, 0, 1, -48, -46, 1, -42, -14, -37, -23, -24, -31, -9, -20, -13, -14, -10, -5, -9, -21, -12, -13, -9, -8, -1, 6, -2, -25, 6, -11, -8, -1, -2, 3, -6, -13, -6, -5, 1, -4, -9, -6, 7, -27, -5, -15, 8, -4, 7, 3, -6, -22, 6, -2, 1, 6, -7, 12, 10, -2, 4, 12, 5, 16, 20, 18, -5, -17, 18, -19, 0, -1, 19, 9, 13, 3, -69, -5, -57, -5, -47, 4, -26, -3, -55, 5, -31, 8, -22, 12, -4, -3, -39, 6, -18, 13, -9, 7, 3, 4, -23, 5, -3, 9, 13, 8, 24, 0, -29, 14, -6, 12, 9, 5, 21, -4, -38, 10, -18, 6, -12, 8, 1, -5, -50, 6, -27, 10, -24, 8, -8, -2, -75, -2, -52, 1, -43, -2, -36, 271, 1, 327, 45, 271, 85, 198, 76, 278, 53, 303, 100, 234, 133, 179, 135, 195, 88, 258, 130, 169, 169, 120, 175, 164, 103, 190, 156, 138, 172, 98, 172, 154, 96, 179, 166, 105, 199, 70, 199, 123, 92, 145, 172, 81, 184, 31, 191, 88, 47, 120, 121, 65, 116, 33, 131, 59, 11, 89, 59, 45, 73, -1, 78 };
        private static readonly int[] PawnBonus = { 3, -10, 3, -6, 10, 10, 19, 0, 16, 14, 19, 7, 7, -5, -5, -19, -9, -10, -15, -10, 11, -10, 15, 4, 32, 4, 22, 3, 5, -6, -22, -4, -8, 6, -23, -2, 6, -8, 20, -4, 40, -13, 17, -12, 4, -10, -12, -9, 13, 9, 0, 4, -13, 3, 1, -12, 11, -12, -2, -6, -13, 13, 5, 8, -5, 28, -12, 20, -7, 21, 22, 28, -8, 30, -5, 7, -15, 6, -18, 13, -7, 0, 7, -11, -3, 12, -13, 21, 5, 25, -16, 19, 10, 4, -8, 7 };
        public static readonly Value[,] PieceValue = new Value[2,16] {
            {0,128,781,825,1276,2538,0,0,0,0,0,0,0,0,0,0},
            {0,213,854,915,1380,2682,0,0,0,0,0,0,0,0,0,0}
        };
        public static readonly Score[,] PieceSquare = new Score[16,64];
        public static void Init()
        {
            for(int pc=1;pc<=6;++pc) {
                int opposite=pc^8; PieceValue[0,opposite]=PieceValue[0,pc]; PieceValue[1,opposite]=PieceValue[1,pc];
                Score material=Score.MakeScore(PieceValue[0,pc],PieceValue[1,pc]);
                for(int square=0;square<64;++square) {
                    int rank=square/8,file=square%8;Score bonus=Score.SCORE_ZERO;
                    if(pc==1 && rank>=1 && rank<=6) {int i=((rank-1)*8+file)*2;bonus=Score.MakeScore(PawnBonus[i],PawnBonus[i+1]);}
                    else if(pc>=2) {int i=(((pc-2)*8+rank)*4+System.Math.Min(file,7-file))*2;bonus=Score.MakeScore(Bonus[i],Bonus[i+1]);}
                    PieceSquare[pc,square]=material+bonus;PieceSquare[opposite,square^56]=-PieceSquare[pc,square];
                }
            }
        }
    }
}
