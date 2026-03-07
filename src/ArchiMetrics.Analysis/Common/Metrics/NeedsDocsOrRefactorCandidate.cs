namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System.Collections.Generic;

    public sealed class NeedsDocsOrRefactorCandidate
    {
        public NeedsDocsOrRefactorCandidate(
            string filePath,
            int lineNumber,
            int endLineNumber,
            string memberName,
            double opacityScore,
            double nameBodySimilarity,
            int cyclomaticComplexity,
            int nestingDepth,
            int magicLiteralCount,
            IReadOnlyList<string> reasons)
        {
            FilePath = filePath;
            LineNumber = lineNumber;
            EndLineNumber = endLineNumber;
            MemberName = memberName;
            OpacityScore = opacityScore;
            NameBodySimilarity = nameBodySimilarity;
            CyclomaticComplexity = cyclomaticComplexity;
            NestingDepth = nestingDepth;
            MagicLiteralCount = magicLiteralCount;
            Reasons = reasons;
        }

        public string FilePath { get; }

        public int LineNumber { get; }

        public int EndLineNumber { get; }

        public string MemberName { get; }

        /// <summary>
        /// Composite score 0..1 where higher means more opaque / harder to understand.
        /// </summary>
        public double OpacityScore { get; }

        /// <summary>
        /// Cosine similarity between the method name embedding and method body embedding.
        /// Low values mean the name does not describe what the code does.
        /// </summary>
        public double NameBodySimilarity { get; }

        public int CyclomaticComplexity { get; }

        public int NestingDepth { get; }

        public int MagicLiteralCount { get; }

        public IReadOnlyList<string> Reasons { get; }

        public override string ToString() =>
            $"{FilePath}:{LineNumber} {MemberName} (opacity={OpacityScore:F2})";
    }
}
