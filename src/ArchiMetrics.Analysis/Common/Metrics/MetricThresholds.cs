namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// The single source of truth for what ArchiMetrics considers a good or bad metric value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every band boundary in the library lives here. That matters for more than tidiness: when
    /// thresholds are copy-pasted into each consumer, they drift, and the tool starts contradicting
    /// itself — a report calling a method "acceptable" while a review rule flags the very same
    /// method as unmaintainable. Callers that need to judge a value should call the <c>Rate*</c>
    /// methods rather than writing their own <c>if (mi &lt; 50)</c>, so that changing the library's
    /// opinion is a one-line edit in one file.
    /// </para>
    /// <para>
    /// The boundaries themselves are exposed as public constants so that consumers can document or
    /// display them (see <see cref="DescribeScales"/>) without duplicating the literals.
    /// </para>
    /// </remarks>
    public static class MetricThresholds
    {
        /// <summary>
        /// Bands for the maintainability index. <b>Higher is better</b>, so each constant is the
        /// <em>inclusive lower bound</em> of its band.
        /// </summary>
        public static class Maintainability
        {
            /// <summary>The lowest value the index can take. Values are clamped, never negative.</summary>
            public const double Minimum = 0.0;

            /// <summary>The highest value the index can take, awarded to trivial or empty members.</summary>
            public const double Maximum = 100.0;

            /// <summary>At or above this, the member is rated <see cref="MetricRating.Healthy"/>.</summary>
            public const double Healthy = 70.0;

            /// <summary>At or above this (but below <see cref="Healthy"/>), rated <see cref="MetricRating.Acceptable"/>.</summary>
            public const double Acceptable = 50.0;

            /// <summary>At or above this (but below <see cref="Acceptable"/>), rated <see cref="MetricRating.Concerning"/>.</summary>
            public const double Concerning = 30.0;

            /// <summary>At or above this (but below <see cref="Concerning"/>), rated <see cref="MetricRating.Problematic"/>.</summary>
            public const double Problematic = 15.0;

            /// <summary>
            /// The point at or below which the <c>TooLowMaintainabilityIndexRule</c> (AM0058) raises a
            /// violation. It sits inside the <see cref="MetricRating.Concerning"/> band: the rule is
            /// deliberately stricter than "not healthy" but more forgiving than "problematic", so that a
            /// method has to be meaningfully tangled before it is reported as a defect.
            /// </summary>
            public const int NeedsRefactoring = 40;
        }

        /// <summary>
        /// Bands for cyclomatic complexity — the number of linearly independent paths through a
        /// member. <b>Lower is better</b>, so each constant is the <em>inclusive upper bound</em> of
        /// its band. The value is at least 1 for any real member and has no upper limit.
        /// </summary>
        public static class CyclomaticComplexity
        {
            /// <summary>At or below this, rated <see cref="MetricRating.Healthy"/>.</summary>
            public const int Healthy = 10;

            /// <summary>At or below this, rated <see cref="MetricRating.Acceptable"/>.</summary>
            public const int Acceptable = 20;

            /// <summary>At or below this, rated <see cref="MetricRating.Concerning"/>.</summary>
            public const int Concerning = 30;

            /// <summary>At or below this, rated <see cref="MetricRating.Problematic"/>. Above it, <see cref="MetricRating.FixImmediately"/>.</summary>
            public const int Problematic = 50;
        }

        /// <summary>
        /// Bands for depth of inheritance — how many base types sit above a type. <b>Lower is
        /// better</b>, so each constant is the <em>inclusive upper bound</em> of its band. Deep
        /// hierarchies make behaviour hard to locate, because the code that runs may be several
        /// files away from the type you are reading.
        /// </summary>
        public static class DepthOfInheritance
        {
            /// <summary>At or below this, rated <see cref="MetricRating.Healthy"/>.</summary>
            public const int Healthy = 3;

            /// <summary>At or below this, rated <see cref="MetricRating.Acceptable"/>.</summary>
            public const int Acceptable = 5;

            /// <summary>At or below this, rated <see cref="MetricRating.Concerning"/>.</summary>
            public const int Concerning = 6;

            /// <summary>At or below this, rated <see cref="MetricRating.Problematic"/>. Above it, <see cref="MetricRating.FixImmediately"/>.</summary>
            public const int Problematic = 8;
        }

        /// <summary>
        /// Bands for efferent coupling — the number of other types this type depends on.
        /// <b>Lower is better</b>, so each constant is the <em>inclusive upper bound</em> of its
        /// band. A type with many outgoing dependencies has many reasons to change and is hard to
        /// test in isolation.
        /// </summary>
        public static class EfferentCoupling
        {
            /// <summary>At or below this, rated <see cref="MetricRating.Healthy"/>.</summary>
            public const int Healthy = 10;

            /// <summary>At or below this, rated <see cref="MetricRating.Acceptable"/>.</summary>
            public const int Acceptable = 20;

            /// <summary>At or below this, rated <see cref="MetricRating.Concerning"/>.</summary>
            public const int Concerning = 30;

            /// <summary>At or below this, rated <see cref="MetricRating.Problematic"/>. Above it, <see cref="MetricRating.FixImmediately"/>.</summary>
            public const int Problematic = 40;
        }

        /// <summary>
        /// Rates a maintainability index value. See <see cref="Maintainability"/> for the bands.
        /// </summary>
        /// <param name="maintainabilityIndex">A value on the 0-100 scale, where higher is better.</param>
        /// <returns>The band the value falls into.</returns>
        public static MetricRating RateMaintainability(double maintainabilityIndex)
        {
            if (maintainabilityIndex >= Maintainability.Healthy)
            {
                return MetricRating.Healthy;
            }

            if (maintainabilityIndex >= Maintainability.Acceptable)
            {
                return MetricRating.Acceptable;
            }

            if (maintainabilityIndex >= Maintainability.Concerning)
            {
                return MetricRating.Concerning;
            }

            return maintainabilityIndex >= Maintainability.Problematic
                       ? MetricRating.Problematic
                       : MetricRating.FixImmediately;
        }

        /// <summary>
        /// Rates a cyclomatic complexity value. See <see cref="CyclomaticComplexity"/> for the bands.
        /// </summary>
        /// <param name="cyclomaticComplexity">The number of independent paths; lower is better.</param>
        /// <returns>The band the value falls into.</returns>
        public static MetricRating RateCyclomaticComplexity(int cyclomaticComplexity)
        {
            if (cyclomaticComplexity <= CyclomaticComplexity.Healthy)
            {
                return MetricRating.Healthy;
            }

            if (cyclomaticComplexity <= CyclomaticComplexity.Acceptable)
            {
                return MetricRating.Acceptable;
            }

            if (cyclomaticComplexity <= CyclomaticComplexity.Concerning)
            {
                return MetricRating.Concerning;
            }

            return cyclomaticComplexity <= CyclomaticComplexity.Problematic
                       ? MetricRating.Problematic
                       : MetricRating.FixImmediately;
        }

        /// <summary>
        /// Rates a depth of inheritance value. See <see cref="DepthOfInheritance"/> for the bands.
        /// </summary>
        /// <param name="depthOfInheritance">The number of base types above the type; lower is better.</param>
        /// <returns>The band the value falls into.</returns>
        public static MetricRating RateDepthOfInheritance(int depthOfInheritance)
        {
            if (depthOfInheritance <= DepthOfInheritance.Healthy)
            {
                return MetricRating.Healthy;
            }

            if (depthOfInheritance <= DepthOfInheritance.Acceptable)
            {
                return MetricRating.Acceptable;
            }

            if (depthOfInheritance <= DepthOfInheritance.Concerning)
            {
                return MetricRating.Concerning;
            }

            return depthOfInheritance <= DepthOfInheritance.Problematic
                       ? MetricRating.Problematic
                       : MetricRating.FixImmediately;
        }

        /// <summary>
        /// Rates an efferent coupling value. See <see cref="EfferentCoupling"/> for the bands.
        /// </summary>
        /// <param name="efferentCoupling">The number of outgoing type dependencies; lower is better.</param>
        /// <returns>The band the value falls into.</returns>
        public static MetricRating RateEfferentCoupling(int efferentCoupling)
        {
            if (efferentCoupling <= EfferentCoupling.Healthy)
            {
                return MetricRating.Healthy;
            }

            if (efferentCoupling <= EfferentCoupling.Acceptable)
            {
                return MetricRating.Acceptable;
            }

            if (efferentCoupling <= EfferentCoupling.Concerning)
            {
                return MetricRating.Concerning;
            }

            return efferentCoupling <= EfferentCoupling.Problematic
                       ? MetricRating.Problematic
                       : MetricRating.FixImmediately;
        }

        /// <summary>
        /// Rates a type by its worst individual metric.
        /// </summary>
        /// <remarks>
        /// Taking the worst rather than the average is deliberate. Averaging lets a type hide one
        /// catastrophic method behind a dozen trivial properties, which is exactly the situation a
        /// health report exists to surface. A single unmaintainable method is a real cost to whoever
        /// has to change it, no matter how tidy the rest of the type is.
        /// </remarks>
        /// <param name="type">The type to rate.</param>
        /// <returns>The worst of the type's maintainability, complexity, inheritance depth and efferent coupling ratings.</returns>
        public static MetricRating RateType(ITypeMetric type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return Worst(
                new[]
                {
                    RateMaintainability(type.MaintainabilityIndex),
                    RateCyclomaticComplexity(type.CyclomaticComplexity),
                    RateDepthOfInheritance(type.DepthOfInheritance),
                    RateEfferentCoupling(type.EfferentCoupling)
                });
        }

        /// <summary>
        /// Reduces a set of ratings to the worst one, or <see cref="MetricRating.Healthy"/> when the
        /// set is empty. An empty set means "nothing to complain about" — an empty namespace should
        /// not be reported as broken.
        /// </summary>
        /// <param name="ratings">The ratings to reduce.</param>
        /// <returns>The highest (worst) rating present, or <see cref="MetricRating.Healthy"/> if there are none.</returns>
        public static MetricRating Worst(IEnumerable<MetricRating> ratings)
        {
            if (ratings == null)
            {
                throw new ArgumentNullException(nameof(ratings));
            }

            var worst = MetricRating.Healthy;
            foreach (var rating in ratings)
            {
                if (rating > worst)
                {
                    worst = rating;
                }
            }

            return worst;
        }

        /// <summary>
        /// Gets the human-readable label for a rating, for use in reports and summaries.
        /// </summary>
        /// <param name="rating">The rating to describe.</param>
        /// <returns>A short label such as <c>Healthy</c> or <c>Fix ASAP</c>.</returns>
        public static string Label(MetricRating rating)
        {
            switch (rating)
            {
                case MetricRating.Healthy: return "Healthy";
                case MetricRating.Acceptable: return "Acceptable";
                case MetricRating.Concerning: return "Concerning";
                case MetricRating.Problematic: return "Problematic";
                default: return "Fix ASAP";
            }
        }

        /// <summary>
        /// Builds a plain-text legend explaining every scale used in a metrics report.
        /// </summary>
        /// <remarks>
        /// A report that prints <c>[3 - Concerning]</c> and <c>Maintainability: 42</c> without this
        /// legend is close to unreadable: the reader cannot tell whether 3 is good, whether 42 is out
        /// of 100 or unbounded, or which of the underlying metrics produced the verdict. Emitting the
        /// legend alongside the numbers keeps the report self-describing, which matters most for
        /// automated consumers that see the text and nothing else.
        /// </remarks>
        /// <returns>A multi-line legend, terminated by a newline.</returns>
        public static string DescribeScales()
        {
            var ratings = string.Join(
                ", ",
                new[]
                {
                    MetricRating.Healthy,
                    MetricRating.Acceptable,
                    MetricRating.Concerning,
                    MetricRating.Problematic,
                    MetricRating.FixImmediately
                }.Select(r => $"{(int)r} = {Label(r)}"));

            var sb = new StringBuilder();
            sb.AppendLine("Scale legend");
            sb.AppendLine($"  Rating (in brackets): {ratings}. Lower is better.");
            sb.AppendLine("  A rating is the worst of the element's maintainability, complexity, inheritance depth and efferent coupling.");
            sb.AppendLine($"  Maintainability: {Maintainability.Minimum:F0}-{Maintainability.Maximum:F0}, higher is better (>= {Maintainability.Healthy:F0} healthy, < {Maintainability.Problematic:F0} critical).");
            sb.AppendLine($"  Complexity (cyclomatic): 1 and up, lower is better (<= {CyclomaticComplexity.Healthy} healthy, > {CyclomaticComplexity.Problematic} critical).");
            sb.AppendLine($"  Inheritance Depth: 0 and up, lower is better (<= {DepthOfInheritance.Healthy} healthy, > {DepthOfInheritance.Problematic} critical).");
            sb.AppendLine($"  Efferent Coupling: outgoing dependencies, lower is better (<= {EfferentCoupling.Healthy} healthy, > {EfferentCoupling.Problematic} critical).");
            sb.AppendLine("  Afferent Coupling: incoming dependencies, count only - no good or bad value.");
            sb.AppendLine("  Instability: 0.0-1.0, efferent / (afferent + efferent). 0 = stable and widely depended on, 1 = unstable and depends on many.");
            sb.AppendLine("  Lines: source lines carrying code, excluding blanks, comments and documentation. Count only - no good or bad value.");
            sb.AppendLine("  Statements: executable statements, unaffected by formatting. This is the size the maintainability index is built on.");
            sb.AppendLine("  Complexity and the two size figures are not comparable with the Visual Studio metrics of the same names.");

            return sb.ToString();
        }
    }
}
