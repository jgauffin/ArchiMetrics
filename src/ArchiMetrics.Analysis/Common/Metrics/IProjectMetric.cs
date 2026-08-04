// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IProjectMetric.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the IProjectMetric type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System.Collections.Generic;

    /// <summary>
	/// Defines the interface for project metrics — the widest view, aggregating every namespace beneath.
	/// </summary>
	/// <remarks>
	/// The values here are most useful for architectural questions rather than local ones: whether a project
	/// depends on too much, whether its types belong together, and whether its abstractness matches how much
	/// else relies on it.
	/// </remarks>
	public interface IProjectMetric : ICodeMetric
	{
		/// <summary>
		/// Gets the <see cref="INamespaceMetric"/> for namespaces defined in the project.
		/// </summary>
		IEnumerable<INamespaceMetric> NamespaceMetrics { get; }

		/// <summary>
		/// Gets the names of the assemblies that depend on this project — the code that would be disturbed
		/// by changing it.
		/// </summary>
		IEnumerable<string> Dependants { get; }

		/// <summary>
		/// Gets the names of the project dependencies.
		/// </summary>
		IEnumerable<string> AssemblyDependencies { get; }

		/// <summary>
		/// Gets the average number of internal relationships per type in the project.
		/// </summary>
		/// <remarks>
		/// <b>Range: 0.0 and up, and higher is usually better</b> — within reason. It asks whether the types
		/// in this assembly actually belong together: a low value suggests the project is a bag of unrelated
		/// code that would be clearer split up, while a very high value suggests types so entangled that
		/// none can be understood alone. No band thresholds are defined, because a sensible figure depends
		/// heavily on what the project is for.
		/// </remarks>
		double RelationalCohesion { get; }

		/// <summary>
		/// Gets the number of assemblies this project depends on.
		/// </summary>
		/// <remarks><b>Range: 0 and up, where lower is better.</b> Counts outgoing dependencies.</remarks>
		int EfferentCoupling { get; }

		/// <summary>
		/// Gets the number of assemblies that depend on this project.
		/// </summary>
		/// <remarks>
		/// <b>Range: 0 and up.</b> Counts incoming dependencies. A count rather than a quality measure — it
		/// says how much would be disturbed by changing this project, not how good the project is.
		/// </remarks>
		int AfferentCoupling { get; }

		/// <summary>
		/// Gets the share of the project's types that are abstract.
		/// </summary>
		/// <remarks>
		/// <b>Range: 0.0 to 1.0. Neither end is good in itself.</b> It is most useful read together with
		/// <see cref="EfferentCoupling"/> and <see cref="AfferentCoupling"/>: a project that many others
		/// depend on should lean abstract so they depend on contracts, whereas a project nothing depends on
		/// gains nothing from abstraction and pays for it in indirection.
		/// </remarks>
		double Abstractness { get; }
	}
}