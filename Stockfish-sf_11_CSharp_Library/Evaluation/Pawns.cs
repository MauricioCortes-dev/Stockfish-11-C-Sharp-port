using System;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Evaluation
{
    /// <summary>Pawn-hash entry, including per-color cached king safety. Keep the reference returned by Probe to persist cache updates.</summary>
    public struct PawnEntry
    {
        public Key Key;
        private Score whiteScore, blackScore;
        private Bitboard whitePassed, blackPassed, whiteAttacks, blackAttacks, whiteSpan, blackSpan;
        internal Square whiteKing, blackKing;
        internal Score whiteKingSafety, blackKingSafety;
        internal int whiteCastling, blackCastling;
        public Score PawnScore(Color c) => c == Color.WHITE ? whiteScore : blackScore;
        public Bitboard PassedPawns(Color c) => c == Color.WHITE ? whitePassed : blackPassed;
        public Bitboard PawnAttacks(Color c) => c == Color.WHITE ? whiteAttacks : blackAttacks;
        public Bitboard PawnAttacksSpan(Color c) => c == Color.WHITE ? whiteSpan : blackSpan;
        public int PassedCount => Bitboard.PopCount(whitePassed | blackPassed);
        private static readonly int[,] ShelterStrength={
            {-6,81,93,58,39,18,25,0},{-43,61,35,-49,-29,-11,-63,0},
            {-10,75,23,-2,32,3,-45,0},{-39,-13,-29,-52,-48,-67,-166,0}};
        private static readonly int[,] UnblockedStorm={
            {85,-289,-166,97,50,45,50,0},{46,-25,122,45,37,-10,20,0},
            {-6,51,168,34,-2,-22,-14,0},{-15,-11,101,4,11,-15,-29,0}};
        private static readonly Score BlockedStorm=Score.MakeScore(82,82);

        public Score EvaluateShelter(Position position, Color us, Square kingSquare)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            Color them=~us;
            Bitboard pawns=position.Pieces(PieceType.PAWN)&~Bitboard.ForwardRanks(them,kingSquare);
            Bitboard ours=pawns&position.Pieces(us),theirs=pawns&position.Pieces(them);
            Score bonus=Score.MakeScore(5,5);
            int center=Math.Max(1,Math.Min(6,(int)Types.File.FileOf(kingSquare)));
            for(int file=center-1;file<=center+1;++file)
            {
                Bitboard own=ours&Bitboard.FileBB((Types.File)file),enemy=theirs&Bitboard.FileBB((Types.File)file);
                int ownRank=own ? (int)Rank.RelativeRank(us,Bitboard.FrontmostSquare(them,own)) : 0;
                int enemyRank=enemy ? (int)Rank.RelativeRank(us,Bitboard.FrontmostSquare(them,enemy)) : 0;
                int distance=Math.Min(file,7-file);
                bonus+=Score.MakeScore(ShelterStrength[distance,ownRank],0);
                if(ownRank!=0 && ownRank==enemyRank-1)bonus-=BlockedStorm*(enemyRank==(int)Rank.RANK_3?1:0);
                else bonus-=Score.MakeScore(UnblockedStorm[distance,enemyRank],0);
            }
            return bonus;
        }

        public Score DoKingSafety(Position position, Color us)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            Square king=position.KingSquare(us);int rights=position.CastlingRightsFor(us);
            if(us==Color.WHITE){whiteKing=king;whiteCastling=rights;}else{blackKing=king;blackCastling=rights;}
            Score shelter=EvaluateShelter(position,us,king);
            if(position.CanCastle(us&CastlingRights.KING_SIDE))
            {
                Score candidate=EvaluateShelter(position,us,Square.RelativeSquare(us,Square.SQ_G1));
                if(Score.MgValue(shelter)<Score.MgValue(candidate))shelter=candidate;
            }
            if(position.CanCastle(us&CastlingRights.QUEEN_SIDE))
            {
                Score candidate=EvaluateShelter(position,us,Square.RelativeSquare(us,Square.SQ_C1));
                if(Score.MgValue(shelter)<Score.MgValue(candidate))shelter=candidate;
            }
            Bitboard pawns=position.Pieces(us,PieceType.PAWN);int minimum=pawns?8:0;
            if(pawns&Bitboards.PseudoAttacks(PieceType.KING,king))minimum=1;
            else while(pawns)minimum=Math.Min(minimum,Bitboards.Distance(king,Bitboard.PopLsb(ref pawns)));
            return shelter-Score.MakeScore(0,16*minimum);
        }

        public Score KingSafety(Position position, Color us)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            Square cachedKing=us==Color.WHITE?whiteKing:blackKing;
            int cachedRights=us==Color.WHITE?whiteCastling:blackCastling;
            if(cachedKing==position.KingSquare(us) && cachedRights==position.CastlingRightsFor(us))
                return us==Color.WHITE?whiteKingSafety:blackKingSafety;
            Score safety=DoKingSafety(position,us);
            if(us==Color.WHITE)whiteKingSafety=safety;else blackKingSafety=safety;
            return safety;
        }
        internal void Set(Color c, Score score, Bitboard passed, Bitboard attacks, Bitboard span)
        {
            if(c==Color.WHITE){whiteScore=score;whitePassed=passed;whiteAttacks=attacks;whiteSpan=span;whiteKing=Square.SQ_NONE;}
            else {blackScore=score;blackPassed=passed;blackAttacks=attacks;blackSpan=span;blackKing=Square.SQ_NONE;}
        }
    }
    public static class Pawns
    {
        private static readonly Score Backward=Score.MakeScore(9,24), Doubled=Score.MakeScore(11,56), Isolated=Score.MakeScore(5,15), WeakLever=Score.MakeScore(0,56), WeakUnopposed=Score.MakeScore(13,27);
        private static readonly int[] Connected={0,7,8,12,29,48,86,0};
        public static ref PawnEntry Probe(Position position, EvaluationContext context)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            if(context==null)throw new ArgumentNullException(nameof(context));
            Key key=position.PawnKey;ref PawnEntry entry=ref context.PawnTable[key];
            if((ulong)entry.Key==(ulong)key)return ref entry;
            entry.Key=key;Evaluate(position,Color.WHITE,ref entry);Evaluate(position,Color.BLACK,ref entry);
            return ref entry;
        }
        private static void Evaluate(Position pos, Color us, ref PawnEntry entry)
        {
            Color them=~us;Direction up=Direction.PawnPush(us);
            Bitboard ours=pos.Pieces(us,PieceType.PAWN),theirs=pos.Pieces(them,PieceType.PAWN);
            Bitboard doubleAttack=Bitboard.PawnDoubleAttacks(them,theirs),passedPawns=0UL;
            Bitboard attacks=Bitboard.PawnAttacks(us,ours),span=attacks;
            Score score=Score.SCORE_ZERO;Piece piece=Piece.MakePiece(us,PieceType.PAWN);
            for(int i=0;i<pos.Count(piece);++i)
            {
                Square s=pos.PieceListAt(piece,i);int rank=Rank.RelativeRank(us,s);
                Bitboard opposed=theirs&Bitboard.ForwardFile(us,s),blocked=theirs&(s+up),stoppers=theirs&Bitboard.PassedPawnSpan(us,s);
                Bitboard lever=theirs&Bitboards.PawnAttacks(us,s),leverPush=theirs&Bitboards.PawnAttacks(us,s+up);
                bool doubled=ours&(s-up);Bitboard neighbours=ours&Bitboard.AdjacentFiles(s),phalanx=neighbours&Bitboard.RankBB(s),support=neighbours&Bitboard.RankBB(s-up);
                bool backward=!(neighbours&Bitboard.ForwardRanks(them,s+up)) && (bool)(leverPush|blocked);
                if(!backward && !(bool)blocked)span|=Bitboard.PawnAttackSpan(us,s);
                bool passed=!(stoppers^lever) || (!(stoppers^leverPush) && Bitboard.PopCount(phalanx)>=Bitboard.PopCount(leverPush)) || ((ulong)stoppers==(ulong)blocked && rank>=(int)Rank.RANK_5 && (bool)(Bitboard.Shift(up,support)&~(theirs|doubleAttack)));
                if(passed)passedPawns|=s;
                if(support|phalanx)
                {
                    int v=Connected[rank]*(2+((bool)phalanx?1:0)-((bool)opposed?1:0))+21*Bitboard.PopCount(support);
                    score+=Score.MakeScore(v,v*(rank-2)/4);
                }
                else if(!neighbours)score-=Isolated+WeakUnopposed*(!(bool)opposed);
                else if(backward)score-=Backward+WeakUnopposed*(!(bool)opposed);
                if(!support)score-=Doubled*doubled+WeakLever*Bitboard.MoreThanOne(lever);
            }
            entry.Set(us,score,passedPawns,attacks,span);
        }
    }
}
