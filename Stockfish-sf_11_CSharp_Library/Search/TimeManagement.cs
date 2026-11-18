using System;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Stockfish 11's move-time allocation. Times are milliseconds, or nodes in nodestime mode.</summary>
    public sealed class TimeManagement
    {
        private const int MoveHorizon = 50;
        private const double SmallestNormalDouble = 2.2250738585072014E-308;
        public long AvailableNodes { get; set; }
        public long StartTime { get; private set; }
        public long Optimum { get; private set; }
        public long Maximum { get; private set; }

        public void Init(LimitsType limits, Color us, int ply, long minimumThinkingTime, long moveOverhead, long slowMover, long nodesTime, bool ponder)
        {
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            int color = us;
            if ((uint)color >= 2) throw new ArgumentOutOfRangeException(nameof(us));
            if (nodesTime != 0)
            {
                if (AvailableNodes == 0) AvailableNodes = nodesTime * limits.Time[color];
                limits.Time[color] = AvailableNodes;
                limits.Increment[color] *= nodesTime;
                limits.NodesPerMillisecond = nodesTime;
            }
            StartTime = limits.StartTime;
            Optimum = Maximum = Math.Max(limits.Time[color], minimumThinkingTime);
            int maxMovesToGo = limits.MovesToGo != 0 ? Math.Min(limits.MovesToGo, MoveHorizon) : MoveHorizon;
            for (int hypotheticalMoves = 1; hypotheticalMoves <= maxMovesToGo; ++hypotheticalMoves)
            {
                long remaining = Math.Max(limits.Time[color] + limits.Increment[color] * (hypotheticalMoves - 1) - moveOverhead * (2 + Math.Min(hypotheticalMoves, 40)), 0);
                long optimum = minimumThinkingTime + Remaining(remaining, hypotheticalMoves, ply, slowMover, false);
                long maximum = minimumThinkingTime + Remaining(remaining, hypotheticalMoves, ply, slowMover, true);
                Optimum = Math.Min(Optimum, optimum);
                Maximum = Math.Min(Maximum, maximum);
            }
            if (ponder) Optimum += Optimum / 4;
        }
        public long Elapsed(LimitsType limits, long now, long searchedNodes)
        {
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            return limits.NodesPerMillisecond != 0 ? searchedNodes : now - StartTime;
        }
        private static double MoveImportance(int ply) => Math.Pow(1 + Math.Exp((ply - 64.5) / 6.85), -0.171) + SmallestNormalDouble;
        private static long Remaining(long time, int movesToGo, int ply, long slowMover, bool maximum)
        {
            double maxRatio = maximum ? 7.3 : 1.0, stealRatio = maximum ? 0.34 : 0.0;
            double importance = MoveImportance(ply) * slowMover / 100.0, other = 0.0;
            for (int i = 1; i < movesToGo; ++i) other += MoveImportance(ply + 2 * i);
            double ratio1 = maxRatio * importance / (maxRatio * importance + other);
            double ratio2 = (importance + stealRatio * other) / (importance + other);
            return (long)(time * Math.Min(ratio1, ratio2));
        }
    }
}
