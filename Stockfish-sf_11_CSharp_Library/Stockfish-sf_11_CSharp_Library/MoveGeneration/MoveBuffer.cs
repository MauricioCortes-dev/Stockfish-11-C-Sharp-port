using System;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.MoveGeneration
{
    /// <summary>One generation destination, borrowed only for this call.</summary>
    internal ref struct MoveBuffer
    {
        private readonly Span<Move> plain;
        private readonly Span<ExtMove> scored;
        private readonly bool useScored;
        internal int Count { get; private set; }
        internal MoveBuffer(Span<Move> destination)
        { plain=destination;scored=default;useScored=false;Count=0; }
        internal MoveBuffer(Span<ExtMove> destination)
        { scored=destination;plain=default;useScored=true;Count=0; }
        internal void Add(Move move)
        {
            int length=useScored?scored.Length:plain.Length;
            if(Count>=length)throw new ArgumentException("Move destination is too small.","destination");
            if(useScored)scored[Count]=new ExtMove { move=move };
            else plain[Count]=move;
            ++Count;
        }
        internal Move this[int index]
        {
            get => useScored?scored[index].move:plain[index];
            set { if(useScored)scored[index]=new ExtMove { move=value };else plain[index]=value; }
        }
        internal void RemoveLast() => --Count;
    }
}
