// --------------------------------------------------------------------------------------------------------------------
// <copyright file="INamespaceMetric.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the INamespaceMetric type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System.Collections.Generic;

    /// <summary>
	/// Defines the interface for namespace metrics.
	/// </summary>
	public interface INamespaceMetric : ICodeMetric
    {
        /// <summary>
        /// Gets the number of distinct types referenced from outside the namespace.
        /// </summary>
        /// <remarks><b>Range: 0 and up, where lower is better.</b> No upper bound.</remarks>
        int ClassCoupling { get; }

        /// <summary>
        /// Gets the deepest inheritance chain among the types in the namespace.
        /// </summary>
        /// <remarks>
        /// <b>Range: 0 and up, where lower is better.</b> This is the maximum rather than an average, so a
        /// single deep hierarchy will show here even if every other type is flat. See
        /// <see cref="MetricThresholds.DepthOfInheritance"/> for the bands.
        /// </remarks>
        int DepthOfInheritance { get; }

        /// <summary>
        /// Gets the <see cref="ITypeMetric"/> for the types defined in the namespace.
        /// </summary>
        IEnumerable<ITypeMetric> TypeMetrics { get; }

        /// <summary>
        /// Gets the share of the namespace's types that are abstract.
        /// </summary>
        /// <remarks>
        /// <b>Range: 0.0 to 1.0. Neither end is good in itself.</b> 0.0 means nothing here can be extended
        /// without editing it; 1.0 means nothing here actually does anything. Read it against how much
        /// depends on the namespace: one that is widely used benefits from being abstract, a leaf one does
        /// not.
        /// </remarks>
        double Abstractness { get; }

        /// <summary>
        /// Gets the <see cref="IDocumentation"/> for the namespace.
        /// </summary>
        /// <remarks>
        /// The namespace documentation uses a convention and loads the documentation from a dummy class named [namespace name]Doc.
        ///
        /// If this class does not exist then the property will return <code>null</code>.
        /// </remarks>
        IDocumentation Documentation { get; }
    }
}