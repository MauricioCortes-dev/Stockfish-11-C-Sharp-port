using System;
using System.Diagnostics;
using System.Threading;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;
using Stockfish_sf_11_CSharp_Library.MoveGeneration;
using Stockfish_sf_11_CSharp_Library.Tablebases.Syzygy;
using Stockfish_sf_11_CSharp_Library.Infrastructure;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Coordinates search workers and the original main-thread time allocation policy.</summary>
    public sealed class SingleWorkerTimeSearch
    {
        private readonly SearchThreadPool pool;
        private readonly Func<long> now;
        private readonly Func<long> readNodes;
        private readonly Func<bool> readPonder;
        private readonly SkillRandomSequence skillRandom;
        private readonly TimeManagement time = new TimeManagement();
        private readonly MainThreadTimeControl control = new MainThreadTimeControl();
        private readonly object ponderGate = new object();
        private bool pondering;
        private int increaseDepth = 1;
        private SingleWorkerSearch? selectedSearch;
        private Position? mainRootPosition;
        private LimitsType? activeLimits;
        private int activeMultiPv;
        private bool skillEnabled, accountedNodesTime;
        private Color rootSide;
        private int previousScore = Value.VALUE_INFINITE;
        private double previousTimeReduction = 1.0;
        public SingleWorkerSearch Search => selectedSearch ?? pool.Workers[0].Search;
        internal Position MainRootPosition => mainRootPosition ?? throw new InvalidOperationException("A search has not been prepared.");
        internal long Elapsed => activeLimits == null ? 0 : ReadElapsed(activeLimits);
        internal Action<int,RootMoves,int,Value,Value>? PvUpdated { get; set; }
        internal Action<int,Move,int>? RootMoveStarted { get; set; }
        internal Action<Value>? NoRootMoves { get; set; }
        internal Action<string>? Diagnostics { get => control.Diagnostics; set => control.Diagnostics=value; }
        public TimeManagement Time => time;
        public TbProbe? Tablebases { get; set; }
        public int SyzygyProbeLimit { get; set; } = 7;
        public int SyzygyProbeDepth { get; set; } = 1;
        public bool Syzygy50MoveRule { get; set; } = true;
        public TablebaseConfig TablebaseConfig { get; private set; }
        /// <summary>Called on the worker after a completed iteration; do not mutate the roots.</summary>
        public Action<int, RootMoves, long>? IterationCompleted { get; set; }
        internal Action<int, RootMoves, long, int, Value, Value>? IterationInterrupted { get; set; }

        public SingleWorkerTimeSearch(SearchThreadPool pool, Func<long>? clock = null)
            : this(pool, clock, Skill.SharedRandom) { }

        internal SingleWorkerTimeSearch(SearchThreadPool pool, Func<long>? clock, SkillRandomSequence skillRandom)
        {
            this.pool = pool ?? throw new ArgumentNullException(nameof(pool));
            now = clock ?? MonotonicMilliseconds;
            readNodes = () => this.pool.Nodes;
            readPonder = () => IsPondering;
            this.skillRandom = skillRandom ?? throw new ArgumentNullException(nameof(skillRandom));
        }

        private long ReadElapsed(LimitsType limits)
            => limits.NodesPerMillisecond!=0?time.Elapsed(limits,0,readNodes()):time.Elapsed(limits,now(),0);

        private static long MonotonicMilliseconds()
        {
            long ticks=Stopwatch.GetTimestamp(), frequency=Stopwatch.Frequency;
            return ticks/frequency*1000 + ticks%frequency*1000/frequency;
        }

        /// <summary>The position and limits must remain owned by this search until Wait returns.</summary>
        public void Start(Position position, LimitsType limits, int multiPv = 1, int contempt = 0,
                          long minimumThinkingTime = 20, long moveOverhead = 30, long slowMover = 84, bool ponder = false, bool ponderOption = false, double skillLevel = 20, bool limitStrength = false, double elo = 1350, long nodesTime = 0)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            if (limits == null) throw new ArgumentNullException(nameof(limits));
            if (limits.Depth < 0 || limits.Depth >= ChessConstants.MAX_PLY || limits.Nodes < 0 || limits.MoveTime < 0)
                throw new ArgumentOutOfRangeException(nameof(limits));
            long startTime = now();
            ulong seed = unchecked((ulong)startTime); if(seed == 0) seed = 1;
            var roundingRandom = new PRNG(seed);
            int level = limitStrength ? Skill.LevelFromElo(elo, ref roundingRandom) : Skill.RoundLevel(skillLevel, ref roundingRandom);
            var skill = new Skill(level, skillRandom);
            int searchMultiPv = skill.Enabled ? Math.Max(multiPv,4) : multiPv;
            RootMoves? rankedRoots = null;
            var positions = new Position[pool.Workers.Count];
            pool.Start((worker, cancellation) =>
            {
                worker.Search.SetThreadCount(pool.Workers.Count);
                worker.Search.Tablebases = Tablebases;
                worker.Search.TablebaseConfig = TablebaseConfig;
                worker.Search.TablebaseProbed = worker.Index == 0 ? (Action)(() => control.CallsCount = 0) : null;
                if (worker.Index == 0) Run(worker, cancellation, positions[0], limits, searchMultiPv, contempt, rankedRoots, skill);
                else
                {
                    worker.Search.IncreaseDepth = () => Volatile.Read(ref increaseDepth) != 0;
                    try
                    {
                        worker.Search.Search(positions[worker.Index], ChessConstants.MAX_PLY - 1, searchMultiPv, contempt,
                            () => cancellation.IsCancellationRequested, mateLimit: limits.Mate,
                            searchMoves: limits.SearchMoves, startNewSearch: false, preparedRoots: rankedRoots);
                        // In Stockfish every worker can stop the pool after finding
                        // the requested mate, not just the main worker.
                        if (worker.Search.MateLimitReached) pool.Stop();
                    }
                    finally { worker.Search.IncreaseDepth = null; }
                }
            }, true, () =>
            {
                // The pool invokes preparation only after rejecting overlapping searches.
                // Do not change the active search's state before that check.
                // MainThread::search uses the unrounded Skill Level (converted
                // to int) and UCI_LimitStrength when deciding worker voting.
                skillEnabled = limitStrength || (int)skillLevel < 20;
                lock (ponderGate) pondering = ponder;
                selectedSearch = null; activeLimits = limits; activeMultiPv = multiPv; rootSide = position.SideToMove; accountedNodesTime = false;
                Volatile.Write(ref increaseDepth, 1);
                limits.StartTime = startTime;
                time.Init(limits, position.SideToMove, position.GamePly, minimumThinkingTime, moveOverhead, slowMover, nodesTime, ponderOption);
                // Stockfish keeps the periodic check counter across searches and
                // resets it only when workers are cleared or a tablebase is probed.
                control.Ponder = IsPondering; control.StopOnPonderHit = false;
                pool.Workers[0].State.Table.NewSearch();
                foreach (SearchThread thread in pool.Workers) thread.Search.BestMoveChanges = 0;
                // Stockfish collects root moves from the caller's position, then builds a
                // fresh position for every worker. Searches may reorder piece lists while
                // making and undoing captures even though the board itself is restored.
                rankedRoots = new RootMoves();
                Span<Move> candidates=stackalloc Move[ChessConstants.MAX_MOVES];
                int candidateCount=MoveGenerator.GenerateInto(position,GenType.LEGAL,candidates);
                for(int i=0;i<candidateCount;++i)
                    if (limits.SearchMoves.Count == 0 || limits.SearchMoves.Contains(candidates[i])) rankedRoots.Add(new RootMove(candidates[i]));
                TablebaseConfig = default;
                if (Tablebases != null && Tablebases.Enabled && Tablebases.MaxCardinality != 0)
                    TablebaseConfig = Tablebases.RankRootMoves(position, rankedRoots, SyzygyProbeLimit, SyzygyProbeDepth, Syzygy50MoveRule);
                for (int i = 0; i < positions.Length; ++i) positions[i] = position.CopyForSearch();
                mainRootPosition = positions[0];
            });
        }

        private void Run(SearchThread worker, CancellationToken cancellation, Position position, LimitsType limits, int multiPv, int contempt, RootMoves? rankedRoots, Skill skill)
        {
            SingleWorkerSearch search = worker.Search;
            search.Elapsed = () => Elapsed;
            search.PvUpdated = (depth, roots, pvIndex, alpha, beta) => PvUpdated?.Invoke(depth, roots, pvIndex, alpha, beta);
            search.RootMoveStarted = (depth, move, number) => RootMoveStarted?.Invoke(depth, move, number);
            bool stop = false;
            int lastBestMoveDepth = 0, iterationIndex = 0;
            Move lastBestMove = Move.MOVE_NONE;
            int[] iterationValues = new int[4];
            for (int i = 0; i < 4; ++i) iterationValues[i] = previousScore == Value.VALUE_INFINITE ? 0 : previousScore;
            double timeReduction = 1, totalBestMoveChanges = 0;
            search.CheckTime = () =>
            {
                if (control.CheckDeferred(limits, time, now, readNodes, readPonder)) { stop = true; pool.Stop(); }
            };
            search.IncreaseDepth = () => Volatile.Read(ref increaseDepth) != 0;
            search.AspirationFailLow = () => control.StopOnPonderHit = false;
            search.IterationInterrupted = (depth, roots, pvIndex, alpha, beta) =>
                IterationInterrupted?.Invoke(depth, roots, pool.Nodes, pvIndex, alpha, beta);
            search.IterationFinished = (depth, roots, value) =>
            {
                IterationCompleted?.Invoke(depth, roots, pool.Nodes);
                if (skill.Enabled && skill.TimeToPick(depth)) skill.PickBest(roots, Math.Min(multiPv,roots.Count));
                totalBestMoveChanges /= 2;
                if (roots[0].Pv[0] != lastBestMove) { lastBestMove = roots[0].Pv[0]; lastBestMoveDepth = depth; }
                if (limits.UseTimeManagement && !control.StopOnPonderHit)
                {
                    double fallingEval = Math.Max(0.5, Math.Min(1.5, (332.0 + 6.0 * (previousScore - (int)value) + 6.0 * (iterationValues[iterationIndex] - (int)value)) / 704));
                    timeReduction = lastBestMoveDepth + 9 < depth ? 1.94 : 0.91;
                    double reduction = (1.41 + previousTimeReduction) / (2.27 * timeReduction);
                    foreach (SearchThread thread in pool.Workers) totalBestMoveChanges += thread.Search.TakeBestMoveChanges();
                    double budget = time.Optimum * fallingEval * reduction * (1 + totalBestMoveChanges / pool.Workers.Count);
                    long elapsed = roots.Count==1?0:ReadElapsed(limits);
                    if (roots.Count == 1 || elapsed > budget)
                    {
                        if (IsPondering) control.StopOnPonderHit = true;
                        else { stop = true; pool.Stop(); }
                    }
                    else if (Volatile.Read(ref increaseDepth) != 0 && !IsPondering && elapsed > budget * 0.6) Volatile.Write(ref increaseDepth, 0);
                    else Volatile.Write(ref increaseDepth, 1);
                }
                iterationValues[iterationIndex] = value;
                iterationIndex = (iterationIndex + 1) & 3;
                return stop;
            };
            try
            {
                search.Search(position, limits.Depth == 0 ? ChessConstants.MAX_PLY - 1 : limits.Depth, multiPv, contempt,
                              () => stop || cancellation.IsCancellationRequested, limits.Mate, limits.SearchMoves, startNewSearch: false, preparedRoots: rankedRoots);
                if(search.RootMoves.Count==0) NoRootMoves?.Invoke(search.BestValue);
                // Like Threads.stop in the original, a time/node or mate limit ends
                // the whole search even when the command also requests ponder/infinite.
                // Reaching depth alone must still wait for stop or ponderhit.
                if (stop || search.MateLimitReached) pool.Stop();
                using (cancellation.Register(() => { lock (ponderGate) Monitor.PulseAll(ponderGate); }))
                {
                    lock (ponderGate)
                    {
                        while (!cancellation.IsCancellationRequested && (pondering || limits.Infinite != 0))
                            Monitor.Wait(ponderGate);
                    }
                }
                if (skill.Enabled && search.RootMoves.Count != 0)
                {
                    Move selected = skill.Best != Move.MOVE_NONE ? skill.Best : skill.PickBest(search.RootMoves, Math.Min(multiPv,search.RootMoves.Count));
                    int index = search.RootMoves.Find(selected); if(index > 0) search.RootMoves.Swap(0,index);
                }
                // MainThread skips Thread::search entirely for a terminal root.
                if(search.RootMoves.Count!=0)previousTimeReduction = timeReduction;
            }
            finally { pool.Stop(); search.CheckTime = null; search.IncreaseDepth = null; search.AspirationFailLow = null; search.IterationFinished = null; search.IterationInterrupted = null; search.Elapsed = null; search.PvUpdated = null; search.RootMoveStarted = null; }
        }
        private bool IsPondering { get { lock (ponderGate) return pondering; } }
        public void PonderHit() { lock (ponderGate) { pondering = false; Monitor.PulseAll(ponderGate); } }
        public void Stop() { pool.Stop(); lock (ponderGate) Monitor.PulseAll(ponderGate); }
        public void Wait()
        {
            pool.Wait();
            SingleWorkerSearch best = pool.Workers[0].Search;
            if (activeLimits != null && activeMultiPv == 1 && !skillEnabled && activeLimits.Depth == 0 && best.RootMoves.Count != 0)
            {
                int minScore = best.RootMoves[0].Score;
                foreach (SearchThread thread in pool.Workers)
                    if (thread.Search.RootMoves.Count != 0) minScore = Math.Min(minScore, thread.Search.RootMoves[0].Score);
                var votes = new System.Collections.Generic.Dictionary<Move,long>();
                foreach (SearchThread thread in pool.Workers)
                {
                    SingleWorkerSearch candidate = thread.Search;
                    if (candidate.RootMoves.Count == 0) continue;
                    Move move = candidate.RootMoves[0].Pv[0];
                    votes.TryGetValue(move, out long old);
                    votes[move] = old + ((long)candidate.RootMoves[0].Score - minScore + 14) * candidate.CompletedDepth;
                    if (best.RootMoves[0].Score >= Value.VALUE_MATE_IN_MAX_PLY)
                    { if (candidate.RootMoves[0].Score > best.RootMoves[0].Score) best = candidate; }
                    else
                    {
                        votes.TryGetValue(best.RootMoves[0].Pv[0], out long bestVotes);
                        if (candidate.RootMoves[0].Score >= Value.VALUE_MATE_IN_MAX_PLY || votes[move] > bestVotes) best = candidate;
                    }
                }
            }
            selectedSearch = best;
            if (!accountedNodesTime && activeLimits != null && activeLimits.NodesPerMillisecond != 0)
            {
                time.AvailableNodes += activeLimits.Increment[rootSide] - pool.Nodes;
                accountedNodesTime = true;
            }
            // The original stores the selected root's score even when a stop
            // interrupted depth one; its terminal dummy root has score -infinite.
            previousScore = best.RootMoves.Count!=0 ? (int)best.BestValue : -(int)Value.VALUE_INFINITE;
        }
    }
}
