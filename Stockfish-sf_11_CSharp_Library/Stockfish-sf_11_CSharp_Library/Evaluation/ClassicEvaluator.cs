using System;
using System.Globalization;
using System.Text;
using Stockfish_sf_11_CSharp_Library.Board;
using Stockfish_sf_11_CSharp_Library.Types;

namespace Stockfish_sf_11_CSharp_Library.Evaluation
{
    /// <summary>Stockfish 11 classic static evaluation. Context is owned by one worker.</summary>
    public static class ClassicEvaluator
    {
        private const int Material = 8, Imbalance = 9, Mobility = 10, Threat = 11,
            Passed = 12, Space = 13, Initiative = 14, Total = 15;
        private static readonly string[] TraceNames = { "    Material", "   Imbalance", "       Pawns", "     Knights", "     Bishops", "       Rooks", "      Queens", "    Mobility", " King safety", "     Threats", "      Passed", "       Space", "  Initiative" };
        private static readonly int[] TraceIndices = { Material, Imbalance, 1, 2, 3, 4, 5, Mobility, 6, Threat, Passed, Space, Initiative };

        public static Value Evaluate(Position position, EvaluationContext context, Score contempt)
            => EvaluateCore(position, context, contempt, null);

        public static Value Evaluate(Position position, EvaluationContext context)
            => Evaluate(position, context, Score.SCORE_ZERO);

        /// <summary>Evaluates with zero contempt and returns the unrounded trace scores by term and color.</summary>
        public static Value EvaluateWithTrace(Position position, EvaluationContext context, out Score[,] terms)
        {
            terms = new Score[16, 2];
            return EvaluateCore(position, context, Score.SCORE_ZERO, terms);
        }

        /// <summary>Returns the original white-oriented diagnostic trace with zero contempt.</summary>
        public static string Trace(Position position, EvaluationContext context)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (position.Checkers) return "Total evaluation: none (in check)";
            Value value = EvaluateWithTrace(position, context, out Score[,] terms);
            if (position.SideToMove == Color.BLACK) value = -value;
            var result = new StringBuilder();
            result.Append("     Term    |    White    |    Black    |    Total   \n")
                .Append("             |   MG    EG  |   MG    EG  |   MG    EG \n")
                .Append(" ------------+-------------+-------------+------------\n");
            for (int i = 0; i < TraceIndices.Length; ++i) AppendTerm(result, TraceNames[i], TraceIndices[i], terms);
            result.Append(" ------------+-------------+-------------+------------\n");
            AppendTerm(result, "       Total", Total, terms);
            result.Append("\nTotal evaluation: ").Append(((double)(int)value / (int)Value.PawnValueEg).ToString("F2", CultureInfo.InvariantCulture)).Append(" (white side)\n");
            return result.ToString();
        }

        private static void AppendTerm(StringBuilder output, string name, int term, Score[,] scores)
        {
            output.Append(name).Append(" | ");
            if (term == Material || term == Imbalance || term == Initiative || term == Total)
                output.Append(" ----  ---- |  ----  ----");
            else
            {
                AppendScore(output, scores[term, 0]);
                output.Append(" | ");
                AppendScore(output, scores[term, 1]);
            }
            output.Append(" | ");
            AppendScore(output, scores[term, 0] - scores[term, 1]);
            output.Append('\n');
        }

        private static void AppendScore(StringBuilder output, Score score)
        {
            output.Append(((double)(int)Score.MgValue(score) / (int)Value.PawnValueEg).ToString("F2", CultureInfo.InvariantCulture).PadLeft(5))
                .Append(' ').Append(((double)(int)Score.EgValue(score) / (int)Value.PawnValueEg).ToString("F2", CultureInfo.InvariantCulture).PadLeft(5));
        }

        private static Value EvaluateCore(Position position, EvaluationContext context, Score contempt, Score[,]? terms)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (position.Checkers) throw new InvalidOperationException("Evaluation requires a position not in check.");
            MaterialEntry material = context.ProbeMaterial(position);
            if (material.SpecializedEvalExists) return material.Evaluate(position);
            ref PawnEntry pawns = ref context.ProbePawns(position);
            Score score = position.PsqScore + material.Imbalance + contempt + pawns.PawnScore(Color.WHITE) - pawns.PawnScore(Color.BLACK);
            int average = ((int)Score.MgValue(score) + (int)Score.EgValue(score)) / 2;
            if (Math.Abs(average) > 1400 + (int)position.TotalNonPawnMaterial / 64)
                return position.SideToMove == Color.WHITE ? average : -average;

            var attacks = context.AcquireAttacks(position, pawns);
            try
            {
                attacks.Initialize(Color.WHITE);
                attacks.Initialize(Color.BLACK);
                for (int piece = 2; piece <= 5; ++piece)
                {
                    Score white = attacks.Pieces(Color.WHITE, piece), black = attacks.Pieces(Color.BLACK, piece);
                    score += white - black;
                    if (terms != null) { terms[piece, 0] = white; terms[piece, 1] = black; }
                }
                score += attacks.Mobility(Color.WHITE) - attacks.Mobility(Color.BLACK);
                Score whiteKing = attacks.King(Color.WHITE, ref pawns), blackKing = attacks.King(Color.BLACK, ref pawns);
                Score whiteThreat = attacks.Threats(Color.WHITE), blackThreat = attacks.Threats(Color.BLACK);
                Score whitePassed = attacks.Passed(Color.WHITE), blackPassed = attacks.Passed(Color.BLACK);
                Score whiteSpace = ClassicEvaluationTerms.Space(position, Color.WHITE, attacks.AttackedBy(Color.BLACK, PieceType.PAWN), attacks.AttackedBy(Color.BLACK, PieceType.ALL_PIECES));
                Score blackSpace = ClassicEvaluationTerms.Space(position, Color.BLACK, attacks.AttackedBy(Color.WHITE, PieceType.PAWN), attacks.AttackedBy(Color.WHITE, PieceType.ALL_PIECES));
                score += whiteKing - blackKing + whiteThreat - blackThreat + whitePassed - blackPassed + whiteSpace - blackSpace;
                Score initiative = ClassicEvaluationTerms.Initiative(position, pawns, score);
                score += initiative;
                if (terms != null)
                {
                    terms[Material, 0] = position.PsqScore; terms[Imbalance, 0] = material.Imbalance;
                    terms[1, 0] = pawns.PawnScore(Color.WHITE); terms[1, 1] = pawns.PawnScore(Color.BLACK);
                    terms[Mobility, 0] = attacks.Mobility(Color.WHITE); terms[Mobility, 1] = attacks.Mobility(Color.BLACK);
                    terms[6, 0] = whiteKing; terms[6, 1] = blackKing;
                    terms[Threat, 0] = whiteThreat; terms[Threat, 1] = blackThreat;
                    terms[Passed, 0] = whitePassed; terms[Passed, 1] = blackPassed;
                    terms[Space, 0] = whiteSpace; terms[Space, 1] = blackSpace;
                    terms[Initiative, 0] = initiative; terms[Total, 0] = score;
                }
                return ClassicEvaluationTerms.Interpolate(position, material, score);
            }
            finally { context.ReleaseAttacks(); }
        }
    }
}
