using System;
using System.Globalization;
using System.Threading;

namespace Stockfish_sf_11_CSharp_Library.Infrastructure
{
    /// <summary>Stockfish's optional process-wide debugging counters. They do not affect evaluation.</summary>
    public sealed class RuntimeStatistics
    {
        public static RuntimeStatistics Shared { get; } = new RuntimeStatistics();
        private long hitCount, hits, meanCount, sum;
        public void HitOn(bool hit) { Interlocked.Increment(ref hitCount); if(hit) Interlocked.Increment(ref hits); }
        public void HitOn(bool condition, bool hit) { if(condition) HitOn(hit); }
        public void MeanOf(int value) { Interlocked.Increment(ref meanCount); Interlocked.Add(ref sum,value); }
        public string Format()
        {
            long count=Interlocked.Read(ref hitCount), successful=Interlocked.Read(ref hits);
            string text=count==0?"":"Total "+count+" Hits "+successful+" hit rate (%) "+unchecked(100*successful)/count+"\n";
            count=Interlocked.Read(ref meanCount);
            if(count!=0) text+="Total "+count+" Mean "+((double)Interlocked.Read(ref sum)/count).ToString("g6",CultureInfo.InvariantCulture)+"\n";
            return text;
        }
        public void Print(Action<string> output)
        {
            if(output==null)throw new ArgumentNullException(nameof(output));
            string text=Format();if(text.Length!=0)output(text.TrimEnd('\n'));
        }
    }
}
