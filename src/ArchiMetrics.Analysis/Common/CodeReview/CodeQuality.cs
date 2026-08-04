// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CodeQuality.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the CodeQuality type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Common.CodeReview
{
	/// <summary>
	/// How serious a rule considers the code it has flagged, and by implication how urgently to act.
	/// </summary>
	/// <remarks>
	/// <b>Lower is worse</b>, the opposite direction to the metric values themselves, so never present the
	/// two as the same kind of number. The gradations exist so that a report can separate "this is a defect"
	/// from "this could be tidier" — a tool that treats both as failures teaches its users to ignore it.
	/// </remarks>
	public enum CodeQuality
	{
		/// <summary>The code is wrong. Expect it to misbehave, not merely to read badly.</summary>
		Broken = 0,

		/// <summary>The design itself is the problem; tidying will not fix it.</summary>
		NeedsReEngineering = 1,

		/// <summary>The behaviour is right but the structure makes it costly to work with.</summary>
		NeedsRefactoring = 2,

		/// <summary>Small, local untidiness — dead code, redundant syntax.</summary>
		NeedsCleanup = 3,

		/// <summary>Worth a human's judgement; the rule cannot decide on its own.</summary>
		NeedsReview = 4,

		/// <summary>Nothing to report.</summary>
		Good = 5
	}
}
