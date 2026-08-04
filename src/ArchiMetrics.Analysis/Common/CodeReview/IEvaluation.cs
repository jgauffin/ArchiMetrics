// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IEvaluation.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the IEvaluation type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Common.CodeReview
{
	/// <summary>
	/// What every code review rule declares about itself, independently of any code it inspects.
	/// </summary>
	/// <remarks>
	/// These are the rule's identity and intent rather than its findings. Keeping them on the rule means a
	/// caller can list, filter and document the available rules — and work out a report's coverage — without
	/// running an analysis first.
	/// </remarks>
	public interface IEvaluation
	{
		/// <summary>
		/// Gets the rule's stable identifier, such as <c>AM0058</c>. Used to suppress or reference a rule,
		/// so it must not change once published even if the rule is renamed.
		/// </summary>
		string ID { get; }

		/// <summary>
		/// Gets the short description of what the rule looks for.
		/// </summary>
		string Title { get; }

		/// <summary>
		/// Gets the advice offered when the rule matches. This is what turns a finding into something
		/// actionable rather than a bare complaint.
		/// </summary>
		string Suggestion { get; }

		/// <summary>
		/// Gets how serious a match is. Note the scale runs the opposite way to the metrics — lower is worse.
		/// </summary>
		CodeQuality Quality { get; }

		/// <summary>
		/// Gets which qualities of the code a match harms. Several may apply at once.
		/// </summary>
		QualityAttribute QualityAttribute { get; }

		/// <summary>
		/// Gets how much code a match affects, from a whole project down to a single expression.
		/// </summary>
		ImpactLevel ImpactLevel { get; }
	}
}