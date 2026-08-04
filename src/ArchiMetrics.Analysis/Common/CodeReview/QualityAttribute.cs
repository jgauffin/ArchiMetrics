// --------------------------------------------------------------------------------------------------------------------
// <copyright file="QualityAttribute.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the QualityAttribute type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Common.CodeReview
{
    using System;

    /// <summary>
	/// The qualities of the code a review rule speaks to.
	/// </summary>
	/// <remarks>
	/// This is a flags enum because one rule usually harms several qualities at once — a method too tangled
	/// to read is also hard to test and hard to change safely. Recording all of them lets a report answer
	/// "what is hurting our testability" rather than only "how many violations are there".
	/// </remarks>
	[Flags]
	public enum QualityAttribute
	{
		/// <summary>General correctness and clarity, where no more specific quality fits.</summary>
		CodeQuality = 1,

		/// <summary>How hard the code is to understand and keep working over time.</summary>
		Maintainability = 2,

		/// <summary>How hard the code is to put under test, usually through branching or hard dependencies.</summary>
		Testability = 4,

		/// <summary>How safely the code can be changed without disturbing something else.</summary>
		Modifiability = 8,

		/// <summary>How readily the code can be used in another context.</summary>
		Reusability = 16,

		/// <summary>Adherence to language and framework conventions, so the code reads as others expect.</summary>
		Conformance = 32,

		/// <summary>Exposure to misuse or attack.</summary>
		Security = 64,

		/// <summary>Wasted time, memory or other resources.</summary>
		Performance = 128
	}
}
