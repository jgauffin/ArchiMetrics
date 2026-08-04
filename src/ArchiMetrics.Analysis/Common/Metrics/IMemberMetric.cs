// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IMemberMetric.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the IMemberMetric type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Common.Metrics
{
    /// <summary>
	/// Defines the interface for member metrics — the level at which measurements are actually taken.
	/// </summary>
	/// <remarks>
	/// Everything above this is an aggregate of these values, which makes members the level at which acting
	/// on the numbers usually makes most sense: a namespace cannot be refactored, but the method dragging
	/// its score down can be.
	/// </remarks>
	public interface IMemberMetric : ICodeMetric
	{
		/// <summary>
		/// Gets the access modifier for the member.
		/// </summary>
		AccessModifierKind AccessModifier { get; }

		/// <summary>
		/// Gets the path to the source file containing the member declaration.
		/// </summary>
		string CodeFile { get; }

		/// <summary>
		/// Gets the line number in the source file where the member is declared.
		/// </summary>
		int LineNumber { get; }

		/// <summary>
		/// Gets the number of distinct types the member references.
		/// </summary>
		/// <remarks>
		/// <b>Range: 0 and up, where lower is better.</b> No upper bound. A member touching many types is
		/// usually doing several jobs at once, and it cannot be tested without standing all of them up.
		/// </remarks>
		int ClassCoupling { get; }

		/// <summary>
		/// Gets the number of parameters the member declares.
		/// </summary>
		/// <remarks>
		/// <b>Range: 0 and up, where lower is better.</b> Long parameter lists are hard to call correctly and
		/// often signal that the arguments belong together in a type of their own.
		/// </remarks>
		int NumberOfParameters { get; }

		/// <summary>
		/// Gets the number of local variables declared in the member.
		/// </summary>
		/// <remarks>
		/// <b>Range: 0 and up, where lower is better.</b> Each local is another piece of state the reader has
		/// to track at once, so a long list is a common early sign that a method should be broken up.
		/// </remarks>
		int NumberOfLocalVariables { get; }

		/// <summary>
		/// Gets the number of places that call this member.
		/// </summary>
		/// <remarks>
		/// <b>Range: 0 and up.</b> A count rather than a quality measure. A high value means changing the
		/// member is risky, not that it is badly written; a value of 0 on a private member is worth a look,
		/// since nothing calls it.
		/// </remarks>
		int AfferentCoupling { get; }

		/// <summary>
		/// Gets the <see cref="IMemberDocumentation"/> for the member.
		/// </summary>
		IMemberDocumentation Documentation { get; }

		/// <summary>
		/// Gets the volume for the underlying source code.
		/// </summary>
		/// <returns>The volume as a <see cref="double"/>.</returns>
		double GetVolume();

		/// <summary>
		/// Gets the Halstead metrics for the member.
		/// </summary>
		/// <returns>The Halstead metrics as an <see cref="IHalsteadMetrics"/>.</returns>
		IHalsteadMetrics GetHalsteadMetrics();
	}
}