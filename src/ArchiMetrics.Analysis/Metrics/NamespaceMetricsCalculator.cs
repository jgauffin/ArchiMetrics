// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NamespaceMetricsCalculator.cs" company="Reimers.dk">
//   Copyright � Matthias Friedrich, Reimers.dk 2014
//   This source is subject to the MIT License.
//   Please see https://opensource.org/licenses/MIT for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the NamespaceMetricsCalculator type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ArchiMetrics.Analysis.Metrics
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Common;
	using Common.Metrics;
	using Microsoft.CodeAnalysis;

	internal sealed class NamespaceMetricsCalculator : SemanticModelMetricsCalculator
	{
		public NamespaceMetricsCalculator(SemanticModel semanticModel)
			: base(semanticModel)
		{
		}

		public INamespaceMetric CalculateFrom(NamespaceDeclarationSyntaxInfo namespaceNode, IEnumerable<ITypeMetric> metrics)
		{
			const string documentationTypeName = "NamespaceDoc";
			var typeMetrics = metrics.AsArray();
			var documentationType = typeMetrics.FirstOrDefault(x => x.Name == documentationTypeName);
			IDocumentation documentation = null;
			if (documentationType != null)
			{
				documentation = documentationType.Documentation;
				typeMetrics = typeMetrics.Where(x => x.Name != documentationTypeName).AsArray();
			}

			var linesOfCode = typeMetrics.Sum(x => x.LinesOfCode);
			var executableStatements = typeMetrics.Sum(x => x.ExecutableStatements);
			var source = typeMetrics.SelectMany(x => x.Dependencies)
						  .GroupBy(x => x.ToString())
						  .Select(x => new TypeCoupling(x.First().TypeName, x.First().Namespace, x.First().Assembly, x.SelectMany(y => y.UsedMethods), x.SelectMany(y => y.UsedProperties), x.SelectMany(y => y.UsedEvents)))
						  .Where(x => x.Namespace != namespaceNode.Name)
						  .OrderBy(x => x.Assembly + x.Namespace + x.TypeName)
						  .AsArray();
			// Weighted by executable statements, matching how the index is built at member and type level.
			var maintainabilitySource = typeMetrics.Select(x => new Tuple<int, double>(x.ExecutableStatements, x.MaintainabilityIndex)).AsArray();
			var maintainabilityIndex = executableStatements > 0 && maintainabilitySource.Any() ? maintainabilitySource.Sum(x => x.Item1 * x.Item2) / executableStatements : 100.0;
			var cyclomaticComplexity = typeMetrics.Sum(x => x.CyclomaticComplexity);
			var depthOfInheritance = typeMetrics.Any() ? typeMetrics.Max(x => x.DepthOfInheritance) : 0;
			return new NamespaceMetric(
				maintainabilityIndex,
				cyclomaticComplexity,
				linesOfCode,
				executableStatements,
				source,
				depthOfInheritance,
				namespaceNode.Name,
				typeMetrics,
				documentation);
		}
	}
}
