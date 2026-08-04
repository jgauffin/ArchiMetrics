// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImpactLevel.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ImpactLevel type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Common.CodeReview
{
	/// <summary>
	/// How much code a rule's finding affects, from a whole project down to a single expression.
	/// </summary>
	/// <remarks>
	/// The scale runs widest to narrowest, so a lower value means a broader blast radius. It matters for
	/// triage: a finding at <see cref="Project"/> level is usually a structural decision to discuss, while
	/// one at <see cref="Node"/> level is a local edit somebody can simply make.
	/// </remarks>
	public enum ImpactLevel
	{
		/// <summary>Affects the project as a whole — typically its dependencies or structure.</summary>
		Project = 0,

		/// <summary>Affects a namespace, such as how its types depend on each other.</summary>
		Namespace = 1,

		/// <summary>Affects an entire type.</summary>
		Type = 2,

		/// <summary>Affects a single method, property or field.</summary>
		Member = 3,

		/// <summary>Affects one line of code.</summary>
		Line = 4,

		/// <summary>Affects a single syntax node — the narrowest scope, such as one expression.</summary>
		Node = 5
	}
}
