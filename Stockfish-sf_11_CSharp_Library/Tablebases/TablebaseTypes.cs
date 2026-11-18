using System;
namespace Stockfish_sf_11_CSharp_Library.Tablebases
{
    /// <summary>Five WDL variants and the unavailable sentinel from Stockfish 11.</summary>
    public readonly struct WdlScore : IEquatable<WdlScore>
    {
        private readonly int value;
        private WdlScore(int value) { this.value=value; }
        public static readonly WdlScore Loss=-2, BlessedLoss=-1, Draw=0, CursedWin=1, Win=2, None=-1000;
        public static implicit operator int(WdlScore score)=>score.value;
        public static implicit operator WdlScore(int value)=>new WdlScore(value);
        public bool Equals(WdlScore other)=>value==other.value;
        public override bool Equals(object? other)=>other is WdlScore score && Equals(score);
        public override int GetHashCode()=>value;
        public override string ToString()=>value==-2?"Loss":value==-1?"Blessed loss":value==0?"Draw":value==1?"Cursed win":value==2?"Win":"None";
    }
    /// <summary>Outcome of an original Syzygy probe.</summary>
    public readonly struct ProbeState : IEquatable<ProbeState>
    {
        private readonly int value;
        private ProbeState(int value) { this.value=value; }
        public static readonly ProbeState Fail=0, Ok=1, ChangeStm=-1, ZeroingBestMove=2;
        public static implicit operator int(ProbeState state)=>state.value;
        public static implicit operator ProbeState(int value)=>new ProbeState(value);
        public bool Equals(ProbeState other)=>value==other.value;
        public override bool Equals(object? other)=>other is ProbeState state && Equals(state);
        public override int GetHashCode()=>value;
        public override string ToString()=>value==0?"Failed":value==1?"Success":value==-1?"Probed opponent side":value==2?"Best move zeroes DTZ":"None";
    }
}
