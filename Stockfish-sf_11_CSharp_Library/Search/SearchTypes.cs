using System;
using System.Collections;
using System.Collections.Generic;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.MoveGeneration;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>State associated with a ply of one worker's search.</summary>
    public sealed class Stack
    {
        public Move[]? Pv;
        public PieceToHistory? ContinuationHistory;
        public int Ply;
        public Move CurrentMove, ExcludedMove;
        public readonly Move[] Killers = new Move[2];
        public Value StaticEval;
        public int StatScore, MoveCount;
    }

    /// <summary>A root candidate and its principal variation.</summary>
    public sealed class RootMove : IComparable<RootMove>
    {
        public Value Score = -Value.VALUE_INFINITE, PreviousScore = -Value.VALUE_INFINITE, TbScore;
        public int SelDepth, TbRank, BestMoveCount;
        public readonly List<Move> Pv;
        public RootMove(Move move) { Pv = new List<Move> { move }; }
        public RootMove(RootMove source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Score=source.Score; PreviousScore=source.PreviousScore; TbScore=source.TbScore;
            SelDepth=source.SelDepth; TbRank=source.TbRank; BestMoveCount=source.BestMoveCount;
            Pv=new List<Move>(source.Pv);
        }
        public int CompareTo(RootMove? other)
        {
            if (other == null) return -1;
            int score = ((int)other.Score).CompareTo((int)Score);
            return score != 0 ? score : ((int)other.PreviousScore).CompareTo((int)PreviousScore);
        }
        public bool Matches(Move move) => (int)Pv[0] == (int)move;
        public static bool operator ==(RootMove a, Move b) => a is null ? false : a.Matches(b);
        public static bool operator !=(RootMove a, Move b) => !(a == b);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => base.GetHashCode();
        public static bool operator <(RootMove a, RootMove b) => a.CompareTo(b)<0;
        public static bool operator >(RootMove a, RootMove b) => a.CompareTo(b)>0;
        public bool ExtractPonderFromTt(Position position, TranspositionTable table)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (Pv.Count != 1) throw new InvalidOperationException("The root PV must contain exactly one move.");
            Move first=Pv[0]; if ((int)first==0) return false;
            position.DoMove(first,new StateInfo());
            try
            {
                ref TTEntry entry=ref table.Probe(position.Key,out bool hit);
                if (hit) { Move move=entry.Move; if (position.ContainsLegalMove(move)) Pv.Add(move); }
                return Pv.Count>1;
            }
            finally { position.UndoMove(first); }
        }
    }

    /// <summary>Root moves with stable ordering and independently copied PVs.</summary>
    public sealed class RootMoves : IReadOnlyList<RootMove>
    {
        private readonly List<RootMove> moves = new List<RootMove>();
        public RootMoves() { }
        public RootMoves(RootMoves source) { if(source==null)throw new ArgumentNullException(nameof(source)); foreach(RootMove move in source) moves.Add(new RootMove(move)); }
        public int Count => moves.Count;
        public RootMove this[int index] => moves[index];
        public void Add(RootMove move) => moves.Add(move ?? throw new ArgumentNullException(nameof(move)));
        public int Find(Move move) => moves.FindIndex(candidate=>candidate.Matches(move));
        public void StableSort(int start,int count)
        {
            if(start<0 || count<0 || start>moves.Count-count)throw new ArgumentOutOfRangeException();
            for(int i=start+1;i<start+count;++i) { RootMove item=moves[i];int j=i;while(j>start && item.CompareTo(moves[j-1])<0){moves[j]=moves[j-1];--j;}moves[j]=item; }
        }
        public void Swap(int first, int second)
        {
            RootMove item=moves[first];moves[first]=moves[second];moves[second]=item;
        }
        /// <summary>Sort higher tablebase ranks first, preserving order among equal ranks.</summary>
        public void SortByTablebaseRank()
        {
            for(int i=1;i<moves.Count;++i) { RootMove item=moves[i];int j=i;while(j>0 && item.TbRank>moves[j-1].TbRank){moves[j]=moves[j-1];--j;}moves[j]=item; }
        }
        public IEnumerator<RootMove> GetEnumerator()=>moves.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }

    /// <summary>Requested limits; times are milliseconds, nodes are signed 64-bit.</summary>
    public sealed class LimitsType
    {
        public readonly List<Move> SearchMoves = new List<Move>();
        public readonly long[] Time = new long[2], Increment = new long[2];
        public long NodesPerMillisecond, MoveTime, StartTime, Nodes;
        public int MovesToGo, Depth, Mate, Perft, Infinite;
        public bool UseTimeManagement => Mate==0 && MoveTime==0 && Depth==0 && Nodes==0 && Perft==0 && Infinite==0;
    }
}
