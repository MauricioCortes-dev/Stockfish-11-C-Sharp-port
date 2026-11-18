using System;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;
namespace Stockfish_sf_11_CSharp_Library.Evaluation
{
    /// <summary>Stockfish 11 king-pawn-king bitbase, normalized to a white pawn on files A–D.</summary>
    public static class Bitbases
    {
        private const int Count = 2 * 24 * 64 * 64;
        private const byte Invalid = 0, Unknown = 1, Draw = 2, Win = 4;
        private static readonly Lazy<uint[]> Table = new Lazy<uint[]>(Build, true);
        private static int Index(int side,int blackKing,int whiteKing,int pawn) => whiteKing | (blackKing << 6) | (side << 12) | ((pawn & 7) << 13) | ((6 - (pawn >> 3)) << 15);
        public static void Initialize() { _ = Table.Value; }
        public static bool Probe(Square whiteKing, Square whitePawn, Square blackKing, Color side)
        {
            int pawn = whitePawn;
            if (!Square.IsOk(whiteKing) || !Square.IsOk(blackKing) || !Square.IsOk(whitePawn) || (pawn & 7) > 3 || pawn / 8 < 1 || pawn / 8 > 6 || (int)side < 0 || (int)side > 1)
                throw new ArgumentOutOfRangeException(nameof(whitePawn), "The pawn and kings must be in the normalized KPK domain.");
            int index = Index(side,blackKing,whiteKing,pawn);
            return (Table.Value[index >> 5] & (1U << (index & 31))) != 0;
        }
        private static uint[] Build()
        {
            var db = new byte[Count];
            for (int i=0;i<Count;++i)
            {
                int white=i&63, black=(i>>6)&63, side=(i>>12)&1, pawn=((6-((i>>15)&7))<<3)|((i>>13)&3);
                if (Bitboards.Distance(white,black)<=1 || white==pawn || black==pawn || (side==0 && (Bitboards.PawnAttacks(Color.WHITE,pawn)&(Square)black))) db[i]=Invalid;
                else if (side==0 && pawn/8==6 && white!=pawn+8 && (Bitboards.Distance(black,pawn+8)>1 || (bool)(Bitboards.PseudoAttacks(PieceType.KING,white)&(Square)(pawn+8)))) db[i]=Win;
                else if (side==1 && (!(Bitboards.PseudoAttacks(PieceType.KING,black)&~(Bitboards.PseudoAttacks(PieceType.KING,white)|Bitboards.PawnAttacks(Color.WHITE,pawn))) || (bool)(Bitboards.PseudoAttacks(PieceType.KING,black)&(Square)pawn&~Bitboards.PseudoAttacks(PieceType.KING,white)))) db[i]=Draw;
                else db[i]=Unknown;
            }
            bool changed;
            do {
                changed=false;
                for(int i=0;i<Count;++i)
                {
                    if(db[i]!=Unknown)continue;
                    int white=i&63,black=(i>>6)&63,side=(i>>12)&1,pawn=((6-((i>>15)&7))<<3)|((i>>13)&3);
                    int other=side^1;byte result=Invalid;
                    Bitboard moves=Bitboards.PseudoAttacks(PieceType.KING,side==0?white:black);
                    while(moves){int target=Bitboard.PopLsb(ref moves);result|=db[side==0?Index(other,black,target,pawn):Index(other,target,white,pawn)];}
                    if(side==0){if(pawn/8<6)result|=db[Index(other,black,white,pawn+8)];if(pawn/8==1 && pawn+8!=white && pawn+8!=black)result|=db[Index(other,black,white,pawn+16)];}
                    byte good=side==0?Win:Draw,bad=side==0?Draw:Win;
                    byte value=(result&good)!=0?good:(result&Unknown)!=0?Unknown:bad;
                    if(value!=Unknown){db[i]=value;changed=true;}
                }
            }while(changed);
            var table=new uint[Count/32];for(int i=0;i<Count;++i)if(db[i]==Win)table[i>>5]|=1U<<(i&31);
            return table;
        }
    }
}
