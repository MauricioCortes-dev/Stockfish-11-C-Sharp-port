using System;
using System.Text;
using Stockfish_sf_11_CSharp_Library.Types;
using ChessFile=Stockfish_sf_11_CSharp_Library.Types.File;
namespace Stockfish_sf_11_CSharp_Library.Board
{
    public sealed partial class Position
    {
        private const string PieceToChar=" PNBRQK  pnbrqk";
        public StateInfo State => state ?? throw new InvalidOperationException("Position is not initialized.");
        /// <summary>Set an endgame material-code position using the layout from Position::set(code, color).</summary>
        public void SetEndgame(string code, Color strongSide, StateInfo newState)
        {
            if (string.IsNullOrEmpty(code) || code.Length >= 8 || code[0] != 'K') throw new ArgumentException("Invalid endgame code.", nameof(code));
            if (strongSide != Color.WHITE && strongSide != Color.BLACK) throw new ArgumentOutOfRangeException(nameof(strongSide));
            int secondKing = code.IndexOf('K', 1);
            if (secondKing < 0) throw new ArgumentException("Two kings are required.", nameof(code));
            string[] sides = { code.Substring(secondKing), code.Substring(0, secondKing) };
            sides[strongSide] = sides[strongSide].ToLowerInvariant();
            string fen = "8/" + sides[0] + (8-sides[0].Length) + "/8/8/8/8/" + sides[1] + (8-sides[1].Length) + "/8 w - - 0 10";
            Set(fen, false, newState);
        }

