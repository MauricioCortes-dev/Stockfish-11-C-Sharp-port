using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Board
{
    public sealed partial class Position
    {
        public int Count(Color color, PieceType type) => pieceCount[Piece.MakePiece(color, type)];
        public int CountAll(PieceType type) => Count(Color.WHITE, type) + Count(Color.BLACK, type);
        public Square SquareOf(Color color, PieceType type)
        {
            Piece piece = Piece.MakePiece(color, type);
            if (pieceCount[piece] != 1) throw new System.InvalidOperationException("Expected exactly one piece.");
            return pieceList[piece, 0];
        }
        public bool IsOnSemiopenFile(Color color, Square square) => !(Pieces(color, PieceType.PAWN) & Bitboard.FileBB(square));
        public Bitboard Checkers => State.checkersBB;
        public Bitboard BlockersForKing(Color color) => State.blockersForKing[color];
        public Bitboard CheckSquares(PieceType type) => State.checkSquares[type];
        public bool IsDiscoveryCheckOnKing(Color color, Move move) => State.blockersForKing[color] & Move.FromSquare(move);
        public Bitboard AttacksFrom(PieceType type, Square square) => Bitboards.Attacks(type, square, Pieces());
        public Bitboard PawnAttacksFrom(Square square, Color color) => Bitboards.PawnAttacks(color, square);
        public Piece MovedPiece(Move move) => PieceOn(Move.FromSquare(move));
        public Piece CapturedPiece => State.capturedPiece;
        public bool Capture(Move move) => (!Empty(Move.ToSquare(move)) && Move.TypeOf(move) != MoveType.CASTLING) || Move.TypeOf(move) == MoveType.ENPASSANT;
        public bool CaptureOrPromotion(Move move) => Move.TypeOf(move) != MoveType.NORMAL ? Move.TypeOf(move) != MoveType.CASTLING : !Empty(Move.ToSquare(move));
        public bool AdvancedPawnPush(Move move) => Piece.TypeOf(MovedPiece(move)) == PieceType.PAWN && Rank.RelativeRank(sideToMove, Rank.RankOf(Move.ToSquare(move))) > Rank.RANK_5;
        public bool PawnPassed(Color color, Square square) => !(Pieces(~color, PieceType.PAWN) & Bitboard.PassedPawnSpan(color, square));
        public bool OppositeBishops() => Count(Color.WHITE, PieceType.BISHOP) == 1 && Count(Color.BLACK, PieceType.BISHOP) == 1 && Bitboard.OppositeColors(SquareOf(Color.WHITE, PieceType.BISHOP), SquareOf(Color.BLACK, PieceType.BISHOP));
        public int PawnsOnSameColorSquares(Color color, Square square)
        {
            const ulong dark = 0xAA55AA55AA55AA55UL;
            return Bitboard.PopCount(Pieces(color, PieceType.PAWN) & (((ulong)Bitboard.SquareBB(square) & dark) != 0 ? dark : ~dark));
        }
        public Key Key => State.key;
        public Key PawnKey => State.pawnKey;
        public Key MaterialKey => State.materialKey;
        public int Rule50Count => State.rule50;
        public Value NonPawnMaterial(Color color) => State.nonPawnMaterial[color];
        public Value TotalNonPawnMaterial => State.nonPawnMaterial[Color.WHITE] + State.nonPawnMaterial[Color.BLACK];
        /// <summary>Checks the conditions in the original fast pos_is_ok path.</summary>
        public bool IsPositionOk()
        {
            if (sideToMove != Color.WHITE && sideToMove != Color.BLACK) return false;
            if (Count(Color.WHITE, PieceType.KING) != 1 || Count(Color.BLACK, PieceType.KING) != 1) return false;
            if (PieceOn(KingSquare(Color.WHITE)) != Piece.MakePiece(Color.WHITE, PieceType.KING)
                || PieceOn(KingSquare(Color.BLACK)) != Piece.MakePiece(Color.BLACK, PieceType.KING)) return false;
            return EpSquare == Square.SQ_NONE || Rank.RelativeRank(sideToMove, Rank.RankOf(EpSquare)) == Rank.RANK_6;
        }
    }
}
