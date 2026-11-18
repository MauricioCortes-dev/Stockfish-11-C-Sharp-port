using System;
namespace Stockfish_sf_11_CSharp_Library.Types
{
    public readonly struct Phase : IEquatable<Phase>
    {
        private readonly int value;
        private Phase(int value) { this.value = value; }
        public static readonly Phase PHASE_ENDGAME = 0, PHASE_MIDGAME = 128, MG = 0, EG = 1, PHASE_NB = 2;
        public static implicit operator int(Phase value) => value.value;
        public static implicit operator Phase(int value) => new Phase(value);
        public bool Equals(Phase other) => value == other.value;
        public override bool Equals(object? other) => other is Phase x && Equals(x);
        public override int GetHashCode() => value;
        public override string ToString() => value.ToString();
    }
}
