using Stockfish_sf_11_CSharp_Library.Types;
namespace Stockfish_sf_11_CSharp_Library.Board
{
    /// <summary>State chain entry used to restore a position after a move.</summary>
    public sealed class StateInfo
    {
        public Key pawnKey, materialKey;
        public readonly Value[] nonPawnMaterial = new Value[2];
        public int castlingRights, rule50, pliesFromNull;
        public Square epSquare;
        public Key key;
        public Bitboard checkersBB;
        public Piece capturedPiece;
        public StateInfo? previous;
        public readonly Bitboard[] blockersForKing = new Bitboard[2], pinners = new Bitboard[2], checkSquares = new Bitboard[8];
        public int repetition;
        internal void Clear() {
            pawnKey=0UL;materialKey=0UL;System.Array.Clear(nonPawnMaterial,0,nonPawnMaterial.Length);
            castlingRights=rule50=pliesFromNull=0;epSquare=0;key=0UL;checkersBB=0UL;capturedPiece=0;previous=null;
            System.Array.Clear(blockersForKing,0,blockersForKing.Length);System.Array.Clear(pinners,0,pinners.Length);System.Array.Clear(checkSquares,0,checkSquares.Length);repetition=0;
        }
        internal void CopyAllFrom(StateInfo source) {
            if(source==null) throw new System.ArgumentNullException(nameof(source));
            if(ReferenceEquals(this,source)) throw new System.ArgumentException("The new state must differ from the previous state.",nameof(source));
            CopyForwardFrom(source);key=source.key;checkersBB=source.checkersBB;capturedPiece=source.capturedPiece;
            System.Array.Copy(source.blockersForKing,blockersForKing,2);System.Array.Copy(source.pinners,pinners,2);System.Array.Copy(source.checkSquares,checkSquares,8);repetition=source.repetition;
        }
        /// <summary>Copy the prefix used by Position::do_move and link the prior state.</summary>
        public void CopyForwardFrom(StateInfo source)
        {
            if(source==null) throw new System.ArgumentNullException(nameof(source));
            if(ReferenceEquals(this,source)) throw new System.ArgumentException("The new state must differ from the previous state.",nameof(source));
            pawnKey=source.pawnKey;materialKey=source.materialKey;
            nonPawnMaterial[0]=source.nonPawnMaterial[0];nonPawnMaterial[1]=source.nonPawnMaterial[1];
            castlingRights=source.castlingRights;rule50=source.rule50;pliesFromNull=source.pliesFromNull;epSquare=source.epSquare;
            previous=source;
        }
    }
}
