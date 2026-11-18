using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Search;
using Stockfish_sf_11_CSharp_Library.MoveGeneration;
using Stockfish_sf_11_CSharp_Library.Evaluation;
using Stockfish_sf_11_CSharp_Library.Tablebases.Syzygy;
using Stockfish_sf_11_CSharp_Library.Types;
using Stockfish_sf_11_CSharp_Library.Infrastructure;

namespace Stockfish_sf_11_CSharp_Library.Protocol
{
    /// <summary>UCI session with private workers. Commands are supplied from one owner thread.</summary>
    public sealed class UciEngine : IDisposable
    {
        private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        private readonly Dictionary<string,Option> options = new Dictionary<string,Option>(StringComparer.OrdinalIgnoreCase)
        {
            ["Debug Log File"]=new Option(""), ["Contempt"]=new Option(24,-100,100),
            ["Analysis Contempt"]=new Option("Both var Off var White var Black var Both","Both"),
            ["Threads"]=new Option(1,1,512), ["Hash"]=new Option(16,1,TranspositionTable.MaximumMegabytes), ["Clear Hash"]=new Option(),
            ["Ponder"]=new Option(false), ["MultiPV"]=new Option(1,1,500), ["Skill Level"]=new Option(20,0,20),
            ["Move Overhead"]=new Option(30,0,5000), ["Minimum Thinking Time"]=new Option(20,0,5000),
            ["Slow Mover"]=new Option(84,10,1000), ["nodestime"]=new Option(0,0,10000),
            ["UCI_Chess960"]=new Option(false), ["UCI_AnalyseMode"]=new Option(false),
            ["UCI_LimitStrength"]=new Option(false), ["UCI_Elo"]=new Option(1350,1350,2850),
            ["SyzygyPath"]=new Option("<empty>"), ["SyzygyProbeDepth"]=new Option(1,1,100),
            ["Syzygy50MoveRule"]=new Option(true), ["SyzygyProbeLimit"]=new Option(7,0,7)
        };
        private readonly Action<string> output;
        private readonly Action<string> diagnostics;
        private readonly object outputGate = new object();
        private System.IO.StreamWriter? log;
        private readonly TranspositionTable table = new TranspositionTable();
        private readonly TbProbe tablebases = new TbProbe();
        private string syzygyPath = "";
        private SearchThreadPool pool;
        private SingleWorkerTimeSearch search;
        private Position position = new Position();
        private Thread? waiter;
        private bool disposed, chess960, ponderOption, limitStrength, analysisMode, syzygyRule50 = true;
        private int hash = 16, threads = 1, multiPv = 1, contempt = 24, overhead = 30, minimumTime = 20, slowMover = 84, syzygyLimit = 7, syzygyDepth = 1, nodesTime = 0;
        private double skillLevel = 20, elo = 1350;
        private string analysisContempt = "Both";
        private ulong lastPerftNodes;

        public UciEngine(Action<string> output, Action<string>? diagnostics = null)
        {
            this.output = output ?? throw new ArgumentNullException(nameof(output));
            this.diagnostics = diagnostics ?? output;
            table.Resize(hash); pool = new SearchThreadPool(1, table); search = new SingleWorkerTimeSearch(pool);
            position.Set(StartFen, false, new StateInfo()); ApplyTablebaseSettings();
        }
        private void Write(string line)
        {
            lock (outputGate) { WriteLog("<< ",line); output(line); }
        }
        private void WriteLog(string prefix, string text)
        {
            if (log == null) return;
            foreach(string line in text.Split('\n')) log.WriteLine(prefix + line.TrimEnd('\r'));
            log.Flush();
        }
        private static long Now()
        {
            long ticks = Stopwatch.GetTimestamp(), frequency = Stopwatch.Frequency;
            return ticks/frequency*1000 + ticks%frequency*1000/frequency;
        }

