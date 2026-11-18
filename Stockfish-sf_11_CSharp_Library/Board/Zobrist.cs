using System;
using Stockfish_sf_11_CSharp_Library.Infrastructure;
using Stockfish_sf_11_CSharp_Library.Types;
namespace Stockfish_sf_11_CSharp_Library.Board
{
    /// <summary>Zobrist and reversible-move cuckoo tables initialized as in Position::init.</summary>
    public static class Zobrist
    {
        public static readonly Key[,] PieceSquare = new Key[16,64];
        public static readonly Key[] EnPassant = new Key[8], Castling = new Key[16];
        public static readonly Key Side, NoPawns;
        private static readonly Key[] cuckoo = new Key[8192];
        private static readonly Move[] cuckooMove = new Move[8192];
        public static int H1(Key key) => (int)((ulong)key&0x1fffUL);
        public static int H2(Key key) => (int)(((ulong)key>>16)&0x1fffUL);
        public static Key CuckooKey(int index) => cuckoo[index];
        public static Move CuckooMove(int index) => cuckooMove[index];
        static Zobrist()
        {
            int[] pieces={1,2,3,4,5,6,9,10,11,12,13,14};var rng=new PRNG(1070372);
            foreach(int pc in pieces)for(int s=0;s<64;++s)PieceSquare[pc,s]=rng.Rand<Key>();
            for(int f=0;f<8;++f)EnPassant[f]=rng.Rand<Key>();
            for(int cr=0;cr<16;++cr) {
                ulong value=0;Bitboard remaining=(ulong)cr;
                while(remaining) {int index=1<<(int)Bitboard.PopLsb(ref remaining);ulong key=Castling[index];value^=key!=0?key:(ulong)rng.Rand<Key>();}
                Castling[cr]=value;
            }
            Side=rng.Rand<Key>();NoPawns=rng.Rand<Key>();int count=0;
            foreach(int pc in pieces)for(int a=0;a<64;++a)for(int b=a+1;b<64;++b) {
                if(((ulong)Bitboards.PseudoAttacks(Piece.TypeOf(pc),a)&(1UL<<b))!=0) {
                    Move move=Move.MakeMove(a,b); Key key=(ulong)PieceSquare[pc,a]^(ulong)PieceSquare[pc,b]^(ulong)Side; int i=H1(key);
                    while(true) {Key oldKey=cuckoo[i];Move oldMove=cuckooMove[i];cuckoo[i]=key;cuckooMove[i]=move;key=oldKey;move=oldMove;if(move==Move.MOVE_NONE)break;i=i==H1(key)?H2(key):H1(key);}
                    ++count;
                }
            }
            if(count!=3668)throw new InvalidOperationException("Unexpected number of cuckoo moves.");
        }
    }
}
