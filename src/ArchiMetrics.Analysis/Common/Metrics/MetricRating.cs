namespace ArchiMetrics.Analysis.Common.Metrics
{
    /// <summary>
    /// A coarse health verdict for a single metric value, or for a code element as a whole.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Raw metric values are hard to act on because every metric has its own scale and its own
    /// direction: a maintainability index of 65 is mediocre (higher is better, out of 100), while
    /// a cyclomatic complexity of 65 is dreadful (lower is better, unbounded). Ratings translate
    /// all of them onto one 1-to-5 scale so that a reader — human or agent — can compare a
    /// namespace's complexity against its coupling without memorising four different rulebooks.
    /// </para>
    /// <para>
    /// <b>Lower is better.</b> The numbering deliberately runs the opposite way from the
    /// maintainability index, so never present a rating and a raw metric as if they were the same
    /// kind of number. Use <see cref="MetricThresholds.Label(MetricRating)"/> when rendering a
    /// rating for a reader — the word carries the direction, the bare digit does not.
    /// </para>
    /// </remarks>
    public enum MetricRating
    {
        /// <summary>No action needed; the value sits in the range typical of well-factored code.</summary>
        Healthy = 1,

        /// <summary>Slightly past ideal, but not worth refactoring on its own.</summary>
        Acceptable = 2,

        /// <summary>Worth a look. Often the first visible sign that a type is accreting responsibilities.</summary>
        Concerning = 3,

        /// <summary>Actively costly to work with. Changes here are likely to be slow and to introduce defects.</summary>
        Problematic = 4,

        /// <summary>Severe. Treat as a defect in its own right rather than as a style preference.</summary>
        FixImmediately = 5,
    }
}