        /// <summary>Reverse ranks and colors as the original debugging helper does.</summary>
        public void Flip()
        {
            string[] fields = Fen().Split(' ');
            string[] ranks = fields[0].Split('/');
            Array.Reverse(ranks);
            string placement = string.Join("/", ranks);
            var chars = (placement + " " + (fields[1] == "w" ? "B" : "W") + " " + fields[2]).ToCharArray();
            for (int i = 0; i < chars.Length; ++i)
                chars[i] = char.IsLower(chars[i]) ? char.ToUpperInvariant(chars[i]) : char.ToLowerInvariant(chars[i]);
            string ep = fields[3] == "-" ? "-" : fields[3].Substring(0, 1) + (fields[3][1] == '3' ? "6" : "3");
            Set(new string(chars) + " " + ep + " " + fields[4] + " " + fields[5], chess960, State, positionThread);
        }
        public Color SideToMove => sideToMove;
        public int GamePly => gamePly;
        public bool IsChess960 => chess960;
        public Square EpSquare => State.epSquare;
        public bool CanCastle(CastlingRights rights) => (State.castlingRights & (int)rights)!=0;
        public int CastlingRightsFor(Color color) => State.castlingRights & (color==Color.WHITE?(int)CastlingRights.WHITE_CASTLING:(int)CastlingRights.BLACK_CASTLING);
        public Square CastlingRookSquare(CastlingRights rights) => castlingRookSquare[rights];
        public bool CastlingImpeded(CastlingRights rights) => (bool)(Pieces()&castlingPath[rights]);
        public Bitboard Pieces(PieceType a,PieceType b) => byTypeBB[a]|byTypeBB[b];
        public Bitboard Pieces(Color color,PieceType a,PieceType b) => byColorBB[color] & (byTypeBB[a]|byTypeBB[b]);
        public Square KingSquare(Color color) {Piece king=Piece.MakePiece(color,PieceType.KING);if(pieceCount[king]!=1)throw new InvalidOperationException("Position must have one king per color.");return pieceList[king,0];}
        public Bitboard AttackersTo(Square square,Bitboard occupied) {
            return (Bitboards.PawnAttacks(Color.BLACK,square)&Pieces(Color.WHITE,PieceType.PAWN)) |
                   (Bitboards.PawnAttacks(Color.WHITE,square)&Pieces(Color.BLACK,PieceType.PAWN)) |
                   (Bitboards.PseudoAttacks(PieceType.KNIGHT,square)&Pieces(PieceType.KNIGHT)) |
                   (Bitboards.Attacks(PieceType.ROOK,square,occupied)&Pieces(PieceType.ROOK,PieceType.QUEEN)) |
                   (Bitboards.Attacks(PieceType.BISHOP,square,occupied)&Pieces(PieceType.BISHOP,PieceType.QUEEN)) |
                   (Bitboards.PseudoAttacks(PieceType.KING,square)&Pieces(PieceType.KING));
        }
        public Bitboard AttackersTo(Square square) => AttackersTo(square,Pieces());
        public Bitboard SliderBlockers(Bitboard sliders,Square square,out Bitboard pinners) {
            Bitboard blockers=0UL; pinners=0UL;
            Bitboard snipers=((Bitboards.PseudoAttacks(PieceType.ROOK,square)&Pieces(PieceType.QUEEN,PieceType.ROOK)) |
                (Bitboards.PseudoAttacks(PieceType.BISHOP,square)&Pieces(PieceType.QUEEN,PieceType.BISHOP))) & sliders;
            Bitboard occupancy=Pieces()^snipers;
            while(snipers) {
                Square sniper=Bitboard.PopLsb(ref snipers);Bitboard between=Bitboards.Between(square,sniper)&occupancy;
                if(between && !Bitboard.MoreThanOne(between)) {blockers|=between;if(between & Pieces(Piece.ColorOf(PieceOn(square))))pinners|=sniper;}
            }
            return blockers;
        }
        private void SetCheckInfo(StateInfo s) {
            s.blockersForKing[0]=SliderBlockers(Pieces(Color.BLACK),KingSquare(Color.WHITE),out Bitboard blackPinners);s.pinners[1]=blackPinners;
            s.blockersForKing[1]=SliderBlockers(Pieces(Color.WHITE),KingSquare(Color.BLACK),out Bitboard whitePinners);s.pinners[0]=whitePinners;
            Color opponent=~sideToMove;Square king=KingSquare(opponent);
            s.checkSquares[PieceType.PAWN]=Bitboards.PawnAttacks(opponent,king);
            s.checkSquares[PieceType.KNIGHT]=Bitboards.PseudoAttacks(PieceType.KNIGHT,king);
            s.checkSquares[PieceType.BISHOP]=Bitboards.Attacks(PieceType.BISHOP,king,Pieces());
            s.checkSquares[PieceType.ROOK]=Bitboards.Attacks(PieceType.ROOK,king,Pieces());
            s.checkSquares[PieceType.QUEEN]=s.checkSquares[PieceType.BISHOP]|s.checkSquares[PieceType.ROOK];
            s.checkSquares[PieceType.KING]=0UL;
        }
        private void RebuildState(StateInfo s) {
            s.key=0UL;s.materialKey=0UL;s.pawnKey=Zobrist.NoPawns;s.nonPawnMaterial[0]=s.nonPawnMaterial[1]=Value.VALUE_ZERO;
            s.checkersBB=AttackersTo(KingSquare(sideToMove))&Pieces(~sideToMove);SetCheckInfo(s);
            Bitboard occupied=Pieces();while(occupied){Square square=Bitboard.PopLsb(ref occupied);Piece pc=PieceOn(square);s.key=(ulong)s.key^(ulong)Zobrist.PieceSquare[pc,square];
                if(Piece.TypeOf(pc)==PieceType.PAWN)s.pawnKey=(ulong)s.pawnKey^(ulong)Zobrist.PieceSquare[pc,square];
                else if(Piece.TypeOf(pc)!=PieceType.KING)s.nonPawnMaterial[Piece.ColorOf(pc)]+=PSQT.PieceValue[Phase.MG,pc];}
            if(s.epSquare!=Square.SQ_NONE)s.key=(ulong)s.key^(ulong)Zobrist.EnPassant[ChessFile.FileOf(s.epSquare)];
            if(sideToMove==Color.BLACK)s.key=(ulong)s.key^(ulong)Zobrist.Side;
            s.key=(ulong)s.key^(ulong)Zobrist.Castling[s.castlingRights];
            int[] pieces={1,2,3,4,5,6,9,10,11,12,13,14};foreach(int pc in pieces)for(int i=0;i<pieceCount[pc];++i)s.materialKey=(ulong)s.materialKey^(ulong)Zobrist.PieceSquare[pc,i];
        }
        private void SetCastlingRight(Color color,Square rookFrom) {
            Square kingFrom=KingSquare(color);CastlingRights rights=color & (kingFrom<rookFrom?CastlingRights.KING_SIDE:CastlingRights.QUEEN_SIDE);
            State.castlingRights|=rights;castlingRightsMask[kingFrom]|=rights;castlingRightsMask[rookFrom]|=rights;castlingRookSquare[rights]=rookFrom;
            bool kingSide=((int)rights & (int)CastlingRights.KING_SIDE)!=0;
            Square kingTo=Square.RelativeSquare(color,kingSide?Square.SQ_G1:Square.SQ_C1),rookTo=Square.RelativeSquare(color,kingSide?Square.SQ_F1:Square.SQ_D1);
            castlingPath[rights]=(Bitboards.Between(rookFrom,rookTo)|Bitboards.Between(kingFrom,kingTo)|Bitboard.SquareBB(rookTo)|Bitboard.SquareBB(kingTo)) & ~(Bitboard.SquareBB(kingFrom)|Bitboard.SquareBB(rookFrom));
        }
        public void Set(string fen,bool isChess960,StateInfo newState) => Set(fen,isChess960,newState,null);
        public void Set(string fen,bool isChess960,StateInfo newState,IPositionThread? worker) {
            if(fen==null)throw new ArgumentNullException(nameof(fen));if(newState==null)throw new ArgumentNullException(nameof(newState));
            string[] fields=fen.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);if(fields.Length!=6)throw new ArgumentException("FEN must contain six fields.",nameof(fen));
            Array.Clear(board,0,board.Length);Array.Clear(byTypeBB,0,byTypeBB.Length);Array.Clear(byColorBB,0,byColorBB.Length);Array.Clear(pieceCount,0,pieceCount.Length);Array.Clear(index,0,index.Length);Array.Clear(castlingRightsMask,0,castlingRightsMask.Length);Array.Clear(castlingRookSquare,0,castlingRookSquare.Length);Array.Clear(castlingPath,0,castlingPath.Length);
            for(int pc=0;pc<16;++pc)for(int i=0;i<16;++i)pieceList[pc,i]=Square.SQ_NONE;psq=Score.SCORE_ZERO;newState.Clear();state=newState;
            string[] rows=fields[0].Split('/');if(rows.Length!=8)throw new ArgumentException("Invalid piece placement.",nameof(fen));
            for(int row=0;row<8;++row){int file=0;foreach(char token in rows[row]) {if(token>='1'&&token<='8')file+=token-'0';else {int pc=PieceToChar.IndexOf(token);if(pc<=0||pc==7||pc==8||file>=8||pieceCount[pc]>=16)throw new ArgumentException("Invalid piece placement.",nameof(fen));PutPiece(pc,(7-row)*8+file);++file;}}if(file!=8)throw new ArgumentException("Invalid rank width.",nameof(fen));}
            if(fields[1]!="w"&&fields[1]!="b")throw new ArgumentException("Invalid side.",nameof(fen));sideToMove=fields[1]=="w"?Color.WHITE:Color.BLACK;
            _=KingSquare(Color.WHITE);_=KingSquare(Color.BLACK);
            foreach(char token in fields[2]) {Color color=char.IsLower(token)?Color.BLACK:Color.WHITE;char upper=char.ToUpperInvariant(token);int rank=color==Color.WHITE?0:7;Piece rook=Piece.MakePiece(color,PieceType.ROOK);int rookFile;
                if(upper=='K'){rookFile=7;while(rookFile>=0 && PieceOn(rank*8+rookFile)!=rook)--rookFile;}
                else if(upper=='Q'){rookFile=0;while(rookFile<8 && PieceOn(rank*8+rookFile)!=rook)++rookFile;}
                else if(upper>='A'&&upper<='H')rookFile=upper-'A';else continue;
                if(rookFile<0||rookFile>=8||PieceOn(rank*8+rookFile)!=rook)throw new ArgumentException("Castling rook missing.",nameof(fen));SetCastlingRight(color,rank*8+rookFile);
            }
            State.epSquare=Square.SQ_NONE;string ep=fields[3];if(ep.Length==2 && ep[0]>='a'&&ep[0]<='h'&&(ep[1]=='3'||ep[1]=='6')){
                Square candidate=Square.MakeSquare(ep[0]-'a',ep[1]-'1');Square pawn=candidate+Direction.PawnPush(~sideToMove);
                if((AttackersTo(candidate)&Pieces(sideToMove,PieceType.PAWN)) && (Square.IsOk(pawn) && (Pieces(~sideToMove,PieceType.PAWN)&pawn)))State.epSquare=candidate;
            }
            if(!int.TryParse(fields[4],out int rule50)||!int.TryParse(fields[5],out int fullmove))throw new ArgumentException("Invalid move counters.",nameof(fen));
            State.rule50=rule50;gamePly=checked(Math.Max(checked(2*(fullmove-1)),0)+(sideToMove==Color.BLACK?1:0));chess960=isChess960;positionThread=worker;RebuildState(State);
        }
        public string Fen() {
            var output=new StringBuilder();for(int rank=7;rank>=0;--rank){int empty=0;for(int file=0;file<8;++file){Piece piece=PieceOn(rank*8+file);if(piece==Piece.NO_PIECE){++empty;continue;}if(empty>0){output.Append(empty);empty=0;}output.Append(PieceToChar[(int)piece]);}if(empty>0)output.Append(empty);if(rank>0)output.Append('/');}
            output.Append(sideToMove==Color.WHITE?" w ":" b ");CastlingRights[] rights={CastlingRights.WHITE_OO,CastlingRights.WHITE_OOO,CastlingRights.BLACK_OO,CastlingRights.BLACK_OOO};char[] normal={'K','Q','k','q'};bool any=false;
            for(int i=0;i<4;++i)if(CanCastle(rights[i])){any=true;output.Append(chess960?(char)((i<2?'A':'a')+(int)ChessFile.FileOf(CastlingRookSquare(rights[i]))):normal[i]);}if(!any)output.Append('-');
            output.Append(' ');if(EpSquare==Square.SQ_NONE)output.Append('-');else{output.Append((char)('a'+(int)ChessFile.FileOf(EpSquare)));output.Append((char)('1'+(int)Rank.RankOf(EpSquare)));}
            output.Append(' ').Append(State.rule50).Append(' ').Append(1+(gamePly-(sideToMove==Color.BLACK?1:0))/2);return output.ToString();
        }
    }
}
