using System;
using System.Diagnostics;
using Stockfish_sf_11_CSharp_Library.Infrastructure;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Sequence shared by successive weaker-play selections.</summary>
    internal sealed class SkillRandomSequence
    {
        private readonly Func<ulong> seed;
        private PRNG random;
        private bool initialized;

        internal SkillRandomSequence(Func<ulong> seed) => this.seed = seed ?? throw new ArgumentNullException(nameof(seed));

        // The caller holds this instance's lock while selecting a move.
        internal uint Next()
        {
            if (!initialized)
            {
                ulong initial = seed();
                random = new PRNG(initial == 0 ? 1 : initial);
                initialized = true;
            }
            return random.Rand<uint>();
        }

        internal static ulong ClockSeed()
        {
            long ticks = Stopwatch.GetTimestamp(), frequency = Stopwatch.Frequency;
            return unchecked((ulong)(ticks / frequency * 1000 + ticks % frequency * 1000 / frequency));
        }
    }

    /// <summary>Stockfish 11's weaker-play selection over already scored root moves.</summary>
    public sealed class Skill
    {
        internal static readonly SkillRandomSequence SharedRandom = new SkillRandomSequence(SkillRandomSequence.ClockSeed);
        private readonly SkillRandomSequence random;
        public int Level { get; }
        public bool Enabled => Level < 20;
        public Move Best { get; private set; } = Move.MOVE_NONE;
        /// <summary>Uses the process-wide sequence retained by the original pick_best function.</summary>
        public Skill(int level) : this(level, SharedRandom) { }
        /// <summary>Creates an independent, seeded sequence for reproducible callers.</summary>
        public Skill(int level, ulong seed) : this(level, new SkillRandomSequence(() => seed))
        {
            if (seed == 0) throw new ArgumentOutOfRangeException(nameof(seed));
        }
        internal Skill(int level, SkillRandomSequence random)
        {
            if(level<0 || level>20) throw new ArgumentOutOfRangeException(nameof(level));
            Level=level; this.random=random ?? throw new ArgumentNullException(nameof(random));
        }
        public bool TimeToPick(int depth) => depth == 1 + Level;
        public Move PickBest(RootMoves roots, int multiPv)
        {
            if(roots==null)throw new ArgumentNullException(nameof(roots));
            if(multiPv<1 || multiPv>roots.Count)throw new ArgumentOutOfRangeException(nameof(multiPv));
            int top=roots[0].Score, delta=Math.Min(top-(int)roots[multiPv-1].Score,Value.PawnValueMg);
            int weakness=120-2*Level, max=-(int)Value.VALUE_INFINITE;
            // A lock preserves the sequence when multiple engine instances search concurrently.
            lock (random)
                for(int i=0;i<multiPv;++i)
                {
                    int push=(weakness*(top-(int)roots[i].Score)+delta*(int)(random.Next()%(uint)weakness))/128;
                    if((int)roots[i].Score+push>=max){max=(int)roots[i].Score+push;Best=roots[i].Pv[0];}
                }
            return Best;
        }
        public static int LevelFromElo(double elo, ref PRNG random)
        {
            double value=Math.Max(0,Math.Min(20,Math.Pow((elo-1346.6)/143.4,1/0.806)));
            return RoundLevel(value, ref random);
        }
        /// <summary>Randomly rounds the fractional strength as in Thread::search.</summary>
        public static int RoundLevel(double value, ref PRNG random)
        {
            if(double.IsNaN(value) || value<0 || value>20) throw new ArgumentOutOfRangeException(nameof(value));
            int whole=(int)value;
            return whole+((value-whole)*1024>random.Rand<uint>()%1024?1:0);
        }
    }
}
