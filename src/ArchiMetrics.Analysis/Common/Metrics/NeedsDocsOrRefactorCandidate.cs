namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System.Collections.Generic;

    /// <summary>
    /// A method that is hard to understand from its name alone, with the evidence for that judgement.
    /// </summary>
    /// <remarks>
    /// The finding is that a reader cannot tell what this method does without reading all of it. It does not
    /// say which remedy applies: a clearer name, a documentation comment, or breaking the method up are all
    /// reasonable answers, and choosing between them needs a human who knows the intent. <see cref="Reasons"/>
    /// carries the specific signals so that choice can be made without re-deriving them.
    /// </remarks>
    public sealed class NeedsDocsOrRefactorCandidate
    {
        /// <summary>
        /// Initialises a candidate.
        /// </summary>
        /// <param name="filePath">Path to the file, relative to the configured root folder.</param>
        /// <param name="lineNumber">The line the member starts on.</param>
        /// <param name="endLineNumber">The line the member ends on.</param>
        /// <param name="memberName">The member's name.</param>
        /// <param name="opacityScore">The composite opacity score, 0.0 to 1.0.</param>
        /// <param name="nameBodySimilarity">Similarity between the name and the body, -1.0 to 1.0.</param>
        /// <param name="cyclomaticComplexity">The member's cyclomatic complexity.</param>
        /// <param name="nestingDepth">The deepest block nesting in the member.</param>
        /// <param name="magicLiteralCount">How many unexplained literals the member contains.</param>
        /// <param name="reasons">Human-readable explanations of what triggered the finding.</param>
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

        /// <summary>
        /// Gets the path to the file, relative to the root folder the agent was given.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the line the member starts on.
        /// </summary>
        public int LineNumber { get; }

        /// <summary>
        /// Gets the line the member ends on.
        /// </summary>
        public int EndLineNumber { get; }

        /// <summary>
        /// Gets the member's name — the thing being judged against what the body actually does.
        /// </summary>
        public string MemberName { get; }

        /// <summary>
        /// Gets the composite opacity score.
        /// </summary>
        /// <remarks>
        /// <b>Range: 0.0 to 1.0, where higher is worse.</b> Combines the name-body gap with complexity,
        /// nesting and unexplained literals. Use it to rank candidates rather than as an absolute verdict;
        /// what counts as too opaque depends on the codebase.
        /// </remarks>
        public double OpacityScore { get; }

        /// <summary>
        /// Gets the cosine similarity between the name's embedding and the body's.
        /// </summary>
        /// <remarks>
        /// <b>Range: -1.0 to 1.0, where higher is better.</b> A low value means the name is not describing
        /// the work, so a reader has no shortcut and must read the implementation.
        /// </remarks>
        public double NameBodySimilarity { get; }

        /// <summary>
        /// Gets the member's cyclomatic complexity, one of the inputs to <see cref="OpacityScore"/>.
        /// <b>Starts at 1; lower is better.</b>
        /// </summary>
        public int CyclomaticComplexity { get; }

        /// <summary>
        /// Gets the deepest block nesting in the member. <b>Range: 0 and up; lower is better.</b> Deep
        /// nesting forces a reader to hold several conditions in mind at once.
        /// </summary>
        public int NestingDepth { get; }

        /// <summary>
        /// Gets how many unexplained literal values the member contains. <b>Range: 0 and up; lower is
        /// better.</b> Each one is a number or string whose meaning lives only in the author's head.
        /// </summary>
        public int MagicLiteralCount { get; }

        /// <summary>
        /// Gets human-readable explanations of what triggered this finding, so the score can be acted on
        /// without re-deriving why it is high.
        /// </summary>
        public IReadOnlyList<string> Reasons { get; }

        /// <summary>
        /// Returns the location, member name and opacity score in a single line.
        /// </summary>
        /// <returns>A short description suitable for a log or list.</returns>
        public override string ToString() =>
            $"{FilePath}:{LineNumber} {MemberName} (opacity={OpacityScore:F2})";
    }
}
