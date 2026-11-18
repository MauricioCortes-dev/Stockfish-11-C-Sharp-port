using System;
using System.Collections.Generic;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.MoveGeneration
{
    /// <summary>Stockfish 11 move generation, including its ordering and pseudo-legal categories.</summary>
    public static class MoveGenerator
    {
        public static List<Move> Generate(Position pos, GenType type)
        {
            Span<Move> buffer=stackalloc Move[ChessConstants.MAX_MOVES];
            int count=GenerateInto(pos,type,buffer);
            var result=new List<Move>(count);
            for(int i=0;i<count;++i)result.Add(buffer[i]);
            return result;
        }
        /// <summary>Generates in original order into caller-owned storage.</summary>
        public static int GenerateInto(Position pos,GenType type,Span<Move> destination)
        {
            var output=new MoveBuffer(destination);
            GenerateCore(pos,type,ref output);
            return output.Count;
        }
        public static int GenerateInto(Position pos,GenType type,Span<ExtMove> destination)
        {
            var output=new MoveBuffer(destination);
            GenerateCore(pos,type,ref output);
            return output.Count;
        }
        private static void GenerateCore(Position pos,GenType type,ref MoveBuffer result)
        {
            if (pos == null) throw new ArgumentNullException(nameof(pos));
            int kind = type; if (kind < 0 || kind > 5) throw new ArgumentOutOfRangeException(nameof(type));
            Color us = pos.SideToMove; Color them = ~us;
            bool checkedKing = pos.Checkers;
            if (kind == GenType.LEGAL)
            {
                GenerateCore(pos, checkedKing ? GenType.EVASIONS : GenType.NON_EVASIONS,ref result);
                Bitboard pinned = pos.BlockersForKing(us) & pos.Pieces(us); Square king = pos.KingSquare(us);
                for (int i = 0; i < result.Count;)
                    if (((bool)pinned || Move.FromSquare(result[i]) == king || Move.TypeOf(result[i]) == MoveType.ENPASSANT) && !pos.Legal(result[i]))
                    { result[i] = result[result.Count - 1]; result.RemoveLast(); }
                    else ++i;
                return;
            }
            if ((kind == GenType.EVASIONS) != checkedKing) throw new InvalidOperationException("Generation category does not match check status.");
            Bitboard target;
            if (kind == GenType.EVASIONS)
            {
                Square king = pos.KingSquare(us); Bitboard slider = 0UL;
                Bitboard checkers = pos.Checkers & ~pos.Pieces(PieceType.KNIGHT, PieceType.PAWN);
                while (checkers) { Square sq = Bitboard.PopLsb(ref checkers); slider |= Bitboards.Line(sq, king) ^ sq; }
                Bitboard destinations = pos.AttacksFrom(PieceType.KING, king) & ~pos.Pieces(us) & ~slider;
                while (destinations) result.Add(Move.MakeMove(king, Bitboard.PopLsb(ref destinations)));
                if (Bitboard.MoreThanOne(pos.Checkers)) return;
                Square checker = Bitboard.Lsb(pos.Checkers); target = Bitboards.Between(checker, king) | checker;
            }
            else
            {
                target = kind == GenType.CAPTURES ? pos.Pieces(them) : kind == GenType.NON_EVASIONS ? ~pos.Pieces(us) : ~pos.Pieces();
                if (kind == GenType.QUIET_CHECKS)
                {
                    Bitboard discoveries = pos.BlockersForKing(them) & pos.Pieces(us);
                    while (discoveries)
                    {
                        Square from = Bitboard.PopLsb(ref discoveries); PieceType pt = Piece.TypeOf(pos.PieceOn(from));
                        if (pt == PieceType.PAWN) continue;
                        Bitboard b = pos.AttacksFrom(pt, from) & ~pos.Pieces();
                        if (pt == PieceType.KING) b &= ~Bitboards.PseudoAttacks(PieceType.QUEEN, pos.KingSquare(them));
                        while (b) result.Add(Move.MakeMove(from, Bitboard.PopLsb(ref b)));
                    }
                }
            }
            GenerateAll(pos, kind, target,ref result);
        }
        private static void GenerateAll(Position pos, int kind, Bitboard target,ref MoveBuffer output)
        {
            Color us = pos.SideToMove; Color them = ~us; Square enemyKing = pos.KingSquare(them);
            Direction up = Direction.PawnPush(us), right = us == Color.WHITE ? Direction.NORTH_EAST : Direction.SOUTH_WEST, left = us == Color.WHITE ? Direction.NORTH_WEST : Direction.SOUTH_EAST;
            Bitboard rank7 = Bitboard.RankBB(us == Color.WHITE ? Rank.RANK_7 : Rank.RANK_2), rank3 = Bitboard.RankBB(us == Color.WHITE ? Rank.RANK_3 : Rank.RANK_6);
            Bitboard on7 = pos.Pieces(us, PieceType.PAWN) & rank7, others = pos.Pieces(us, PieceType.PAWN) & ~rank7;
            Bitboard enemies = kind == GenType.EVASIONS ? pos.Pieces(them) & target : kind == GenType.CAPTURES ? target : pos.Pieces(them);
            Bitboard empty = 0UL;
            if (kind != GenType.CAPTURES)
            {
                empty = kind == GenType.QUIETS || kind == GenType.QUIET_CHECKS ? target : ~pos.Pieces();
                Bitboard one = Bitboard.Shift(up, others) & empty, two = Bitboard.Shift(up, one & rank3) & empty;
                if (kind == GenType.EVASIONS) { one &= target; two &= target; }
                if (kind == GenType.QUIET_CHECKS)
                {
                    one &= Bitboards.PawnAttacks(them, enemyKing); two &= Bitboards.PawnAttacks(them, enemyKing);
                    Bitboard dc = pos.BlockersForKing(them) & others;
                    Bitboard d1 = Bitboard.Shift(up, dc) & empty & ~Bitboard.FileBB(enemyKing);
                    Bitboard d2 = Bitboard.Shift(up, d1 & rank3) & empty; one |= d1; two |= d2;
                }
                AddPawnMoves(ref output, one, up); AddPawnMoves(ref output, two, (int)up * 2);
            }
            if (on7)
            {
                if (kind == GenType.CAPTURES) empty = ~pos.Pieces();
                if (kind == GenType.EVASIONS) empty &= target;
                Promotions(ref output, Bitboard.Shift(right,on7)&enemies, right, kind, enemyKing);
                Promotions(ref output, Bitboard.Shift(left,on7)&enemies, left, kind, enemyKing);
                Promotions(ref output, Bitboard.Shift(up,on7)&empty, up, kind, enemyKing);
            }
            if (kind == GenType.CAPTURES || kind == GenType.EVASIONS || kind == GenType.NON_EVASIONS)
            {
                AddPawnMoves(ref output, Bitboard.Shift(right,others)&enemies, right);
                AddPawnMoves(ref output, Bitboard.Shift(left,others)&enemies, left);
                if (pos.EpSquare != Square.SQ_NONE && (kind != GenType.EVASIONS || (bool)(target & (pos.EpSquare-up))))
                {
                    Bitboard sources = others & Bitboards.PawnAttacks(them,pos.EpSquare);
                    while(sources) output.Add(Move.Make(MoveType.ENPASSANT,Bitboard.PopLsb(ref sources),pos.EpSquare));
                }
            }
            for (int t=(int)PieceType.KNIGHT; t<=(int)PieceType.QUEEN; ++t)
            {
                PieceType pt=t; Piece piece=Piece.MakePiece(us,pt);
                for (int i=0;i<pos.Count(piece);++i)
                {
                    Square from=pos.PieceListAt(piece,i);
                    if(kind==GenType.QUIET_CHECKS)
                    {
                        if ((pt==PieceType.BISHOP || pt==PieceType.ROOK || pt==PieceType.QUEEN) && !(Bitboards.PseudoAttacks(pt,from)&target&pos.CheckSquares(pt))) continue;
                        if(pos.BlockersForKing(them)&from)continue;
                    }
                    Bitboard b=pos.AttacksFrom(pt,from)&target;
                    if(kind==GenType.QUIET_CHECKS)b&=pos.CheckSquares(pt);
                    while(b)output.Add(Move.MakeMove(from,Bitboard.PopLsb(ref b)));
                }
            }
            if(kind==GenType.QUIET_CHECKS || kind==GenType.EVASIONS)return;
            Square k=pos.KingSquare(us);Bitboard squares=pos.AttacksFrom(PieceType.KING,k)&target;
            while(squares)output.Add(Move.MakeMove(k,Bitboard.PopLsb(ref squares)));
            if(kind==GenType.CAPTURES)return;
            CastlingRights oo=us&CastlingRights.KING_SIDE, ooo=us&CastlingRights.QUEEN_SIDE;
            if(pos.CanCastle(oo) && !pos.CastlingImpeded(oo))output.Add(Move.Make(MoveType.CASTLING,k,pos.CastlingRookSquare(oo)));
            if(pos.CanCastle(ooo) && !pos.CastlingImpeded(ooo))output.Add(Move.Make(MoveType.CASTLING,k,pos.CastlingRookSquare(ooo)));
        }
        private static void AddPawnMoves(ref MoveBuffer output,Bitboard targets,Direction direction)
        { while(targets){Square to=Bitboard.PopLsb(ref targets);output.Add(Move.MakeMove(to-direction,to));} }
        private static void Promotions(ref MoveBuffer output,Bitboard targets,Direction direction,int kind,Square king)
        {
            while(targets){Square to=Bitboard.PopLsb(ref targets);Square from=to-direction;
                if(kind==GenType.CAPTURES || kind==GenType.EVASIONS || kind==GenType.NON_EVASIONS)output.Add(Move.Make(MoveType.PROMOTION,from,to,PieceType.QUEEN));
                if(kind==GenType.QUIETS || kind==GenType.EVASIONS || kind==GenType.NON_EVASIONS){output.Add(Move.Make(MoveType.PROMOTION,from,to,PieceType.ROOK));output.Add(Move.Make(MoveType.PROMOTION,from,to,PieceType.BISHOP));output.Add(Move.Make(MoveType.PROMOTION,from,to,PieceType.KNIGHT));}
                if(kind==GenType.QUIET_CHECKS && (Bitboards.PseudoAttacks(PieceType.KNIGHT,to)&king))output.Add(Move.Make(MoveType.PROMOTION,from,to,PieceType.KNIGHT));
            }
        }
    }
}
