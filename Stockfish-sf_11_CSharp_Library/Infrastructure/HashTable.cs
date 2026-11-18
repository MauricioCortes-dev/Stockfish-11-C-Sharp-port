using System;
using Stockfish_sf_11_CSharp_Library.Types;
namespace Stockfish_sf_11_CSharp_Library.Infrastructure
{
    /// <summary>Direct-mapped table. Each owner must keep its own instance.</summary>
    public sealed class HashTable<T> where T : new()
    {
        private readonly T[] entries;
        public HashTable(int size)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            entries = new T[size];
            for (int i = 0; i < size; ++i) entries[i] = new T();
        }
        public int Size => entries.Length;
        public ref T this[Key key] => ref entries[unchecked((uint)(ulong)key) & (uint)(entries.Length - 1)];
    }
}