        /// <summary>Processes one command; returns false for quit. Stop is nonblocking.</summary>
        public bool ProcessCommand(string command)
        {
            if (disposed) throw new ObjectDisposedException(nameof(UciEngine));
            if (command == null) throw new ArgumentNullException(nameof(command));
            lock (outputGate) WriteLog(">> ",command);
            string[] words = command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return true;
            try
            {
                switch (words[0])
                {
                    case "uci": Identify(); break;
                    case "isready": Write("readyok"); break;
                    case "setoption": SetOption(words); break;
                    case "ucinewgame": NewGame(); break;
                    case "position": SetPosition(words); break;
                    case "go": Go(words); break;
                    case "stop": search.Stop(); break;
                    case "ponderhit": search.PonderHit(); break;
                    case "d": StopAndWait(); Write(position.ToString(tablebases)); break;
                    case "eval": StopAndWait(); Write(ClassicEvaluator.Trace(position, pool.Workers[0].Evaluation)); break;
                    case "flip": StopAndWait(); position.Flip(); break;
                    case "compiler": Write(EngineDiagnostics.CompilerInfo()); break;
                    case "bench": Benchmark(words); break;
                    case "quit": StopAndWait(); return false;
                    default: Write("Unknown command: " + command); break;
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException || ex is InvalidOperationException || ex is System.IO.IOException || ex is UnauthorizedAccessException)
            { Write("info string " + ex.Message.Replace('\n', ' ')); }
            return true;
        }

        private void Identify()
        {
            Write("id name " + EngineDiagnostics.EngineInfo(true));
            Write("option name Debug Log File type string default ");
            Write("option name Contempt type spin default 24 min -100 max 100");
            Write("option name Analysis Contempt type combo default Both var Off var White var Black var Both");
            Write("option name Threads type spin default 1 min 1 max 512");
            Write("option name Hash type spin default 16 min 1 max " + TranspositionTable.MaximumMegabytes);
            Write("option name Clear Hash type button");
            Write("option name Ponder type check default false");
            Write("option name MultiPV type spin default 1 min 1 max 500");
            Write("option name Skill Level type spin default 20 min 0 max 20");
            Write("option name Move Overhead type spin default 30 min 0 max 5000");
            Write("option name Minimum Thinking Time type spin default 20 min 0 max 5000");
            Write("option name Slow Mover type spin default 84 min 10 max 1000");
            Write("option name nodestime type spin default 0 min 0 max 10000");
            Write("option name UCI_Chess960 type check default false");
            Write("option name UCI_AnalyseMode type check default false");
            Write("option name UCI_LimitStrength type check default false");
            Write("option name UCI_Elo type spin default 1350 min 1350 max 2850");
            Write("option name SyzygyPath type string default <empty>");
            Write("option name SyzygyProbeDepth type spin default 1 min 1 max 100");
            Write("option name Syzygy50MoveRule type check default true");
            Write("option name SyzygyProbeLimit type spin default 7 min 0 max 7");
            Write("uciok");
        }
        private static float ParseSpin(Option option) => (float)(double)option;
        private void SetOption(string[] words)
        {
            if (words.Length < 3 || words[1] != "name") throw new ArgumentException("Invalid setoption command.");
            int index = Array.IndexOf(words, "value", 2);
            string name = string.Join(" ", words, 2, (index < 0 ? words.Length : index) - 2);
            string value = index < 0 ? "" : string.Join(" ", words, index + 1, words.Length - index - 1);
            string option = name.ToLowerInvariant();
            if (!options.TryGetValue(name,out Option definition)) { Write("No such option: " + name); return; }
            if (!definition.Assign(value)) return;
            StopAndWait();
            switch (option)
            {
                case "debug log file":
                    lock (outputGate)
                    {
                        if (value.Length == 0 || value == "<empty>") { log?.Dispose(); log = null; }
                        else if (log == null) log = new System.IO.StreamWriter(value, false);
                    }
                    break;
                case "hash": hash = (int)ParseSpin(definition); table.Resize(hash); break;
                // The original callback invokes Search::clear(), which also resets
                // worker histories, available nodes and loaded tablebase mappings.
                case "clear hash": NewGame(); break;
                case "threads":
                    int count = (int)ParseSpin(definition);
                    long availableNodes = search.Time.AvailableNodes;
                    // ThreadPool::set recreates all workers and resizes (clears)
                    // the TT even when the requested count is unchanged.
                    pool.Dispose(); threads = count; table.Resize(hash);
                    pool = new SearchThreadPool(threads, table); search = new SingleWorkerTimeSearch(pool);
                    search.Time.AvailableNodes = availableNodes;
                    break;
                case "multipv": multiPv = (int)ParseSpin(definition); break;
                case "ponder": ponderOption = bool.Parse(value); break;
                case "skill level": skillLevel = ParseSpin(definition); break;
                case "uci_limitstrength": limitStrength = bool.Parse(value); break;
                case "uci_elo": elo = ParseSpin(definition); break;
                case "contempt": contempt = (int)ParseSpin(definition); break;
                case "analysis contempt":
                    analysisContempt = value; break;
                case "nodestime": nodesTime = (int)ParseSpin(definition); break;
                case "uci_analysemode": analysisMode = bool.Parse(value); break;
                case "move overhead": overhead = (int)ParseSpin(definition); break;
                case "minimum thinking time": minimumTime = (int)ParseSpin(definition); break;
                case "slow mover": slowMover = (int)ParseSpin(definition); break;
                case "uci_chess960": chess960 = bool.Parse(value); break;
                case "syzygypath":
                    syzygyPath = value == "<empty>" ? "" : value; tablebases.Init(syzygyPath);
                    Write("info string Found " + tablebases.WdlFileCount + " tablebases"); break;
                case "syzygyprobedepth": syzygyDepth = (int)ParseSpin(definition); break;
                case "syzygy50moverule": syzygyRule50 = bool.Parse(value); break;
                case "syzygyprobelimit": syzygyLimit = (int)ParseSpin(definition); break;
                default: Write("No such option: " + name); break;
            }
            ApplyTablebaseSettings();
        }
        private void ApplyTablebaseSettings()
        {
            search.Tablebases = tablebases; search.SyzygyProbeLimit = syzygyLimit;
            search.SyzygyProbeDepth = syzygyDepth; search.Syzygy50MoveRule = syzygyRule50;
        }
        private void NewGame()
        {
            StopAndWait(); table.Clear(); tablebases.Init(syzygyPath); pool.Dispose(); pool = new SearchThreadPool(threads, table); search = new SingleWorkerTimeSearch(pool); ApplyTablebaseSettings();
        }
        private void SetPosition(string[] words)
        {
            if (words.Length < 2) throw new ArgumentException("Missing position.");
            StopAndWait();
            int movesIndex = Array.IndexOf(words, "moves", 2);
            int end = movesIndex < 0 ? words.Length : movesIndex;
            string fen;
            if (words[1] == "startpos") fen = StartFen;
            else if (words[1] == "fen" && end > 2) fen = string.Join(" ", words, 2, end - 2);
            else throw new ArgumentException("Expected startpos or fen.");
            // Stockfish's own benchmark includes four-field FENs. Its stream
            // parser leaves omitted counters at zero (fullmove normalizes to 1).
            int fields=fen.Split(' ').Length;
            if(fields==4)fen+=" 0 1";
            else if(fields==5)fen+=" 1";
            var next = new Position(); next.Set(fen, chess960, new StateInfo());
            if (movesIndex >= 0)
                for (int i = movesIndex + 1; i < words.Length; ++i)
                {
                    Move move = UciNotation.ToMove(next, words[i]);
                    if (move == Move.MOVE_NONE) throw new ArgumentException("Illegal move: " + words[i]);
                    next.DoMove(move, new StateInfo());
                }
            position = next;
        }
        private static long Number(string[] words, ref int index)
        {
            if (++index >= words.Length) throw new ArgumentException("Missing go parameter value.");
            long value = long.Parse(words[index], NumberStyles.Integer, CultureInfo.InvariantCulture);
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(words), "Go values must be nonnegative.");
            return value;
        }
        private void Go(string[] words)
        {
            StopAndWait(); var limits = new LimitsType(); bool pondering = false;
            for (int i = 1; i < words.Length; ++i)
                switch (words[i])
                {
                    case "wtime": limits.Time[0] = Number(words, ref i); break;
                    case "btime": limits.Time[1] = Number(words, ref i); break;
                    case "winc": limits.Increment[0] = Number(words, ref i); break;
                    case "binc": limits.Increment[1] = Number(words, ref i); break;
                    case "movestogo": limits.MovesToGo = checked((int)Number(words, ref i)); break;
                    case "depth": limits.Depth = checked((int)Number(words, ref i)); break;
                    case "nodes": limits.Nodes = Number(words, ref i); break;
                    case "movetime": limits.MoveTime = Number(words, ref i); break;
                    case "mate": limits.Mate = checked((int)Number(words, ref i)); break;
                    case "perft": limits.Perft = checked((int)Number(words, ref i)); break;
                    case "infinite": limits.Infinite = 1; break;
                    case "ponder": pondering = true; break;
                    case "searchmoves":
                        while (++i < words.Length) limits.SearchMoves.Add(UciNotation.ToMove(position, words[i]));
                        break;
                    default: throw new ArgumentException("Unsupported go parameter: " + words[i]);
                }
            if (limits.Perft != 0)
            {
                if (limits.Perft >= ChessConstants.MAX_PLY) throw new ArgumentOutOfRangeException(nameof(words), "Perft depth too large.");
                ulong total=MoveGeneration.Perft.CountRootMoves(position,limits.Perft,
                    (move,nodes)=>Write(UciNotation.Move(move,position.IsChess960)+": "+nodes));
                lastPerftNodes = total; Write("Nodes searched: " + total); return;
            }
            search.PvUpdated = (depth, roots, pvIndex, alpha, beta) => Report(depth, roots, pool.Nodes, pvIndex, alpha, beta);
            search.RootMoveStarted = (depth, move, number) => Write("info depth " + depth
                + " currmove " + UciNotation.Move(move, position.IsChess960) + " currmovenumber " + number);
            search.NoRootMoves = value => Write("info depth 0 score " + UciNotation.Value(value));
            search.Diagnostics = diagnostics;
            int searchContempt = contempt * (int)Value.PawnValueEg / 100;
            if (limits.Infinite != 0 || analysisMode)
            {
                if (string.Equals(analysisContempt,"Off",StringComparison.OrdinalIgnoreCase)) searchContempt = 0;
                else if ((string.Equals(analysisContempt,"White",StringComparison.OrdinalIgnoreCase) && position.SideToMove == Color.BLACK)
                    || (string.Equals(analysisContempt,"Black",StringComparison.OrdinalIgnoreCase) && position.SideToMove == Color.WHITE)) searchContempt = -searchContempt;
            }
            search.Start(position, limits, multiPv, searchContempt, minimumTime, overhead, slowMover, pondering, ponderOption, skillLevel, limitStrength, elo, nodesTime);
            waiter = new Thread(WaitForSearch) { IsBackground = true, Name = "UCI search completion" };
            waiter.Start();
        }
        private void Benchmark(string[] words)
        {
            StopAndWait();
            var commands = BenchmarkCommands.Create(position, words.Skip(1).ToArray());
            int totalPositions = commands.Count(c => c.StartsWith("go ", StringComparison.Ordinal) || c == "eval"), index = 0;
            long started = Now(); ulong nodes = 0;
            foreach (string command in commands)
            {
                if (command.StartsWith("go ", StringComparison.Ordinal) || command == "eval")
                    diagnostics("\nPosition: " + (++index) + "/" + totalPositions);
                ProcessCommand(command);
                if(command=="ucinewgame")started=Now();
                if (command.StartsWith("go ", StringComparison.Ordinal))
                {
                    bool perft = command.StartsWith("go perft ", StringComparison.Ordinal);
                    if (!perft) { WaitForCurrentSearch(); nodes += (ulong)pool.Nodes; }
                    else nodes += lastPerftNodes;
                }
            }
            long elapsed = Math.Max(1, Now() - started + 1);
            RuntimeStatistics.Shared.Print(diagnostics);
            diagnostics("\n===========================\nTotal time (ms) : " + elapsed + "\nNodes searched  : " + nodes + "\nNodes/second    : " + nodes*1000/(ulong)elapsed);
        }
        /// <summary>Waits for the active command without requesting a stop, for one-shot console commands.</summary>
        public void WaitForSearchCompletion()
        {
            if(disposed)throw new ObjectDisposedException(nameof(UciEngine));
            WaitForCurrentSearch();
        }
        private void WaitForCurrentSearch()
        {
            Thread? pending = waiter;
            if (pending == null) return;
            pending.Join(); waiter = null;
        }
        private void Report(int depth, RootMoves roots, long nodes, int pvIndex = -1, Value alpha = default, Value beta = default)
        {
            long elapsed = Math.Max(1, search.Elapsed + 1);
            for (int i = 0; i < Math.Min(multiPv, roots.Count); ++i)
            {
                RootMove root = roots[i];
                bool updated = root.Score != -Value.VALUE_INFINITE;
                if (depth == 1 && !updated) continue;
                int reportedDepth = updated ? depth : depth - 1;
                Value value = updated ? root.Score : root.PreviousScore;
                if (value <= -Value.VALUE_INFINITE || value >= Value.VALUE_INFINITE) continue;
                bool tablebase = search.TablebaseConfig.RootInTb && Math.Abs((int)value) < Value.VALUE_MATE - ChessConstants.MAX_PLY;
                Value score = tablebase ? root.TbScore : value;
                string bound = !tablebase && i == pvIndex ? value >= beta ? " lowerbound" : value <= alpha ? " upperbound" : "" : "";
                string pv = string.Join(" ", root.Pv.Select(m => UciNotation.Move(m, position.IsChess960)));
                long tbHits = pool.TbHits + (search.TablebaseConfig.RootInTb ? roots.Count : 0);
                Write("info depth " + reportedDepth + " seldepth " + root.SelDepth + " multipv " + (i + 1)
                    + " score " + UciNotation.Value(score) + bound + " nodes " + nodes + " nps " + (nodes * 1000 / elapsed)
                    + (elapsed > 1000 ? " hashfull " + table.Hashfull() : "")
                    + " tbhits " + tbHits + " time " + elapsed + " pv " + pv);
            }
        }
        private void WaitForSearch()
        {
            try
            {
                search.Wait();
                RootMoves roots = search.Search.RootMoves;
                if (roots.Count == 0)
                {
                    Write("bestmove (none)");
                }
                else
                {
                    if(!ReferenceEquals(search.Search,pool.Workers[0].Search))
                        Report(search.Search.CompletedDepth,roots,pool.Nodes);
                    RootMove best = roots[0];
                    if (best.Pv.Count == 1) best.ExtractPonderFromTt(search.MainRootPosition, table);
                    string reply = "bestmove " + UciNotation.Move(best.Pv[0], position.IsChess960);
                    if (best.Pv.Count > 1) reply += " ponder " + UciNotation.Move(best.Pv[1], position.IsChess960);
                    Write(reply);
                }
            }
            catch (Exception ex) { Write("info string search failed: " + ex.Message.Replace('\n', ' ')); Write("bestmove (none)"); }
        }
        private void StopAndWait()
        {
            Thread? pending = waiter;
            if (pending == null) return;
            search.Stop(); pending.Join(); waiter = null;
        }
        public void Dispose()
        {
            if (disposed) return;
            StopAndWait(); pool.Dispose(); tablebases.Dispose(); table.Dispose();
            lock (outputGate) { log?.Dispose(); log = null; }
            disposed = true;
        }
    }
}
