using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Three contiguous entries and the original two bytes of padding.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 2, Size = 32)]
    public struct Cluster
    {
        internal TTEntry first, second, third;
        private ushort padding;
    }

    /// <summary>Managed port of Stockfish 11's clustered transposition table.</summary>
    public sealed class TranspositionTable : IDisposable
    {
        public const int ClusterSize = 3;
        private const int ClusterBytes = 32;
        // Each block stays below the CLR's maximum array size; entries within a
        // cluster remain contiguous and retain the original 32-byte stride.
        private const int SegmentShift = 20;
        private const int SegmentClusters = 1 << SegmentShift;
        private Cluster[][] segments = Array.Empty<Cluster[]>();
        private long clusterCount;
        public static int MaximumMegabytes => IntPtr.Size == 8 ? 131072 : 2048;
        public byte Generation { get; private set; }
        public long ClusterCount => clusterCount;

        public void Resize(int megabytes) => Resize(megabytes, count => new Cluster[count]);

        internal void Resize(int megabytes, Func<int,Cluster[]> allocate)
        {
            if (allocate == null) throw new ArgumentNullException(nameof(allocate));
            if (megabytes < 1 || megabytes > MaximumMegabytes) throw new ArgumentOutOfRangeException(nameof(megabytes));
            long count = checked((long)megabytes * 1024 * 1024 / ClusterBytes);
            int segmentCount = checked((int)((count + SegmentClusters - 1) / SegmentClusters));
            // Release the previous owner before allocation, as resize() does in
            // C++; retaining both full tables can double peak memory.
            segments = Array.Empty<Cluster[]>();
            clusterCount = 0;
            var allocated = new Cluster[segmentCount][];
            for (int i = 0; i < segmentCount; ++i)
                allocated[i] = allocate((int)Math.Min(SegmentClusters, count - (long)i * SegmentClusters));
            segments = allocated;
            clusterCount = count;
        }
        public void Clear() { foreach (Cluster[] segment in segments) Array.Clear(segment, 0, segment.Length); }
        /// <summary>Releases this table's reference to its storage. Outstanding entry references keep the old array alive.</summary>
        public void Dispose() { segments = Array.Empty<Cluster[]>(); clusterCount = 0; }
        public void NewSearch() => Generation = unchecked((byte)(Generation + 8));
        public long ClusterIndex(Key key)
        {
            EnsureAllocated();
            return IndexFor(key, clusterCount);
        }
        internal static long IndexFor(Key key, long count)
        {
            if (count < 1 || count > (1L << 32)) throw new ArgumentOutOfRangeException(nameof(count));
            return (long)(((ulong)(uint)(ulong)key * (ulong)count) >> 32);
        }
        public ref TTEntry FirstEntry(Key key) => ref ClusterAt(ClusterIndex(key)).first;
        public ref TTEntry Probe(Key key, out bool found)
        {
            // Resolve the segmented owner once, then retain the live cluster
            // reference, as probe() retains its original TTEntry pointer.
            ref Cluster cluster = ref ClusterAt(ClusterIndex(key));
            ushort keyPart = (ushort)((ulong)key >> 48);
            for (int i = 0; i < ClusterSize; ++i)
            {
                ref TTEntry entry = ref EntryIn(ref cluster, i);
                if (entry.key16 == 0 || entry.key16 == keyPart)
                {
                    entry.genBound8 = (byte)(Generation | (entry.genBound8 & 7));
                    found = entry.key16 != 0;
                    return ref entry;
                }
            }
            int replace = 0;
            for (int i = 1; i < ClusterSize; ++i)
            {
                ref TTEntry a = ref EntryIn(ref cluster, replace);
                ref TTEntry b = ref EntryIn(ref cluster, i);
                if (a.depth8 - ((263 + Generation - a.genBound8) & 0xf8) >
                    b.depth8 - ((263 + Generation - b.genBound8) & 0xf8)) replace = i;
            }
            found = false;
            return ref EntryIn(ref cluster, replace);
        }
        public int Hashfull()
        {
            EnsureAllocated();
            int count = 0;
            for (int i = 0; i < 1000 / ClusterSize; ++i)
            {
                ref Cluster cluster = ref ClusterAt(i);
                for (int j = 0; j < ClusterSize; ++j)
                    if ((EntryIn(ref cluster, j).genBound8 & 0xf8) == Generation) ++count;
            }
            return count * 1000 / (ClusterSize * (1000 / ClusterSize));
        }
        private void EnsureAllocated()
        {
            if (clusterCount == 0) throw new InvalidOperationException("Resize the table before using it.");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref Cluster ClusterAt(long cluster) =>
            ref segments[(int)(cluster >> SegmentShift)][(int)(cluster & (SegmentClusters - 1))];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref TTEntry EntryIn(ref Cluster value, int entry)
        {
            switch (entry)
            {
                case 0: return ref value.first;
                case 1: return ref value.second;
                case 2: return ref value.third;
                default: throw new ArgumentOutOfRangeException(nameof(entry));
            }
        }
    }
}
