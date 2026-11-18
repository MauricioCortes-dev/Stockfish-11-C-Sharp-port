using System;
using System.Runtime.CompilerServices;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    public enum StatsType { NoCaptures, Captures }
    public interface IHistoryLimit { int Limit { get; } }
    public readonly struct ButterflyLimit : IHistoryLimit { public int Limit => 10692; }
    public readonly struct PieceToLimit : IHistoryLimit { public int Limit => 29952; }

    /// <summary>One signed 16-bit history value, updated with Stockfish's bounded gravity formula.</summary>
    public struct StatsEntry<TLimit> where TLimit : struct, IHistoryLimit
    {
        private short entry;
        public StatsEntry(short value) { entry = value; }
        public static implicit operator int(StatsEntry<TLimit> entry) => entry.entry;
        public static implicit operator StatsEntry<TLimit>(short value) => new StatsEntry<TLimit>(value);
        public static StatsEntry<TLimit> operator <<(StatsEntry<TLimit> entry, int bonus)
        {
            int limit = default(TLimit).Limit;
            if (bonus < -limit || bonus > limit) throw new ArgumentOutOfRangeException(nameof(bonus));
            entry.entry = unchecked((short)(entry.entry + bonus - entry.entry * Math.Abs(bonus) / limit));
            return entry;
        }
    }

    /// <summary>Contiguous multidimensional table with live reference access and independent copies of elements.</summary>
    public sealed class Stats<T> where T : struct
    {
        private readonly T[] entries;
        private readonly int[] dimensions;
        private readonly int firstSize, secondSize, thirdSize;
        public Stats(params int[] dimensions)
        {
            if (dimensions == null || dimensions.Length == 0) throw new ArgumentException("At least one dimension is required.", nameof(dimensions));
            this.dimensions = (int[])dimensions.Clone();
            firstSize = dimensions[0];
            secondSize = dimensions.Length > 1 ? dimensions[1] : 0;
            thirdSize = dimensions.Length > 2 ? dimensions[2] : 0;
            int count = 1;
            foreach (int size in dimensions) { if (size <= 0) throw new ArgumentOutOfRangeException(nameof(dimensions)); count = checked(count * size); }
            entries = new T[count];
        }
        // Fixed arity keeps ordinary history[...] expressions allocation-free.
        // Check each coordinate: checking only the flattened offset would allow
        // an invalid column to alias a valid element in the following row.
        public ref T this[int first, int second]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (dimensions.Length != 2) throw new ArgumentException("Incorrect number of indices.", "indices");
                if ((uint)first >= (uint)firstSize || (uint)second >= (uint)secondSize) throw new IndexOutOfRangeException();
                return ref entries[first * secondSize + second];
            }
        }
        public ref T this[int first, int second, int third]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (dimensions.Length != 3) throw new ArgumentException("Incorrect number of indices.", "indices");
                if ((uint)first >= (uint)firstSize || (uint)second >= (uint)secondSize || (uint)third >= (uint)thirdSize) throw new IndexOutOfRangeException();
                return ref entries[(first * secondSize + second) * thirdSize + third];
            }
        }
        /// <summary>General-rank access retained for callers that already own an index array.</summary>
        public ref T this[params int[] indices]
        {
            get
            {
                if (indices == null || indices.Length != dimensions.Length) throw new ArgumentException("Incorrect number of indices.", nameof(indices));
                int index = 0;
                for (int i = 0; i < indices.Length; ++i)
                { if ((uint)indices[i] >= (uint)dimensions[i]) throw new IndexOutOfRangeException(); index = index * dimensions[i] + indices[i]; }
                return ref entries[index];
            }
        }
        public void Fill(T value) { for (int i = 0; i < entries.Length; ++i) entries[i] = value; }
        public void CopyFrom(Stats<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.dimensions.Length != dimensions.Length) throw new ArgumentException("Dimension mismatch.", nameof(source));
            for (int i = 0; i < dimensions.Length; ++i) if (source.dimensions[i] != dimensions[i]) throw new ArgumentException("Dimension mismatch.", nameof(source));
            Array.Copy(source.entries, entries, entries.Length);
        }
    }
    public sealed class ButterflyHistory
    {
        private readonly Stats<StatsEntry<ButterflyLimit>> table = new Stats<StatsEntry<ButterflyLimit>>(2,4096);
        public ref StatsEntry<ButterflyLimit> this[int color,int fromTo]
        { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ref table[color,fromTo]; }
        public void Fill(short v) => table.Fill(v);
    }
    public sealed class CapturePieceToHistory
    {
        private readonly Stats<StatsEntry<ButterflyLimit>> table = new Stats<StatsEntry<ButterflyLimit>>(16,64,8);
        public ref StatsEntry<ButterflyLimit> this[int piece,int to,int captured]
        { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ref table[piece,to,captured]; }
        public void Fill(short v) => table.Fill(v);
    }
    public sealed class PieceToHistory
    {
        private readonly Stats<StatsEntry<PieceToLimit>> table = new Stats<StatsEntry<PieceToLimit>>(16,64);
        public ref StatsEntry<PieceToLimit> this[int piece,int to]
        { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ref table[piece,to]; }
        public void Fill(short v) => table.Fill(v);
        public void CopyFrom(PieceToHistory source) => table.CopyFrom(source.table);
    }
    public sealed class CounterMoveHistory
    {
        private readonly Stats<Move> table = new Stats<Move>(16,64);
        public ref Move this[int piece,int to]
        { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => ref table[piece,to]; }
        public void Fill(Move move) => table.Fill(move);
    }
    public sealed class ContinuationHistory
    {
        private readonly PieceToHistory[] tables = new PieceToHistory[16*64];
        public PieceToHistory this[int piece,int to] { get { if ((uint)piece>=16 || (uint)to>=64) throw new IndexOutOfRangeException(); int i=piece*64+to; return tables[i] ?? (tables[i]=new PieceToHistory()); } }
        public void Fill(short value) { for (int p=0;p<16;++p) for (int s=0;s<64;++s) this[p,s].Fill(value); }
        public void Fill(PieceToHistory source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            // Preserve the source even when it is one of the destination tables.
            var snapshot = new PieceToHistory(); snapshot.CopyFrom(source);
            for (int p=0;p<16;++p) for (int s=0;s<64;++s) this[p,s].CopyFrom(snapshot);
        }
    }
}
