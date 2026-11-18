using System;
using System.Runtime.CompilerServices;
using Stockfish_sf_11_CSharp_Library.Infrastructure;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Portable form of the main search thread's periodic time and node check.</summary>
    public sealed class MainThreadTimeControl
    {
        public int CallsCount { get; set; }
        public bool Ponder { get; set; }
        public bool StopOnPonderHit { get; set; }
        private static long lastInfoTime;
        private static volatile bool infoTimeInitialized;
        private static readonly object infoGate = new object();
        internal Action<string>? Diagnostics { get; set; }

        public bool Check(LimitsType limits, TimeManagement time, long now, long nodes)
        {
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            if (time == null) throw new ArgumentNullException(nameof(time));
            if (!BeginCheck(limits)) return false;
            long elapsed = time.Elapsed(limits, now, nodes);
            bool print;
            lock(infoGate)
            {
                if(!infoTimeInitialized) InitializeInfoTime(now);
                print=AdvanceInfoTime(limits.StartTime+elapsed);
            }
            if(print) PrintDiagnostics();
            return !Ponder && (ElapsedLimitReached(limits,time,elapsed) || (limits.Nodes!=0 && nodes>=limits.Nodes));
        }

        // Providers are private, stable delegates of the coordinator. No sampling,
        // ponder lock or worker aggregation takes place on the counter-only path.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool CheckDeferred(LimitsType limits,TimeManagement time,Func<long> clock,Func<long> nodes,Func<bool> pondering)
        {
            if(!BeginCheck(limits))return false;
            return CheckDeferredCore(limits,time,clock,nodes,pondering);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool BeginCheck(LimitsType limits)
        {
            if(--CallsCount>0)return false;
            CallsCount=limits.Nodes!=0?(int)Math.Min(1024,limits.Nodes/1024):1024;
            return true;
        }

        private bool CheckDeferredCore(LimitsType limits,TimeManagement time,Func<long> clock,Func<long> nodes,Func<bool> pondering)
        {
            // Like the C++ static initializer, wall time is needed once even
            // in nodestime mode. Publish it once, before sampling elapsed time.
            if(!infoTimeInitialized)
                lock(infoGate)if(!infoTimeInitialized)InitializeInfoTime(clock());
            long elapsed=limits.NodesPerMillisecond!=0?time.Elapsed(limits,0,nodes()):time.Elapsed(limits,clock(),0);
            bool print;
            lock(infoGate)print=AdvanceInfoTime(limits.StartTime+elapsed);
            if(print)PrintDiagnostics();
            Ponder=pondering();
            // Keep C++ short-circuit order: a time stop or ponder must not cause
            // an unnecessary global node read. In nodestime a node limit can
            // require a second, current aggregation, as in the original.
            return !Ponder && (ElapsedLimitReached(limits,time,elapsed) || (limits.Nodes!=0 && nodes()>=limits.Nodes));
        }

        private bool ElapsedLimitReached(LimitsType limits,TimeManagement time,long elapsed)
            => (limits.UseTimeManagement && (elapsed>time.Maximum-10 || StopOnPonderHit))
                || (limits.MoveTime!=0 && elapsed>=limits.MoveTime);

        private static void InitializeInfoTime(long now)
        {lastInfoTime=now;infoTimeInitialized=true;}
        // Both helpers are called with infoGate held, including first publication.
        private static bool AdvanceInfoTime(long tick)
        {
            if(tick-lastInfoTime<1000)return false;
            lastInfoTime=tick;return true;
        }
        private void PrintDiagnostics() => RuntimeStatistics.Shared.Print(text=>Diagnostics?.Invoke(text));
    }
}
