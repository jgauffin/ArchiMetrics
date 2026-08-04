// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ICodeMetric.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ICodeMetric type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System.Collections.Generic;

    /// <summary>
	/// Defines the base interface for types providing code metric values.
	/// </summary>
	public interface ICodeMetric
	{
		/// <summary>
		/// Gets the type couplings for the members.
		/// </summary>
		IEnumerable<ITypeCoupling> Dependencies { get; }

		/// <summary>
		/// Gets the lines of code.
		/// </summary>
		/// <remarks>
		/// <para><b>Range: 0 and up.</b> A size measure, not a quality measure — there is no good or bad
		/// value and no rating is derived from it.</para>
		/// <para>These are real source lines: the lines the element occupies that carry code. Blank lines
		/// and comment-only lines are excluded, and so is documentation, so commenting a member does not
		/// make it measure larger. Lines holding only a brace are included, because a reader still scrolls
		/// past them.</para>
		/// <para>At type level the figure is taken from the type's own declaration, so it includes the
		/// declaration line, the fields and the braces between members — it is therefore larger than the
		/// sum of its members' values, and that is intended. Namespace and project figures are the sums of
		/// the types beneath them.</para>
		/// <para>Use this for anything the reader experiences as bulk. For the size input to the
		/// maintainability index, see <see cref="ExecutableStatements"/> — a figure that formatting cannot
		/// move.</para>
		/// </remarks>
		int LinesOfCode { get; }

		/// <summary>
		/// Gets the number of executable statements — the units of work the code performs.
		/// </summary>
		/// <remarks>
		/// <para><b>Range: 0 and up.</b> Like <see cref="LinesOfCode"/> this is a size measure with no good
		/// or bad value, but it counts syntax constructs rather than text. Formatting cannot change it: a
		/// statement wrapped over five lines still counts once, and braces and comments count for nothing.
		/// A member with no body at all — an interface method, an abstract declaration — scores 0.</para>
		/// <para>That insensitivity to layout is why this, and not <see cref="LinesOfCode"/>, is the size
		/// term in <see cref="MaintainabilityIndex"/> and the weight used when rolling member scores up into
		/// type, namespace and project figures. A metric that moved when only the whitespace changed would
		/// be worse than no metric.</para>
		/// <para>Counted are statements (expression, if, switch, loops, using, lock, throw, yield, labelled,
		/// goto, checked, fixed, unsafe and empty statements), non-<c>const</c> local declarations,
		/// <c>return</c> only when it returns a value, an initializer as a single statement however many
		/// elements it holds, constructor declarations and expression-bodied accessors.</para>
		/// <para>The result is not comparable with the Visual Studio metric of the same name. Use it to
		/// compare elements within a solution, not across tools.</para>
		/// </remarks>
		int ExecutableStatements { get; }

		/// <summary>
		/// Gets the maintainability index.
		/// </summary>
		/// <remarks>
		/// <para><b>Range: 0 to 100, where higher is better.</b> This is the normalised form of the
		/// index, not the raw Halstead-based formula that can go negative: results are scaled by
		/// <c>100 / 171</c> and clamped at 0, and a member with no measurable body scores 100.</para>
		/// <para>The formula is
		/// <c>MAX(0, (171 - 5.2 * ln(HalsteadVolume) - 0.23 * CyclomaticComplexity - 16.2 * ln(LinesOfCode)) * 100 / 171)</c>,
		/// so the index falls as a member grows in size, branching or vocabulary. It is a rough
		/// indicator of how hard a member is to read and change, and is best used to rank members
		/// against each other rather than as an absolute score.</para>
		/// <para>For the band boundaries used across the library — healthy, concerning, and so on —
		/// see <see cref="MetricThresholds.Maintainability"/>, and use
		/// <see cref="MetricThresholds.RateMaintainability"/> to classify a value rather than
		/// hard-coding comparisons.</para>
		/// </remarks>
		double MaintainabilityIndex { get; }

		/// <summary>
		/// Gets the cyclomatic complexity.
		/// </summary>
		/// <remarks>
		/// <para><b>Range: 1 and up, where lower is better.</b> There is no upper limit. The value counts
		/// the linearly independent paths through the code: one for the body itself, plus one at every
		/// point where control flow can go more than one way.</para>
		/// <para>Counted are <c>if</c>, all four loops, <c>case</c> labels (but not <c>default:</c>, which
		/// is the fall-through), <c>catch</c> clauses and their <c>when</c> filters, the short-circuiting
		/// <c>&amp;&amp;</c> and <c>||</c>, <c>??</c> and <c>??=</c>, <c>?:</c>, null-conditional access,
		/// each non-discard arm of a switch expression, and the <c>and</c>/<c>or</c> pattern combinators.
		/// Branches inside a lambda count towards the member that contains it.</para>
		/// <para>Deliberately <em>not</em> counted: negation, <c>default(T)</c>, <c>continue</c> and
		/// <c>goto</c>. None of them chooses between paths, and counting them penalised guard clauses —
		/// usually the more readable way to write the same logic.</para>
		/// <para>The value is a lower bound on the number of test cases needed for full branch coverage,
		/// which is why high values are a testability problem rather than a style complaint. At type,
		/// namespace and project level it is aggregated from the members beneath, so it grows with size as
		/// well as with branching — compare elements of similar size.</para>
		/// <para>See <see cref="MetricThresholds.CyclomaticComplexity"/> for the band boundaries. The
		/// result is not comparable with the Visual Studio metric of the same name.</para>
		/// </remarks>
		int CyclomaticComplexity { get; }

		/// <summary>
		/// Gets the name of the instance the metrics are related to.
		/// </summary>
		string Name { get; }
	}
}