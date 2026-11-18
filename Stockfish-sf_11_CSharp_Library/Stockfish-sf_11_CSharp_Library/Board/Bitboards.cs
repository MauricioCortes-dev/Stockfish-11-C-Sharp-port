using System;
using System.Runtime.CompilerServices;
using System.Text;
using Stockfish_sf_11_CSharp_Library.Types;
using Stockfish_sf_11_CSharp_Library.Infrastructure;
namespace Stockfish_sf_11_CSharp_Library.Board
{
    /// <summary>Immutable geometry and readable scalar attacks for Stockfish 11.</summary>
    public static class Bitboards
    {
        // Row-major storage, like the original tables. Public queries still
        // validate each coordinate before flattening it (no row aliasing).
        private static readonly ulong[] lines = new ulong[64*64];
        private static readonly int[] distances = new int[64*64];
        private static readonly ulong[] pseudo = new ulong[8*64];
        private static readonly ulong[] pawns = new ulong[2*64];
        private static readonly Magic[] rookMagics = new Magic[64], bishopMagics = new Magic[64];
        private static readonly int[] RookSteps = {8,1,-8,-1};
        private static readonly int[] BishopSteps = {9,-7,-9,7};
        static Bitboards()
        {
            for(int a=0;a<64;++a)for(int b=0;b<64;++b) distances[a*64+b]=Math.Max(Math.Abs(a%8-b%8),Math.Abs(a/8-b/8));
            for(int s=0;s<64;++s) {
                for(int color=0;color<2;++color) pawns[color*64+s]=Bitboard.PawnAttacks(color,Bitboard.SquareBB(s));
                foreach(int step in new[]{-9,-8,-7,-1,1,7,8,9}) pseudo[6*64+s]|=Landing(s,step);
                foreach(int step in new[]{-17,-15,-10,-6,6,10,15,17}) pseudo[2*64+s]|=Landing(s,step);
                pseudo[3*64+s]=RayAttacks(s,0,BishopSteps); pseudo[4*64+s]=RayAttacks(s,0,RookSteps); pseudo[5*64+s]=pseudo[3*64+s]|pseudo[4*64+s];
            }
            InitializeMagics(rookMagics,RookSteps);
            InitializeMagics(bishopMagics,BishopSteps);
            for(int a=0;a<64;++a)for(int b=0;b<64;++b)foreach(int type in new[]{3,4})
                if((pseudo[type*64+a]&(1UL<<b))!=0) lines[a*64+b]=(pseudo[type*64+a]&pseudo[type*64+b])|(1UL<<a)|(1UL<<b);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Check(Square s) { if(!Square.IsOk(s)) throw new ArgumentOutOfRangeException(nameof(s)); }
        private static ulong Landing(int s,int step) {int t=s+step;return t>=0 && t<64 && distances[s*64+t]<=2 ? 1UL<<t : 0;}
        private static ulong RayAttacks(int square,ulong occupied,int[] steps)
        {
            ulong result=0;
            foreach(int step in steps) {int last=square;for(int next=last+step;next>=0 && next<64 && distances[last*64+next]==1;last=next,next+=step) {ulong bit=1UL<<next;result|=bit;if((occupied&bit)!=0)break;}}
            return result;
        }
        private static void InitializeMagics(Magic[] destination,int[] steps)
        {
            int[,] seeds={{8977,44560,54343,38998,5731,95205,104912,17020},{728,10316,55013,32803,12281,15100,16645,255}};
            bool is64=Environment.Is64BitProcess; int[] epoch=new int[4096]; int count=0;
            for(int square=0;square<64;++square) {
                ulong rank=(ulong)Bitboard.RankBB((Square)square), file=(ulong)Bitboard.FileBB((Square)square);
                ulong edges=((0xffUL|0xff00000000000000UL)&~rank)|((0x0101010101010101UL|0x8080808080808080UL)&~file);
                ulong mask=RayAttacks(square,0,steps)&~edges; int shift=(is64?64:32)-Bitboard.PopCount(mask);
                int size=1 << Bitboard.PopCount(mask); var occupancies=new ulong[size]; var reference=new ulong[size]; var attacks=new Bitboard[size];
                ulong b=0;int i=0;do {occupancies[i]=b;reference[i]=RayAttacks(square,b,steps);++i;b=unchecked(b-mask)&mask;}while(b!=0);
                var rng=new PRNG((ulong)seeds[is64?1:0,square/8]); ulong candidate;
                do {
                    do {candidate=rng.SparseRand64();} while(Bitboard.PopCount(unchecked(candidate*mask)>>56)<6);
                    ++count;i=0;
                    for(;i<size;++i) {uint index=Magic.ComputeIndex(occupancies[i],mask,candidate,shift,is64);
                        if(epoch[index]<count){epoch[index]=count;attacks[index]=reference[i];}
                        else if((ulong)attacks[index]!=reference[i]) break;
                    }
                }while(i<size);
                destination[square]=new Magic(mask,candidate,shift,attacks,is64);
            }
        }
        public static Magic MagicFor(PieceType type,Square square) {
            Check(square); if(type==PieceType.ROOK)return rookMagics[(int)square];
            if(type==PieceType.BISHOP)return bishopMagics[(int)square];
            throw new ArgumentException("A rook or bishop is required.",nameof(type));
        }
        /// <summary>Returns the original ASCII representation, with rank 8 at the top.</summary>
        public static string Pretty(Bitboard board)
        {
            const string border = "+---+---+---+---+---+---+---+---+\n";
            var result = new StringBuilder(border);
            for (int rank = 7; rank >= 0; --rank)
            {
                for (int file = 0; file < 8; ++file)
                    result.Append(((ulong)board & (1UL << (rank * 8 + file))) != 0 ? "| X " : "|   ");
                result.Append("|\n").Append(border);
            }
            return result.ToString();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Distance(Square a,Square b) {Check(a);Check(b);return distances[(int)a*64+(int)b];}
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FileDistance(Square a,Square b) {Check(a);Check(b);return Math.Abs((int)Types.File.FileOf(a)-(int)Types.File.FileOf(b));}
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RankDistance(Square a,Square b) {Check(a);Check(b);return Math.Abs((int)Rank.RankOf(a)-(int)Rank.RankOf(b));}
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard Line(Square a,Square b) {Check(a);Check(b);return lines[(int)a*64+(int)b];}
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard Between(Square a,Square b) {
            Check(a);Check(b);int x=a,y=b;return lines[x*64+y] & ((ulong.MaxValue<<(x+(x<y?1:0))) ^ (ulong.MaxValue<<(y+(x<y?0:1))));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Aligned(Square a,Square b,Square c) {Check(c);return ((ulong)Line(a,b)&(1UL<<(int)c))!=0;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard PawnAttacks(Color color,Square square) {Check(square);if((int)color<0||(int)color>1)throw new ArgumentOutOfRangeException(nameof(color));return pawns[(int)color*64+(int)square];}
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bitboard PseudoAttacks(PieceType type,Square square) {Check(square);if((int)type<0||(int)type>=8)throw new ArgumentOutOfRangeException(nameof(type));return pseudo[(int)type*64+(int)square];}
        public static Bitboard Attacks(PieceType type,Square square,Bitboard occupied) {
            Check(square);if(type==PieceType.PAWN)throw new ArgumentException("Use PawnAttacks for pawns.",nameof(type));
            if(type==PieceType.BISHOP)return bishopMagics[(int)square].Attack(occupied);
            if(type==PieceType.ROOK)return rookMagics[(int)square].Attack(occupied);
            if(type==PieceType.QUEEN)return bishopMagics[(int)square].Attack(occupied)|rookMagics[(int)square].Attack(occupied);
            return PseudoAttacks(type,square);
        }
    }
}
