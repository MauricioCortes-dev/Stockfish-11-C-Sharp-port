using System;
using System.Threading;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Marks shallow nodes so workers can reduce duplicate work as in Stockfish 11.</summary>
    internal sealed class SearchBreadcrumbs
    {
        internal sealed class Entry { internal object? Owner; internal long Key; }
        private readonly Entry[] entries = new Entry[1024];
        internal SearchBreadcrumbs() { for (int i=0;i<entries.Length;++i) entries[i]=new Entry(); }
        internal Lease Enter(object worker, Key key, int ply)
        {
            if (ply >= 8) return default;
            Entry entry=entries[(int)((ulong)key & 1023)];
            object? owner=Interlocked.CompareExchange(ref entry.Owner,worker,null);
            if(owner==null) { Interlocked.Exchange(ref entry.Key,unchecked((long)(ulong)key)); return new Lease(entry,worker,false); }
            bool marked=!ReferenceEquals(owner,worker) && unchecked((ulong)Interlocked.Read(ref entry.Key))==(ulong)key;
            return new Lease(null,null,marked);
        }
        internal readonly struct Lease : IDisposable
        {
            private readonly Entry? entry;
            private readonly object? owner;
            internal readonly bool Marked;
            internal Lease(Entry? entry,object? owner,bool marked) { this.entry=entry;this.owner=owner;Marked=marked; }
            public void Dispose() { if(entry!=null) Interlocked.CompareExchange(ref entry.Owner,null,owner); }
        }
    }
}
