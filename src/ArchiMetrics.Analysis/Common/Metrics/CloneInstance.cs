namespace ArchiMetrics.Analysis.Common.Metrics
{
    public sealed class CloneInstance
    {
        public CloneInstance(string filePath, int lineNumber, int endLineNumber, string memberName, string normalizedText)
        {
            FilePath = filePath;
            LineNumber = lineNumber;
            EndLineNumber = endLineNumber;
            MemberName = memberName;
            NormalizedText = normalizedText;
        }

        public string FilePath { get; }

        public int LineNumber { get; }

        public int EndLineNumber { get; }

        public string MemberName { get; }

        public string NormalizedText { get; }

        public override string ToString() => $"{FilePath}:{LineNumber} ({MemberName})";
    }
}
