using System;
using System.Globalization;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.MoveGeneration;
using Stockfish_sf_11_CSharp_Library.Types;
using ChessFile = Stockfish_sf_11_CSharp_Library.Types.File;

namespace Stockfish_sf_11_CSharp_Library.Protocol
{
    /// <summary>Stockfish 11's UCI notation for scores, squares and moves.</summary>
    public static class UciNotation
    {
        public static string Value(Value score)
        {
            int value=score;
            if(value<=-(int)Types.Value.VALUE_INFINITE || value>=(int)Types.Value.VALUE_INFINITE)throw new ArgumentOutOfRangeException(nameof(score));
            if(Math.Abs(value)<(int)Types.Value.VALUE_MATE-ChessConstants.MAX_PLY)
                return "cp " + (value*100/(int)Types.Value.PawnValueEg).ToString(CultureInfo.InvariantCulture);
            return "mate " + ((value>0?(int)Types.Value.VALUE_MATE-value+1:-(int)Types.Value.VALUE_MATE-value)/2).ToString(CultureInfo.InvariantCulture);
        }
        public static string Square(Square square)
        {
            if(!Types.Square.IsOk(square))throw new ArgumentOutOfRangeException(nameof(square));
            return new string(new[]{(char)('a'+(int)ChessFile.FileOf(square)),(char)('1'+(int)Rank.RankOf(square))});
        }
        public static string Move(Move move,bool chess960)
        {
            if((int)move==(int)Types.Move.MOVE_NONE)return "(none)";
            if((int)move==(int)Types.Move.MOVE_NULL)return "0000";
            Square from=Types.Move.FromSquare(move),to=Types.Move.ToSquare(move);
            if(Types.Move.TypeOf(move)==MoveType.CASTLING && !chess960)
                to=Types.Square.MakeSquare(to>from?ChessFile.FILE_G:ChessFile.FILE_C,Rank.RankOf(from));
            string text=Square(from)+Square(to);
            if(Types.Move.TypeOf(move)==MoveType.PROMOTION)text+=" pnbrqk"[(int)Types.Move.PromotionType(move)];
            return text;
        }
        public static Move ToMove(Position position,string text)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            if(text==null)throw new ArgumentNullException(nameof(text));
            if(text.Length==5)text=text.Substring(0,4)+char.ToLowerInvariant(text[4]);
            Span<Types.Move> moves=stackalloc Types.Move[ChessConstants.MAX_MOVES];
            int count=MoveGenerator.GenerateInto(position,GenType.LEGAL,moves);
            for(int i=0;i<count;++i)
                if(text==Move(moves[i],position.IsChess960))return moves[i];
            return Types.Move.MOVE_NONE;
        }
    }
}
