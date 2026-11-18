using Stockfish_sf_11_CSharp_Library.Types;
namespace Stockfish_sf_11_CSharp_Library.Board
{
    /// <summary>Incremental board representation and state transitions.</summary>
    public sealed partial class Position
    {
        private readonly Piece[] board=new Piece[64];
        private readonly Bitboard[] byTypeBB=new Bitboard[8], byColorBB=new Bitboard[2];
        private readonly int[] pieceCount=new int[16], index=new int[64];
        private readonly Square[,] pieceList=new Square[16,16];
        private Score psq=Score.SCORE_ZERO;
        private readonly int[] castlingRightsMask=new int[64];
        private readonly Square[] castlingRookSquare=new Square[16];
        private readonly Bitboard[] castlingPath=new Bitboard[16];
        private int gamePly;
        private Color sideToMove;
        private StateInfo? state;
        private bool chess960;
        private IPositionThread? positionThread;
        public IPositionThread? ThisThread => positionThread;
        static Position() { PSQT.Init(); }
        public Position() { for(int pc=0;pc<16;++pc)for(int i=0;i<16;++i)pieceList[pc,i]=Square.SQ_NONE; }
        /// <summary>Creates an independent board and current state while preserving its read-only history chain.</summary>
        public Position CopyForSearch()
        {
            var copy = new Position();
            copy.Set(Fen(), chess960, new StateInfo());
            copy.State.CopyAllFrom(State);
            copy.State.previous = State.previous;
            return copy;
        }
        public Piece PieceOn(Square square) => board[square];
        public bool Empty(Square square) => PieceOn(square)==Piece.NO_PIECE;
        public Bitboard Pieces() => byTypeBB[PieceType.ALL_PIECES];
        public Bitboard Pieces(PieceType type) => byTypeBB[type];
        public Bitboard Pieces(Color color) => byColorBB[color];
        public Bitboard Pieces(Color color,PieceType type) => byColorBB[color]&byTypeBB[type];
        public int Count(Piece piece) => pieceCount[piece];
        public Square PieceListAt(Piece piece,int i) => pieceList[piece,i];
        public Score PsqScore => psq;
        internal void PutPiece(Piece pc,Square square) {
            board[square]=pc;byTypeBB[PieceType.ALL_PIECES]|=square;byTypeBB[Piece.TypeOf(pc)]|=square;byColorBB[Piece.ColorOf(pc)]|=square;
            index[square]=pieceCount[pc]++;pieceList[pc,index[square]]=square;pieceCount[Piece.MakePiece(Piece.ColorOf(pc),PieceType.ALL_PIECES)]++;
            psq+=PSQT.PieceSquare[pc,square];
        }
        internal void RemovePiece(Piece pc,Square square) {
            byTypeBB[PieceType.ALL_PIECES]^=square;byTypeBB[Piece.TypeOf(pc)]^=square;byColorBB[Piece.ColorOf(pc)]^=square;
            Square last=pieceList[pc,--pieceCount[pc]];index[last]=index[square];pieceList[pc,index[last]]=last;pieceList[pc,pieceCount[pc]]=Square.SQ_NONE;
            pieceCount[Piece.MakePiece(Piece.ColorOf(pc),PieceType.ALL_PIECES)]--;psq-=PSQT.PieceSquare[pc,square];
        }
        internal void MovePiece(Piece pc,Square from,Square to) {
            Bitboard both=from|to;byTypeBB[PieceType.ALL_PIECES]^=both;byTypeBB[Piece.TypeOf(pc)]^=both;byColorBB[Piece.ColorOf(pc)]^=both;
            board[from]=Piece.NO_PIECE;board[to]=pc;index[to]=index[from];pieceList[pc,index[to]]=to;psq+=PSQT.PieceSquare[pc,to]-PSQT.PieceSquare[pc,from];
        }
    }
}
