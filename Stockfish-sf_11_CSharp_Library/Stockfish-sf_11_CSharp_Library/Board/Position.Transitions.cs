using System;
using Stockfish_sf_11_CSharp_Library.Types;
using ChessFile=Stockfish_sf_11_CSharp_Library.Types.File;
namespace Stockfish_sf_11_CSharp_Library.Board
{
    public sealed partial class Position
    {
        /// <summary>Test a pseudo-legal move after the caller has applied generation preconditions.</summary>
        public bool Legal(Move move)
        {
            if(!Move.IsOk(move))throw new ArgumentException("A real move is required.",nameof(move));
            Color us=sideToMove;Square from=Move.FromSquare(move),to=Move.ToSquare(move);MoveType type=Move.TypeOf(move);
            if(type==MoveType.ENPASSANT){Square king=KingSquare(us),capture=to-Direction.PawnPush(us);Bitboard occupied=(Pieces()^from^capture)|to;
                return !(Bitboards.Attacks(PieceType.ROOK,king,occupied)&Pieces(~us,PieceType.QUEEN,PieceType.ROOK)) &&
                       !(Bitboards.Attacks(PieceType.BISHOP,king,occupied)&Pieces(~us,PieceType.QUEEN,PieceType.BISHOP));}
            if(type==MoveType.CASTLING){to=Square.RelativeSquare(us,to>from?Square.SQ_G1:Square.SQ_C1);Direction step=to>from?Direction.WEST:Direction.EAST;
                for(Square square=to;square!=from;square+=step)if(AttackersTo(square)&Pieces(~us))return false;
                return !chess960 || !(Bitboards.Attacks(PieceType.ROOK,to,Pieces()^Move.ToSquare(move))&Pieces(~us,PieceType.ROOK,PieceType.QUEEN));}
            if(Piece.TypeOf(PieceOn(from))==PieceType.KING)return !(AttackersTo(to)&Pieces(~us));
            return !(State.blockersForKing[us]&from)||Bitboards.Aligned(from,to,KingSquare(us));
        }
        public bool GivesCheck(Move move)
        {
            if(!Move.IsOk(move))throw new ArgumentException("A real move is required.",nameof(move));
            Square from=Move.FromSquare(move),to=Move.ToSquare(move);Color them=~sideToMove;Square king=KingSquare(them);
            if(State.checkSquares[Piece.TypeOf(PieceOn(from))] & to)return true;
            if((State.blockersForKing[them]&from) && !Bitboards.Aligned(from,to,king))return true;
            MoveType type=Move.TypeOf(move);
            if(type==MoveType.NORMAL)return false;
            if(type==MoveType.PROMOTION)return Bitboards.Attacks(Move.PromotionType(move),to,Pieces()^from)&king;
            if(type==MoveType.ENPASSANT){Square capture=Square.MakeSquare(ChessFile.FileOf(to),Rank.RankOf(from));Bitboard occupied=(Pieces()^from^capture)|to;
                return (Bitboards.Attacks(PieceType.ROOK,king,occupied)&Pieces(sideToMove,PieceType.QUEEN,PieceType.ROOK)) |
                       (Bitboards.Attacks(PieceType.BISHOP,king,occupied)&Pieces(sideToMove,PieceType.QUEEN,PieceType.BISHOP));}
            if(type==MoveType.CASTLING){Square rookFrom=to,kingTo=Square.RelativeSquare(sideToMove,rookFrom>from?Square.SQ_G1:Square.SQ_C1),rookTo=Square.RelativeSquare(sideToMove,rookFrom>from?Square.SQ_F1:Square.SQ_D1);
                return (bool)(Bitboards.PseudoAttacks(PieceType.ROOK,rookTo)&king) && (bool)(Bitboards.Attacks(PieceType.ROOK,rookTo,(Pieces()^from^rookFrom)|rookTo|kingTo)&king);}
            throw new ArgumentException("Unsupported move type.",nameof(move));
        }
        public void DoMove(Move move,StateInfo next) => DoMove(move,next,GivesCheck(move));
        /// <summary>Make an already validated move; givesCheck must match the original caller's computation.</summary>
        public void DoMove(Move move,StateInfo next,bool givesCheck)
        {
            if(!Move.IsOk(move))throw new ArgumentException("A real move is required.",nameof(move));
            if(next==null)throw new ArgumentNullException(nameof(next));
            if(ReferenceEquals(next,State))throw new ArgumentException("The new state must differ from the previous state.",nameof(next));
            positionThread?.IncrementNodes();
            StateInfo previous=State;ulong key=(ulong)previous.key^(ulong)Zobrist.Side;
            next.CopyForwardFrom(previous);state=next;
            ++gamePly;++next.rule50;++next.pliesFromNull;
            Color us=sideToMove,them=~us;Square from=Move.FromSquare(move),to=Move.ToSquare(move);Piece pc=PieceOn(from);
            MoveType type=Move.TypeOf(move);Piece captured=type==MoveType.ENPASSANT?Piece.MakePiece(them,PieceType.PAWN):PieceOn(to);
            if(type==MoveType.CASTLING){DoCastling(true,us,from,ref to,out Square rookFrom,out Square rookTo);key^=(ulong)Zobrist.PieceSquare[captured,rookFrom]^(ulong)Zobrist.PieceSquare[captured,rookTo];captured=Piece.NO_PIECE;}
            if(captured!=Piece.NO_PIECE){Square captureSquare=to;
                if(Piece.TypeOf(captured)==PieceType.PAWN){if(type==MoveType.ENPASSANT){captureSquare=to-Direction.PawnPush(us);board[captureSquare]=Piece.NO_PIECE;}next.pawnKey=(ulong)next.pawnKey^(ulong)Zobrist.PieceSquare[captured,captureSquare];}
                else next.nonPawnMaterial[them]-=PSQT.PieceValue[Phase.MG,captured];
                RemovePiece(captured,captureSquare);key^=Zobrist.PieceSquare[captured,captureSquare];next.materialKey=(ulong)next.materialKey^(ulong)Zobrist.PieceSquare[captured,pieceCount[captured]];next.rule50=0;
            }
            key^=(ulong)Zobrist.PieceSquare[pc,from]^(ulong)Zobrist.PieceSquare[pc,to];
            if(next.epSquare!=Square.SQ_NONE){key^=Zobrist.EnPassant[ChessFile.FileOf(next.epSquare)];next.epSquare=Square.SQ_NONE;}
            if(next.castlingRights!=0 && (castlingRightsMask[from]|castlingRightsMask[to])!=0){int cr=castlingRightsMask[from]|castlingRightsMask[to];key^=Zobrist.Castling[next.castlingRights&cr];next.castlingRights&=~cr;}
            if(type!=MoveType.CASTLING)MovePiece(pc,from,to);
            if(Piece.TypeOf(pc)==PieceType.PAWN){
                if((((int)to^(int)from)==16) && (Bitboards.PawnAttacks(us,to-Direction.PawnPush(us))&Pieces(them,PieceType.PAWN))){next.epSquare=to-Direction.PawnPush(us);key^=Zobrist.EnPassant[ChessFile.FileOf(next.epSquare)];}
                else if(type==MoveType.PROMOTION){Piece promotion=Piece.MakePiece(us,Move.PromotionType(move));RemovePiece(pc,to);PutPiece(promotion,to);key^=(ulong)Zobrist.PieceSquare[pc,to]^(ulong)Zobrist.PieceSquare[promotion,to];next.pawnKey=(ulong)next.pawnKey^(ulong)Zobrist.PieceSquare[pc,to];next.materialKey=(ulong)next.materialKey^(ulong)Zobrist.PieceSquare[promotion,pieceCount[promotion]-1]^(ulong)Zobrist.PieceSquare[pc,pieceCount[pc]];next.nonPawnMaterial[us]+=PSQT.PieceValue[Phase.MG,promotion];}
                next.pawnKey=(ulong)next.pawnKey^(ulong)Zobrist.PieceSquare[pc,from]^(ulong)Zobrist.PieceSquare[pc,to];next.rule50=0;
            }
            next.capturedPiece=captured;next.key=key;next.checkersBB=givesCheck?AttackersTo(KingSquare(them))&Pieces(us):(Bitboard)0UL;sideToMove=them;SetCheckInfo(next);
            next.repetition=0;int end=Math.Min(next.rule50,next.pliesFromNull);
            if(end>=4){StateInfo? prior=next.previous?.previous;for(int i=4;i<=end;i+=2){prior=prior?.previous?.previous;if(prior==null)throw new InvalidOperationException("State history is incomplete.");if((ulong)prior.key==(ulong)next.key){next.repetition=prior.repetition!=0?-i:i;break;}}}
        }
        public void UndoMove(Move move)
        {
            if(!Move.IsOk(move))throw new ArgumentException("A real move is required.",nameof(move));StateInfo current=State;StateInfo previous=current.previous ?? throw new InvalidOperationException("No previous state.");
            sideToMove=~sideToMove;Color us=sideToMove;Square from=Move.FromSquare(move),to=Move.ToSquare(move);Piece pc=PieceOn(to);MoveType type=Move.TypeOf(move);
            if(type==MoveType.PROMOTION){RemovePiece(pc,to);pc=Piece.MakePiece(us,PieceType.PAWN);PutPiece(pc,to);}
            if(type==MoveType.CASTLING)DoCastling(false,us,from,ref to,out _,out _);
            else {MovePiece(pc,to,from);if(current.capturedPiece!=Piece.NO_PIECE){Square captureSquare=type==MoveType.ENPASSANT?to-Direction.PawnPush(us):to;PutPiece(current.capturedPiece,captureSquare);}}
            state=previous;--gamePly;
        }
        private void DoCastling(bool forward,Color us,Square from,ref Square to,out Square rookFrom,out Square rookTo)
        {
            bool kingSide=to>from;rookFrom=to;rookTo=Square.RelativeSquare(us,kingSide?Square.SQ_F1:Square.SQ_D1);to=Square.RelativeSquare(us,kingSide?Square.SQ_G1:Square.SQ_C1);
            Square kingStart=forward?from:to,rookStart=forward?rookFrom:rookTo;
            RemovePiece(Piece.MakePiece(us,PieceType.KING),kingStart);RemovePiece(Piece.MakePiece(us,PieceType.ROOK),rookStart);
            board[kingStart]=board[rookStart]=Piece.NO_PIECE;PutPiece(Piece.MakePiece(us,PieceType.KING),forward?to:from);PutPiece(Piece.MakePiece(us,PieceType.ROOK),forward?rookTo:rookFrom);
        }
        public void DoNullMove(StateInfo next)
        {
            if(State.checkersBB)throw new InvalidOperationException("A null move cannot be made in check.");if(next==null)throw new ArgumentNullException(nameof(next));
            StateInfo previous=State;next.CopyAllFrom(previous);state=next;
            if(next.epSquare!=Square.SQ_NONE){next.key=(ulong)next.key^(ulong)Zobrist.EnPassant[ChessFile.FileOf(next.epSquare)];next.epSquare=Square.SQ_NONE;}
            next.key=(ulong)next.key^(ulong)Zobrist.Side;++next.rule50;next.pliesFromNull=0;sideToMove=~sideToMove;SetCheckInfo(next);next.repetition=0;
        }
        public void UndoNullMove(){StateInfo previous=State.previous??throw new InvalidOperationException("No previous state.");state=previous;sideToMove=~sideToMove;}
        public Key KeyAfter(Move move){Square from=Move.FromSquare(move),to=Move.ToSquare(move);Piece pc=PieceOn(from),captured=PieceOn(to);ulong key=(ulong)State.key^(ulong)Zobrist.Side;if(captured!=Piece.NO_PIECE)key^=Zobrist.PieceSquare[captured,to];return key^(ulong)Zobrist.PieceSquare[pc,to]^(ulong)Zobrist.PieceSquare[pc,from];}
    }
}
