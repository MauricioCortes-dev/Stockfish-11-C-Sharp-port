using System;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Search
{
    /// <summary>Root aspiration window and fail-high/low adjustments from Stockfish 11.</summary>
    public sealed class AspirationWindow
    {
        public Value Alpha { get; private set; }
        public Value Beta { get; private set; }
        public Value Delta { get; private set; }
        public int FailedHighCount { get; private set; }
        public Score Contempt { get; }
        public AspirationWindow(int depth, Value previousScore, Value alpha, Value beta, Value delta, int contempt, Color side)
        {
            if(depth<1 || depth>=ChessConstants.MAX_PLY)throw new ArgumentOutOfRangeException(nameof(depth));
            int dynamicContempt=contempt;
            if(depth>=4)
            {
                delta=21+Math.Abs((int)previousScore)/256;
                alpha=Math.Max((int)previousScore-(int)delta,-(int)Value.VALUE_INFINITE);
                beta=Math.Min((int)previousScore+(int)delta,(int)Value.VALUE_INFINITE);
                dynamicContempt+= (102-contempt/2)*(int)previousScore/(Math.Abs((int)previousScore)+157);
            }
            Alpha=alpha;Beta=beta;Delta=delta;
            Score score=Score.MakeScore(dynamicContempt,dynamicContempt/2);
            Contempt=side==Color.WHITE?score:-score;
        }
        public int AdjustedDepth(int rootDepth,int searchAgainCount)=>Math.Max(1,rootDepth-FailedHighCount-searchAgainCount);
        /// <summary>Returns true when the score is inside the window; otherwise expands it for a retry.</summary>
        public bool Accept(Value result)
        {
            if(result<=Alpha)
            {
                Beta=((int)Alpha+(int)Beta)/2;
                Alpha=Math.Max((int)result-(int)Delta,-(int)Value.VALUE_INFINITE);
                FailedHighCount=0;
            }
            else if(result>=Beta)
            {
                Beta=Math.Min((int)result+(int)Delta,(int)Value.VALUE_INFINITE);
                ++FailedHighCount;
            }
            else return true;
            Delta+=(int)Delta/4+5;
            return false;
        }
    }
}
