using System;
using System.Runtime.CompilerServices;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Infrastructure;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Evaluation
{
    /// <summary>Material data stored in a worker's direct-mapped cache.</summary>
    public sealed class MaterialEntry
    {
        public Key Key { get; internal set; }
        public short Value { get; internal set; }
        public Phase GamePhase { get; internal set; }
        public EndgameValue? EvaluationFunction { get; internal set; }
        // The original stores these pairs inline in its entry. Keep cache-slot
        // identity, but avoid two separate heap objects for every material slot.
        private EndgameScale? whiteScaling, blackScaling;
        private byte whiteFactor, blackFactor;
        public Score Imbalance => Score.MakeScore(Value,Value);
        public bool SpecializedEvalExists => EvaluationFunction != null;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EndgameScale? ScalingFunction(Color color) => (int)color switch
        {
            0 => whiteScaling, 1 => blackScaling,
            _ => throw new IndexOutOfRangeException()
        };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Factor(Color color) => (int)color switch
        {
            0 => whiteFactor, 1 => blackFactor,
            _ => throw new IndexOutOfRangeException()
        };
        public Value Evaluate(Position position) => (EvaluationFunction ?? throw new InvalidOperationException("No specialized evaluation.")).Evaluate(position);
        public ScaleFactor ScaleFactor(Position position, Color color)
        {
            ScaleFactor result = ScalingFunction(color)?.Evaluate(position) ?? Types.ScaleFactor.SCALE_FACTOR_NONE;
            return result != Types.ScaleFactor.SCALE_FACTOR_NONE ? result : (ScaleFactor)Factor(color);
        }
        internal void Reset(Key key)
        {
            Key=key;Value=0;GamePhase=0;EvaluationFunction=null;
            whiteScaling=blackScaling=null;whiteFactor=blackFactor=(byte)(int)Types.ScaleFactor.SCALE_FACTOR_NORMAL;
        }
        internal void SetScaling(Color color, EndgameScale function)
        {
            switch((int)color)
            {
                case 0: whiteScaling=function;break;
                case 1: blackScaling=function;break;
                default: throw new IndexOutOfRangeException();
            }
        }
        internal void SetFactor(Color color, int factor)
        {
            byte value=checked((byte)factor);
            switch((int)color)
            {
                case 0: whiteFactor=value;break;
                case 1: blackFactor=value;break;
                default: throw new IndexOutOfRangeException();
            }
        }
    }

    /// <summary>Private evaluation caches owned by one worker, not shared across workers.</summary>
    public sealed class EvaluationContext
    {
        internal readonly HashTable<MaterialEntry> MaterialTable = new HashTable<MaterialEntry>(8192);
        internal readonly HashTable<PawnEntry> PawnTable = new HashTable<PawnEntry>(131072);
        private EvaluationAttacks? attacks;
        internal bool AttacksInUse { get; private set; }
        // Evaluation has no recursive calls or callbacks. Its maps stop being
        // live before search enters a child; one workspace per context suffices.
        internal EvaluationAttacks AcquireAttacks(Position position,PawnEntry pawns)
        {
            if(AttacksInUse)throw new InvalidOperationException("Evaluation maps are already in use by this context.");
            if(attacks==null)attacks=new EvaluationAttacks();
            attacks.Reset(position,pawns);AttacksInUse=true;return attacks;
        }
        internal void ReleaseAttacks()
        {
            if(!AttacksInUse)throw new InvalidOperationException("No evaluation maps are borrowed.");
            attacks!.ClearPosition();AttacksInUse=false;
        }
        public MaterialEntry ProbeMaterial(Position position) => Material.Probe(position,this);
        public ref PawnEntry ProbePawns(Position position) => ref Pawns.Probe(position,this);
    }

    public static class Material
    {
        // Like the original material.cpp, generic evaluators are immutable and
        // shared by material entries; only their owning cache slots are mutable.
        private static readonly EndgameValue[] EvaluateKXK={new EndgameValue(EndgameCode.KXK,Color.WHITE),new EndgameValue(EndgameCode.KXK,Color.BLACK)};
        private static readonly EndgameScale[] ScaleKBPsK={new EndgameScale(EndgameCode.KBPsK,Color.WHITE),new EndgameScale(EndgameCode.KBPsK,Color.BLACK)};
        private static readonly EndgameScale[] ScaleKQKRPs={new EndgameScale(EndgameCode.KQKRPs,Color.WHITE),new EndgameScale(EndgameCode.KQKRPs,Color.BLACK)};
        private static readonly EndgameScale[] ScaleKPsK={new EndgameScale(EndgameCode.KPsK,Color.WHITE),new EndgameScale(EndgameCode.KPsK,Color.BLACK)};
        private static readonly EndgameScale[] ScaleKPKP={new EndgameScale(EndgameCode.KPKP,Color.WHITE),new EndgameScale(EndgameCode.KPKP,Color.BLACK)};
        private static readonly int[,] Ours={{1438,0,0,0,0,0},{40,38,0,0,0,0},{32,255,-62,0,0,0},{0,104,4,0,0,0},{-26,-2,47,105,-208,0},{-189,24,117,133,-134,-6}};
        private static readonly int[,] Theirs={{0,0,0,0,0,0},{36,0,0,0,0,0},{9,63,0,0,0,0},{59,65,42,0,0,0},{46,39,24,-24,0,0},{97,100,-42,137,268,0}};
        private static int Imbalance(ReadOnlySpan<int> count,int us)
        {
            int bonus=0;
            for(int a=0;a<=5;++a)
            {
                if(count[us*6+a]==0)continue;
                int v=0;
                for(int b=0;b<=a;++b)v+=Ours[a,b]*count[us*6+b]+Theirs[a,b]*count[(us^1)*6+b];
                bonus+=count[us*6+a]*v;
            }
            return bonus;
        }
        public static MaterialEntry Probe(Position position, EvaluationContext context)
        {
            if(position==null)throw new ArgumentNullException(nameof(position));
            if(context==null)throw new ArgumentNullException(nameof(context));
            Key key=position.MaterialKey;MaterialEntry entry=context.MaterialTable[key];
            if((ulong)entry.Key==(ulong)key)return entry;
            entry.Reset(key);
            int white=position.NonPawnMaterial(Color.WHITE),black=position.NonPawnMaterial(Color.BLACK);
            int npm=Math.Max((int)Value.EndgameLimit,Math.Min(white+black,(int)Value.MidgameLimit));
            entry.GamePhase=(npm-(int)Value.EndgameLimit)*(int)Phase.PHASE_MIDGAME/((int)Value.MidgameLimit-(int)Value.EndgameLimit);
            entry.EvaluationFunction=Endgames.Probe(key);
            if(entry.SpecializedEvalExists)return entry;
            for(int c=0;c<2;++c)
            {
                Color us=c;
                if(!Bitboard.MoreThanOne(position.Pieces(~us)) && position.NonPawnMaterial(us)>=Value.RookValueMg)
                {entry.EvaluationFunction=EvaluateKXK[us];return entry;}
            }
            EndgameScale? special=Endgames.ProbeScale(key);
            if(special!=null){entry.SetScaling(special.StrongSide,special);return entry;}
            for(int c=0;c<2;++c)
            {
                Color us=c,them=~us; int own=position.NonPawnMaterial(us);
                if(own==Value.BishopValueMg && position.Count(us,PieceType.PAWN)>=1)
                    entry.SetScaling(us,ScaleKBPsK[us]);
                else if(position.Count(us,PieceType.PAWN)==0 && own==Value.QueenValueMg && position.Count(them,PieceType.ROOK)==1 && position.Count(them,PieceType.PAWN)>=1)
                    entry.SetScaling(us,ScaleKQKRPs[us]);
            }
            int wp=position.Count(Color.WHITE,PieceType.PAWN),bp=position.Count(Color.BLACK,PieceType.PAWN);
            if(white+black==0 && (bool)position.Pieces(PieceType.PAWN))
            {
                if(bp==0)entry.SetScaling(Color.WHITE,ScaleKPsK[Color.WHITE]);
                else if(wp==0)entry.SetScaling(Color.BLACK,ScaleKPsK[Color.BLACK]);
                else if(wp==1 && bp==1){entry.SetScaling(Color.WHITE,ScaleKPKP[Color.WHITE]);entry.SetScaling(Color.BLACK,ScaleKPKP[Color.BLACK]);}
            }
            if(wp==0 && white-black<=Value.BishopValueMg)entry.SetFactor(Color.WHITE,white<Value.RookValueMg?(int)Types.ScaleFactor.SCALE_FACTOR_DRAW:black<=Value.BishopValueMg?4:14);
            if(bp==0 && black-white<=Value.BishopValueMg)entry.SetFactor(Color.BLACK,black<Value.RookValueMg?(int)Types.ScaleFactor.SCALE_FACTOR_DRAW:white<=Value.BishopValueMg?4:14);
            Span<int> counts=stackalloc int[2*6];
            for(int c=0;c<2;++c){counts[c*6]=position.Count(c,PieceType.BISHOP)>1?1:0;for(int pt=1;pt<=5;++pt)counts[c*6+pt]=position.Count(c,pt);}
            entry.Value=unchecked((short)((Imbalance(counts,0)-Imbalance(counts,1))/16));
            return entry;
        }
    }
}
