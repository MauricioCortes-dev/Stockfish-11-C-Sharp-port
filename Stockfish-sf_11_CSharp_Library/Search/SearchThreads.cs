using System;
using System.Collections.Generic;
using System.Threading;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Evaluation;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>A parked managed worker with private evaluation and history caches.</summary>
    public sealed class SearchThread : IDisposable
    {
        private readonly object gate = new object();
        private readonly System.Threading.Thread thread;
        private Action<SearchThread, CancellationToken>? work;
        private CancellationToken cancellation;
        private bool busy, exiting, disposed;
        private Exception? error;
        private readonly SearchWorkerState state;
        private readonly Action onFailure;
        public int Index { get; }
        public EvaluationContext Evaluation { get; } = new EvaluationContext();
        public ButterflyHistory MainHistory { get; } = new ButterflyHistory();
        public CapturePieceToHistory CaptureHistory { get; } = new CapturePieceToHistory();
        public CounterMoveHistory CounterMoves => state.CounterMoves;
        public SearchWorkerState State => state;
        public SingleWorkerSearch Search { get; }
        public QuiescenceSearch Quiescence { get; }
        public long Nodes => state.Nodes;
        internal bool IsCurrent => System.Threading.Thread.CurrentThread == thread;

        internal SearchThread(int index, TranspositionTable table, Action onFailure)
        {
            this.onFailure=onFailure ?? throw new ArgumentNullException(nameof(onFailure));
            Index=index; state=new SearchWorkerState(Evaluation,table,MainHistory,CaptureHistory);
            Quiescence=new QuiescenceSearch(state); Search=new SingleWorkerSearch(state);
            // NativeThread requests 8 MiB on runtimes whose default stack is too small.
            // The CLR/OS rounds this portable stack-size request; it is not a resident buffer.
            thread=new System.Threading.Thread(IdleLoop, 8 * 1024 * 1024) { IsBackground=true, Name="Stockfish worker " + index };
            thread.Start();
        }
        public Value SearchCaptures(Position position, Value alpha, Value beta, Depth depth, bool pvNode, CancellationToken token)
            => Quiescence.Search(position,alpha,beta,depth,pvNode,token);
        internal void Start(Action<SearchThread,CancellationToken> action, CancellationToken token)
        {
            lock(gate)
            {
                if(disposed)throw new ObjectDisposedException(nameof(SearchThread));
                if(busy)throw new InvalidOperationException("Worker is already running.");
                error=null;state.Nodes=0;work=action;cancellation=token;busy=true;Monitor.PulseAll(gate);
            }
        }
        public void Wait()
        {
            if(System.Threading.Thread.CurrentThread==thread)throw new InvalidOperationException("A worker cannot wait for itself.");
            lock(gate)
            {
                while(busy)Monitor.Wait(gate);
                if(error!=null)throw new InvalidOperationException("Worker failed.",error);
            }
        }
        private void IdleLoop()
        {
            while(true)
            {
                Action<SearchThread,CancellationToken> action;CancellationToken token;
                lock(gate){while(!busy && !exiting)Monitor.Wait(gate);if(exiting)return;action=work!;token=cancellation;}
                Exception? failure=null;
                try{action(this,token);}
                catch(OperationCanceledException) when(token.IsCancellationRequested){}
                catch(Exception ex)
                {
                    failure=ex;
                    // A failed worker must release the others, including a main
                    // worker waiting for a stop or ponderhit that may never arrive.
                    try { onFailure(); }
                    catch(Exception signalError) { failure=new AggregateException(ex,signalError); }
                }
                lock(gate){error=failure;work=null;busy=false;Monitor.PulseAll(gate);}
            }
        }
        public void Dispose()
        {
            if(System.Threading.Thread.CurrentThread==thread)throw new InvalidOperationException("A worker cannot join itself.");
            lock(gate){if(disposed)return;while(busy)Monitor.Wait(gate);disposed=true;exiting=true;Monitor.PulseAll(gate);}
            thread.Join();
        }
    }

    /// <summary>Owns workers and cooperative stopping. Search orchestration is supplied by the caller.</summary>
    public sealed class SearchThreadPool : IDisposable
    {
        private readonly List<SearchThread> workers = new List<SearchThread>();
        private readonly object gate = new object();
        private readonly SearchBreadcrumbs breadcrumbs = new SearchBreadcrumbs();
        private CancellationTokenSource? cancellation;
        private bool disposed, running;
        public IReadOnlyList<SearchThread> Workers => workers;
        public long Nodes { get { long sum=0;foreach(SearchThread worker in workers)sum+=worker.Nodes;return sum; } }
        public long TbHits { get { long sum=0;foreach(SearchThread worker in workers)sum+=worker.State.TbHits;return sum; } }
        public SearchThreadPool(int count, TranspositionTable table)
        {
            if(count<1)throw new ArgumentOutOfRangeException(nameof(count));
            if(table==null)throw new ArgumentNullException(nameof(table));
            try{for(int i=0;i<count;++i){var worker=new SearchThread(i,table,Stop);worker.Search.Breadcrumbs=breadcrumbs;workers.Add(worker);}}
            catch{foreach(SearchThread worker in workers)worker.Dispose();throw;}
        }
        public void Start(Action<SearchThread,CancellationToken> action, bool mainLast = false, Action? prepare = null)
        {
            if(action==null)throw new ArgumentNullException(nameof(action));
            lock(gate)
            {
                if(disposed)throw new ObjectDisposedException(nameof(SearchThreadPool));
                if(running)throw new InvalidOperationException("Wait for the preceding search first.");
                prepare?.Invoke();
                cancellation?.Dispose();cancellation=new CancellationTokenSource();running=true;
                if(mainLast)
                {
                    for(int i=1;i<workers.Count;++i)workers[i].Start(action,cancellation.Token);
                    workers[0].Start(action,cancellation.Token);
                }
                else foreach(SearchThread worker in workers)worker.Start(action,cancellation.Token);
            }
        }
        public void Stop()
        {
            CancellationTokenSource? source;
            lock(gate) source=cancellation;
            try{source?.Cancel();}catch(ObjectDisposedException) { }
        }
        public void Wait()
        {
            foreach(SearchThread worker in workers)if(worker.IsCurrent)throw new InvalidOperationException("A worker cannot wait for its own pool.");
            Exception? failure=null;
            foreach(SearchThread worker in workers)try{worker.Wait();}catch(Exception ex){if(failure==null)failure=ex;}
            lock(gate)running=false;
            if(failure!=null)throw new InvalidOperationException("A worker failed.",failure);
        }
        public void Dispose()
        {
            foreach(SearchThread worker in workers)if(worker.IsCurrent)throw new InvalidOperationException("A worker cannot dispose its own pool.");
            lock(gate){if(disposed)return;disposed=true;}
            Stop();
            foreach(SearchThread worker in workers)worker.Dispose();
            lock(gate){cancellation?.Dispose();running=false;}
        }
    }
}
