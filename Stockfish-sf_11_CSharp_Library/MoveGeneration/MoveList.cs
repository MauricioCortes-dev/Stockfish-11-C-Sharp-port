using System;
using System.Collections;
using System.Collections.Generic;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.MoveGeneration
{
    /// <summary>A snapshot of the ordered moves generated for one position.</summary>
    public sealed class MoveList : IReadOnlyList<ExtMove>
    {
        private readonly ExtMove[] moves;
        public MoveList(Position position, GenType type)
        {
            Span<ExtMove> temporary=stackalloc ExtMove[ChessConstants.MAX_MOVES];
            int count=MoveGenerator.GenerateInto(position,type,temporary);
            moves=temporary.Slice(0,count).ToArray();
        }
        public int Count => moves.Length;
        public ExtMove this[int index] => (uint)index<(uint)moves.Length?moves[index]:throw new ArgumentOutOfRangeException(nameof(index));
        public bool Contains(Move move) { foreach (ExtMove item in moves) if (item.move.Equals(move)) return true; return false; }
        public IEnumerator<ExtMove> GetEnumerator() { for(int i=0;i<moves.Length;++i)yield return moves[i]; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static class Perft
    {
        public static ulong Count(Position position, int depth)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            if (depth < 0) throw new ArgumentOutOfRangeException(nameof(depth));
            return CountCore(position,depth,0,new List<PerftBuffers>(),null);
        }
        internal static ulong CountRootMoves(Position position,int depth,Action<Move,ulong> report)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            if(depth<1)throw new ArgumentOutOfRangeException(nameof(depth));
            if(report==null)throw new ArgumentNullException(nameof(report));
            return CountCore(position,depth,0,new List<PerftBuffers>(),report);
        }
        private sealed class PerftBuffers
        {
            internal readonly StateInfo State=new StateInfo();
            internal readonly ExtMove[] Moves=new ExtMove[ChessConstants.MAX_MOVES];
        }
        private static ulong CountCore(Position position,int depth,int ply,List<PerftBuffers> buffers,Action<Move,ulong>? report)
        {
            if(depth==0)return 1;
            if(ply==buffers.Count)buffers.Add(new PerftBuffers());
            PerftBuffers temporary=buffers[ply];
            int count=MoveGenerator.GenerateInto(position,GenType.LEGAL,temporary.Moves.AsSpan());
            if(depth==1 && report==null)return (ulong)count;
            ulong nodes = 0;
            for(int i=0;i<count;++i)
            {
                Move move=temporary.Moves[i].move;
                temporary.State.checkSquares[0]=temporary.State.checkSquares[7]=0UL;
                position.DoMove(move,temporary.State);
                ulong childNodes;
                try { childNodes=CountCore(position, depth - 1,ply+1,buffers,null); }
                finally { position.UndoMove(move);temporary.State.previous=null; }
                report?.Invoke(move,childNodes);
                nodes=report==null?checked(nodes+childNodes):unchecked(nodes+childNodes);
            }
            return nodes;
        }
    }
}
