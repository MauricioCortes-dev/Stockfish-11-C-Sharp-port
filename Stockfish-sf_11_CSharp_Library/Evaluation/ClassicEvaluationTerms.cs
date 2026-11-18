using System;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Evaluation
{
    /// <summary>Portable helpers for Stockfish 11's classic evaluation.</summary>
    public static class ClassicEvaluationTerms
    {
        public const int Tempo = 28;
        /// <summary>Evaluate paths that finish before piece attack maps are needed.</summary>
        public static bool TryEvaluateEarly(Position position, EvaluationContext context, Score contempt, out Value value)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            if(context==null)throw new ArgumentNullException(nameof(context));
            if(position.Checkers)throw new InvalidOperationException("Evaluation requires a position not in check.");
            MaterialEntry material=context.ProbeMaterial(position);
            if(material.SpecializedEvalExists){value=material.Evaluate(position);return true;}
            ref PawnEntry pawns=ref context.ProbePawns(position);
            Score score=position.PsqScore+material.Imbalance+contempt+pawns.PawnScore(Color.WHITE)-pawns.PawnScore(Color.BLACK);
            int average=((int)Score.MgValue(score)+(int)Score.EgValue(score))/2;
            if(Math.Abs(average)>1400+(int)position.TotalNonPawnMaterial/64)
            { value=position.SideToMove==Color.WHITE?average:-average;return true; }
            value=Value.VALUE_NONE;return false;
        }
        /// <summary>Space bonus after the caller has populated the opponent's attack maps.</summary>
        public static Score Space(Position position, Color us, Bitboard enemyPawnAttacks, Bitboard enemyAttacks)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            if(position.TotalNonPawnMaterial<12222)return Score.SCORE_ZERO;
            const ulong centerFiles=0x3c3c3c3c3c3c3c3cUL;
            ulong ranks=us==Color.WHITE ? 0x00000000ffffff00UL : 0x00ffffff00000000UL;
            Bitboard safe=(centerFiles&ranks)&~position.Pieces(us,PieceType.PAWN)&~enemyPawnAttacks;
            Direction down=-Direction.PawnPush(us);
            Bitboard behind=position.Pieces(us,PieceType.PAWN);
            behind|=Bitboard.Shift(down,behind);
            behind|=Bitboard.Shift((int)down*2,behind);
            int bonus=Bitboard.PopCount(safe)+Bitboard.PopCount(behind&safe&~enemyAttacks);
            int weight=position.Count(us,PieceType.ALL_PIECES)-1;
            return Score.MakeScore(bonus*weight*weight/16,0);
        }
        public static Score Initiative(Position position, PawnEntry pawns, Score score)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            int mg=Score.MgValue(score),eg=Score.EgValue(score);
            Square whiteKing=position.KingSquare(Color.WHITE),blackKing=position.KingSquare(Color.BLACK);
            int outflanking=Bitboards.FileDistance(whiteKing,blackKing)-Bitboards.RankDistance(whiteKing,blackKing);
            bool infiltration=Rank.RankOf(whiteKing)>Rank.RANK_4 || Rank.RankOf(blackKing)<Rank.RANK_5;
            const ulong queenSide=0x0f0f0f0f0f0f0f0fUL, kingSide=0xf0f0f0f0f0f0f0f0UL;
            ulong pawnSquares=position.Pieces(PieceType.PAWN);
            bool bothFlanks=(pawnSquares&queenSide)!=0 && (pawnSquares&kingSide)!=0;
            bool almostUnwinnable=pawns.PassedCount==0 && outflanking<0 && !bothFlanks;
            int complexity=9*pawns.PassedCount+11*position.CountAll(PieceType.PAWN)+9*outflanking
                +12*(infiltration?1:0)+21*(bothFlanks?1:0)+51*(position.TotalNonPawnMaterial==Value.VALUE_ZERO?1:0)
                -43*(almostUnwinnable?1:0)-100;
            int u=Math.Sign(mg)*Math.Max(Math.Min(complexity+50,0),-Math.Abs(mg));
            int v=Math.Sign(eg)*Math.Max(complexity,-Math.Abs(eg));
            return Score.MakeScore(u,v);
        }
        public static ScaleFactor ScaleFactor(Position position, MaterialEntry material, Value endgameScore)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            if(material==null)throw new ArgumentNullException(nameof(material));
            Color strong=endgameScore>Value.VALUE_DRAW?Color.WHITE:Color.BLACK;
            int factor=material.ScaleFactor(position,strong);
            if(factor==Types.ScaleFactor.SCALE_FACTOR_NORMAL)
            {
                if(position.OppositeBishops() && position.TotalNonPawnMaterial==2*Value.BishopValueMg)factor=22;
                else factor=Math.Min(factor,36+(position.OppositeBishops()?2:7)*position.Count(strong,PieceType.PAWN));
                factor=Math.Max(0,factor-(position.Rule50Count-12)/4);
            }
            return factor;
        }
        public static Value Interpolate(Position position, MaterialEntry material, Score score)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            if(material==null)throw new ArgumentNullException(nameof(material));
            int phase=material.GamePhase;int factor=ScaleFactor(position,material,Score.EgValue(score));
            int value=((int)Score.MgValue(score)*phase+(int)Score.EgValue(score)*((int)Phase.PHASE_MIDGAME-phase)*factor/(int)Types.ScaleFactor.SCALE_FACTOR_NORMAL)/(int)Phase.PHASE_MIDGAME;
            return (position.SideToMove==Color.WHITE?value:-value)+Tempo;
        }
    }
}
