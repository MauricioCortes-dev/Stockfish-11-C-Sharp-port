using System;
using System.Collections.Generic;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Temporaries belonging to an active call, not to a logical ply.</summary>
    internal sealed class SearchBuffers
    {
        internal readonly StateInfo State=new StateInfo();
        internal readonly MovePicker Picker=new MovePicker();
        internal readonly PieceToHistory[] Histories=new PieceToHistory[6];
        internal readonly Move[] Quiets=new Move[64],Captures=new Move[32];
        internal readonly List<Move> ChildPv=new List<Move>();
        internal ulong Version;
        internal int Ply;
        internal StateInfo NextState()
        {
            // do_move overwrites every other suffix field. These unused slots
            // must retain the zero values of a newly constructed managed state.
            State.checkSquares[0]=State.checkSquares[7]=0UL;
            return State;
        }
        internal void Release()
        {
            // The caller must undo its move before returning this slot.
            State.previous=null;
            Picker.ClearReferences();
            Array.Clear(Histories,0,Histories.Length);
            ChildPv.Clear();
        }
    }

    /// <summary>Private, lazy LIFO bank. Never share one bank across workers.</summary>
    internal sealed class SearchBufferBank
    {
        private readonly List<SearchBuffers> slots=new List<SearchBuffers>();
        internal int ActiveCount { get; private set; }
        internal int Capacity => slots.Count;
        internal long SamePlyReentries { get; private set; }
        internal Lease Rent(int ply)
        {
            if(ActiveCount==slots.Count)slots.Add(new SearchBuffers());
            SearchBuffers slot=slots[ActiveCount];
            if(ActiveCount!=0 && slots[ActiveCount-1].Ply==ply)++SamePlyReentries;
            slot.Ply=ply;
            unchecked { ++slot.Version; }
            return new Lease(this,ActiveCount++,slot.Version,slot);
        }
        private void Release(int index,ulong version)
        {
            if(index!=ActiveCount-1 || slots[index].Version!=version)
                throw new InvalidOperationException("Search buffers must be returned once, in call order.");
            slots[index].Release();
            --ActiveCount;
        }
        internal readonly struct Lease : IDisposable
        {
            private readonly SearchBufferBank bank;
            private readonly int index;
            private readonly ulong version;
            internal readonly SearchBuffers Buffers;
            internal Lease(SearchBufferBank bank,int index,ulong version,SearchBuffers buffers)
            {this.bank=bank;this.index=index;this.version=version;Buffers=buffers;}
            public void Dispose() => bank.Release(index,version);
        }
    }
}
