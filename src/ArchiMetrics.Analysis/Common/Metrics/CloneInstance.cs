namespace ArchiMetrics.Analysis.Common.Metrics
{
    /// <summary>
    /// One copy of duplicated code — a single member that appears in a <see cref="CloneClass"/>.
    /// </summary>
    public sealed class CloneInstance
    {
        /// <summary>
        /// Initialises a clone instance.
        /// </summary>
        /// <param name="filePath">Path to the file, relative to the configured root folder.</param>
        /// <param name="lineNumber">The line the member starts on.</param>
        /// <param name="endLineNumber">The line the member ends on.</param>
        /// <param name="memberName">The member's name.</param>
        /// <param name="normalizedText">The member's normalised source.</param>
        public CloneInstance(string filePath, int lineNumber, int endLineNumber, string memberName, string normalizedText)
        {
            FilePath = filePath;
            LineNumber = lineNumber;
            EndLineNumber = endLineNumber;
            MemberName = memberName;
            NormalizedText = normalizedText;
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
        /// Gets the member's name.
        /// </summary>
        public string MemberName { get; }

        /// <summary>
        /// Gets the member's source with formatting, comments and identifier names stripped out.
        /// </summary>
        /// <remarks>
        /// This is the form the clone was actually matched on, which is why two instances with quite
        /// different-looking source can end up in the same class. Compare it against a sibling instance to
        /// see what the detector considered equivalent before deciding whether merging them is wise.
        /// </remarks>
        public string NormalizedText { get; }

        /// <summary>
        /// Returns the location and member name in a single line.
        /// </summary>
        /// <returns>A short description suitable for a log or list.</returns>
        public override string ToString() => $"{FilePath}:{LineNumber} ({MemberName})";
    }
}
