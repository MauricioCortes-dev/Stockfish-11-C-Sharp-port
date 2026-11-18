using System;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct Key : IEquatable<Key>
    {
        private readonly ulong value;
        private Key(ulong value) { this.value=value; }
        public static implicit operator ulong(Key key) => key.value;
        public static implicit operator Key(ulong value) => new Key(value);
        public bool Equals(Key other) => value==other.value;
        public override bool Equals(object? other) => other is Key key && Equals(key);
        public override int GetHashCode() => value.GetHashCode();
        public override string ToString() => value.ToString();
    }
}
