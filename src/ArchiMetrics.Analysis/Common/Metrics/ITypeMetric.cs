// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ITypeMetric.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ITypeMetric type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Common.Metrics
{
    using System.Collections.Generic;

    /// <summary>
    /// Defines the interface for type metrics.
    /// </summary>
    /// <remarks>
    /// Use <see cref="MetricThresholds.RateType"/> to reduce these to a single verdict rather than comparing
    /// against hand-written numbers, so that reports and review rules cannot drift apart.
    /// </remarks>
    public interface ITypeMetric : ICodeMetric
    {
        /// <summary>
        /// Gets the declared accessibility of the type.
        /// </summary>
        AccessModifierKind AccessModifier { get; }

        /// <summary>
        /// Gets the kind of type — class, interface, struct and so on.
        /// </summary>
        TypeMetricKind Kind { get; }

        /// <summary>
        /// Gets the <see cref="IMemberMetric"/> for each member the type declares.
        /// </summary>
        IEnumerable<IMemberMetric> MemberMetrics { get; }

        /// <summary>
        /// Gets how many base types sit above this one.
        /// </summary>
        /// <remarks>
        /// <b>Range: 0 and up, where lower is better.</b> Deep hierarchies make behaviour hard to locate,
        /// because the code that actually runs may be several files away from the one being read. See
        /// <see cref="MetricThresholds.DepthOfInheritance"/> for the bands.
        /// </remarks>
        int DepthOfInheritance { get; }

        /// <summary>
        /// Gets the number of distinct types this type references.
        /// </summary>
        /// <remarks><b>Range: 0 and up, where lower is better.</b> No upper bound.</remarks>
        int ClassCoupling { get; }

        /// <summary>
        /// Gets the number of types that depend on this one.
        /// </summary>
        /// <remarks>
        /// <b>Range: 0 and up.</b> A count rather than a quality measure — neither direction is good or bad
        /// on its own. A high value marks the type as widely relied upon, which makes changing it risky
        /// without saying anything about how well it is written.
        /// </remarks>
        int AfferentCoupling { get; }

        /// <summary>
        /// Gets the number of types this one depends on.
        /// </summary>
        /// <remarks>
        /// <b>Range: 0 and up, where lower is better.</b> Many outgoing dependencies mean many reasons to
        /// change and a type that is hard to test in isolation. See
        /// <see cref="MetricThresholds.EfferentCoupling"/> for the bands.
        /// </remarks>
        int EfferentCoupling { get; }

        /// <summary>
        /// Gets how free the type is to change, as
        /// <c>EfferentCoupling / (AfferentCoupling + EfferentCoupling)</c>.
        /// </summary>
        /// <remarks>
        /// <b>Range: 0.0 to 1.0. Neither end is good in itself.</b> 0.0 is stable: many types depend on it
        /// and it depends on nothing, so it is safe to rely on but painful to change. 1.0 is unstable: it
        /// depends on much and nothing depends on it, so it can be changed freely. The combination worth
        /// watching for is a type that is stable and concrete at once — everything relies on it, and nothing
        /// can be substituted for it.
        /// </remarks>
        double Instability { get; }

        /// <summary>
        /// Gets a value indicating whether the type is abstract. Feeds the abstractness ratio of the
        /// namespace and project that contain it.
        /// </summary>
        bool IsAbstract { get; }

        /// <summary>
        /// Gets the <see cref="ITypeDocumentation"/> for the type.
        /// </summary>
        ITypeDocumentation Documentation { get; }
    }
}